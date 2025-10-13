using System;
using System.Drawing;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Configuration for the optimal detection loop.
/// Combines V1 proven algorithms with V2 modular architecture.
/// </summary>
public class DetectionLoopConfig
{
    /// <summary>
    /// Target frames per second (default: 15 FPS = 67ms per frame).
    /// </summary>
    public int TargetFps { get; set; } = 15;
    
    /// <summary>
    /// Sample rate for initial stability detection (higher = more similar required).
    /// V1 used 500 for initial detection.
    /// </summary>
    public int StableSampleRate { get; set; } = 500;
    
    /// <summary>
    /// Sample rate for detecting text changes in stable textbox (lower = more sensitive).
    /// V1 used 50 for follow-up dialogue detection (99% similarity).
    /// </summary>
    public int ChangeSampleRate { get; set; } = 50;
    
    /// <summary>
    /// Enable textbox location caching to avoid re-detection on every frame.
    /// </summary>
    public bool EnableTextboxCache { get; set; } = true;
    
    /// <summary>
    /// Number of frames before cached textbox position is invalidated.
    /// Default: 300 frames = 20 seconds at 15 FPS.
    /// </summary>
    public int CacheInvalidateAfterFrames { get; set; } = 300;
    
    /// <summary>
    /// Enable pre-crop optimization (crop textbox before comparison).
    /// Critical performance optimization: 30× faster comparison.
    /// </summary>
    public bool EnablePreCrop { get; set; } = true;
    
    /// <summary>
    /// Enable content hash check to skip OCR on identical textbox content.
    /// </summary>
    public bool EnableHashCheck { get; set; } = true;
    
    /// <summary>
    /// Textbox detection configuration (game-specific).
    /// </summary>
    public TextboxDetectionConfig TextboxConfig { get; set; } = new();
}
