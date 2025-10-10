using NAudio.Wave;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// High-pass filter effect. Removes low frequencies below the cutoff.
/// Makes audio sound thinner or brighter.
/// 
/// Parameters:
/// - cutoff_frequency (float) - Frequency in Hz below which frequencies are attenuated (50-2000)
/// - resonance (float) - Peak at cutoff frequency, also called Q (0.0-1.0)
/// 
/// Examples:
/// - cutoff=300, resonance=0.3 → Telephone quality (remove bass rumble)
/// - cutoff=800, resonance=0.8 → Robot/metallic voice
/// - cutoff=200, resonance=0.2 → Gentle bass reduction (whisper effect)
/// </summary>
public class HighPassFilterEffect : AudioEffectBase
{
    public override string Type => "HighPassFilter";

    public override ISampleProvider Apply(ISampleProvider source)
    {
        var cutoffFreq = GetParameter("cutoff_frequency", 300.0f);
        var resonance = GetParameter("resonance", 0.5f);
        
        // Clamp to safe ranges
        cutoffFreq = Math.Clamp(cutoffFreq, 50f, 2000f);
        resonance = Math.Clamp(resonance, 0.1f, 1.0f);
        
        return new HighPassFilterSampleProvider(source, cutoffFreq, resonance);
    }
}

/// <summary>
/// ISampleProvider wrapper that applies a high-pass filter using BiQuadFilter.
/// </summary>
internal class HighPassFilterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly NAudio.Dsp.BiQuadFilter[] filters;

    public WaveFormat WaveFormat => source.WaveFormat;

    public HighPassFilterSampleProvider(ISampleProvider source, float cutoffFreq, float q)
    {
        this.source = source;
        
        // Create one filter per channel for stereo/mono support
        filters = new NAudio.Dsp.BiQuadFilter[source.WaveFormat.Channels];
        for (int i = 0; i < filters.Length; i++)
        {
            filters[i] = NAudio.Dsp.BiQuadFilter.HighPassFilter(
                source.WaveFormat.SampleRate,
                cutoffFreq,
                q
            );
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        // Apply filter to each sample
        for (int i = 0; i < samplesRead; i++)
        {
            int channel = i % filters.Length;
            buffer[offset + i] = filters[channel].Transform(buffer[offset + i]);
        }
        
        return samplesRead;
    }
}
