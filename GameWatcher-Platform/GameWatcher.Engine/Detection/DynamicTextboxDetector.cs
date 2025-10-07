using System.Drawing;
using GameWatcher.Engine.Detection;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Enhanced textbox detector ported from V1 with all optimizations preserved
/// Dynamically finds dialogue boxes using targeted search areas (79.3% reduction)
/// </summary>
public class DynamicTextboxDetector : ITextboxDetector
{
    private Rectangle? _lastKnownTextbox = null;
    private int _consecutiveFailures = 0;
    
    public Rectangle? DetectTextbox(Bitmap screenshot)
    {
        // Strategy 1: Check near last known position first (V1 optimization)
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
            
            // Clear cache after failures (V1 stability logic)
            if (_consecutiveFailures > 3)
            {
                _lastKnownTextbox = null;
                _consecutiveFailures = 0;
                Console.WriteLine("🔄 Cleared cached textbox location after consecutive failures");
            }
        }
        
        // Strategy 2: Targeted search area (V1's 79.3% optimization)
        var fullScreenResult = FindDialogueBoxInTargetedArea(screenshot);
        
        if (fullScreenResult.HasValue)
        {
            _lastKnownTextbox = fullScreenResult;
            _consecutiveFailures = 0;
            Console.WriteLine($"🎯 TEXTBOX FOUND: {fullScreenResult.Value}");
            return fullScreenResult;
        }
        
        return null;
    }
    
    private Rectangle? SearchForTextboxInRegion(Bitmap screenshot, Rectangle region, string regionName)
    {
        try
        {
            // Clamp to screenshot bounds
            region = Rectangle.Intersect(region, new Rectangle(0, 0, screenshot.Width, screenshot.Height));
            if (region.IsEmpty) return null;
            
            var bluePixelCount = 0;
            
            // Sample region for blue textbox borders
            for (int y = region.Y; y < region.Bottom; y += 10)
            {
                for (int x = region.X; x < region.Right; x += 10)
                {
                    if (IsDialogueBoxBorder(screenshot.GetPixel(x, y)))
                    {
                        bluePixelCount++;
                        
                        // Found border pixel - trace the rectangle
                        var dialogueRect = TraceDialogueRectangle(screenshot, x, y);
                        if (dialogueRect.HasValue && IsValidDialogueBoxSize(dialogueRect.Value))
                        {
                            Console.WriteLine($"✅ Found textbox in {regionName}: {dialogueRect.Value}");
                            return dialogueRect.Value;
                        }
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error searching {regionName} region: {ex.Message}");
            return null;
        }
    }
    
    private Rectangle? FindDialogueBoxInTargetedArea(Bitmap screenshot)
    {
        // V1's proven targeted search coordinates (79.3% reduction)
        // Based on FF1 analysis: X=0.196875, Y=0.050926, Width=0.604688, Height=0.282407
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
        
        // Calculate performance improvement
        var searchAreaPixels = searchArea.Width * searchArea.Height;
        var fullScreenPixels = screenshot.Width * screenshot.Height;
        var reductionPercent = (1.0 - (double)searchAreaPixels / fullScreenPixels) * 100;
        
        Console.WriteLine($"🎯 Targeted search: {searchArea.Width}x{searchArea.Height} ({reductionPercent:F1}% reduction)");
        
        // Search within targeted area only
        var candidates = new List<Rectangle>();
        var stepSize = Math.Max(10, Math.Min(targetWidth, targetHeight) / 100);
        
        for (int y = searchArea.Top; y < searchArea.Bottom; y += stepSize)
        {
            for (int x = searchArea.Left; x < searchArea.Right; x += stepSize)
            {
                if (IsDialogueBoxBorder(screenshot.GetPixel(x, y)))
                {
                    var rect = TraceDialogueRectangle(screenshot, x, y);
                    if (rect.HasValue && IsValidDialogueBoxSize(rect.Value))
                    {
                        // Avoid duplicates
                        bool isDuplicate = candidates.Any(existing => 
                            RectanglesOverlap(rect.Value, existing, 0.7f));
                        
                        if (!isDuplicate)
                        {
                            candidates.Add(rect.Value);
                        }
                    }
                }
            }
        }
        
        // Return largest candidate (most likely main dialogue box)
        if (candidates.Count > 0)
        {
            candidates.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
            var bestCandidate = candidates[0];
            Console.WriteLine($"🎯 Best dialogue box from {candidates.Count} candidates: {bestCandidate}");
            return bestCandidate;
        }
        
        return null;
    }
    
    private Rectangle? TraceDialogueRectangle(Bitmap screenshot, int startX, int startY)
    {
        try
        {
            // Trace rectangle bounds from starting border pixel
            int minX = startX, maxX = startX;
            int minY = startY, maxY = startY;
            
            // Expand horizontally
            for (int x = startX; x >= 0 && x < screenshot.Width; x--)
            {
                if (!IsDialogueBoxBorder(screenshot.GetPixel(x, startY))) break;
                minX = x;
            }
            for (int x = startX; x < screenshot.Width; x++)
            {
                if (!IsDialogueBoxBorder(screenshot.GetPixel(x, startY))) break;
                maxX = x;
            }
            
            // Expand vertically
            for (int y = startY; y >= 0 && y < screenshot.Height; y--)
            {
                if (!IsDialogueBoxBorder(screenshot.GetPixel(startX, y))) break;
                minY = y;
            }
            for (int y = startY; y < screenshot.Height; y++)
            {
                if (!IsDialogueBoxBorder(screenshot.GetPixel(startX, y))) break;
                maxY = y;
            }
            
            var rect = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            
            if (IsValidDialogueBoxSize(rect) && HasValidBorder(screenshot, rect))
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
    
    private bool HasValidBorder(Bitmap screenshot, Rectangle rect)
    {
        try
        {
            // Check for consistent border on all sides
            int samplesPerSide = 20;
            int borderPixelsFound = 0;
            
            // Sample all edges
            for (int i = 0; i < samplesPerSide; i++)
            {
                if (rect.Width > samplesPerSide && rect.Height > samplesPerSide)
                {
                    int x = rect.X + (rect.Width * i / samplesPerSide);
                    int y = rect.Y + (rect.Height * i / samplesPerSide);
                    
                    // Check edges for border pixels
                    if (x >= 0 && x < screenshot.Width && rect.Y >= 0 && rect.Y < screenshot.Height)
                    {
                        if (IsDialogueBoxBorder(screenshot.GetPixel(x, rect.Y))) borderPixelsFound++;
                        if (IsDialogueBoxBorder(screenshot.GetPixel(x, rect.Bottom - 1))) borderPixelsFound++;
                    }
                    
                    if (y >= 0 && y < screenshot.Height && rect.X >= 0 && rect.X < screenshot.Width)
                    {
                        if (IsDialogueBoxBorder(screenshot.GetPixel(rect.X, y))) borderPixelsFound++;
                        if (IsDialogueBoxBorder(screenshot.GetPixel(rect.Right - 1, y))) borderPixelsFound++;
                    }
                }
            }
            
            // Require good border coverage
            return borderPixelsFound >= (samplesPerSide * 2); // 50% of border should match
        }
        catch
        {
            return false;
        }
    }
    
    private bool IsValidDialogueBoxSize(Rectangle rect)
    {
        // Dialogue box size constraints from V1
        bool validSize = rect.Width >= 200 && rect.Width <= 1920 && 
                        rect.Height >= 100 && rect.Height <= 800;
        
        // Should be wider than tall (landscape orientation)
        bool validAspectRatio = rect.Width > rect.Height;
        
        return validSize && validAspectRatio;
    }
    
    private bool IsDialogueBoxBorder(Color pixel)
    {
        // Enhanced border detection from V1 - covers multiple game types
        var borderColors = new[]
        {
            Color.FromArgb(66, 66, 231),   // FF1 dark blue
            Color.FromArgb(99, 99, 255),   // FF1 medium blue  
            Color.FromArgb(33, 33, 165),   // FF1 very dark blue
            Color.FromArgb(0, 88, 248),    // FF1 bright blue
            Color.FromArgb(82, 82, 247),   // FF1 variant
            // Add other game border colors as needed
        };
        
        const int tolerance = 80; // Color matching tolerance
        
        foreach (var borderColor in borderColors)
        {
            if (Math.Abs(pixel.R - borderColor.R) <= tolerance &&
                Math.Abs(pixel.G - borderColor.G) <= tolerance &&
                Math.Abs(pixel.B - borderColor.B) <= tolerance)
            {
                return true;
            }
        }
        
        // General blue-dominant pixel check
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