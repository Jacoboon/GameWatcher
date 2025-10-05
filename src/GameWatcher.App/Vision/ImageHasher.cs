using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace GameWatcher.App.Vision;

internal static class ImageHasher
{
    public static string ComputeSHA1(Bitmap bmp)
        => ComputeSHA1(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));

    public static string ComputeSHA1(Bitmap bmp, Rectangle region)
    {
        var rect = Rectangle.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height), region);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int bytes = Math.Abs(data.Stride) * data.Height;
            byte[] buffer = new byte[bytes];
            Marshal.Copy(data.Scan0, buffer, 0, bytes);
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(buffer);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
