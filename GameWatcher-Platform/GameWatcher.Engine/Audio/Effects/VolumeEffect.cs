using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// Volume adjustment effect. Changes the amplitude (loudness) of the audio.
/// Parameter: gain_db (double) - Volume adjustment in decibels (-20 to +20)
///   - Negative values make audio quieter
///   - Positive values make audio louder
///   - 0 = no change
///   - -6 dB ≈ half volume
///   - +6 dB ≈ double volume
/// </summary>
public class VolumeEffect : AudioEffectBase
{
    public override string Type => "Volume";

    public override ISampleProvider Apply(ISampleProvider source)
    {
        var gainDb = GetParameter("gain_db", 0.0);
        
        // Convert dB to linear multiplier: linear = 10^(dB/20)
        var linearGain = Math.Pow(10.0, gainDb / 20.0);
        
        return new VolumeSampleProvider(source)
        {
            Volume = (float)linearGain
        };
    }
}
