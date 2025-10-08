using System;
using System.Drawing;

namespace SimpleLoop
{
    public class SimpleTextboxDetector : ITextboxDetector
    {
        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // Look for FF1's specific blue color in horizontal lines
            // Based on your screenshots, the blue appears to be around RGB(66,66,231) or similar dark blue
            
            var targetBlue = Color.FromArgb(66, 66, 231);
            var tolerance = 60; // Higher tolerance for variations
            
            // Focus search on known FF1 textbox area only (much faster!)
            var knownTextboxArea = new Rectangle(407, 87, 1102, 237);
            
            int totalBlueFound = 0;
            
            // Search only within the known textbox bounds
            for (int y = knownTextboxArea.Y; y < knownTextboxArea.Bottom - 10; y += 3) // Skip rows for speed
            {
                int bluePixels = 0;
                int startX = -1;
                int endX = -1;
                
                // Sample across the width within the known textbox area
                for (int x = knownTextboxArea.X; x < knownTextboxArea.Right - 10; x += 8) // Skip pixels for speed
                {
                    try
                    {
                        var pixel = screenshot.GetPixel(x, y);
                        
                        // Check if this pixel is FF1 textbox blue
                        if (IsFF1Blue(pixel, tolerance))
                        {
                            if (startX == -1) startX = x;
                            endX = x;
                            bluePixels++;
                            totalBlueFound++;
                        }
                    }
                    catch 
                    {
                        // Skip pixel access errors
                        continue;
                    }
                }
                
                // If we found a horizontal blue line in the textbox area, textbox is present
                if (bluePixels > 15 && (endX - startX) > 200) // Lower thresholds since we're in focused area
                {
                    Console.WriteLine($"🎯 TEXTBOX FOUND: {knownTextboxArea} (blue pixels: {bluePixels}, Y: {y})");
                    return knownTextboxArea;
                }
            }
            
            // Debug: show why we didn't find a textbox
            if (totalBlueFound > 0)
            {
                Console.WriteLine($"🔍 Focused search found {totalBlueFound} blue pixels in textbox area, but no qualifying lines");
            }
            
            return null;
        }
        
        private bool IsFF1Blue(Color pixel, int tolerance)
        {
            // Check multiple FF1 blue variations
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