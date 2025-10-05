using System.Drawing;
using System.Drawing.Imaging;

namespace GameWatcher.App.Capture;

internal static class Win32Capture
{
    public static Bitmap? CaptureClient(IntPtr hwnd)
    {
        // Allow alternate backend via env
        var backend = Environment.GetEnvironmentVariable("GW_CAPTURE_BACKEND");
        if (string.Equals(backend, "wgc", StringComparison.OrdinalIgnoreCase))
        {
            var wgc = WgcCapture.CaptureClient(hwnd);
            if (wgc != null) return wgc;
            // fall through to Win32 path if WGC not available
        }
        else if (string.Equals(backend, "dd", StringComparison.OrdinalIgnoreCase))
        {
            var dd = DxgiCapture.CaptureClient(hwnd);
            if (dd != null) return dd;
        }

        if (hwnd == IntPtr.Zero) return null;

        // First attempt: use client rect size
        if (!Win32.GetClientRect(hwnd, out var rcClient)) return null;
        int cw = Math.Max(0, rcClient.Right - rcClient.Left);
        int ch = Math.Max(0, rcClient.Bottom - rcClient.Top);

        Bitmap? bmp = null;
        if (cw >= 50 && ch >= 50)
        {
            bmp = TryPrintWindow(hwnd, cw, ch);
            if (bmp != null && !IsLikelyBlank(bmp)) return bmp;
            bmp?.Dispose();
            bmp = null;
        }

        // Second attempt: use window rect (works for borderless/zero-client windows)
        if (Win32.GetWindowRect(hwnd, out var rcWin))
        {
            int ww = Math.Max(1, rcWin.Right - rcWin.Left);
            int wh = Math.Max(1, rcWin.Bottom - rcWin.Top);
            bmp = TryPrintWindow(hwnd, ww, wh);
            if (bmp != null && !IsLikelyBlank(bmp)) return bmp;
            bmp?.Dispose();
            bmp = null;

            // Third attempt: copy from screen (always last; may capture occlusions)
            try
            {
                var scr = new Bitmap(ww, wh, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(scr))
                {
                    g.CopyFromScreen(rcWin.Left, rcWin.Top, 0, 0, new Size(ww, wh), CopyPixelOperation.SourceCopy);
                }
                if (!IsLikelyBlank(scr)) return scr;
                scr.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static Bitmap? TryPrintWindow(IntPtr hwnd, int width, int height)
    {
        try
        {
            var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            var hdc = g.GetHdc();
            try
            {
                if (!Win32.PrintWindow(hwnd, hdc, Win32.PW_RENDERFULLCONTENT))
                {
                    // Fallback: BitBlt from window DC
                    var src = Win32.GetWindowDC(hwnd);
                    try
                    {
                        Win32.BitBlt(hdc, 0, 0, width, height, src, 0, 0, Win32.SRCCOPY);
                    }
                    finally
                    {
                        if (src != IntPtr.Zero) Win32.ReleaseDC(hwnd, src);
                    }
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLikelyBlank(Bitmap bmp)
    {
        if (bmp.Width <= 2 || bmp.Height <= 2) return true;
        // Sample a 5x5 grid
        int steps = 5;
        int minR = 255, minG = 255, minB = 255;
        int maxR = 0, maxG = 0, maxB = 0;
        for (int yi = 0; yi < steps; yi++)
        {
            for (int xi = 0; xi < steps; xi++)
            {
                int x = (int)Math.Min(bmp.Width - 1, Math.Round(xi * (bmp.Width - 1.0) / (steps - 1)));
                int y = (int)Math.Min(bmp.Height - 1, Math.Round(yi * (bmp.Height - 1.0) / (steps - 1)));
                var c = bmp.GetPixel(x, y);
                if (c.R < minR) minR = c.R; if (c.R > maxR) maxR = c.R;
                if (c.G < minG) minG = c.G; if (c.G > maxG) maxG = c.G;
                if (c.B < minB) minB = c.B; if (c.B > maxB) maxB = c.B;
            }
        }
        // If color range is extremely small, it's likely a blank/solid/black frame
        int rangeR = maxR - minR;
        int rangeG = maxG - minG;
        int rangeB = maxB - minB;
        return rangeR < 2 && rangeG < 2 && rangeB < 2;
    }
}
