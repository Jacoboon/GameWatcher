using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SimpleLoop
{
    public class EveryFrameTextboxDetector
    {
        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // ALWAYS check for textbox on every new frame - no caching nonsense!
            return PerformFastDetection(screenshot);
        }

        private Rectangle? PerformFastDetection(Bitmap screenshot)
        {
            // FF textboxes can appear anywhere - search entire screen!
            var searchStartY = 20; // Skip very top to avoid window chrome
            var searchEndY = screenshot.Height - 20;
            
            // FF1 textbox colors - try multiple blue shades
            var blueColors = new[] {
                Color.FromArgb(0, 88, 248),   // Original bright blue
                Color.FromArgb(66, 66, 231),  // FF1 darker blue
                Color.FromArgb(33, 33, 165),  // Even darker variant
                Color.FromArgb(0, 0, 165),    // Pure dark blue
                Color.FromArgb(99, 99, 231)   // Lighter variant
            };
            var tolerance = 80; // Increased tolerance
            
            // DEBUG: Log search area
            Console.WriteLine($"🔍 Searching for textbox from Y={searchStartY} to Y={searchEndY} (image size: {screenshot.Width}x{screenshot.Height})");
            
            // Look for horizontal blue lines (textbox borders)
            // Sample every 5th row, every 10th pixel for speed
            for (int y = searchStartY; y < searchEndY; y += 5)
            {
                int blueCount = 0;
                int firstBlue = -1;
                int lastBlue = -1;
                
                for (int x = 0; x < screenshot.Width; x += 10)
                {
                    var pixel = screenshot.GetPixel(x, y);
                    
                    // Check against all possible blue colors
                    bool isBlue = false;
                    foreach (var blueColor in blueColors)
                    {
                        if (IsColorSimilar(pixel, blueColor, tolerance))
                        {
                            isBlue = true;
                            break;
                        }
                    }
                    
                    if (isBlue)
                    {
                        if (firstBlue == -1) firstBlue = x;
                        lastBlue = x;
                        blueCount++;
                        
                        // DEBUG: Show actual colors found
                        if (blueCount == 1) // First blue pixel found
                        {
                            Console.WriteLine($"🔵 Found blue pixel at Y={y}, X={x}: RGB({pixel.R}, {pixel.G}, {pixel.B})");
                        }
                    }
                }
                
                // If we found a long blue line, this is likely the textbox border
                if (blueCount > 15 && (lastBlue - firstBlue) > 300)
                {
                    Console.WriteLine($"🎯 Found textbox candidate at Y={y}, blue pixels: {blueCount}, width: {lastBlue - firstBlue}");
                    return new Rectangle(
                        Math.Max(0, firstBlue - 20),
                        Math.Max(0, y - 10),
                        Math.Min(screenshot.Width - (firstBlue - 20), 520),
                        100
                    );
                }
                else if (blueCount > 5) // Debug: show smaller candidates too
                {
                    Console.WriteLine($"📊 Potential textbox at Y={y}, blue pixels: {blueCount}, width: {lastBlue - firstBlue} (too small)");
                }
            }
            
            return null; // No textbox found
        }

        private bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
        }
    }
}