using System.Drawing;
using GameWatcher.Engine.Detection;

namespace FF1.PixelRemaster.Detection;

/// <summary>
/// FF1 Pixel Remaster textbox detection configuration.
/// This is the reference implementation demonstrating how to configure detection for a specific game.
/// </summary>
public static class FF1DetectionConfig
{
    /// <summary>
    /// Creates the optimized detection config for FF1 Pixel Remaster.
    /// Coordinates based on empirical analysis of FF1 dialogue box positions.
    /// </summary>
    public static TextboxDetectionConfig GetConfig()
    {
        return new TextboxDetectionConfig
        {
            // Targeted search area (normalized percentages, 0-100)
            // This reduces search area by 79.3% for massive performance gains
            // Based on FF1 analysis: dialogue boxes appear in upper-center portion of screen
            TargetSearchArea = new RectangleF(
                19.6875f,     // X: 19.6875% from left edge
                5.0926f,      // Y: 5.0926% from top edge
                60.4688f,     // Width: 60.4688% of screen width
                28.2407f      // Height: 28.2407% of screen height
            ),

            // FF1's distinctive blue border colors
            // Multiple shades account for compression artifacts and theme variations
            BorderColors = new[]
            {
                Color.FromArgb(66, 66, 231),   // Dark blue (primary)
                Color.FromArgb(99, 99, 255),   // Medium blue
                Color.FromArgb(33, 33, 165),   // Very dark blue
                Color.FromArgb(0, 88, 248),    // Bright blue
                Color.FromArgb(82, 82, 247),   // Blue variant
                Color.FromArgb(0, 0, 255),     // Pure blue
                Color.FromArgb(0, 100, 200),   // Cyan-blue
                Color.FromArgb(50, 50, 200)    // Dark blue-purple
            },

            // RGB tolerance for color matching
            // 80 works well for pixel-art style games with potential compression
            ColorTolerance = 80,

            // FF1 dialogue boxes are consistently sized
            MinSize = new Size(200, 100),   // Minimum dimensions
            MaxSize = new Size(1920, 800),  // Maximum dimensions

            // FF1 boxes are always landscape (wider than tall)
            RequireLandscapeAspect = true,

            // Cache optimization settings
            CachedPositionExpansion = 50,   // 50px expansion around last known position
            MaxConsecutiveFailures = 3      // Clear cache after 3 failures
        };
    }
}
