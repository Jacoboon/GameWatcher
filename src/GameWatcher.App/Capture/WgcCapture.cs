using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace GameWatcher.App.Capture;

internal static class WgcCapture
{
    private static readonly object _lock = new();
    private static IntPtr _currentHwnd = IntPtr.Zero;
    private static IDirect3DDevice? _device;
    private static GraphicsCaptureItem? _item;
    private static Direct3D11CaptureFramePool? _pool;
    private static GraphicsCaptureSession? _session;
    private static SizeInt32 _lastSize;

    public static Bitmap? CaptureClient(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return null;
            EnsureSession(hwnd);
            if (_pool is null) return null;

            using var frame = _pool.TryGetNextFrame();
            if (frame is null) return null;

            if (!frame.ContentSize.Equals(_lastSize))
            {
                RecreatePool(frame.ContentSize);
            }

            // TODO: Implement Direct3D11 texture conversion properly
            // For now, return null to allow fallback to DXGI/Win32
            return null; // ConvertD3D11TextureToBitmap(frame.Surface);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Bitmap? ConvertD3D11TextureToBitmap(IDirect3DSurface surface)
    {
        try
        {
            // Get the DXGI interface from the surface
            var access = surface.As<IDirect3DDxgiInterfaceAccess>();
            var hr = access.GetInterface(typeof(Vortice.DXGI.IDXGISurface).GUID, out IntPtr surfacePtr);
            if (hr < 0) return null;

            using var dxgiSurface = new Vortice.DXGI.IDXGISurface(surfacePtr);
            var desc = dxgiSurface.Description;
            
            // Create D3D11 device for staging texture
            using var device = CreateD3D11DeviceForTexture();
            if (device == null) return null;

            // Create staging texture to read GPU data to CPU
            var stagingDesc = new Vortice.Direct3D11.Texture2DDescription
            {
                Width = (int)desc.Width,
                Height = (int)desc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                Usage = Vortice.Direct3D11.ResourceUsage.Staging,
                BindFlags = Vortice.Direct3D11.BindFlags.None,
                CPUAccessFlags = Vortice.Direct3D11.CpuAccessFlags.Read,
                MiscFlags = Vortice.Direct3D11.ResourceOptionFlags.None
            };

            using var stagingTexture = device.CreateTexture2D(stagingDesc);
            using var sourceTexture = dxgiSurface.QueryInterface<Vortice.Direct3D11.ID3D11Resource>();
            
            // Copy the surface data to our staging texture
            device.ImmediateContext.CopyResource(sourceTexture, stagingTexture);

            // Map and read the pixel data
            var mapped = device.ImmediateContext.Map(stagingTexture, 0, Vortice.Direct3D11.MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            
            try
            {
                // Create bitmap and copy pixel data
                var bitmap = new Bitmap((int)desc.Width, (int)desc.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), 
                    System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);

                try
                {
                    unsafe
                    {
                        var src = (byte*)mapped.DataPointer;
                        var dst = (byte*)bitmapData.Scan0;
                        
                        for (int y = 0; y < bitmap.Height; y++)
                        {
                            var srcRow = src + y * mapped.RowPitch;
                            var dstRow = dst + y * bitmapData.Stride;
                            
                            // Copy row of BGRA pixels (no conversion needed for Format32bppArgb)
                            for (int x = 0; x < bitmap.Width * 4; x++)
                            {
                                dstRow[x] = srcRow[x];
                            }
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                return bitmap;
            }
            finally
            {
                device.ImmediateContext.Unmap(stagingTexture, 0);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Vortice.Direct3D11.ID3D11Device? CreateD3D11DeviceForTexture()
    {
        try
        {
            // Create a simple D3D11 device for texture operations
            Vortice.Direct3D11.D3D11.D3D11CreateDevice(
                null, 
                Vortice.Direct3D.DriverType.Hardware, 
                Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,
                null,
                out var device,
                out var context).CheckError();
                
            context?.Dispose(); // We'll use device.ImmediateContext instead
            return device;
        }
        catch { }

        try
        {
            // Try WARP as fallback
            Vortice.Direct3D11.D3D11.D3D11CreateDevice(
                null, 
                Vortice.Direct3D.DriverType.Warp, 
                Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,
                null,
                out var device,
                out var context).CheckError();
                
            context?.Dispose();
            return device;
        }
        catch { }

        return null;
    }

    private static void EnsureSession(IntPtr hwnd)
    {
        lock (_lock)
        {
            if (_session != null && _currentHwnd == hwnd) return;
            Cleanup();
            _currentHwnd = hwnd;

            _device = CreateD3DDevice();
            _item = CreateItemForWindow(hwnd);
            if (_device == null || _item == null) { Cleanup(); return; }

            _lastSize = _item!.Size;
            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device!, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _lastSize);
            _session = _pool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = false;
            _session.StartCapture();
        }
    }

    private static void RecreatePool(SizeInt32 size)
    {
        lock (_lock)
        {
            _pool?.Dispose();
            _lastSize = size;
            if (_device == null) return;
            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _lastSize);
        }
    }

    private static void Cleanup()
    {
        lock (_lock)
        {
            try { _session?.Dispose(); } catch { }
            try { _pool?.Dispose(); } catch { }
            try { _item = null; } catch { }
            try { _device = null; } catch { }
            
            _session = null; 
            _pool = null; 
            _item = null; 
            _device = null; 
            _currentHwnd = IntPtr.Zero;
            _lastSize = default;
        }
    }

    public static void Dispose()
    {
        Cleanup();
    }

    private static IDirect3DDevice? CreateD3DDevice()
    {
        IntPtr d3dDevice = IntPtr.Zero;
        IntPtr d3dContext = IntPtr.Zero;
        const uint D3D11_SDK_VERSION = 7;
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        int hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            IntPtr.Zero, 0, D3D11_SDK_VERSION, out d3dDevice, IntPtr.Zero, out d3dContext);
        if (hr < 0)
        {
            const int D3D_DRIVER_TYPE_WARP = 5;
            hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_WARP, IntPtr.Zero, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                IntPtr.Zero, 0, D3D11_SDK_VERSION, out d3dDevice, IntPtr.Zero, out d3dContext);
            if (hr < 0) return null;
        }
        IntPtr dxgiDevice;
        Guid iidIdxgiDevice = new("77DB970F-6276-48BA-BA28-070143B4392C"); // IID_IDXGIDevice
        hr = Marshal.QueryInterface(d3dDevice, ref iidIdxgiDevice, out dxgiDevice);
        if (hr < 0) return null;
        IntPtr id3d;
        hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out id3d);
        if (hr < 0) return null;
        var obj = Marshal.GetObjectForIUnknown(id3d);
        return (IDirect3DDevice)obj;
    }

    private static GraphicsCaptureItem? CreateItemForWindow(IntPtr hwnd)
    {
        try
        {
            // Use the WinRT factory method
            var interop = GetGraphicsCaptureItemInterop();
            if (interop == null) return null;
            
            Guid iid = typeof(GraphicsCaptureItem).GUID;
            IntPtr pItem = interop.CreateForWindow(hwnd, ref iid);
            var obj = Marshal.GetObjectForIUnknown(pItem);
            return (GraphicsCaptureItem)obj;
        }
        catch
        {
            return null;
        }
    }

    private static IGraphicsCaptureItemInterop? GetGraphicsCaptureItemInterop()
    {
        try
        {
            var guid = new Guid("3628E81B-3CAC-4C60-B7F4-23B6C3E8D7BD");
            var hr = WindowsCreateString("Windows.Graphics.Capture.GraphicsCaptureItem", 
                (uint)"Windows.Graphics.Capture.GraphicsCaptureItem".Length, out IntPtr hString);
            if (hr < 0) return null;

            hr = RoGetActivationFactory(hString, ref guid, out IntPtr factory);
            if (hr < 0) return null;

            return Marshal.GetObjectForIUnknown(factory) as IGraphicsCaptureItemInterop;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int DriverType,
        IntPtr Software,
        uint Flags,
        IntPtr pFeatureLevels,
        int FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        IntPtr pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("Windows.Graphics.DirectX.Direct3D11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string sourceString, 
        uint length, out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3628E81B-3CAC-4C60-B7F4-23B6C3E8D7BD")]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, ref Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    private interface IDirect3DDxgiInterfaceAccess
    {
        int GetInterface(Guid iid, out IntPtr ppv);
    }
}
