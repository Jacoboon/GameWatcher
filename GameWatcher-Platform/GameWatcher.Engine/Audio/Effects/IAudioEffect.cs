using NAudio.Wave;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// Interface for all audio effects that can be applied to an audio stream.
/// Effects are chained together via ISampleProvider pattern.
/// </summary>
public interface IAudioEffect
{
    /// <summary>
    /// The type identifier for this effect (e.g., "Volume", "LowPassFilter")
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Whether this effect is enabled. Disabled effects are skipped in the chain.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Effect parameters as key-value pairs (e.g., "gain_db": -3.0)
    /// </summary>
    Dictionary<string, object> Parameters { get; set; }

    /// <summary>
    /// Apply this effect to the source audio stream.
    /// Returns a new ISampleProvider with the effect applied.
    /// </summary>
    /// <param name="source">The input audio stream</param>
    /// <returns>The output audio stream with effect applied</returns>
    ISampleProvider Apply(ISampleProvider source);
}

/// <summary>
/// Base class for audio effects providing common functionality.
/// </summary>
public abstract class AudioEffectBase : IAudioEffect
{
    public abstract string Type { get; }
    
    public bool Enabled { get; set; } = true;
    
    public Dictionary<string, object> Parameters { get; set; } = new();

    public abstract ISampleProvider Apply(ISampleProvider source);

    /// <summary>
    /// Helper to safely get a parameter value with a default fallback.
    /// </summary>
    protected T GetParameter<T>(string key, T defaultValue)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
}
