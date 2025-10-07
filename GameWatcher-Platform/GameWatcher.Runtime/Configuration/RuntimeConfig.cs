namespace GameWatcher.Runtime.Configuration;

/// <summary>
/// Runtime configuration settings following the hierarchical structure from docs.
/// Supports global settings, pack-specific overrides, and streaming integrations.
/// </summary>
public class RuntimeConfig
{
    public GlobalConfig Global { get; set; } = new();
    public Dictionary<string, PackConfig> Packs { get; set; } = new();
    public StreamingConfig Streaming { get; set; } = new();
    public List<string> PackDirectories { get; set; } = new();
}

public class GlobalConfig
{
    public int CaptureFrameRate { get; set; } = 15;
    public bool EnablePerformanceOptimizations { get; set; } = true;
    public string MaxMemoryUsage { get; set; } = "200MB";
    public string LogLevel { get; set; } = "Information";
    public bool AutoDetectGames { get; set; } = true;
    public bool EnableHotSwapping { get; set; } = true;
}

public class PackConfig
{
    public string DetectionStrategy { get; set; } = "HybridOptimized";
    public TargetedSearchConfig? TargetedSearchArea { get; set; }
    public SpeakerConfig Speakers { get; set; } = new();
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

public class TargetedSearchConfig
{
    public bool Enabled { get; set; } = true;
    public double[] Coordinates { get; set; } = Array.Empty<double>();
    public double ReductionPercentage { get; set; } = 79.3;
}

public class SpeakerConfig
{
    public string DefaultVoice { get; set; } = "fable";
    public double DefaultSpeed { get; set; } = 1.2;
    public bool EnableAutoGeneration { get; set; } = true;
    public Dictionary<string, string> VoiceOverrides { get; set; } = new();
}

public class StreamingConfig
{
    public TwitchConfig Twitch { get; set; } = new();
    public ObsConfig Obs { get; set; } = new();
    public DiscordConfig Discord { get; set; } = new();
}

public class TwitchConfig
{
    public bool Enabled { get; set; } = false;
    public bool SendDialogueToChat { get; set; } = true;
    public bool CreatePredictions { get; set; } = false;
    public string? ChannelName { get; set; }
    public string? BotToken { get; set; }
}

public class ObsConfig
{
    public bool Enabled { get; set; } = false;
    public string OverlayTemplate { get; set; } = "Modern";
    public string UpdateFrequency { get; set; } = "OnDialogue";
    public string? WebSocketUrl { get; set; }
    public string? WebSocketPassword { get; set; }
}

public class DiscordConfig
{
    public bool Enabled { get; set; } = false;
    public string? BotToken { get; set; }
    public string? ChannelId { get; set; }
}