using System.Text.Json.Serialization;

namespace GameWatcher.AuthorStudio.Models;

/// <summary>
/// Represents pack-specific player settings overrides that ship with a voice pack.
/// These settings are recommendations from the pack author for optimal playback.
/// </summary>
public class PackSettingsOverrides
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("overrides")]
    public SettingsOverrides Overrides { get; set; } = new();
}

public class SettingsOverrides
{
    [JsonPropertyName("capture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CaptureOverrides? Capture { get; set; }

    [JsonPropertyName("audio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AudioOverrides? Audio { get; set; }

    [JsonPropertyName("ocr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OcrOverrides? Ocr { get; set; }
}

public class CaptureOverrides
{
    [JsonPropertyName("target_fps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TargetFps { get; set; }

    [JsonPropertyName("confidence_threshold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ConfidenceThreshold { get; set; }

    [JsonPropertyName("enable_optimization")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableOptimization { get; set; }

    [JsonPropertyName("enable_duplicate_detection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableDuplicateDetection { get; set; }
}

public class AudioOverrides
{
    [JsonPropertyName("master_volume")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MasterVolume { get; set; }

    [JsonPropertyName("enable_crossfade")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableCrossfade { get; set; }

    [JsonPropertyName("playback_speed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PlaybackSpeed { get; set; }

    [JsonPropertyName("enable_caching")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableCaching { get; set; }
}

public class OcrOverrides
{
    [JsonPropertyName("confidence_threshold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ConfidenceThreshold { get; set; }

    [JsonPropertyName("enable_preprocessing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnablePreprocessing { get; set; }

    [JsonPropertyName("scale_factor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ScaleFactor { get; set; }

    [JsonPropertyName("convert_to_grayscale")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ConvertToGrayscale { get; set; }
}
