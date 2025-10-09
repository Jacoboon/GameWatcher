using System.Drawing;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Configuration for textbox detection that can be customized per game pack.
/// All coordinates are normalized percentages (0-100) to work across different resolutions.
/// </summary>
public class TextboxDetectionConfig
{
    /// <summary>
    /// Optional targeted search area to reduce scan region (79.3% performance gain for FF1).
    /// Coordinates are percentages of screen dimensions (0-100).
    /// If null, full screen search is performed.
    /// </summary>
    public RectangleF? TargetSearchArea { get; set; }

    /// <summary>
    /// Colors that typically appear in the game's textbox borders.
    /// Multiple colors allow for theme variations and compression artifacts.
    /// </summary>
    public Color[] BorderColors { get; set; } = Array.Empty<Color>();

    /// <summary>
    /// RGB tolerance for color matching (0-255).
    /// Higher values catch more variations but may produce false positives.
    /// Recommended: 80 for pixel-art games, 50 for HD games.
    /// </summary>
    public int ColorTolerance { get; set; } = 80;

    /// <summary>
    /// Minimum valid textbox dimensions in pixels.
    /// Prevents tiny UI elements from being detected as dialogue boxes.
    /// </summary>
    public Size MinSize { get; set; } = new Size(200, 100);

    /// <summary>
    /// Maximum valid textbox dimensions in pixels.
    /// Prevents full-screen overlays from being detected as dialogue boxes.
    /// </summary>
    public Size MaxSize { get; set; } = new Size(1920, 800);

    /// <summary>
    /// Whether the textbox must be wider than it is tall (landscape orientation).
    /// Most dialogue boxes are landscape, but some games use portrait boxes.
    /// </summary>
    public bool RequireLandscapeAspect { get; set; } = true;

    /// <summary>
    /// Expansion margin (in pixels) around last known textbox position for fast re-detection.
    /// Larger values increase search area but improve stability during camera movement.
    /// </summary>
    public int CachedPositionExpansion { get; set; } = 50;

    /// <summary>
    /// Number of consecutive detection failures before clearing cached position.
    /// Higher values prevent cache invalidation during brief occlusions.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;
}
