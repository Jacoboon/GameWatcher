using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;

namespace GameWatcher.Engine.Ocr;

/// <summary>
/// Tesseract OCR engine - ported from V1 SimpleLoop with proven performance
/// Uses enhanced preprocessing specifically designed for FF1 dialogue detection
/// </summary>
public class TesseractOcrEngine : IOcrEngine
{
    private TesseractEngine? _engine;
    private readonly string _tessDataPath;
    private bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    public TesseractOcrEngine(string tessDataPath = @"C:\Program Files\Tesseract-OCR\tessdata")
    {
        _tessDataPath = tessDataPath;
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        var tesseractPaths = new[]
        {
            @"C:\Program Files\Tesseract-OCR\tessdata",
            @"C:\Program Files (x86)\Tesseract-OCR\tessdata",
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX"),
            _tessDataPath
        };

        foreach (var path in tesseractPaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            try
            {
                if (Directory.Exists(path))
                {
                    _engine = new TesseractEngine(path, "eng", EngineMode.Default);
                    
                    // Configure character whitelist for better accuracy
                    _engine.SetVariable("tessedit_char_whitelist", 
                        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?'-");
                    
                    _isAvailable = true;
                    Console.WriteLine($"[Tesseract OCR] ✅ Initialized at: {path}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tesseract OCR] Failed to initialize at {path}: {ex.Message}");
            }
        }
        
        _isAvailable = false;
        Console.WriteLine("[Tesseract OCR] ❌ Not available - could not find tessdata");
    }

    public async Task<string> ExtractTextAsync(Bitmap image)
    {
        return await Task.Run(() => ExtractTextFast(image));
    }

    public string ExtractTextFast(Bitmap image)
    {
        if (_engine == null || !_isAvailable)
        {
            return "";
        }

        try
        {
            // Apply enhanced preprocessing specifically designed for FF1
            using var preprocessed = PreprocessForFF1(image);
            
            // Convert to format Tesseract can process
            using var pix = Pix.LoadFromMemory(ImageToByteArray(preprocessed));
            using var page = _engine.Process(pix);
            
            var text = page.GetText().Trim();
            var confidence = page.GetMeanConfidence();
            
            Console.WriteLine($"[Tesseract OCR] Confidence: {confidence:F2}");
            
            return text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tesseract OCR] Error: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Enhanced image preprocessing specifically for FF1 dialogue boxes
    /// Ported from V1 EnhancedOCR with proven optimizations
    /// </summary>
    private static Bitmap PreprocessForFF1(Bitmap source)
    {
        try 
        {
            // Step 1: Convert to grayscale with optimized weights for blue backgrounds
            using var grayscale = ConvertToGrayscaleOptimized(source);
            
            // Step 2: Scale up 3x for better OCR accuracy
            var scaledWidth = grayscale.Width * 3;
            var scaledHeight = grayscale.Height * 3;
            var scaled = new Bitmap(scaledWidth, scaledHeight);
            
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.DrawImage(grayscale, 0, 0, scaledWidth, scaledHeight);
            }
            
            // Step 3: Apply threshold to create clean black/white image
            var enhanced = ApplySimpleThreshold(scaled, 128);
            scaled.Dispose();
            
            return enhanced;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tesseract OCR] Preprocessing error: {ex.Message}");
            return new Bitmap(source);
        }
    }

    private static Bitmap ConvertToGrayscaleOptimized(Bitmap source)
    {
        var grayscale = new Bitmap(source.Width, source.Height);
        
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                
                // Optimized weights for white text on blue background
                var gray = (int)(pixel.R * 0.2 + pixel.G * 0.3 + pixel.B * 0.5);
                
                // Invert if we detect blue background
                if (pixel.B > pixel.R && pixel.B > pixel.G && pixel.B > 100)
                {
                    gray = 255 - gray;
                }
                
                gray = Math.Max(0, Math.Min(255, gray));
                var grayColor = Color.FromArgb(gray, gray, gray);
                grayscale.SetPixel(x, y, grayColor);
            }
        }
        
        return grayscale;
    }

    private static Bitmap ApplySimpleThreshold(Bitmap source, int threshold)
    {
        var result = new Bitmap(source.Width, source.Height);
        
        for (int x = 0; x < source.Width; x++)
        {
            for (int y = 0; y < source.Height; y++)
            {
                var pixel = source.GetPixel(x, y);
                var gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                
                // Binary threshold: white text on black background for Tesseract
                result.SetPixel(x, y, gray > threshold ? Color.White : Color.Black);
            }
        }
        
        return result;
    }

    private byte[] ImageToByteArray(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
