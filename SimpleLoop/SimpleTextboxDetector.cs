using System;
using System.Drawing;

namespace SimpleLoop
{
    public class SimpleTextboxDetector
    {
        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // Look for FF1's specific blue color in horizontal lines
            // Based on your screenshots, the blue appears to be around RGB(66,66,231) or similar dark blue
            
            var targetBlue = Color.FromArgb(66, 66, 231);
            var tolerance = 60; // Higher tolerance for variations
            
            // Search entire screen height
            for (int y = 50; y < screenshot.Height - 150; y += 3) // Skip rows for speed
            {
                int bluePixels = 0;
                int startX = -1;
                int endX = -1;
                
                // Sample across the width looking for blue horizontal lines
                for (int x = 50; x < screenshot.Width - 50; x += 8) // Skip pixels for speed
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
                        }
                    }
                    catch 
                    {
                        // Skip pixel access errors
                        continue;
                    }
                }
                
                // If we found a long horizontal blue line, it's likely a textbox border
                if (bluePixels > 20 && (endX - startX) > 400)
                {
                    // Found textbox! Use the proven coordinates from your working setup
                    var textboxRect = new Rectangle(407, 87, 1102, 237);
                    
                    Console.WriteLine($"🎯 TEXTBOX FOUND: {textboxRect} (blue pixels: {bluePixels}, border width: {endX - startX})");
                    return textboxRect;
                }
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