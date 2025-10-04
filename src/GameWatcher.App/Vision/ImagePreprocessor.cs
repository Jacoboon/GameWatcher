using System.Drawing;
using System.Drawing.Imaging;

namespace GameWatcher.App.Vision;

internal static class ImagePreprocessor
{
    public static Bitmap GrayscaleUpscaleThreshold(Bitmap src, int scale = 2, int threshold = 140)
    {
        var gray = new Bitmap(src.Width, src.Height);
        using (var g = Graphics.FromImage(gray))
        {
            var cm = new ColorMatrix(new float[][]
            {
                new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
            });
            var ia = new ImageAttributes();
            ia.SetColorMatrix(cm);
            g.DrawImage(src, new Rectangle(0, 0, gray.Width, gray.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }

        var up = new Bitmap(gray.Width * scale, gray.Height * scale);
        using (var g = Graphics.FromImage(up))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(gray, new Rectangle(0, 0, up.Width, up.Height), 0, 0, gray.Width, gray.Height, GraphicsUnit.Pixel);
        }

        for (int y = 0; y < up.Height; y++)
        {
            for (int x = 0; x < up.Width; x++)
            {
                var c = up.GetPixel(x, y);
                int v = c.R > threshold ? 255 : 0;
                up.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        }

        gray.Dispose();
        return up;
    }
}

