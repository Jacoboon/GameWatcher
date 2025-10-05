using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SimpleLoop
{
    public class FixedPositionTextboxDetector : ITextboxDetector
    {
        private Rectangle? _fixedTextboxRect;
        private bool _positionLearned = false;
        private DateTime _lastFullDetection = DateTime.MinValue;
        
        // If no textbox found for this long, re-scan
        private readonly TimeSpan _rescanInterval = TimeSpan.FromSeconds(10);
        
        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // If we've learned the position, just validate it's still there
            if (_positionLearned && _fixedTextboxRect.HasValue)
            {
                if (QuickValidatePosition(screenshot, _fixedTextboxRect.Value))
                {
                    return _fixedTextboxRect.Value; // ⚡ INSTANT - 0ms detection!
                }
                else
                {
                    // Position changed, need to re-learn
                    Console.WriteLine("Textbox moved, re-learning position...");
                    _positionLearned = false;
                    _fixedTextboxRect = null;
                }
            }

            // Need to find the textbox (first time or position changed)
            if (DateTime.Now - _lastFullDetection > _rescanInterval || !_positionLearned)
            {
                var detectedRect = PerformFullDetection(screenshot);
                
                if (detectedRect.HasValue)
                {
                    _fixedTextboxRect = detectedRect;
                    _positionLearned = true;
                    _lastFullDetection = DateTime.Now;
                    Console.WriteLine($"✅ Textbox position learned: {detectedRect.Value}");
                    Console.WriteLine("🚀 Subsequent detections will be INSTANT!");
                }
                
                return detectedRect;
            }

            return _fixedTextboxRect;
        }

        private bool QuickValidatePosition(Bitmap screenshot, Rectangle rect)
        {
            // Ultra-fast validation - check 3 strategic pixels for blue color
            try
            {
                var blueColor = Color.FromArgb(0, 88, 248);
                var tolerance = 60;
                
                // Check top-left, top-middle, top-right of textbox border
                var checkPoints = new Point[]
                {
                    new Point(rect.Left + 10, rect.Top + 2),
                    new Point(rect.Left + rect.Width / 2, rect.Top + 2),
                    new Point(rect.Right - 10, rect.Top + 2)
                };

                int blueFound = 0;
                foreach (var point in checkPoints)
                {
                    if (point.X >= 0 && point.X < screenshot.Width && 
                        point.Y >= 0 && point.Y < screenshot.Height)
                    {
                        var pixel = screenshot.GetPixel(point.X, point.Y);
                        if (IsColorSimilar(pixel, blueColor, tolerance))
                        {
                            blueFound++;
                        }
                    }
                }
                
                // If at least 2 of 3 points are blue, textbox is probably still there
                return blueFound >= 2;
            }
            catch
            {
                return false; // If anything goes wrong, force re-detection
            }
        }

        private Rectangle? PerformFullDetection(Bitmap screenshot)
        {
            Console.WriteLine("🔍 Performing full textbox detection...");
            
            // FF textboxes typically appear in bottom 40% of screen
            var searchStartY = (int)(screenshot.Height * 0.6);
            var searchEndY = screenshot.Height - 50;
            
            var blueColor = Color.FromArgb(0, 88, 248);
            var tolerance = 50;
            
            // Look for horizontal blue lines (textbox borders)
            for (int y = searchStartY; y < searchEndY; y += 3) // Every 3rd row for speed
            {
                int blueCount = 0;
                int firstBlue = -1;
                int lastBlue = -1;
                
                // Sample every 8th pixel horizontally
                for (int x = 0; x < screenshot.Width; x += 8)
                {
                    var pixel = screenshot.GetPixel(x, y);
                    if (IsColorSimilar(pixel, blueColor, tolerance))
                    {
                        if (firstBlue == -1) firstBlue = x;
                        lastBlue = x;
                        blueCount++;
                    }
                }
                
                // If we found a long blue line, this is likely the textbox border
                if (blueCount > 15 && (lastBlue - firstBlue) > 300)
                {
                    // Create standard FF textbox rectangle
                    var textboxRect = new Rectangle(
                        Math.Max(0, firstBlue - 20),
                        Math.Max(0, y - 10),
                        Math.Min(screenshot.Width - (firstBlue - 20), 520), // Standard width
                        100 // Standard height
                    );
                    
                    Console.WriteLine($"📍 Found textbox at Y={y}, width={lastBlue - firstBlue}px");
                    return textboxRect;
                }
            }
            
            Console.WriteLine("❌ No textbox detected in current frame");
            return null;
        }

        private bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
        }

        public void ResetPosition()
        {
            _positionLearned = false;
            _fixedTextboxRect = null;
            Console.WriteLine("🔄 Textbox position reset - will re-learn on next detection");
        }
    }
}