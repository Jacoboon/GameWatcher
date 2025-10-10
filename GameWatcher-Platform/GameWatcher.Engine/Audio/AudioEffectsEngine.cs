using Microsoft.Extensions.Logging;
using NAudio.Wave;
using GameWatcher.Engine.Audio.Effects;

namespace GameWatcher.Engine.Audio;

/// <summary>
/// Core audio effects processing engine.
/// Chains effects together and applies them to audio streams.
/// </summary>
public class AudioEffectsEngine
{
    private readonly ILogger<AudioEffectsEngine> _logger;

    public AudioEffectsEngine(ILogger<AudioEffectsEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Apply a chain of effects to an audio source.
    /// Effects are applied in order, with each effect's output becoming the next effect's input.
    /// Disabled effects are skipped.
    /// </summary>
    /// <param name="source">The input audio stream</param>
    /// <param name="effects">List of effects to apply</param>
    /// <returns>The final audio stream with all effects applied</returns>
    public ISampleProvider ApplyEffectsChain(ISampleProvider source, List<IAudioEffect> effects)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (effects == null || effects.Count == 0)
        {
            _logger.LogDebug("No effects to apply, returning original source");
            return source;
        }

        ISampleProvider current = source;
        int appliedCount = 0;

        foreach (var effect in effects)
        {
            if (!effect.Enabled)
            {
                _logger.LogDebug("Skipping disabled effect: {Type}", effect.Type);
                continue;
            }

            try
            {
                _logger.LogDebug("Applying effect: {Type} with parameters: {Parameters}", 
                    effect.Type, 
                    string.Join(", ", effect.Parameters.Select(p => $"{p.Key}={p.Value}")));
                
                current = effect.Apply(current);
                appliedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply effect: {Type}", effect.Type);
                // Continue with remaining effects even if one fails
            }
        }

        _logger.LogInformation("Applied {Count} effects to audio stream", appliedCount);
        return current;
    }

    /// <summary>
    /// Play an audio file with effects applied.
    /// Useful for testing and previewing effects.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file (MP3, WAV, etc.)</param>
    /// <param name="effects">List of effects to apply</param>
    /// <param name="cancellationToken">Token to cancel playback</param>
    public async Task PlayWithEffects(
        string audioFilePath, 
        List<IAudioEffect> effects,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found", audioFilePath);

        _logger.LogInformation("Playing audio with effects: {FilePath}", audioFilePath);

        using var reader = new AudioFileReader(audioFilePath);
        var effectsChain = ApplyEffectsChain(reader, effects);

        using var output = new WaveOutEvent();
        output.Init(effectsChain);
        output.Play();

        _logger.LogDebug("Playback started");

        // Wait for playback to complete or cancellation
        while (output.PlaybackState == PlaybackState.Playing)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Playback cancelled");
                output.Stop();
                break;
            }

            await Task.Delay(100, cancellationToken);
        }

        _logger.LogInformation("Playback completed");
    }

    /// <summary>
    /// Export audio file with effects applied (bake effects).
    /// Creates a new audio file with effects permanently applied.
    /// </summary>
    /// <param name="inputPath">Source audio file</param>
    /// <param name="outputPath">Destination audio file</param>
    /// <param name="effects">Effects to apply</param>
    public async Task ExportWithEffects(
        string inputPath,
        string outputPath,
        List<IAudioEffect> effects)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input audio file not found", inputPath);

        _logger.LogInformation("Exporting audio with effects: {Input} -> {Output}", inputPath, outputPath);

        using var reader = new AudioFileReader(inputPath);
        var effectsChain = ApplyEffectsChain(reader, effects);

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Export to WAV (can be converted to MP3 later if needed)
        WaveFileWriter.CreateWaveFile16(outputPath, effectsChain);

        _logger.LogInformation("Export completed: {OutputPath}", outputPath);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Create a factory for common effects with default parameters.
    /// </summary>
    public static class EffectFactory
    {
        public static VolumeEffect CreateVolume(double gainDb)
        {
            return new VolumeEffect
            {
                Parameters = new Dictionary<string, object>
                {
                    ["gain_db"] = gainDb
                }
            };
        }

        public static LowPassFilterEffect CreateLowPassFilter(float cutoffFreq, float resonance = 0.5f)
        {
            return new LowPassFilterEffect
            {
                Parameters = new Dictionary<string, object>
                {
                    ["cutoff_frequency"] = cutoffFreq,
                    ["resonance"] = resonance
                }
            };
        }

        public static HighPassFilterEffect CreateHighPassFilter(float cutoffFreq, float resonance = 0.5f)
        {
            return new HighPassFilterEffect
            {
                Parameters = new Dictionary<string, object>
                {
                    ["cutoff_frequency"] = cutoffFreq,
                    ["resonance"] = resonance
                }
            };
        }

        /// <summary>
        /// Create a "Telephone" preset effect chain.
        /// Band-limits audio to 300Hz - 3000Hz with slight volume reduction.
        /// </summary>
        public static List<IAudioEffect> CreateTelephonePreset()
        {
            return new List<IAudioEffect>
            {
                CreateHighPassFilter(300f, 0.3f),
                CreateLowPassFilter(3000f, 0.4f),
                CreateVolume(-2.0)
            };
        }

        /// <summary>
        /// Create an "Underwater" preset effect chain.
        /// Heavily muffled with low cutoff and volume reduction.
        /// </summary>
        public static List<IAudioEffect> CreateUnderwaterPreset()
        {
            return new List<IAudioEffect>
            {
                CreateLowPassFilter(1500f, 0.7f),
                CreateVolume(-6.0)
            };
        }

        /// <summary>
        /// Create a "Whisper" preset effect chain.
        /// Removes low rumble and reduces volume significantly.
        /// </summary>
        public static List<IAudioEffect> CreateWhisperPreset()
        {
            return new List<IAudioEffect>
            {
                CreateHighPassFilter(200f, 0.2f),
                CreateVolume(-8.0)
            };
        }
    }
}
