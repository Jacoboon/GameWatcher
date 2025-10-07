using System;
using System.Drawing;
using System.Collections.Generic;
using GameWatcher.Runtime.Services.Detection;

namespace GameWatcher.Runtime.Services.Detection
{
    /// <summary>
    /// Enhanced textbox detector that dynamically finds FF1 dialogue boxes regardless of screen position
    /// </summary>
    public class DynamicTextboxDetector : ITextboxDetector
    {
        private Rectangle? _lastKnownTextbox = null;
        private int _consecutiveFailures = 0;
        
        public Rectangle? DetectTextbox(Bitmap screenshot)
        {
            // Strategy 1: If we have a known textbox location, check nearby first (fastest)
            if (_lastKnownTextbox.HasValue)
            {
                var expandedSearch = ExpandRectangle(_lastKnownTextbox.Value, 50);
                var nearbyResult = SearchForTextboxInRegion(screenshot, expandedSearch, "nearby");
                
                if (nearbyResult.HasValue)
                {
                    _lastKnownTextbox = nearbyResult;
                    _consecutiveFailures = 0;
                    return nearbyResult;
                }
                
                _consecutiveFailures++;
                
                // After 3 failures, clear the cached location
                if (_consecutiveFailures > 3)
                {
                    _lastKnownTextbox = null;
                    _consecutiveFailures = 0;
                    Console.WriteLine("🔄 Cleared cached textbox location after consecutive failures");
                }
            }
            
            // Strategy 2: Full screen search for blue rectangular regions
            var fullScreenResult = FindBlueRectangularRegions(screenshot);
            
            if (fullScreenResult.HasValue)
            {
                _lastKnownTextbox = fullScreenResult;
                _consecutiveFailures = 0;
                Console.WriteLine($"🎯 NEW TEXTBOX FOUND: {fullScreenResult.Value}");
                return fullScreenResult;
            }
            
            Console.WriteLine("❌ No textbox found in full screen search");
            return null;
        }
        
        private Rectangle? SearchForTextboxInRegion(Bitmap screenshot, Rectangle region, string regionName)
        {
            try
            {
                // Clamp region to screenshot bounds
                region = Rectangle.Intersect(region, new Rectangle(0, 0, screenshot.Width, screenshot.Height));
                if (region.IsEmpty) return null;
                
                var bluePixelCount = 0;
                var blueRegions = new List<Rectangle>();
                
                // Sample the region looking for blue horizontal and vertical lines
                for (int y = region.Y; y < region.Bottom; y += 10)
                {
                    for (int x = region.X; x < region.Right; x += 10)
                    {
                        if (IsFF1Blue(screenshot.GetPixel(x, y)))
                        {
                            bluePixelCount++;
                            
                            // Found blue pixel - check if it's part of a rectangular border
                            var blueRect = TraceBlueRectangle(screenshot, x, y);
                            if (blueRect.HasValue && IsValidTextboxSize(blueRect.Value))
                            {
                                Console.WriteLine($"✅ Found valid textbox in {regionName} region: {blueRect.Value}");
                                return blueRect.Value;
                            }
                        }
                    }
                }
                
                Console.WriteLine($"🔍 {regionName} search: {bluePixelCount} blue pixels found, no valid rectangles");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error searching {regionName} region: {ex.Message}");
                return null;
            }
        }
        
        private Rectangle? FindBlueRectangularRegions(Bitmap screenshot)
        {
            var blueRegions = new List<Rectangle>();
            var sampledPoints = new List<Point>();
            
            // Define targeted search area based on FF1 dialogue coordinates analysis
            // Normalized coordinates: X=0.196875, Y=0.050926, Width=0.604688, Height=0.282407
            // With 25px buffer: X=378, Y=55, Width=1161, Height=305
            var targetX = (int)(screenshot.Width * 0.196875) - 25;
            var targetY = (int)(screenshot.Height * 0.050926) - 25;
            var targetWidth = (int)(screenshot.Width * 0.604688) + 50;
            var targetHeight = (int)(screenshot.Height * 0.282407) + 50;
            
            // Ensure bounds stay within screen
            targetX = Math.Max(0, targetX);
            targetY = Math.Max(0, targetY);
            targetWidth = Math.Min(targetWidth, screenshot.Width - targetX);
            targetHeight = Math.Min(targetHeight, screenshot.Height - targetY);
            
            var searchArea = new Rectangle(targetX, targetY, targetWidth, targetHeight);
            
            // Sample only within the targeted dialogue area
            var stepSize = Math.Max(10, Math.Min(targetWidth, targetHeight) / 100);
            
            for (int y = searchArea.Top; y < searchArea.Bottom; y += stepSize)
            {
                for (int x = searchArea.Left; x < searchArea.Right; x += stepSize)
                {
                    if (IsFF1Blue(screenshot.GetPixel(x, y)))
                    {
                        sampledPoints.Add(new Point(x, y));
                    }
                }
            }
            
            var searchAreaPixels = searchArea.Width * searchArea.Height;
            var fullScreenPixels = screenshot.Width * screenshot.Height;
            var reductionPercent = (1.0 - (double)searchAreaPixels / fullScreenPixels) * 100;
            
            Console.WriteLine($"🎯 Targeted search area: {searchArea.Width}x{searchArea.Height} ({reductionPercent:F1}% reduction)");
            Console.WriteLine($"🔍 Found {sampledPoints.Count} blue sample points in targeted area");
            
            // For each blue point, try to trace a rectangle
            foreach (var point in sampledPoints)
            {
                var rect = TraceBlueRectangle(screenshot, point.X, point.Y);
                if (rect.HasValue && IsValidTextboxSize(rect.Value))
                {
                    // Avoid duplicates
                    bool isDuplicate = false;
                    foreach (var existing in blueRegions)
                    {
                        if (RectanglesOverlap(rect.Value, existing, 0.7f))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    
                    if (!isDuplicate)
                    {
                        blueRegions.Add(rect.Value);
                    }
                }
            }
            
            // Return the largest valid region (most likely to be the main textbox)
            if (blueRegions.Count > 0)
            {
                blueRegions.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
                var bestRegion = blueRegions[0];
                Console.WriteLine($"🎯 Selected best region from {blueRegions.Count} candidates: {bestRegion}");
                return bestRegion;
            }
            
            return null;
        }
        
        private Rectangle? TraceBlueRectangle(Bitmap screenshot, int startX, int startY)
        {
            try
            {
                // From the starting blue pixel, try to find the bounds of a rectangle
                int minX = startX, maxX = startX;
                int minY = startY, maxY = startY;
                
                // Expand left and right along this row
                for (int x = startX; x >= 0 && x < screenshot.Width; x--)
                {
                    if (!IsFF1Blue(screenshot.GetPixel(x, startY))) break;
                    minX = x;
                }
                for (int x = startX; x < screenshot.Width; x++)
                {
                    if (!IsFF1Blue(screenshot.GetPixel(x, startY))) break;
                    maxX = x;
                }
                
                // Expand up and down along this column
                for (int y = startY; y >= 0 && y < screenshot.Height; y--)
                {
                    if (!IsFF1Blue(screenshot.GetPixel(startX, y))) break;
                    minY = y;
                }
                for (int y = startY; y < screenshot.Height; y++)
                {
                    if (!IsFF1Blue(screenshot.GetPixel(startX, y))) break;
                    maxY = y;
                }
                
                // Create rectangle and validate it looks like a textbox border
                var rect = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                
                if (IsValidTextboxSize(rect) && HasRectangularBorder(screenshot, rect))
                {
                    return rect;
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        private bool HasRectangularBorder(Bitmap screenshot, Rectangle rect)
        {
            try
            {
                // Check if the rectangle has blue borders on all sides
                int bluePixelsTop = 0, bluePixelsBottom = 0, bluePixelsLeft = 0, bluePixelsRight = 0;
                int samplesPerSide = 20;
                
                // Sample top and bottom edges
                for (int i = 0; i < samplesPerSide && rect.Width > samplesPerSide; i++)
                {
                    int x = rect.X + (rect.Width * i / samplesPerSide);
                    if (x >= 0 && x < screenshot.Width)
                    {
                        if (rect.Y >= 0 && rect.Y < screenshot.Height && IsFF1Blue(screenshot.GetPixel(x, rect.Y)))
                            bluePixelsTop++;
                        if (rect.Bottom - 1 >= 0 && rect.Bottom - 1 < screenshot.Height && IsFF1Blue(screenshot.GetPixel(x, rect.Bottom - 1)))
                            bluePixelsBottom++;
                    }
                }
                
                // Sample left and right edges
                for (int i = 0; i < samplesPerSide && rect.Height > samplesPerSide; i++)
                {
                    int y = rect.Y + (rect.Height * i / samplesPerSide);
                    if (y >= 0 && y < screenshot.Height)
                    {
                        if (rect.X >= 0 && rect.X < screenshot.Width && IsFF1Blue(screenshot.GetPixel(rect.X, y)))
                            bluePixelsLeft++;
                        if (rect.Right - 1 >= 0 && rect.Right - 1 < screenshot.Width && IsFF1Blue(screenshot.GetPixel(rect.Right - 1, y)))
                            bluePixelsRight++;
                    }
                }
                
                // At least 50% of border samples should be blue
                int minBlueRequired = samplesPerSide / 2;
                bool hasGoodBorders = bluePixelsTop >= minBlueRequired && 
                                    bluePixelsBottom >= minBlueRequired && 
                                    bluePixelsLeft >= minBlueRequired && 
                                    bluePixelsRight >= minBlueRequired;
                
                if (hasGoodBorders)
                {
                    // Console.WriteLine($"🔷 Rectangle has good blue borders: T:{bluePixelsTop} B:{bluePixelsBottom} L:{bluePixelsLeft} R:{bluePixelsRight}");
                }
                
                return hasGoodBorders;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        private bool IsValidTextboxSize(Rectangle rect)
        {
            // FF1 textbox should be reasonably large but not too huge
            bool validSize = rect.Width >= 200 && rect.Width <= 1920 && 
                           rect.Height >= 100 && rect.Height <= 800;
            
            // Aspect ratio should be wider than tall (landscape textbox)
            bool validAspectRatio = rect.Width > rect.Height;
            
            return validSize && validAspectRatio;
        }
        
        private bool IsFF1Blue(Color pixel)
        {
            // Enhanced blue detection with more variations
            var blues = new[] {
                Color.FromArgb(66, 66, 231),   // Dark blue
                Color.FromArgb(99, 99, 255),   // Medium blue  
                Color.FromArgb(33, 33, 165),   // Very dark blue
                Color.FromArgb(0, 88, 248),    // Bright blue
                Color.FromArgb(82, 82, 247),   // Another variant
                Color.FromArgb(0, 0, 255),     // Pure blue
                Color.FromArgb(0, 100, 200),   // Cyan-blue
                Color.FromArgb(50, 50, 200),   // Dark blue-purple
            };
            
            const int tolerance = 80; // Higher tolerance for variations
            
            foreach (var blue in blues)
            {
                if (Math.Abs(pixel.R - blue.R) <= tolerance &&
                    Math.Abs(pixel.G - blue.G) <= tolerance &&
                    Math.Abs(pixel.B - blue.B) <= tolerance)
                {
                    return true;
                }
            }
            
            // Additional check: any pixel where blue channel is dominant
            if (pixel.B > pixel.R + 50 && pixel.B > pixel.G + 50 && pixel.B > 100)
            {
                return true;
            }
            
            return false;
        }
        
        private Rectangle ExpandRectangle(Rectangle rect, int margin)
        {
            return new Rectangle(
                Math.Max(0, rect.X - margin),
                Math.Max(0, rect.Y - margin),
                rect.Width + (margin * 2),
                rect.Height + (margin * 2)
            );
        }
        
        private bool RectanglesOverlap(Rectangle a, Rectangle b, float minOverlap)
        {
            var intersection = Rectangle.Intersect(a, b);
            if (intersection.IsEmpty) return false;
            
            var aArea = a.Width * a.Height;
            var bArea = b.Width * b.Height;
            var intersectionArea = intersection.Width * intersection.Height;
            
            var overlapRatio = (float)intersectionArea / Math.Min(aArea, bArea);
            return overlapRatio >= minOverlap;
        }
    }
}