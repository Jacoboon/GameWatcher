using System;
using System.Drawing;
using System.Threading.Tasks;

namespace SimpleLoop
{
    /// <summary>
    /// Hybrid OCR engine that compares results from multiple OCR engines
    /// </summary>
    public class HybridOCR : IOcrEngine
    {
        private readonly SimpleOCR _tesseractOcr;
        private readonly WindowsOCR _windowsOcr;
        private bool _preferWindowsOcr = false;

        public bool WindowsOcrAvailable => _windowsOcr.IsAvailable;

        public HybridOCR()
        {
            _tesseractOcr = new SimpleOCR();
            _windowsOcr = new WindowsOCR();
            
            Console.WriteLine($"[Hybrid OCR] Tesseract: ✅ Available");
            Console.WriteLine($"[Hybrid OCR] Windows OCR: {(_windowsOcr.IsAvailable ? "✅ Available" : "❌ Not Available")}");
            
            if (_windowsOcr.IsAvailable)
            {
                Console.WriteLine("[Hybrid OCR] Using Windows OCR as primary engine");
                _preferWindowsOcr = true;
            }
            else
            {
                Console.WriteLine("[Hybrid OCR] Using Tesseract as fallback engine");
            }
        }

        /// <summary>
        /// Extract text using the best available OCR engine with enhanced preprocessing
        /// </summary>
        public string ExtractTextFast(Bitmap image)
        {
            try
            {
                Console.WriteLine($"[Hybrid OCR] Processing {image.Width}x{image.Height} image");
                
                // Create enhanced B&W version for better OCR
                using var preprocessed = CreateSolidBWVersion(image);
                
                string windowsResult = "";
                string tesseractResult = "";
                
                // Try Windows OCR if available
                if (_windowsOcr.IsAvailable)
                {
                    Console.WriteLine("[Hybrid OCR] Testing Windows OCR engine");
                    windowsResult = _windowsOcr.ExtractTextFast(preprocessed);
                    Console.WriteLine($"[Hybrid OCR] Windows OCR: '{windowsResult}' (length: {windowsResult.Length})");
                }

                // Try Tesseract with preprocessing
                Console.WriteLine("[Hybrid OCR] Testing Tesseract OCR engine with preprocessing");
                tesseractResult = _tesseractOcr.ExtractTextFast(preprocessed);
                Console.WriteLine($"[Hybrid OCR] Tesseract: '{tesseractResult}' (length: {tesseractResult.Length})");
                
                // Choose the best result based on length and quality
                if (string.IsNullOrWhiteSpace(windowsResult) && string.IsNullOrWhiteSpace(tesseractResult))
                {
                    Console.WriteLine("[Hybrid OCR] Both engines failed");
                    return "";
                }
                
                if (string.IsNullOrWhiteSpace(windowsResult))
                {
                    Console.WriteLine("[Hybrid OCR] Using Tesseract (Windows OCR failed)");
                    return tesseractResult;
                }
                
                if (string.IsNullOrWhiteSpace(tesseractResult))
                {
                    Console.WriteLine("[Hybrid OCR] Using Windows OCR (Tesseract failed)");
                    return windowsResult;
                }
                
                // Both have results - compare quality
                // Prefer longer results that look more like complete sentences
                if (tesseractResult.Length > windowsResult.Length + 5 && tesseractResult.Length > 10)
                {
                    Console.WriteLine("[Hybrid OCR] Using Tesseract (longer, more complete result)");
                    return tesseractResult;
                }
                else if (windowsResult.Length > 5)
                {
                    Console.WriteLine("[Hybrid OCR] Using Windows OCR (reasonable length)");
                    return windowsResult;
                }
                else
                {
                    Console.WriteLine("[Hybrid OCR] Using Tesseract (fallback)");
                    return tesseractResult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hybrid OCR] Error: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Create a solid black & white version optimized for OCR
        /// </summary>
        private Bitmap CreateSolidBWVersion(Bitmap original)
        {
            try
            {
                // Scale up 3x for better OCR accuracy (4x was too much)
                var scaledWidth = original.Width * 3;
                var scaledHeight = original.Height * 3;
                var scaled = new Bitmap(scaledWidth, scaledHeight);
                
                using (var g = Graphics.FromImage(scaled))
                {
                    // Use nearest neighbor for pixelated text to preserve sharp edges
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.DrawImage(original, 0, 0, scaledWidth, scaledHeight);
                }

                // Convert to pure B&W with FF1-optimized threshold
                for (int y = 0; y < scaled.Height; y++)
                {
                    for (int x = 0; x < scaled.Width; x++)
                    {
                        var pixel = scaled.GetPixel(x, y);
                        
                        // FF1 uses white text on blue background
                        // Blue background: RGB ~(0, 0, 139) = low luminance
                        // White text: RGB ~(255, 255, 255) = high luminance
                        var luminance = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                        
                        // Lower threshold to catch more text pixels
                        // Blue background ~= 15-30 luminance, white text ~= 220-255
                        var isText = luminance > 150;
                        
                        // Set to pure black (text) or pure white (background)
                        scaled.SetPixel(x, y, isText ? Color.Black : Color.White);
                    }
                }

                Console.WriteLine($"[Hybrid OCR] Created {scaledWidth}x{scaledHeight} B&W version with optimized threshold");
                return scaled;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hybrid OCR] Preprocessing error: {ex.Message}");
                // Return original if preprocessing fails
                return new Bitmap(original);
            }
        }

        /// <summary>
        /// Compare results from both OCR engines for debugging
        /// </summary>
        public async Task<string> ExtractTextWithComparison(Bitmap image)
        {
            try
            {
                Console.WriteLine("[Hybrid OCR] Running comparison mode...");
                
                string tesseractResult = "";
                string windowsResult = "";
                
                // Get Tesseract result
                try
                {
                    tesseractResult = _tesseractOcr.ExtractTextFast(image);
                    Console.WriteLine($"[Hybrid OCR] Tesseract: '{tesseractResult}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Hybrid OCR] Tesseract error: {ex.Message}");
                }
                
                // Get Windows OCR result
                if (_windowsOcr.IsAvailable)
                {
                    try
                    {
                        windowsResult = await _windowsOcr.ExtractTextAsync(image);
                        Console.WriteLine($"[Hybrid OCR] Windows: '{windowsResult}'");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Hybrid OCR] Windows OCR error: {ex.Message}");
                    }
                }
                
                // Comparison logic - prefer longer, more coherent results
                if (string.IsNullOrWhiteSpace(windowsResult))
                {
                    Console.WriteLine("[Hybrid OCR] Using Tesseract (Windows OCR failed)");
                    return tesseractResult;
                }
                
                if (string.IsNullOrWhiteSpace(tesseractResult))
                {
                    Console.WriteLine("[Hybrid OCR] Using Windows OCR (Tesseract failed)");
                    return windowsResult;
                }
                
                // If both have results, compare quality
                if (windowsResult.Length > tesseractResult.Length && windowsResult.Length > 5)
                {
                    Console.WriteLine("[Hybrid OCR] Using Windows OCR (longer result)");
                    return windowsResult;
                }
                else
                {
                    Console.WriteLine("[Hybrid OCR] Using Tesseract (default preference)");
                    return tesseractResult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hybrid OCR] Comparison error: {ex.Message}");
                return _tesseractOcr.ExtractTextFast(image);
            }
        }

        public void Dispose()
        {
            _tesseractOcr?.Dispose();
            _windowsOcr?.Dispose();
        }
    }
}