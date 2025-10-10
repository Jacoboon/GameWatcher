using NAudio.Wave;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// Low-pass filter effect. Removes high frequencies above the cutoff.
/// Makes audio sound muffled or darker.
/// 
/// Parameters:
/// - cutoff_frequency (float) - Frequency in Hz above which frequencies are attenuated (500-8000)
/// - resonance (float) - Peak at cutoff frequency, also called Q (0.0-1.0)
/// 
/// Examples:
/// - cutoff=1500, resonance=0.7 → Underwater/muffled sound
/// - cutoff=3000, resonance=0.4 → Telephone quality
/// - cutoff=6000, resonance=0.2 → Subtle warmth
/// </summary>
public class LowPassFilterEffect : AudioEffectBase
{
    public override string Type => "LowPassFilter";

    public override ISampleProvider Apply(ISampleProvider source)
    {
        var cutoffFreq = GetParameter("cutoff_frequency", 5000.0f);
        var resonance = GetParameter("resonance", 0.5f);
        
        // Clamp to safe ranges
        cutoffFreq = Math.Clamp(cutoffFreq, 500f, 8000f);
        resonance = Math.Clamp(resonance, 0.1f, 1.0f);
        
        return new LowPassFilterSampleProvider(source, cutoffFreq, resonance);
    }
}

/// <summary>
/// ISampleProvider wrapper that applies a low-pass filter using BiQuadFilter.
/// </summary>
internal class LowPassFilterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly NAudio.Dsp.BiQuadFilter[] filters;

    public WaveFormat WaveFormat => source.WaveFormat;

    public LowPassFilterSampleProvider(ISampleProvider source, float cutoffFreq, float q)
    {
        this.source = source;
        
        // Create one filter per channel for stereo/mono support
        filters = new NAudio.Dsp.BiQuadFilter[source.WaveFormat.Channels];
        for (int i = 0; i < filters.Length; i++)
        {
            filters[i] = NAudio.Dsp.BiQuadFilter.LowPassFilter(
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
