using System;
using System.Drawing;

namespace GameWatcher.App.Vision;

internal sealed class TemplateMatcher
{
    // Returns (x,y,score). Score is NCC in [-1..1], higher is better.
    public (Point Location, double Score) MatchBest(Bitmap haystack, Bitmap needle)
    {
        using var hayGray = ToGrayscale(haystack);
        using var neeGray = ToGrayscale(needle);

        var hData = GetIntensity(hayGray);
        var nData = GetIntensity(neeGray);

        int H = hayGray.Height, W = hayGray.Width;
        int h = neeGray.Height, w = neeGray.Width;

        // Precompute needle stats
        double nMean = 0, nSq = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double v = nData[y, x];
                nMean += v;
                nSq += v * v;
            }
        }
        int nPix = w * h;
        nMean /= nPix;
        double nVar = nSq - nPix * nMean * nMean;
        if (nVar <= 1e-9) nVar = 1e-9; // avoid zero-variance

        double bestScore = double.NegativeInfinity;
        Point best = Point.Empty;

        for (int y0 = 0; y0 <= H - h; y0++)
        {
            for (int x0 = 0; x0 <= W - w; x0++)
            {
                double sum = 0, sumSq = 0, sumProd = 0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        double hv = hData[y0 + y, x0 + x];
                        double nv = nData[y, x];
                        sum += hv;
                        sumSq += hv * hv;
                        sumProd += (hv * nv);
                    }
                }
                double hMean = sum / nPix;
                double hVar = sumSq - nPix * hMean * hMean;
                if (hVar <= 1e-9) hVar = 1e-9;

                // NCC = (sum(xy) - n*μx*μy) / sqrt( (∑x^2 - n μx^2) (∑y^2 - n μy^2) )
                double num = sumProd - nPix * hMean * nMean;
                double den = Math.Sqrt(hVar * nVar);
                double score = num / den;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Point(x0, y0);
                }
            }
        }

        return (best, bestScore);
    }

    private static Bitmap ToGrayscale(Bitmap source)
    {
        var gray = new Bitmap(source.Width, source.Height);
        using var g = Graphics.FromImage(gray);
        var cm = new System.Drawing.Imaging.ColorMatrix(new float[][]
        {
            new float[] {0.299f, 0.299f, 0.299f, 0, 0},
            new float[] {0.587f, 0.587f, 0.587f, 0, 0},
            new float[] {0.114f, 0.114f, 0.114f, 0, 0},
            new float[] {0,      0,      0,      1, 0},
            new float[] {0,      0,      0,      0, 1}
        });
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm);
        g.DrawImage(source, new Rectangle(0, 0, gray.Width, gray.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, ia);
        return gray;
    }

    private static double[,] GetIntensity(Bitmap bmp)
    {
        int H = bmp.Height, W = bmp.Width;
        var data = new double[H, W];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                var c = bmp.GetPixel(x, y);
                data[y, x] = c.R; // grayscale already: R=G=B
            }
        }
        return data;
    }
}

