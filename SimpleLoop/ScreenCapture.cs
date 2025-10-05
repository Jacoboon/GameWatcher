using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;

namespace SimpleLoop
{
    public class ScreenCapture
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint SRCCOPY = 0x00CC0020;
        
        private static IntPtr _gameWindowHandle = IntPtr.Zero;
        private static string[] _gameWindowTitles = {
            "FINAL FANTASY", // FF1 common title
            "Final Fantasy", 
            "FF1",
            "ePSXe", // Popular PS1 emulator
            "PCSX-R", // Another PS1 emulator  
            "RetroArch", // Multi-system emulator
            "Duckstation" // Modern PS1 emulator
        };

        public static IntPtr FindGameWindow()
        {
            if (_gameWindowHandle != IntPtr.Zero)
            {
                // Check if cached handle is still valid
                if (GetWindowRect(_gameWindowHandle, out _))
                    return _gameWindowHandle;
            }

            // Try to find game window by common titles
            foreach (var title in _gameWindowTitles)
            {
                var handle = FindWindow(null, title);
                if (handle != IntPtr.Zero)
                {
                    _gameWindowHandle = handle;
                    Console.WriteLine($"🎮 Found game window: \"{title}\" (Handle: {handle})");
                    return handle;
                }
            }

            // Fallback to desktop capture
            Console.WriteLine("⚠️ No game window found, using desktop capture");
            return GetDesktopWindow();
        }

        public static Bitmap CaptureGameWindow()
        {
            var gameWindow = FindGameWindow();
            
            if (gameWindow == GetDesktopWindow())
            {
                // Fallback to full screen
                var screenSize = SystemInformation.PrimaryMonitorSize;
                return CaptureScreen(0, 0, screenSize.Width, screenSize.Height);
            }

            // Capture specific game window
            if (GetWindowRect(gameWindow, out RECT windowRect))
            {
                var width = windowRect.Right - windowRect.Left;
                var height = windowRect.Bottom - windowRect.Top;
                return CaptureWindow(gameWindow, width, height);
            }

            // Fallback
            return CaptureScreen();
        }

        public static Bitmap CaptureWindow(IntPtr windowHandle, int width, int height)
        {
            var hWindowDC = GetWindowDC(windowHandle);
            var hCaptureDC = CreateCompatibleDC(hWindowDC);
            var hCaptureBitmap = CreateCompatibleBitmap(hWindowDC, width, height);
            
            var hOld = SelectObject(hCaptureDC, hCaptureBitmap);
            BitBlt(hCaptureDC, 0, 0, width, height, hWindowDC, 0, 0, SRCCOPY);
            
            var bitmap = Image.FromHbitmap(hCaptureBitmap);
            
            SelectObject(hCaptureDC, hOld);
            DeleteDC(hCaptureDC);
            DeleteObject(hCaptureBitmap);
            ReleaseDC(windowHandle, hWindowDC);
            
            return bitmap;
        }

        public static Bitmap CaptureScreen()
        {
            var screenSize = SystemInformation.PrimaryMonitorSize;
            return CaptureScreen(0, 0, screenSize.Width, screenSize.Height);
        }

        public static Bitmap CaptureScreen(int x, int y, int width, int height)
        {
            var hDesktopWnd = GetDesktopWindow();
            var hDesktopDC = GetWindowDC(hDesktopWnd);
            var hCaptureDC = CreateCompatibleDC(hDesktopDC);
            var hCaptureBitmap = CreateCompatibleBitmap(hDesktopDC, width, height);
            
            var hOld = SelectObject(hCaptureDC, hCaptureBitmap);
            BitBlt(hCaptureDC, 0, 0, width, height, hDesktopDC, x, y, SRCCOPY);
            
            var bitmap = Image.FromHbitmap(hCaptureBitmap);
            
            SelectObject(hCaptureDC, hOld);
            DeleteDC(hCaptureDC);
            DeleteObject(hCaptureBitmap);
            ReleaseDC(hDesktopWnd, hDesktopDC);
            
            return bitmap;
        }

        // Fast comparison using unsafe code for better performance
        public static unsafe bool AreImagesEqual(Bitmap bmp1, Bitmap bmp2)
        {
            if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                return false;

            var rect = new Rectangle(0, 0, bmp1.Width, bmp1.Height);
            
            var bmpData1 = bmp1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var bmpData2 = bmp2.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            var stride = bmpData1.Stride;
            var bytes = Math.Abs(stride) * bmp1.Height;
            
            var ptr1 = (byte*)bmpData1.Scan0;
            var ptr2 = (byte*)bmpData2.Scan0;
            
            bool areEqual = true;
            
            for (int i = 0; i < bytes; i++)
            {
                if (ptr1[i] != ptr2[i])
                {
                    areEqual = false;
                    break;
                }
            }
            
            bmp1.UnlockBits(bmpData1);
            bmp2.UnlockBits(bmpData2);
            
            return areEqual;
        }

        // Optimized comparison that samples pixels with tolerance for real-world variations
        public static unsafe bool AreImagesSimilar(Bitmap bmp1, Bitmap bmp2, int sampleRate = 100)
        {
            if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                return false;

            var rect = new Rectangle(0, 0, bmp1.Width, bmp1.Height);
            
            var bmpData1 = bmp1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var bmpData2 = bmp2.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            var stride = bmpData1.Stride;
            var bytes = Math.Abs(stride) * bmp1.Height;
            
            var ptr1 = (byte*)bmpData1.Scan0;
            var ptr2 = (byte*)bmpData2.Scan0;
            
            int diffPixels = 0;
            int totalSampled = 0;
            const int tolerance = 10; // Allow small pixel variations
            
            // Sample every Nth pixel for speed, count differences with tolerance
            for (int i = 0; i < bytes; i += sampleRate)
            {
                totalSampled++;
                int diff = Math.Abs(ptr1[i] - ptr2[i]);
                if (diff > tolerance)
                {
                    diffPixels++;
                }
                
                // If more than 5% of sampled pixels are significantly different, consider frames different
                if (diffPixels * 20 > totalSampled) // 5% threshold
                {
                    bmp1.UnlockBits(bmpData1);
                    bmp2.UnlockBits(bmpData2);
                    return false;
                }
            }
            
            bmp1.UnlockBits(bmpData1);
            bmp2.UnlockBits(bmpData2);
            
            // Frames are similar if less than 5% of pixels differ significantly
            return true;
        }
    }
}