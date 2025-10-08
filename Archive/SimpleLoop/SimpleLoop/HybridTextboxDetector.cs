using System;
using System.Drawing;
using System.IO;

namespace SimpleLoop
{
    public class HybridTextboxDetector : ITextboxDetector
    {
        private Bitmap? _topLeft;
        private Bitmap? _topRight;
        private Bitmap? _bottomLeft;
        private Bitmap? _bottomRight;
        
        public HybridTextboxDetector()
        {
            LoadCornerTemplates();
        }
        
        private void LoadCornerTemplates()
        {
            try
            {
                var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "assets", "templates");
                
                if (File.Exists(basePath + "FF-TextBox-TL.png"))
                    _topLeft = new Bitmap(basePath + "FF-TextBox-TL.png");
                    
                if (File.Exists(basePath + "FF-TextBox-TR.png"))
                    _topRight = new Bitmap(basePath + "FF-TextBox-TR.png");
                    
                if (File.Exists(basePath + "FF-TextBox-BL.png"))
                    _bottomLeft = new Bitmap(basePath + "FF-TextBox-BL.png");
                    
                if (File.Exists(basePath + "FF-TextBox-BR.png"))
                    _bottomRight = new Bitmap(basePath + "FF-TextBox-BR.png");
                    
                Console.WriteLine($"🔍 Hybrid detector loaded corner templates: TL={_topLeft != null}, TR={_topRight != null}, BL={_bottomLeft != null}, BR={_bottomRight != null}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to load corner templates: {ex.Message}");
            }
        }

        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // Phase 1: Use blue field detection to find candidate areas
            var blueCandidate = FindBlueFieldCandidate(screenshot);
            if (!blueCandidate.HasValue)
            {
                return null; // No blue field found, no textbox
            }

            Console.WriteLine($"🟦 Blue field candidate: {blueCandidate.Value}");

            // Phase 2: If we have corner templates, validate and refine with corner detection
            if (_topLeft != null && _topRight != null && _bottomLeft != null && _bottomRight != null)
            {
                var cornerRefined = RefineWithCorners(screenshot, blueCandidate.Value);
                if (cornerRefined.HasValue)
                {
                    Console.WriteLine($"🎯 HYBRID TEXTBOX: {cornerRefined.Value} (blue field + corner validation)");
                    return cornerRefined.Value;
                }
            }

            // Phase 3: Fall back to blue field result if corner validation fails
            Console.WriteLine($"🎯 BLUE FIELD TEXTBOX: {blueCandidate.Value} (corners unavailable or failed)");
            return blueCandidate.Value;
        }

        private Rectangle? FindBlueFieldCandidate(Bitmap screenshot)
        {
            // Use the proven blue field detection logic from SimpleTextboxDetector
            var targetBlue = Color.FromArgb(66, 66, 231);
            var tolerance = 60;
            
            // Search entire screen height
            for (int y = 50; y < screenshot.Height - 150; y += 3)
            {
                int bluePixels = 0;
                int startX = -1;
                int endX = -1;
                
                // Sample across the width looking for blue horizontal lines
                for (int x = 50; x < screenshot.Width - 50; x += 8)
                {
                    try
                    {
                        var pixel = screenshot.GetPixel(x, y);
                        
                        if (IsFF1Blue(pixel, tolerance))
                        {
                            if (startX == -1) startX = x;
                            endX = x;
                            bluePixels++;
                        }
                    }
                    catch 
                    {
                        continue;
                    }
                }
                
                // If we found a long horizontal blue line, it's a candidate
                if (bluePixels > 20 && (endX - startX) > 400)
                {
                    // Return expanded search area around the blue field for corner validation
                    var candidateRect = new Rectangle(
                        Math.Max(0, startX - 50),
                        Math.Max(0, y - 30),
                        Math.Min(screenshot.Width - (startX - 50), (endX - startX) + 100),
                        Math.Min(screenshot.Height - (y - 30), 200)
                    );
                    
                    return candidateRect;
                }
            }
            
            return null;
        }

        private Rectangle? RefineWithCorners(Bitmap screenshot, Rectangle blueArea)
        {
            // Look for corners within the blue field area (plus some padding)
            var searchArea = new Rectangle(
                Math.Max(0, blueArea.X - 20),
                Math.Max(0, blueArea.Y - 20), 
                Math.Min(screenshot.Width - (blueArea.X - 20), blueArea.Width + 40),
                Math.Min(screenshot.Height - (blueArea.Y - 20), blueArea.Height + 40)
            );

            // Find top-left corner first (most reliable)
            var tlCorner = FindCorner(screenshot, _topLeft!, searchArea, 0.75);
            if (!tlCorner.HasValue)
            {
                Console.WriteLine("⚠️ TL corner not found in blue area");
                return null;
            }

            Console.WriteLine($"📍 Found TL corner at {tlCorner}");

            // Look for top-right corner to the right of TL
            var trSearchArea = new Rectangle(
                tlCorner.Value.X + 400, // FF1 textboxes are ~800-1100px wide
                tlCorner.Value.Y - 10,
                Math.Min(600, screenshot.Width - (tlCorner.Value.X + 400)),
                30
            );
            
            var trCorner = FindCorner(screenshot, _topRight!, trSearchArea, 0.75);
            if (!trCorner.HasValue)
            {
                Console.WriteLine("⚠️ TR corner not found, trying broader search");
                // Try broader search with lower threshold
                trSearchArea.X = tlCorner.Value.X + 300;
                trSearchArea.Width = Math.Min(700, screenshot.Width - trSearchArea.X);
                trCorner = FindCorner(screenshot, _topRight!, trSearchArea, 0.65);
            }

            if (trCorner.HasValue)
            {
                Console.WriteLine($"📍 Found TR corner at {trCorner}");
                
                // Calculate precise textbox rectangle from corner positions
                var textboxWidth = trCorner.Value.X - tlCorner.Value.X + _topRight!.Width;
                var textboxHeight = 120; // Standard FF1 textbox height
                
                var preciseRect = new Rectangle(
                    tlCorner.Value.X,
                    tlCorner.Value.Y,
                    textboxWidth,
                    textboxHeight
                );
                
                return preciseRect;
            }

            Console.WriteLine("⚠️ TR corner not found, using TL corner estimate");
            
            // If we only found TL corner, estimate textbox size
            return new Rectangle(
                tlCorner.Value.X,
                tlCorner.Value.Y,
                900, // Estimated width
                120  // Standard height
            );
        }

        private Point? FindCorner(Bitmap screenshot, Bitmap template, Rectangle searchArea, double threshold)
        {
            var maxX = Math.Min(searchArea.Right, screenshot.Width - template.Width);
            var maxY = Math.Min(searchArea.Bottom, screenshot.Height - template.Height);
            
            double bestMatch = 0.0;
            Point? bestLocation = null;
            
            // Search with step size 2 for speed (19x19 templates are small enough)
            for (int y = searchArea.Y; y < maxY; y += 2)
            {
                for (int x = searchArea.X; x < maxX; x += 2)
                {
                    double match = CalculateTemplateMatch(screenshot, template, x, y);
                    
                    if (match > bestMatch)
                    {
                        bestMatch = match;
                        bestLocation = new Point(x, y);
                    }
                    
                    if (match >= threshold)
                    {
                        return new Point(x, y);
                    }
                }
            }
            
            // Log best match for debugging
            if (bestLocation.HasValue)
            {
                Console.WriteLine($"🔍 Best corner match: {bestMatch:F3} at {bestLocation} (threshold: {threshold:F3})");
            }
            
            return null;
        }

        private double CalculateTemplateMatch(Bitmap screenshot, Bitmap template, int offsetX, int offsetY)
        {
            int matches = 0;
            int total = 0;
            
            // Sample every pixel for 19x19 templates (they're small enough)
            for (int y = 0; y < template.Height; y++)
            {
                for (int x = 0; x < template.Width; x++)
                {
                    var templatePixel = template.GetPixel(x, y);
                    
                    // Skip transparent pixels in template (rounded corners)
                    if (templatePixel.A < 128)
                    {
                        continue; // Don't count transparent areas
                    }
                    
                    if (offsetX + x >= screenshot.Width || offsetY + y >= screenshot.Height) 
                        continue;
                        
                    var screenPixel = screenshot.GetPixel(offsetX + x, offsetY + y);
                    
                    // Compare RGB values with tolerance for compression artifacts
                    bool match = Math.Abs(screenPixel.R - templatePixel.R) < 30 &&
                               Math.Abs(screenPixel.G - templatePixel.G) < 30 &&
                               Math.Abs(screenPixel.B - templatePixel.B) < 30;
                    
                    if (match) matches++;
                    total++;
                }
            }
            
            return total > 0 ? (double)matches / total : 0.0;
        }
        
        private bool IsFF1Blue(Color pixel, int tolerance)
        {
            // Check multiple FF1 blue variations (from SimpleTextboxDetector)
            var blues = new[] {
                Color.FromArgb(66, 66, 231),   // Dark blue
                Color.FromArgb(99, 99, 255),   // Medium blue  
                Color.FromArgb(33, 33, 165),   // Very dark blue
                Color.FromArgb(0, 88, 248),    // Bright blue
                Color.FromArgb(82, 82, 247)    // Another variant
            };
            
            foreach (var blue in blues)
            {
                if (Math.Abs(pixel.R - blue.R) <= tolerance &&
                    Math.Abs(pixel.G - blue.G) <= tolerance &&
                    Math.Abs(pixel.B - blue.B) <= tolerance)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}