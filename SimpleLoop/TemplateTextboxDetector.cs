using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SimpleLoop
{
    public class TemplateTextboxDetector : ITextboxDetector
    {
        private Bitmap? _topLeft;
        private Bitmap? _topRight;
        private Bitmap? _bottomLeft;
        private Bitmap? _bottomRight;
        
        public TemplateTextboxDetector()
        {
            LoadTemplates();
        }
        
        private void LoadTemplates()
        {
            try
            {
                var basePath = @"C:\Code Projects\GameWatcher\assets\templates\";
                
                if (File.Exists(basePath + "FF-TextBox-TL.png"))
                    _topLeft = new Bitmap(basePath + "FF-TextBox-TL.png");
                    
                if (File.Exists(basePath + "FF-TextBox-TR.png"))
                    _topRight = new Bitmap(basePath + "FF-TextBox-TR.png");
                    
                if (File.Exists(basePath + "FF-TextBox-BL.png"))
                    _bottomLeft = new Bitmap(basePath + "FF-TextBox-BL.png");
                    
                if (File.Exists(basePath + "FF-TextBox-BR.png"))
                    _bottomRight = new Bitmap(basePath + "FF-TextBox-BR.png");
                    
                Console.WriteLine($"✅ Loaded FF1 textbox templates: TL={_topLeft != null}, TR={_topRight != null}, BL={_bottomLeft != null}, BR={_bottomRight != null}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load templates: {ex.Message}");
            }
        }

        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            if (_topLeft == null || _topRight == null || _bottomLeft == null || _bottomRight == null)
            {
                Console.WriteLine("⚠️ Templates not loaded, cannot detect textbox");
                return null;
            }

            // Find top-left corner first (most distinctive) with more relaxed threshold
            var tlMatch = FindTemplate(screenshot, _topLeft, 0.70); // Lowered from 0.85
            if (tlMatch == null)
            {
                // Try with even more relaxed threshold
                tlMatch = FindTemplate(screenshot, _topLeft, 0.60);
                if (tlMatch == null)
                {
                    Console.WriteLine("⚠️ No TL corner found even with relaxed threshold");
                    return null;
                }
            }

            Console.WriteLine($"🔍 Found TL corner at {tlMatch}");

            // Look for top-right corner in expected area (textbox is ~800-1000px wide)
            var expectedTrX = tlMatch.Value.X + 900; // Approximate textbox width from templates
            var trSearchArea = new Rectangle(expectedTrX - 150, tlMatch.Value.Y - 15, 300, 40);
            var trMatch = FindTemplate(screenshot, _topRight, 0.65, trSearchArea); // Lowered threshold

            if (trMatch == null)
            {
                Console.WriteLine("⚠️ Could not find TR corner, trying broader search");
                // Try much broader search with lower threshold
                trSearchArea = new Rectangle(tlMatch.Value.X + 400, tlMatch.Value.Y - 20, 600, 50);
                trMatch = FindTemplate(screenshot, _topRight, 0.55, trSearchArea); // Even lower
            }

            if (trMatch != null)
            {
                Console.WriteLine($"🔍 Found TR corner at {trMatch}");
                
                // Calculate textbox dimensions
                var textboxWidth = trMatch.Value.X - tlMatch.Value.X + _topRight.Width;
                var textboxHeight = 100; // Standard FF1 textbox height
                
                var textboxRect = new Rectangle(
                    tlMatch.Value.X,
                    tlMatch.Value.Y,
                    textboxWidth,
                    textboxHeight
                );
                
                Console.WriteLine($"🎯 TEXTBOX DETECTED: {textboxRect}");
                return textboxRect;
            }

            Console.WriteLine("⚠️ Found TL but not TR corner");
            return null;
        }

        private Point? FindTemplate(Bitmap screenshot, Bitmap template, double threshold, Rectangle? searchArea = null)
        {
            var search = searchArea ?? new Rectangle(0, 0, screenshot.Width, screenshot.Height);
            
            var maxX = Math.Min(search.Right, screenshot.Width - template.Width);
            var maxY = Math.Min(search.Bottom, screenshot.Height - template.Height);
            
            double bestMatch = 0.0;
            Point? bestLocation = null;
            int samplesChecked = 0;
            
            for (int y = search.Y; y < maxY; y += 3) // Slightly wider sampling for better coverage
            {
                for (int x = search.X; x < maxX; x += 3) 
                {
                    samplesChecked++;
                    double match = CalculateMatch(screenshot, template, x, y);
                    
                    if (match > bestMatch)
                    {
                        bestMatch = match;
                        bestLocation = new Point(x, y);
                    }
                    
                    if (match >= threshold)
                    {
                        Console.WriteLine($"🎯 Template match: {match:F3} at ({x},{y}) - SUCCESS!");
                        return new Point(x, y);
                    }
                }
            }
            
            // Debug output for failed searches
            if (bestLocation.HasValue)
            {
                Console.WriteLine($"⚠️ Template search: Best match {bestMatch:F3} at {bestLocation} (threshold: {threshold:F3}, samples: {samplesChecked})");
            }
            else
            {
                Console.WriteLine($"❌ Template search: No matches found (samples: {samplesChecked})");
            }
            
            return null;
        }

        private double CalculateMatch(Bitmap screenshot, Bitmap template, int startX, int startY)
        {
            // Thread-safe template matching using GetPixel (slower but safe)
            int matches = 0;
            int total = 0;
            
            for (int y = 0; y < template.Height; y += 2) // Sample every 2nd row for speed
            {
                for (int x = 0; x < template.Width; x += 2) // Sample every 2nd column for speed  
                {
                    if (startX + x >= screenshot.Width || startY + y >= screenshot.Height) 
                        continue;
                        
                    var screenPixel = screenshot.GetPixel(startX + x, startY + y);
                    var templatePixel = template.GetPixel(x, y);
                    
                    // Compare RGB values (tolerance of 40 per channel)
                    bool match = Math.Abs(screenPixel.R - templatePixel.R) < 40 &&
                               Math.Abs(screenPixel.G - templatePixel.G) < 40 &&
                               Math.Abs(screenPixel.B - templatePixel.B) < 40;
                    
                    if (match) matches++;
                    total++;
                }
            }
            
            return total > 0 ? (double)matches / total : 0.0;
        }
    }
}