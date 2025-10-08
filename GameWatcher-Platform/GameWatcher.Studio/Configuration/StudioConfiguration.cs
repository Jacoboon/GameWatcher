using System.ComponentModel.DataAnnotations;

namespace GameWatcher.Studio.Configuration;

/// <summary>
/// Configuration settings for the GameWatcher Studio application.
/// Matches the structure defined in appsettings.json and V2 Platform architecture.
/// </summary>
public class GameWatcherConfig
{
    [Required]
    public bool AutoStart { get; set; } = true;
    
    [Range(500, 30000)]
    public int DetectionIntervalMs { get; set; } = 2000;
    
    public string[] PackDirectories { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Capture engine configuration based on V2 Platform CaptureConfig specification.
/// Controls frame capture rate, performance optimizations, and quality settings.
/// </summary>
public class CaptureConfig
{
    [Range(1, 60)]
    public int TargetFps { get; set; } = 10;
    
    public bool EnableOptimization { get; set; } = true;
    
    [Range(0.1, 1.0)]
    public double OptimizationThreshold { get; set; } = 0.85;
    
    public bool EnableFrameSkipping { get; set; } = true;
    
    public bool EnableDuplicateDetection { get; set; } = true;
    
    public CaptureQuality Quality { get; set; } = CaptureQuality.Balanced;
}

/// <summary>
/// OCR engine configuration based on V2 Platform OcrConfig specification.
/// Controls text recognition accuracy, preprocessing, and language settings.
/// </summary>
public class OcrConfig
{
    [Required]
    public string Language { get; set; } = "en-US";
    
    [Range(0.1, 1.0)]
    public double ConfidenceThreshold { get; set; } = 0.7;
    
    public bool EnablePreprocessing { get; set; } = true;
    
    public bool ConvertToGrayscale { get; set; } = true;
    
    public bool ApplyThreshold { get; set; } = true;
    
    public bool ReduceNoise { get; set; } = true;
    
    public bool EnhanceContrast { get; set; } = false;
    
    [Range(1.0, 4.0)]
    public double ScaleFactor { get; set; } = 2.0;
}

/// <summary>
/// Audio engine configuration for TTS and playback settings.
/// Based on V2 Platform AudioConfig specification.
/// </summary>
public class AudioConfig
{
    [Range(0, 100)]
    public int MasterVolume { get; set; } = 80;
    
    [Required]
    public string OutputDevice { get; set; } = "Default";
    
    public bool EnableCrossfade { get; set; } = true;
    
    [Range(0.5, 2.0)]
    public double DefaultSpeed { get; set; } = 1.0;
    
    public string DefaultVoice { get; set; } = "fable";
    
    public bool EnableAudioCaching { get; set; } = true;
}

/// <summary>
/// Performance optimization settings based on V1 learnings and V2 Platform design.
/// Controls memory usage, threading, and advanced optimizations.
/// </summary>
public class PerformanceConfig
{
    public bool EnableTargetedSearch { get; set; } = true;
    
    public bool EnableMemoryOptimization { get; set; } = true;
    
    [Range(1, 16)]
    public int MaxConcurrentOperations { get; set; } = 4;
    
    [Range(1, 72)]
    public int CacheRetentionHours { get; set; } = 24;
    
    public string MaxMemoryUsage { get; set; } = "200MB";
    
    public bool EnableHotSwapping { get; set; } = true;
}

/// <summary>
/// Detection engine configuration for textbox detection strategies.
/// Based on V2 Platform DetectionConfig and FF1 reference implementation.
/// </summary>
public class DetectionConfig
{
    public bool EnableOptimizedDetection { get; set; } = true;
    
    [Range(0.1, 1.0)]
    public double ConfidenceThreshold { get; set; } = 0.85;
    
    public DetectionStrategy Strategy { get; set; } = DetectionStrategy.HybridOptimized;
    
    public bool UseTargetedSearchArea { get; set; } = true;
    
    public SimilarityThresholds SimilarityThresholds { get; set; } = new();
}

/// <summary>
/// Similarity threshold configuration for frame comparison optimizations.
/// Based on V1 performance learnings (isBusy detection with dynamic thresholds).
/// </summary>
public class SimilarityThresholds
{
    [Range(10, 1000)]
    public int Idle { get; set; } = 500;
    
    [Range(10, 500)]
    public int Busy { get; set; } = 50;
}

public enum CaptureQuality
{
    Performance = 0,
    Balanced = 1,
    Quality = 2
}

public enum DetectionStrategy
{
    ColorBased = 0,
    Template = 1,
    HybridOptimized = 2,
    Custom = 3
}