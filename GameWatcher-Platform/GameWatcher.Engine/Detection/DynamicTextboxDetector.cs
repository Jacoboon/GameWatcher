using System.Drawing;
using GameWatcher.Engine.Detection;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Enhanced textbox detector ported from V1 with all optimizations preserved.
/// Now configurable per game pack via TextboxDetectionConfig.
/// Dynamically finds dialogue boxes using targeted search areas (79.3% reduction for FF1).
/// </summary>
public class DynamicTextboxDetector : ITextboxDetector
{
    private readonly TextboxDetectionConfig _config;
    private readonly ILogger<DynamicTextboxDetector>? _logger;
    private Rectangle? _lastKnownTextbox = null;
    private int _consecutiveFailures = 0;
    
    public DynamicTextboxDetector(TextboxDetectionConfig config, ILogger<DynamicTextboxDetector>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }
    
    public Rectangle? DetectTextbox(Bitmap screenshot)
    {
        // Strategy 1: Check near last known position first (V1 optimization)
        if (_lastKnownTextbox.HasValue)
        {
            var expandedSearch = ExpandRectangle(_lastKnownTextbox.Value, _config.CachedPositionExpansion);
            var nearbyResult = SearchForTextboxInRegion(screenshot, expandedSearch, "nearby");
            
            if (nearbyResult.HasValue)
            {
                _lastKnownTextbox = nearbyResult;
                _consecutiveFailures = 0;
                return nearbyResult;
            }
            
            _consecutiveFailures++;
            
            // Clear cache after failures (V1 stability logic)
            if (_consecutiveFailures > _config.MaxConsecutiveFailures)
            {
                _lastKnownTextbox = null;
                _consecutiveFailures = 0;
                _logger?.LogDebug("🔄 Cleared cached textbox location after consecutive failures");
            }
        }
        
        // Strategy 2: Targeted search area (V1's 79.3% optimization)
        var fullScreenResult = FindDialogueBoxInTargetedArea(screenshot);
        
        if (fullScreenResult.HasValue)
        {
            _lastKnownTextbox = fullScreenResult;
            _consecutiveFailures = 0;
            _logger?.LogInformation("🎯 TEXTBOX FOUND: {TextboxRect}", fullScreenResult.Value);
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
                            _logger?.LogDebug("✅ Found textbox in {RegionName}: {TextboxRect}", regionName, dialogueRect.Value);
                            return dialogueRect.Value;
                        }
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "❌ Error searching {RegionName} region", regionName);
            return null;
        }
    }
    
    private Rectangle? FindDialogueBoxInTargetedArea(Bitmap screenshot)
    {
        Rectangle searchArea;
        
        if (_config.TargetSearchArea.HasValue)
        {
            // Use configured target area (normalized percentages converted to pixels)
            var target = _config.TargetSearchArea.Value;
            var targetX = (int)(screenshot.Width * target.X / 100.0) - 25;
            var targetY = (int)(screenshot.Height * target.Y / 100.0) - 25;
            var targetWidth = (int)(screenshot.Width * target.Width / 100.0) + 50;
            var targetHeight = (int)(screenshot.Height * target.Height / 100.0) + 50;
            
            // Ensure bounds stay within screen
            targetX = Math.Max(0, targetX);
            targetY = Math.Max(0, targetY);
            targetWidth = Math.Min(targetWidth, screenshot.Width - targetX);
            targetHeight = Math.Min(targetHeight, screenshot.Height - targetY);
            
            searchArea = new Rectangle(targetX, targetY, targetWidth, targetHeight);
            
            // Calculate performance improvement
            var searchAreaPixels = searchArea.Width * searchArea.Height;
            var fullScreenPixels = screenshot.Width * screenshot.Height;
            var reductionPercent = (1.0 - (double)searchAreaPixels / fullScreenPixels) * 100;
            
            _logger?.LogDebug("🎯 Targeted search: {Width}x{Height} ({Reduction:F1}% reduction)", 
                searchArea.Width, searchArea.Height, reductionPercent);
        }
        else
        {
            // Full screen search (no optimization)
            searchArea = new Rectangle(0, 0, screenshot.Width, screenshot.Height);
            _logger?.LogDebug("🔍 Full screen search: {Width}x{Height}", searchArea.Width, searchArea.Height);
        }
        
        // Search within targeted area
        var candidates = new List<Rectangle>();
        var stepSize = Math.Max(10, Math.Min(searchArea.Width, searchArea.Height) / 100);
        
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
            _logger?.LogDebug("🎯 Best dialogue box from {Count} candidates: {BestCandidate}", candidates.Count, bestCandidate);
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
        // Use configurable size constraints
        bool validSize = rect.Width >= _config.MinSize.Width && rect.Width <= _config.MaxSize.Width && 
                        rect.Height >= _config.MinSize.Height && rect.Height <= _config.MaxSize.Height;
        
        // Check aspect ratio if required
        bool validAspectRatio = !_config.RequireLandscapeAspect || rect.Width > rect.Height;
        
        return validSize && validAspectRatio;
    }
    
    private bool IsDialogueBoxBorder(Color pixel)
    {
        // Use configured border colors with tolerance
        foreach (var borderColor in _config.BorderColors)
        {
            if (Math.Abs(pixel.R - borderColor.R) <= _config.ColorTolerance &&
                Math.Abs(pixel.G - borderColor.G) <= _config.ColorTolerance &&
                Math.Abs(pixel.B - borderColor.B) <= _config.ColorTolerance)
            {
                return true;
            }
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