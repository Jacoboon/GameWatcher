using System;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameWatcher.Engine.Detection;
using GameWatcher.Engine.Ocr;
using GameWatcher.Engine.Packs;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Packs.FF1.PixelRemaster;

/// <summary>
/// FF1 Pixel Remaster game pack - Reference implementation using all V1 optimizations
/// Preserves 79.3% search area reduction and 2.3ms average processing time
/// </summary>
public class FF1Pack : GamePackBase
{
    private PackManifest? _manifest;
    private FF1DetectionConfig? _detectionConfig;
    private SpeakerCollection? _speakers;
    
    public override PackManifest Manifest
    {
        get
        {
            if (_manifest == null)
            {
                LoadManifest();
            }
            return _manifest ?? CreateDefaultManifest();
        }
    }
    
    public override ITextboxDetector CreateDetectionStrategy()
    {
        var config = GetDetectionConfig();
        return new FF1HybridTextboxDetector(config);
    }
    
    public override ISpeakerCollection GetSpeakers()
    {
        if (_speakers == null)
        {
            LoadSpeakers();
        }
        return _speakers ?? new SpeakerCollection();
    }
    
    public override OcrConfig GetOcrConfiguration()
    {
        var config = GetDetectionConfig();
        
        return new OcrConfig
        {
            Language = "en-US",
            ScaleFactor = 2.0, // V1 learning: 2x scaling improves accuracy
            Preprocessing = new OcrPreprocessing
            {
                ConvertToGrayscale = true,
                ApplyThreshold = true,
                ReduceNoise = true,
                EnhanceContrast = false
            }
        };
    }
    
    protected override async Task InitializePackResourcesAsync()
    {
        await base.InitializePackResourcesAsync();
        
        // Pre-load configurations for performance
        LoadManifest();
        LoadSpeakers();
        
        _logger?.LogInformation("FF1 Pack initialized with {SpeakerCount} speakers", 
            GetSpeakers().GetAllSpeakers().Count());
    }
    
    private void LoadManifest()
    {
        try
        {
            var manifestData = LoadConfigurationFile<PackManifestData>("pack.json");
            if (manifestData != null)
            {
                _manifest = new PackManifest
                {
                    Name = manifestData.Name,
                    Version = manifestData.Version,
                    DisplayName = manifestData.DisplayName,
                    Description = manifestData.Description,
                    Author = manifestData.Author,
                    GameExecutable = manifestData.GameExecutable,
                    WindowTitle = manifestData.WindowTitle,
                    SupportedVersions = manifestData.SupportedVersions ?? Array.Empty<string>(),
                    EngineVersion = manifestData.EngineVersion,
                    Performance = new PackPerformance
                    {
                        TargetedDetection = manifestData.Performance?.TargetedDetection ?? false,
                        SearchAreaReduction = manifestData.Performance?.SearchAreaReduction ?? 0,
                        AverageProcessingTime = manifestData.Performance?.AverageProcessingTime ?? ""
                    },
                    Detection = new PackDetection
                    {
                        Strategy = manifestData.Detection?.Strategy ?? "",
                        FallbackStrategies = manifestData.Detection?.FallbackStrategies ?? Array.Empty<string>(),
                        OcrEngine = manifestData.Detection?.OcrEngine ?? "WindowsOCR",
                        OcrLanguage = manifestData.Detection?.OcrLanguage ?? "en-US"
                    },
                    Audio = new PackAudio
                    {
                        DefaultVoice = manifestData.Audio?.DefaultVoice ?? "",
                        DefaultSpeed = manifestData.Audio?.DefaultSpeed ?? 1.0,
                        Speakers = manifestData.Audio?.Speakers ?? 0,
                        PreGeneratedLines = manifestData.Audio?.PreGeneratedLines ?? 0
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load FF1 pack manifest");
        }
    }
    
    private void LoadSpeakers()
    {
        try
        {
            var speakerConfig = LoadConfigurationFile<SpeakerConfigurationData>("speakers.json");
            if (speakerConfig != null)
            {
                _speakers = new SpeakerCollection();
                _speakers.LoadFromConfiguration(speakerConfig);
                
                _logger?.LogInformation("Loaded {Count} FF1 speakers", speakerConfig.Speakers.Length);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load FF1 speakers configuration");
        }
    }
    
    private FF1DetectionConfig GetDetectionConfig()
    {
        if (_detectionConfig == null)
        {
            _detectionConfig = LoadConfigurationFile<FF1DetectionConfig>("detection.json");
        }
        
        return _detectionConfig ?? new FF1DetectionConfig();
    }
    
    private PackManifest CreateDefaultManifest()
    {
        return new PackManifest
        {
            Name = "FF1.PixelRemaster",
            Version = "2.1.0",
            DisplayName = "Final Fantasy I Pixel Remaster",
            Description = "Default FF1 pack configuration",
            Author = "GameWatcher Team",
            GameExecutable = "FINAL FANTASY.exe",
            WindowTitle = "FINAL FANTASY",
            EngineVersion = "2.0.0"
        };
    }
}

// Configuration data structures
public class PackManifestData
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string GameExecutable { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string[]? SupportedVersions { get; set; }
    public string EngineVersion { get; set; } = "";
    public PackPerformanceData? Performance { get; set; }
    public PackDetectionData? Detection { get; set; }
    public PackAudioData? Audio { get; set; }
}

public class PackPerformanceData
{
    public bool TargetedDetection { get; set; }
    public double SearchAreaReduction { get; set; }
    public string AverageProcessingTime { get; set; } = "";
}

public class PackDetectionData
{
    public string Strategy { get; set; } = "";
    public string[]? FallbackStrategies { get; set; }
    public string OcrEngine { get; set; } = "";
    public string OcrLanguage { get; set; } = "";
}

public class PackAudioData
{
    public string DefaultVoice { get; set; } = "";
    public double DefaultSpeed { get; set; }
    public int Speakers { get; set; }
    public int PreGeneratedLines { get; set; }
}