using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// Serializable representation of an audio effect for JSON storage.
/// Used in .effects.json files and preset files.
/// </summary>
public class AudioEffectMetadata
{
    /// <summary>
    /// The effect type identifier (e.g., "Volume", "LowPassFilter", "Reverb")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this effect is enabled. Disabled effects are skipped during playback.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Effect parameters as key-value pairs.
    /// Examples: { "gain_db": -3.0 }, { "cutoff_frequency": 3000, "resonance": 0.5 }
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Create an AudioEffectMetadata from an IAudioEffect instance.
    /// </summary>
    public static AudioEffectMetadata FromEffect(IAudioEffect effect)
    {
        return new AudioEffectMetadata
        {
            Type = effect.Type,
            Enabled = effect.Enabled,
            Parameters = new Dictionary<string, object>(effect.Parameters)
        };
    }

    /// <summary>
    /// Convert this metadata back into an IAudioEffect instance.
    /// </summary>
    public IAudioEffect ToEffect()
    {
        IAudioEffect effect = Type switch
        {
            "Volume" => new VolumeEffect(),
            "LowPassFilter" => new LowPassFilterEffect(),
            "HighPassFilter" => new HighPassFilterEffect(),
            "Echo" => new EchoEffect(),
            "Reverb" => new ReverbEffect(),
            _ => throw new NotSupportedException($"Unknown effect type: {Type}")
        };

        effect.Enabled = Enabled;
        effect.Parameters = new Dictionary<string, object>(Parameters);

        return effect;
    }
}

/// <summary>
/// Represents a named collection of effects that can be applied as a preset.
/// Used for both built-in presets and user-created custom presets.
/// </summary>
public class EffectPreset
{
    /// <summary>
    /// User-friendly name of the preset (e.g., "Cave Echo", "Telephone")
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Brief description of what this preset does or when to use it.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for organization (e.g., "Environmental", "Communication", "Emotional", "Special")
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The effects chain that makes up this preset.
    /// </summary>
    [JsonPropertyName("effects")]
    public List<AudioEffectMetadata> Effects { get; set; } = new();

    /// <summary>
    /// Create an EffectPreset from a list of IAudioEffect instances.
    /// </summary>
    public static EffectPreset FromEffects(string name, string description, string category, List<IAudioEffect> effects)
    {
        return new EffectPreset
        {
            Name = name,
            Description = description,
            Category = category,
            Effects = effects.Select(AudioEffectMetadata.FromEffect).ToList()
        };
    }

    /// <summary>
    /// Convert this preset back into a list of IAudioEffect instances.
    /// </summary>
    public List<IAudioEffect> ToEffects()
    {
        return Effects.Select(e => e.ToEffect()).ToList();
    }

    /// <summary>
    /// Save this preset to a JSON file.
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Load a preset from a JSON file.
    /// </summary>
    public static EffectPreset LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<EffectPreset>(json) 
               ?? throw new InvalidOperationException($"Failed to deserialize preset from {filePath}");
    }
}

/// <summary>
/// Per-file effects metadata. Stored as {audio_filename}.effects.json
/// Links an audio file to its effects chain and optional preset.
/// </summary>
public class DialogueAudioMetadata
{
    /// <summary>
    /// Schema version for future compatibility.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// The audio file this metadata applies to (relative path or filename).
    /// </summary>
    [JsonPropertyName("audio_file")]
    public string AudioFile { get; set; } = string.Empty;

    /// <summary>
    /// Optional: The preset name this was created from (for reference).
    /// </summary>
    [JsonPropertyName("preset_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PresetName { get; set; }

    /// <summary>
    /// The effects chain applied to this audio file.
    /// </summary>
    [JsonPropertyName("effects")]
    public List<AudioEffectMetadata> Effects { get; set; } = new();

    /// <summary>
    /// Create metadata from an audio file path and effects list.
    /// </summary>
    public static DialogueAudioMetadata Create(string audioFilePath, List<IAudioEffect> effects, string? presetName = null)
    {
        return new DialogueAudioMetadata
        {
            AudioFile = Path.GetFileName(audioFilePath),
            PresetName = presetName,
            Effects = effects.Select(AudioEffectMetadata.FromEffect).ToList()
        };
    }

    /// <summary>
    /// Get the expected path for the .effects.json file given an audio file path.
    /// Example: "line_001.mp3" -> "line_001.effects.json"
    /// </summary>
    public static string GetMetadataPath(string audioFilePath)
    {
        var directory = Path.GetDirectoryName(audioFilePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(audioFilePath);
        return Path.Combine(directory, $"{fileNameWithoutExt}.effects.json");
    }

    /// <summary>
    /// Convert this metadata back into a list of IAudioEffect instances.
    /// </summary>
    public List<IAudioEffect> ToEffects()
    {
        return Effects.Select(e => e.ToEffect()).ToList();
    }

    /// <summary>
    /// Save this metadata to a .effects.json file next to the audio file.
    /// </summary>
    public void SaveToFile(string audioFilePath)
    {
        var metadataPath = GetMetadataPath(audioFilePath);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(metadataPath, json);
    }

    /// <summary>
    /// Load metadata from the .effects.json file for a given audio file.
    /// Returns null if the metadata file doesn't exist.
    /// </summary>
    public static DialogueAudioMetadata? LoadFromFile(string audioFilePath)
    {
        var metadataPath = GetMetadataPath(audioFilePath);
        if (!File.Exists(metadataPath))
            return null;

        var json = File.ReadAllText(metadataPath);
        return JsonSerializer.Deserialize<DialogueAudioMetadata>(json);
    }
}
