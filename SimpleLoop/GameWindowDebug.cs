using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace SimpleLoop
{
    public class GameWindowDebug
    {
        public static void SaveFullGameCapture()
        {
            try
            {
                var gameCapture = ScreenCapture.CaptureGameWindow();
                var debugPath = $"full_game_capture_{DateTime.Now:HHmmss}.png";
                gameCapture.Save(debugPath, ImageFormat.Png);
                Console.WriteLine($"💾 Saved full game capture: {debugPath}");
                Console.WriteLine($"📏 Game window size: {gameCapture.Width}x{gameCapture.Height}");
                
                // Look for potential textbox areas by scanning for common colors
                ScanForTextboxColors(gameCapture);
                
                gameCapture.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug capture error: {ex.Message}");
            }
        }
        
        private static void ScanForTextboxColors(Bitmap image)
        {
            Console.WriteLine("🔍 Scanning for potential textbox colors...");
            
            // Common FF textbox colors to look for
            var targetColors = new[]
            {
                Color.FromArgb(0, 88, 248),    // Classic blue FF1
                Color.FromArgb(0, 0, 0),       // Black background
                Color.FromArgb(255, 255, 255), // White text
                Color.FromArgb(64, 64, 64),    // Dark gray
                Color.FromArgb(128, 128, 128), // Medium gray
            };
            
            var width = image.Width;
            var height = image.Height;
            
            // Sample key areas where textboxes typically appear
            var sampleAreas = new[]
            {
                new Rectangle(0, height - 150, width, 150),           // Bottom area
                new Rectangle(0, height - 200, width, 100),           // Lower middle
                new Rectangle(width/4, height - 120, width/2, 80),    // Centered bottom
            };
            
            foreach (var area in sampleAreas)
            {
                Console.WriteLine($"🔎 Checking area: {area}");
                ScanAreaForColors(image, area, targetColors);
            }
        }
        
        private static void ScanAreaForColors(Bitmap image, Rectangle area, Color[] colors)
        {
            try
            {
                for (int y = area.Top; y < Math.Min(area.Bottom, image.Height); y += 10)
                {
                    for (int x = area.Left; x < Math.Min(area.Right, image.Width); x += 10)
                    {
                        var pixel = image.GetPixel(x, y);
                        
                        foreach (var targetColor in colors)
                        {
                            if (IsColorSimilar(pixel, targetColor, 30))
                            {
                                Console.WriteLine($"   Found {targetColor.Name} at ({x},{y}) - RGB({pixel.R},{pixel.G},{pixel.B})");
                                return; // Found something interesting
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Scan error: {ex.Message}");
            }
        }
        
        private static bool IsColorSimilar(Color c1, Color c2, int threshold)
        {
            return Math.Abs(c1.R - c2.R) <= threshold &&
                   Math.Abs(c1.G - c2.G) <= threshold &&
                   Math.Abs(c1.B - c2.B) <= threshold;
        }
    }
}