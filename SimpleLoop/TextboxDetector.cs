using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SimpleLoop
{
    public class TextboxDetector
    {
        private Bitmap? _template;
        private readonly string _templatePath;

        public TextboxDetector(string templatePath)
        {
            _templatePath = templatePath;
            LoadTemplate();
        }

        private void LoadTemplate()
        {
            if (File.Exists(_templatePath))
            {
                _template = new Bitmap(_templatePath);
            }
        }

        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            if (_template == null) 
            {
                // Fallback to color-based detection for FF blue textbox
                return DetectByColor(screenshot);
            }

            // Template matching approach
            return TemplateMatch(screenshot, _template);
        }

        private Rectangle? DetectByColor(Bitmap image)
        {
            // FF1 textbox has a distinctive blue color: RGB(0, 88, 248) or similar
            var targetColor = Color.FromArgb(0, 88, 248);
            var tolerance = 30;

            int minX = image.Width, minY = image.Height;
            int maxX = 0, maxY = 0;
            bool foundPixels = false;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    
                    if (IsColorSimilar(pixel, targetColor, tolerance))
                    {
                        foundPixels = true;
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (!foundPixels) return null;

            // Add some padding and return the bounding rectangle
            var padding = 10;
            return new Rectangle(
                Math.Max(0, minX - padding),
                Math.Max(0, minY - padding),
                Math.Min(image.Width - (minX - padding), maxX - minX + 2 * padding),
                Math.Min(image.Height - (minY - padding), maxY - minY + 2 * padding)
            );
        }

        private bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
        }

        private Rectangle? TemplateMatch(Bitmap source, Bitmap template)
        {
            // Simple template matching - this is computationally expensive but works
            var sourceWidth = source.Width;
            var sourceHeight = source.Height;
            var templateWidth = template.Width;
            var templateHeight = template.Height;

            double bestMatch = 0;
            Point bestLocation = Point.Empty;

            for (int y = 0; y <= sourceHeight - templateHeight; y += 5) // Skip pixels for speed
            {
                for (int x = 0; x <= sourceWidth - templateWidth; x += 5)
                {
                    double match = CalculateMatch(source, template, x, y);
                    if (match > bestMatch)
                    {
                        bestMatch = match;
                        bestLocation = new Point(x, y);
                    }
                }
            }

            // If match is good enough, return the rectangle
            if (bestMatch > 0.8) // 80% similarity threshold
            {
                return new Rectangle(bestLocation.X, bestLocation.Y, templateWidth, templateHeight);
            }

            return null;
        }

        private double CalculateMatch(Bitmap source, Bitmap template, int offsetX, int offsetY)
        {
            int matches = 0;
            int total = 0;
            
            // Sample every 4th pixel for speed
            for (int y = 0; y < template.Height; y += 4)
            {
                for (int x = 0; x < template.Width; x += 4)
                {
                    var sourcePixel = source.GetPixel(offsetX + x, offsetY + y);
                    var templatePixel = template.GetPixel(x, y);
                    
                    if (IsColorSimilar(sourcePixel, templatePixel, 50))
                    {
                        matches++;
                    }
                    total++;
                }
            }

            return total > 0 ? (double)matches / total : 0;
        }

        // Fast textbox detection using a simple heuristic
        public Rectangle? DetectTextboxFast(Bitmap image)
        {
            // Look for horizontal blue lines (top/bottom of textbox)
            var blueColor = Color.FromArgb(0, 88, 248);
            var tolerance = 40;
            
            // Scan for horizontal blue lines
            for (int y = image.Height / 2; y < image.Height - 50; y += 2) // Start from middle, work down
            {
                int bluePixelCount = 0;
                int startX = -1;
                int endX = -1;

                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (IsColorSimilar(pixel, blueColor, tolerance))
                    {
                        if (startX == -1) startX = x;
                        endX = x;
                        bluePixelCount++;
                    }
                }

                // If we found a long horizontal blue line, assume it's the textbox
                if (bluePixelCount > 200 && (endX - startX) > 300) // Minimum width
                {
                    // Estimate textbox dimensions
                    int textboxHeight = 120; // Typical FF textbox height
                    return new Rectangle(
                        Math.Max(0, startX - 20),
                        Math.Max(0, y - 10),
                        Math.Min(image.Width - startX + 20, endX - startX + 40),
                        Math.Min(image.Height - y + 10, textboxHeight)
                    );
                }
            }

            return null;
        }

        // Ultra-fast detection - samples only specific regions where FF textboxes appear
        public Rectangle? DetectTextboxUltraFast(Bitmap image)
        {
            // FF textboxes typically appear in bottom portion of screen
            var searchStartY = (int)(image.Height * 0.6); // Start at 60% down
            var searchEndY = image.Height - 50;
            
            // Sample every 10th pixel for speed, every 5th row
            var blueColor = Color.FromArgb(0, 88, 248);
            var tolerance = 50;
            
            for (int y = searchStartY; y < searchEndY; y += 5)
            {
                int blueCount = 0;
                int firstBlue = -1;
                int lastBlue = -1;
                
                // Sample every 10th pixel horizontally
                for (int x = 0; x < image.Width; x += 10)
                {
                    var pixel = image.GetPixel(x, y);
                    if (IsColorSimilar(pixel, blueColor, tolerance))
                    {
                        if (firstBlue == -1) firstBlue = x;
                        lastBlue = x;
                        blueCount++;
                    }
                }
                
                // If we found enough blue pixels in a line, it's probably a textbox border
                if (blueCount > 15 && (lastBlue - firstBlue) > 250) // Relaxed requirements
                {
                    return new Rectangle(
                        Math.Max(0, firstBlue - 30),
                        Math.Max(0, y - 15),
                        Math.Min(image.Width - firstBlue + 30, lastBlue - firstBlue + 60),
                        120 // Standard textbox height
                    );
                }
            }
            
            return null;
        }
    }
}