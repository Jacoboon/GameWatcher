using System;
using System.Drawing;
using System.Drawing.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace GameWatcher.App.Capture;

internal static class DxgiCapture
{
    private static readonly object _lock = new();
    private static IntPtr _currentMonitor = IntPtr.Zero;
    private static ID3D11Device? _device;
    private static ID3D11DeviceContext? _context;
    private static IDXGIOutputDuplication? _duplication;
    private static Rectangle _monitorBounds;

    public static Bitmap? CaptureClient(IntPtr hwnd)
    {
        EnsureDuplication(hwnd);
        if (_duplication == null || _context == null) return null;

        try
        {
            var result = _duplication.AcquireNextFrame(16, out var frameInfo, out var resource);
            if (result.Failure)
            {
                return null; // timeout or lost
            }

            using var tex = resource.QueryInterfaceOrNull<ID3D11Texture2D>();
            resource.Dispose();
            if (tex == null) { _duplication.ReleaseFrame(); return null; }

            var desc = tex.Description;
            var stagingDesc = new Texture2DDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = desc.Format,
                SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None
            };
            using var staging = _device!.CreateTexture2D(stagingDesc);
            _context.CopyResource(tex, staging);

            _context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out var mapped);
            try
            {
                // Copy to managed buffer (BGRA8)
                int width = desc.Width;
                int height = desc.Height;
                int stride = mapped.RowPitch;
                int bytes = stride * height;
                byte[] buffer = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(mapped.DataPointer, buffer, 0, bytes);

                // Compute window crop within monitor space (or take full monitor)
                bool forceFull = string.Equals(Environment.GetEnvironmentVariable("GW_DD_FORCE_MONITOR"), "1", StringComparison.OrdinalIgnoreCase);
                int minW = int.TryParse(Environment.GetEnvironmentVariable("GW_DD_MINCROP_W"), out var mw) ? Math.Max(1, mw) : 400;
                int minH = int.TryParse(Environment.GetEnvironmentVariable("GW_DD_MINCROP_H"), out var mh) ? Math.Max(1, mh) : 300;

                int cropX = 0, cropY = 0, cropW = width, cropH = height;
                if (!forceFull)
                {
                    if (!Win32.GetWindowRect(hwnd, out var rc))
                    {
                        // if window rect fails, keep full monitor
                    }
                    else
                    {
                        cropX = Math.Clamp(rc.Left - _monitorBounds.Left, 0, width);
                        cropY = Math.Clamp(rc.Top - _monitorBounds.Top, 0, height);
                        cropW = Math.Clamp(rc.Right - _monitorBounds.Left - cropX, 0, width - cropX);
                        cropH = Math.Clamp(rc.Bottom - _monitorBounds.Top - cropY, 0, height - cropY);
                        if (cropW < minW || cropH < minH)
                        {
                            // Window rect looks like a caption/control area or overlay; use full monitor
                            cropX = 0; cropY = 0; cropW = width; cropH = height;
                        }
                    }
                }

                // Create bitmap and fill
                var bmp = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(new Rectangle(0, 0, cropW, cropH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int dstStride = bmpData.Stride;
                    IntPtr dstBase = bmpData.Scan0;
                    for (int y = 0; y < cropH; y++)
                    {
                        int srcIndex = ((cropY + y) * stride) + (cropX * 4);
                        IntPtr dst = dstBase + y * dstStride;
                        System.Runtime.InteropServices.Marshal.Copy(buffer, srcIndex, dst, cropW * 4);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
                return bmp;
            }
            finally
            {
                _context.Unmap(staging, 0);
                _duplication.ReleaseFrame();
            }
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureDuplication(IntPtr hwnd)
    {
        lock (_lock)
        {
            var mon = Win32.MonitorFromWindow(hwnd, 2 /*MONITOR_DEFAULTTONEAREST*/);
            if (_duplication != null && mon == _currentMonitor) return;

            Cleanup();
            _currentMonitor = mon;

            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            IDXGIAdapter1? selectedAdapter = null;
            for (int i = 0; factory.EnumAdapters1(i, out var ad).Success; i++)
            {
                selectedAdapter = ad;
                break;
            }

            D3D11.D3D11CreateDevice(selectedAdapter, Vortice.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport, null, out _device, out _context).CheckError();

            // Find output matching the monitor
            IDXGIOutput? foundOutput = null;
            if (selectedAdapter != null)
            {
                for (int i = 0; selectedAdapter.EnumOutputs(i, out var output).Success; i++)
                {
                    var od = output.Description;
                    if (od.Monitor == mon)
                    {
                        foundOutput = output;
                        _monitorBounds = new Rectangle(od.DesktopCoordinates.Left, od.DesktopCoordinates.Top,
                                                       od.DesktopCoordinates.Right - od.DesktopCoordinates.Left,
                                                       od.DesktopCoordinates.Bottom - od.DesktopCoordinates.Top);
                        break;
                    }
                    output.Dispose();
                }
            }
            if (foundOutput == null)
            {
                Cleanup();
                return;
            }

            var out1 = foundOutput.QueryInterface<IDXGIOutput1>();
            _duplication = out1.DuplicateOutput(_device);
            foundOutput.Dispose();
            out1.Dispose();
        }
    }

    private static void Cleanup()
    {
        try { _duplication?.Dispose(); } catch { }
        try { _context?.Dispose(); } catch { }
        try { _device?.Dispose(); } catch { }
        _duplication = null; _context = null; _device = null; _currentMonitor = IntPtr.Zero;
    }
}
