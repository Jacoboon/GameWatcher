using NAudio.Wave;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// Reverb effect. Simulates acoustic space/room reflections.
/// Uses a simplified Schroeder reverb with multiple comb and allpass filters.
/// 
/// Parameters:
/// - room_size (float) - Size of the simulated space (0.0-1.0)
///   * 0.0-0.3: Small room (bedroom, closet)
///   * 0.4-0.6: Medium room (studio, hall)
///   * 0.7-1.0: Large space (cathedral, arena)
/// - damping (float) - High frequency absorption (0.0-1.0)
///   * 0.0: Bright, reflective surfaces (tile, glass)
///   * 0.5: Balanced (wood, plaster)
///   * 1.0: Dark, absorptive (carpet, drapes)
/// - wet_level (float) - Reverb volume (0.0-1.0)
///   * 0.0: No reverb (dry signal only)
///   * 0.3: Subtle ambience
///   * 0.7: Heavy reverb
/// - dry_level (float) - Original signal volume (0.0-1.0)
///   * 0.5: 50% original, creates distance effect
///   * 1.0: Full original signal
/// 
/// Examples:
/// - room=0.9, damp=0.3, wet=0.5, dry=0.5 → Cave
/// - room=0.6, damp=0.8, wet=0.4, dry=0.6 → Underwater
/// - room=0.8, damp=0.5, wet=0.6, dry=0.4 → Cathedral
/// </summary>
public class ReverbEffect : AudioEffectBase
{
    public override string Type => "Reverb";

    public override ISampleProvider Apply(ISampleProvider source)
    {
        var roomSize = GetParameter("room_size", 0.5f);
        var damping = GetParameter("damping", 0.5f);
        var wetLevel = GetParameter("wet_level", 0.3f);
        var dryLevel = GetParameter("dry_level", 0.7f);
        
        // Clamp to safe ranges
        roomSize = Math.Clamp(roomSize, 0.0f, 1.0f);
        damping = Math.Clamp(damping, 0.0f, 1.0f);
        wetLevel = Math.Clamp(wetLevel, 0.0f, 1.0f);
        dryLevel = Math.Clamp(dryLevel, 0.0f, 1.0f);
        
        return new ReverbSampleProvider(source, roomSize, damping, wetLevel, dryLevel);
    }
}

/// <summary>
/// Simple Schroeder reverb implementation using comb filters.
/// This is a basic reverb - production quality would use more sophisticated algorithms.
/// </summary>
internal class ReverbSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly CombFilter[] combFilters;
    private readonly float wetLevel;
    private readonly float dryLevel;

    public WaveFormat WaveFormat => source.WaveFormat;

    public ReverbSampleProvider(ISampleProvider source, float roomSize, float damping, float wetLevel, float dryLevel)
    {
        this.source = source;
        this.wetLevel = wetLevel;
        this.dryLevel = dryLevel;

        // Create comb filters with different delay times to simulate reflections
        // Delay times based on Schroeder's recommendations (in samples at 44.1kHz)
        var sampleRate = source.WaveFormat.SampleRate;
        var delayTimes = new[] { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
        
        // Scale delay times by room size
        var roomSizeMultiplier = 0.5f + (roomSize * 1.5f); // Range: 0.5 to 2.0
        
        combFilters = new CombFilter[delayTimes.Length];
        for (int i = 0; i < delayTimes.Length; i++)
        {
            var scaledDelay = (int)(delayTimes[i] * roomSizeMultiplier * sampleRate / 44100f);
            var feedback = 0.5f + (roomSize * 0.3f); // Larger rooms have more feedback
            var dampingFactor = 1.0f - (damping * 0.5f); // Convert damping to filter coefficient
            
            combFilters[i] = new CombFilter(scaledDelay, feedback, dampingFactor);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);

        for (int i = 0; i < samplesRead; i++)
        {
            float inputSample = buffer[offset + i];
            float reverbSample = 0;

            // Sum all comb filter outputs
            foreach (var filter in combFilters)
            {
                reverbSample += filter.Process(inputSample);
            }

            // Average the comb filter outputs
            reverbSample /= combFilters.Length;

            // Mix dry and wet signals
            buffer[offset + i] = (inputSample * dryLevel) + (reverbSample * wetLevel);
        }

        return samplesRead;
    }

    /// <summary>
    /// Comb filter - creates series of echoes with feedback
    /// </summary>
    private class CombFilter
    {
        private readonly float[] buffer;
        private int bufferIndex;
        private readonly float feedback;
        private readonly float dampingCoeff;
        private float filterStore;

        public CombFilter(int delayInSamples, float feedback, float dampingCoeff)
        {
            buffer = new float[delayInSamples];
            this.feedback = feedback;
            this.dampingCoeff = dampingCoeff;
            bufferIndex = 0;
            filterStore = 0;
        }

        public float Process(float input)
        {
            // Read delayed sample
            float output = buffer[bufferIndex];

            // Simple one-pole lowpass filter for damping high frequencies
            filterStore = (output * (1 - dampingCoeff)) + (filterStore * dampingCoeff);

            // Write new sample with feedback
            buffer[bufferIndex] = input + (filterStore * feedback);

            // Advance buffer position
            bufferIndex++;
            if (bufferIndex >= buffer.Length)
                bufferIndex = 0;

            return output;
        }
    }
}
