using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameWatcher.Engine.Detection;
using GameWatcher.Engine.Ocr;

namespace GameWatcher.Engine.Packs;

/// <summary>
/// Core interface that all game packs must implement.
/// Provides game-specific detection, OCR, and speaker configuration.
/// </summary>
public interface IGamePack
{
    /// <summary>
    /// Pack metadata and information
    /// </summary>
    PackManifest Manifest { get; }
    
    /// <summary>
    /// Create game-specific detection strategy using Engine services
    /// </summary>
    ITextboxDetector CreateDetectionStrategy();
    
    /// <summary>
    /// Get collection of speaker profiles for voice matching
    /// </summary>
    ISpeakerCollection GetSpeakers();
    
    /// <summary>
    /// Get game-specific OCR configuration
    /// </summary>
    OcrConfig GetOcrConfiguration();
    
    /// <summary>
    /// Check if target game is currently running
    /// </summary>
    Task<bool> IsTargetGameRunningAsync();
    
    /// <summary>
    /// Validate that the running game version is supported
    /// </summary>
    Task<bool> ValidateGameVersionAsync();
    
    /// <summary>
    /// Initialize pack with Engine services
    /// </summary>
    Task<bool> InitializeAsync(IServiceProvider serviceProvider);
    
    /// <summary>
    /// Clean up resources when pack is unloaded
    /// </summary>
    Task DisposeAsync();
}

/// <summary>
/// Pack metadata and configuration
/// </summary>
public class PackManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string GameExecutable { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string[] SupportedVersions { get; set; } = Array.Empty<string>();
    public string EngineVersion { get; set; } = "2.0.0";
    public int TargetFrameRate { get; set; } = 15;
    
    // Performance metadata from design docs
    public PackPerformance Performance { get; set; } = new();
    public PackDetection Detection { get; set; } = new();
    public PackAudio Audio { get; set; } = new();
}

public class PackPerformance
{
    public bool TargetedDetection { get; set; }
    public double SearchAreaReduction { get; set; }
    public string AverageProcessingTime { get; set; } = "";
}

public class PackDetection
{
    public string Strategy { get; set; } = "";
    public string[] FallbackStrategies { get; set; } = Array.Empty<string>();
    public string OcrEngine { get; set; } = "WindowsOCR";
    public string OcrLanguage { get; set; } = "en-US";
}

public class PackAudio
{
    public string DefaultVoice { get; set; } = "";
    public double DefaultSpeed { get; set; } = 1.0;
    public int Speakers { get; set; }
    public int PreGeneratedLines { get; set; }
}