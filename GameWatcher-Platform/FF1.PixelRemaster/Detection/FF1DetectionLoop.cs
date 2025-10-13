using System.Drawing;
using GameWatcher.Engine.Detection;

namespace FF1.PixelRemaster.Detection;

/// <summary>
/// FF1 Pixel Remaster specific detection loop configuration.
/// Provides optimal settings for FF1's blue dialogue box.
/// </summary>
public static class FF1DetectionLoop
{
    /// <summary>
    /// Get the optimal detection loop configuration for FF1 Pixel Remaster.
    /// Based on V1 proven values with V2 improvements.
    /// </summary>
    public static DetectionLoopConfig GetConfig()
    {
        return new DetectionLoopConfig
        {
            // Timing (V1 proven: 15 FPS = 67ms per frame)
            TargetFps = 15,
            
            // Frame comparison thresholds (V1 proven values)
            StableSampleRate = 500,  // Initial detection: every 500th pixel must match
            ChangeSampleRate = 50,   // Follow-up detection: every 50th pixel (99% similarity)
            
            // Caching (NEW optimization)
            EnableTextboxCache = true,
            CacheInvalidateAfterFrames = 300,  // 20 seconds at 15 FPS
            
            // Performance optimizations (NEW)
            EnablePreCrop = true,   // 30× faster comparison
            EnableHashCheck = true, // Skip unnecessary OCR
            
            // FF1-specific textbox detection
            TextboxConfig = GetTextboxConfig()
        };
    }
    
    /// <summary>
    /// Get FF1-specific textbox detection configuration.
    /// V1 hardcoded coordinates that actually worked (79.3% reduction).
    /// </summary>
    public static TextboxDetectionConfig GetTextboxConfig()
    {
        return new TextboxDetectionConfig
        {
            // V1 PROVEN search area (79.3% reduction vs full screen)
            // These coordinates are normalized (0-1) relative to screen dimensions
            TargetSearchArea = new RectangleF(
                x: 0.196875f,      // 19.7% from left
                y: 0.050926f,      // 5.1% from top
                width: 0.604688f,  // 60.5% of screen width
                height: 0.282407f  // 28.2% of screen height
            ),
            
            // FF1 blue dialogue box border colors
            BorderColors = new[]
            {
                Color.FromArgb(66, 66, 231),    // Primary blue
                Color.FromArgb(99, 99, 255),    // Light blue
                Color.FromArgb(33, 33, 165),    // Dark blue
                Color.FromArgb(0, 88, 248),     // Medium blue
                Color.FromArgb(82, 82, 247)     // Alt blue
            },
            
            // Color matching tolerance
            ColorTolerance = 80,
            
            // Size validation
            MinSize = new Size(200, 100),
            MaxSize = new Size(1920, 800),
            RequireLandscapeAspect = true,  // Dialogue boxes are wider than tall
            
            // Cached position strategy
            CachedPositionExpansion = 50,    // Search ±50px from last position
            MaxConsecutiveFailures = 3       // Clear cache after 3 failures
        };
    }
}
