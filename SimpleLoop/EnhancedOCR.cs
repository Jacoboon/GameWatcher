using System;
using System.Drawing;

namespace SimpleLoop
{
    /// <summary>
    /// Enhanced OCR engine with better preprocessing for FF1 dialogue detection
    /// </summary>
    public class EnhancedOCR : IDisposable
    {
        private readonly SimpleOCR _tesseractOcr;
        
        public EnhancedOCR()
        {
            _tesseractOcr = new SimpleOCR();
            Console.WriteLine("✅ Enhanced OCR initialized with improved FF1 preprocessing");
        }
        
        /// <summary>
        /// Extract text using enhanced preprocessing
        /// </summary>
        public string ExtractTextFast(Bitmap image)
        {
            try
            {
                Console.WriteLine($"EnhancedOCR: Processing {image.Width}x{image.Height} image");
                
                // Apply enhanced preprocessing specifically designed for FF1
                Console.WriteLine("EnhancedOCR: Starting preprocessing...");
                using var preprocessed = PreprocessForFF1OCR(image);
                Console.WriteLine($"EnhancedOCR: Preprocessed to {preprocessed.Width}x{preprocessed.Height}");
                
                // Use Tesseract with the enhanced image
                Console.WriteLine("EnhancedOCR: Calling SimpleOCR...");
                var result = _tesseractOcr.ExtractTextFast(preprocessed);
                Console.WriteLine($"EnhancedOCR: Result: '{result}' (length: {result?.Length ?? 0})");
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnhancedOCR Error: {ex.Message}");
                Console.WriteLine($"EnhancedOCR Error Stack: {ex.StackTrace}");
                return "[EnhancedOCR Error]";
            }
        }
        
        /// <summary>
        /// Enhanced image preprocessing specifically for FF1 dialogue boxes
        /// </summary>
        public static Bitmap PreprocessForFF1OCR(Bitmap source)
        {
            // Simplified preprocessing to avoid hangs with large images
            try 
            {
                Console.WriteLine("Step 1: Converting to grayscale...");
                // Step 1: Convert to grayscale with optimized weights for blue backgrounds
                var grayscale = ConvertToGrayscaleOptimized(source);
                
                Console.WriteLine("Step 2: Scaling image...");
                // Step 2: Scale up moderately (3x instead of 6x for stability)
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
                grayscale.Dispose();
                
                Console.WriteLine("Step 3: Applying threshold...");
                // Step 3: Simple threshold instead of adaptive
                var enhanced = ApplySimpleThreshold(scaled, 128);
                scaled.Dispose();
                
                Console.WriteLine("Preprocessing complete!");
                return enhanced;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Preprocessing error: {ex.Message}");
                // Return a copy of original if preprocessing fails
                return new Bitmap(source);
            }
        }
        
        private static Bitmap AdjustContrast(Bitmap source, float contrast)
        {
            var result = new Bitmap(source.Width, source.Height);
            var factor = (259f * (contrast + 255f)) / (255f * (259f - contrast));
            
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    
                    var newR = Math.Max(0, Math.Min(255, (int)(factor * (pixel.R - 128) + 128)));
                    var newG = Math.Max(0, Math.Min(255, (int)(factor * (pixel.G - 128) + 128)));
                    var newB = Math.Max(0, Math.Min(255, (int)(factor * (pixel.B - 128) + 128)));
                    
                    result.SetPixel(x, y, Color.FromArgb(newR, newG, newB));
                }
            }
            
            return result;
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
                    // Give more weight to blue channel differences
                    var gray = (int)(pixel.R * 0.2 + pixel.G * 0.3 + pixel.B * 0.5);
                    
                    // Invert if we detect blue background (blue > red && blue > green)
                    if (pixel.B > pixel.R && pixel.B > pixel.G && pixel.B > 100)
                    {
                        gray = 255 - gray; // Invert for blue backgrounds
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
                    
                    // Simple binary threshold
                    if (gray > threshold)
                        result.SetPixel(x, y, Color.White);
                    else
                        result.SetPixel(x, y, Color.Black);
                }
            }
            
            return result;
        }
        
        private static Bitmap ApplyAdaptiveThreshold(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height);
            var windowSize = 15; // Adaptive window size
            
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    // Calculate local average in window around pixel
                    var sum = 0;
                    var count = 0;
                    
                    for (int dy = -windowSize/2; dy <= windowSize/2; dy++)
                    {
                        for (int dx = -windowSize/2; dx <= windowSize/2; dx++)
                        {
                            var nx = x + dx;
                            var ny = y + dy;
                            
                            if (nx >= 0 && nx < source.Width && ny >= 0 && ny < source.Height)
                            {
                                sum += source.GetPixel(nx, ny).R;
                                count++;
                            }
                        }
                    }
                    
                    var localAverage = count > 0 ? sum / count : 128;
                    var currentPixel = source.GetPixel(x, y).R;
                    
                    // Threshold based on local average
                    var threshold = localAverage * 0.9; // Slightly below local average
                    var newColor = currentPixel > threshold ? Color.White : Color.Black;
                    
                    result.SetPixel(x, y, newColor);
                }
            }
            
            return result;
        }
        
        private static Bitmap RemoveNoise(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height);
            
            for (int y = 1; y < source.Height - 1; y++)
            {
                for (int x = 1; x < source.Width - 1; x++)
                {
                    var center = source.GetPixel(x, y);
                    
                    // Count neighboring black pixels
                    int blackNeighbors = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            
                            var neighbor = source.GetPixel(x + dx, y + dy);
                            if (neighbor.R < 128) blackNeighbors++; // Black pixel
                        }
                    }
                    
                    // Remove isolated pixels (noise reduction)
                    if (center.R < 128 && blackNeighbors < 2) // Black pixel with few black neighbors
                    {
                        result.SetPixel(x, y, Color.White); // Remove noise
                    }
                    else if (center.R >= 128 && blackNeighbors > 6) // White pixel surrounded by black
                    {
                        result.SetPixel(x, y, Color.Black); // Fill holes
                    }
                    else
                    {
                        result.SetPixel(x, y, center); // Keep as is
                    }
                }
            }
            
            // Copy border pixels as-is
            for (int x = 0; x < source.Width; x++)
            {
                result.SetPixel(x, 0, source.GetPixel(x, 0));
                result.SetPixel(x, source.Height - 1, source.GetPixel(x, source.Height - 1));
            }
            for (int y = 0; y < source.Height; y++)
            {
                result.SetPixel(0, y, source.GetPixel(0, y));
                result.SetPixel(source.Width - 1, y, source.GetPixel(source.Width - 1, y));
            }
            
            return result;
        }
        
        public void Dispose()
        {
            _tesseractOcr?.Dispose();
        }
    }
}