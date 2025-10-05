using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using Tesseract;

namespace SimpleLoop
{
    public class SimpleOCR : IOcrEngine
    {
        private TesseractEngine? _engine;
        private readonly string _tessDataPath;

        public SimpleOCR(string tessDataPath = @"C:\Program Files\Tesseract-OCR\tessdata")
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
                    Console.WriteLine($"Checking Tesseract path: {path}");
                    if (System.IO.Directory.Exists(path))
                    {
                        Console.WriteLine($"Directory exists, trying to initialize Tesseract at: {path}");
                        _engine = new TesseractEngine(path, "eng", EngineMode.Default);
                        
                        // Test the engine with a simple operation
                        _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?'-");
                        
                        Console.WriteLine($"✅ Tesseract engine created successfully!");
                        Console.WriteLine($"✅ Variables set successfully!");
                        Console.WriteLine($"✅ Full initialization at: {path}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"Directory does not exist: {path}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to initialize Tesseract at {path}: {ex.Message}");
                    Console.WriteLine($"❌ Exception type: {ex.GetType().Name}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                }
            }
            
            Console.WriteLine("❌ OCR will be disabled. Could not find Tesseract data in any standard location.");
            Console.WriteLine("❌ Available paths checked:");
            foreach (var path in tesseractPaths.Where(p => !string.IsNullOrEmpty(p)))
            {
                Console.WriteLine($"   - {path}");
            }
        }

        public string ExtractText(Bitmap textboxImage)
        {
            if (_engine == null) 
                return "[OCR not available]";

            try
            {
                // Preprocess the image for better OCR
                var processedImage = PreprocessImage(textboxImage);
                
                using var pix = Pix.LoadFromMemory(ImageToByteArray(processedImage));
                using var page = _engine.Process(pix);
                
                var text = page.GetText().Trim();
                var confidence = page.GetMeanConfidence();
                
                Console.WriteLine($"OCR Confidence: {confidence:F2}");
                
                return string.IsNullOrWhiteSpace(text) ? "[No text detected]" : text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR Error: {ex.Message}");
                return "[OCR Error]";
            }
        }

        private Bitmap PreprocessImage(Bitmap original)
        {
            // Create a copy to work with
            var processed = new Bitmap(original.Width * 3, original.Height * 3); // Scale up for better OCR
            
            using (var g = Graphics.FromImage(processed))
            {
                // Scale up the image
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(original, 0, 0, processed.Width, processed.Height);
            }

            // Convert to grayscale and apply threshold
            for (int y = 0; y < processed.Height; y++)
            {
                for (int x = 0; x < processed.Width; x++)
                {
                    var pixel = processed.GetPixel(x, y);
                    var gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                    
                    // Apply threshold - make text black on white background
                    var newColor = gray > 128 ? Color.White : Color.Black;
                    processed.SetPixel(x, y, newColor);
                }
            }

            return processed;
        }

        // Fast text extraction with minimal preprocessing
        public string ExtractTextFast(Bitmap textboxImage)
        {
            if (_engine == null) 
            {
                Console.WriteLine("OCR Engine is null!");
                return "[OCR not available]";
            }

            try
            {
                Console.WriteLine($"Processing {textboxImage.Width}x{textboxImage.Height} image with Tesseract...");
                using var pix = Pix.LoadFromMemory(ImageToByteArray(textboxImage));
                using var page = _engine.Process(pix);
                var result = page.GetText().Trim();
                var confidence = page.GetMeanConfidence();
                Console.WriteLine($"OCR Result: '{result}' (confidence: {confidence:F2})");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fast OCR Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return "[OCR Error]";
            }
        }

        private byte[] ImageToByteArray(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}