using System.Drawing;
using System.Drawing.Imaging;

namespace GameWatcher.App.Capture;

internal static class Win32Capture
{
    public static Bitmap? CaptureClient(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        if (!Win32.GetClientRect(hwnd, out var rc)) return null;
        int width = Math.Max(1, rc.Right - rc.Left);
        int height = Math.Max(1, rc.Bottom - rc.Top);

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var hdc = g.GetHdc();
        try
        {
            // Try PrintWindow full content
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
}

