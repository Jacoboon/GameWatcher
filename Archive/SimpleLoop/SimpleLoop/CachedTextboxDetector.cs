using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SimpleLoop
{
    public class CachedTextboxDetector
    {
        private Rectangle? _cachedTextboxRect;
        private DateTime _lastValidation = DateTime.MinValue;
        private readonly TimeSpan _revalidationInterval = TimeSpan.FromSeconds(5);
        
        // Template paths  
        private readonly string[] _templatePaths = {
            @"..\..\..\assets\templates\FF-TextBox-TL.png",
            @"..\..\..\assets\templates\FF-TextBox-TR.png", 
            @"..\..\..\assets\templates\FF-TextBox-Position.png"
        };
        
        private Bitmap[]? _templates;

        public CachedTextboxDetector()
        {
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            var validTemplates = new List<Bitmap>();
            
            foreach (var path in _templatePaths)
            {
                if (File.Exists(path))
                {
                    try 
                    {
                        validTemplates.Add(new Bitmap(path));
                        Console.WriteLine($"Loaded template: {Path.GetFileName(path)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load template {path}: {ex.Message}");
                    }
                }
            }
            
            _templates = validTemplates.ToArray();
            Console.WriteLine($"Loaded {_templates.Length} textbox templates");
        }

        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // If we have a cached position and it's still valid, use it!
            if (_cachedTextboxRect.HasValue && 
                DateTime.Now - _lastValidation < _revalidationInterval)
            {
                // Quick validation - just check if there's still content at the cached position
                if (ValidateQuick(screenshot, _cachedTextboxRect.Value))
                {
                    return _cachedTextboxRect.Value;
                }
                else
                {
                    Console.WriteLine("Cached textbox position no longer valid, re-detecting...");
                    _cachedTextboxRect = null;
                }
            }

            // Need to detect (first time or validation failed)
            var detectedRect = PerformFullDetection(screenshot);
            
            if (detectedRect.HasValue)
            {
                _cachedTextboxRect = detectedRect;
                _lastValidation = DateTime.Now;
                Console.WriteLine($"Textbox detected and cached at: {detectedRect.Value}");
            }
            
            return detectedRect;
        }

        private bool ValidateQuick(Bitmap screenshot, Rectangle rect)
        {
            // Quick validation - just check if the area still looks like a textbox
            // Look for blue border pixels at expected positions
            try
            {
                var blueColor = Color.FromArgb(0, 88, 248);
                var tolerance = 50;
                
                // Check top border
                int blueCount = 0;
                for (int x = rect.Left; x < rect.Right && x < screenshot.Width; x += 10)
                {
                    if (rect.Top < screenshot.Height)
                    {
                        var pixel = screenshot.GetPixel(x, rect.Top);
                        if (IsColorSimilar(pixel, blueColor, tolerance))
                            blueCount++;
                    }
                }
                
                // If we found some blue pixels, assume the textbox is still there
                return blueCount > 3;
            }
            catch
            {
                return false; // If anything goes wrong, force re-detection
            }
        }

        private Rectangle? PerformFullDetection(Bitmap screenshot)
        {
            // Try templates first if available
            if (_templates != null && _templates.Length > 0)
            {
                foreach (var template in _templates)
                {
                    var rect = TemplateMatch(screenshot, template);
                    if (rect.HasValue)
                    {
                        // Expand template match to full textbox area
                        return ExpandToFullTextbox(rect.Value);
                    }
                }
            }

            // Fallback to color detection
            return DetectByColorFast(screenshot);
        }

        private Rectangle? TemplateMatch(Bitmap source, Bitmap template)
        {
            // Fast template matching - sample every 8th pixel for speed
            var bestMatch = 0.0;
            Point bestLocation = Point.Empty;
            var threshold = 0.7; // 70% similarity required

            // Only search bottom half where FF textboxes appear
            var searchStartY = source.Height / 2;
            var searchEndY = source.Height - template.Height;
            var searchEndX = source.Width - template.Width;

            for (int y = searchStartY; y < searchEndY; y += 8)
            {
                for (int x = 0; x < searchEndX; x += 8)
                {
                    var match = CalculateMatchFast(source, template, x, y);
                    if (match > bestMatch)
                    {
                        bestMatch = match;
                        bestLocation = new Point(x, y);
                    }
                    
                    // Early exit if we find a very good match
                    if (match > 0.9)
                    {
                        return new Rectangle(bestLocation.X, bestLocation.Y, template.Width, template.Height);
                    }
                }
            }

            return bestMatch > threshold ? 
                new Rectangle(bestLocation.X, bestLocation.Y, template.Width, template.Height) : 
                null;
        }

        private double CalculateMatchFast(Bitmap source, Bitmap template, int offsetX, int offsetY)
        {
            int matches = 0;
            int total = 0;
            
            // Sample every 4th pixel for speed
            for (int y = 0; y < template.Height; y += 4)
            {
                for (int x = 0; x < template.Width; x += 4)
                {
                    if (offsetX + x < source.Width && offsetY + y < source.Height)
                    {
                        var sourcePixel = source.GetPixel(offsetX + x, offsetY + y);
                        var templatePixel = template.GetPixel(x, y);
                        
                        if (IsColorSimilar(sourcePixel, templatePixel, 60))
                        {
                            matches++;
                        }
                        total++;
                    }
                }
            }

            return total > 0 ? (double)matches / total : 0;
        }

        private Rectangle ExpandToFullTextbox(Rectangle templateRect)
        {
            // Assuming template is a corner, expand to full textbox
            // FF textboxes are typically ~500x120 pixels
            return new Rectangle(
                Math.Max(0, templateRect.X - 20),
                Math.Max(0, templateRect.Y - 10), 
                500,  // Standard FF textbox width
                120   // Standard FF textbox height
            );
        }

        private Rectangle? DetectByColorFast(Bitmap image)
        {
            // Ultra-fast color detection - only search bottom portion
            var searchStartY = (int)(image.Height * 0.6);
            var searchEndY = image.Height - 50;
            
            var blueColor = Color.FromArgb(0, 88, 248);
            var tolerance = 50;
            
            // Sample every 15th pixel for maximum speed
            for (int y = searchStartY; y < searchEndY; y += 10)
            {
                int blueCount = 0;
                int firstBlue = -1;
                int lastBlue = -1;
                
                for (int x = 0; x < image.Width; x += 15)
                {
                    var pixel = image.GetPixel(x, y);
                    if (IsColorSimilar(pixel, blueColor, tolerance))
                    {
                        if (firstBlue == -1) firstBlue = x;
                        lastBlue = x;
                        blueCount++;
                    }
                }
                
                if (blueCount > 10 && (lastBlue - firstBlue) > 200)
                {
                    return new Rectangle(
                        Math.Max(0, firstBlue - 30),
                        Math.Max(0, y - 15),
                        Math.Min(image.Width - firstBlue + 30, lastBlue - firstBlue + 60),
                        120
                    );
                }
            }
            
            return null;
        }

        private bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
        }

        public void InvalidateCache()
        {
            _cachedTextboxRect = null;
            Console.WriteLine("Textbox cache invalidated");
        }
    }
}