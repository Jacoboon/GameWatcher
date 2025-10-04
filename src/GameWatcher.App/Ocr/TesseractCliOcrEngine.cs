using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using GameWatcher.Core;

namespace GameWatcher.App.Ocr;

internal sealed class TesseractCliOcrEngine : IOcrEngine
{
    private readonly string? _exePath;
    private readonly string _args;

    public TesseractCliOcrEngine(string? exePath = null, string language = "eng", int psm = 6)
    {
        _exePath = exePath ?? TryResolveExe();
        _args = $"stdin stdout -l {language} --psm {psm}";
    }

    public async Task<string> ReadTextAsync(Bitmap image, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_exePath) || !File.Exists(_exePath))
            throw new InvalidOperationException("tesseract.exe not found. Install Tesseract or set TESSERACT_EXE.");

        using var ms = new MemoryStream();
        // Preprocess: grayscale, upscale, and threshold
        using (var pre = Preprocess(image))
        {
            pre.Save(ms, ImageFormat.Png);
        }
        ms.Position = 0;

        var psi = new ProcessStartInfo
        {
            FileName = _exePath,
            Arguments = _args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // Stream PNG to stdin
        await ms.CopyToAsync(proc.StandardInput.BaseStream, ct);
        await proc.StandardInput.FlushAsync();
        proc.StandardInput.Close();

        string output = await proc.StandardOutput.ReadToEndAsync();
        string err = await proc.StandardError.ReadToEndAsync();
        await Task.Run(() => proc.WaitForExit());

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Tesseract error (code {proc.ExitCode}): {err}");

        return output.Replace("\r", "").Trim();
    }

    private static string? TryResolveExe()
    {
        // Priority: env var, common install path, PATH
        var ev = Environment.GetEnvironmentVariable("TESSERACT_EXE");
        if (!string.IsNullOrWhiteSpace(ev) && File.Exists(ev)) return ev;

        var common = @"C:\\Program Files\\Tesseract-OCR\\tesseract.exe";
        if (File.Exists(common)) return common;

        try
        {
            var where = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "tesseract",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var p = Process.Start(where)!;
            var path = p.StandardOutput.ReadLine();
            p.WaitForExit();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        }
        catch { /* ignore */ }

        return null;
    }

    private static Bitmap Preprocess(Bitmap src)
    {
        // 1) Convert to grayscale
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

        // 2) Upscale 2x for pixel fonts
        var scale = 2;
        var up = new Bitmap(gray.Width * scale, gray.Height * scale);
        using (var g = Graphics.FromImage(up))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(gray, new Rectangle(0, 0, up.Width, up.Height), 0, 0, gray.Width, gray.Height, GraphicsUnit.Pixel);
        }

        // 3) Apply binary threshold
        for (int y = 0; y < up.Height; y++)
        {
            for (int x = 0; x < up.Width; x++)
            {
                var c = up.GetPixel(x, y);
                int v = c.R > 140 ? 255 : 0;
                up.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        }

        gray.Dispose();
        return up;
    }
}

