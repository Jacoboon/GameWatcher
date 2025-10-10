using NAudio.Wave;

namespace GameWatcher.Engine.Audio.Effects;

/// <summary>
/// Echo/Delay effect. Creates delayed repetitions of the audio signal.
/// 
/// Parameters:
/// - delay_ms (float) - Delay time in milliseconds (50-2000)
///   * 50-150ms: Slapback echo
///   * 200-500ms: Distinct echo
///   * 500-2000ms: Long delay/repeat
/// - decay (float) - How much the echo fades each repetition (0.0-1.0)
///   * 0.0 = No echo (single repeat)
///   * 0.5 = Echo fades to 50% each time
///   * 0.9 = Long sustained echo tail
/// - wet_level (float) - Echo volume relative to original (0.0-1.0)
///   * 0.0 = No echo heard
///   * 0.3 = Subtle echo
///   * 0.7 = Prominent echo
/// 
/// Examples:
/// - delay=400ms, decay=0.6, wet=0.4 → Cave echo
/// - delay=120ms, decay=0.3, wet=0.2 → Slapback (rockabilly vocal)
/// - delay=500ms, decay=0.8, wet=0.5 → Long cathedral tail
/// </summary>
public class EchoEffect : AudioEffectBase
{
    public override string Type => "Echo";

    public override ISampleProvider Apply(ISampleProvider source)
    {
        var delayMs = GetParameter("delay_ms", 300.0f);
        var decay = GetParameter("decay", 0.5f);
        var wetLevel = GetParameter("wet_level", 0.3f);
        
        // Clamp to safe ranges
        delayMs = Math.Clamp(delayMs, 50f, 2000f);
        decay = Math.Clamp(decay, 0.0f, 0.95f); // Prevent runaway feedback
        wetLevel = Math.Clamp(wetLevel, 0.0f, 1.0f);
        
        return new EchoSampleProvider(source, delayMs, decay, wetLevel);
    }
}

/// <summary>
/// ISampleProvider wrapper that applies echo/delay effect using a circular buffer.
/// </summary>
internal class EchoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float[] delayBuffer;
    private int bufferPosition;
    private readonly float decay;
    private readonly float wetLevel;

    public WaveFormat WaveFormat => source.WaveFormat;

    public EchoSampleProvider(ISampleProvider source, float delayMs, float decay, float wetLevel)
    {
        this.source = source;
        this.decay = decay;
        this.wetLevel = wetLevel;

        // Calculate delay buffer size: (sampleRate * channels * delaySeconds)
        var delaySamples = (int)(source.WaveFormat.SampleRate * source.WaveFormat.Channels * delayMs / 1000f);
        delayBuffer = new float[delaySamples];
        bufferPosition = 0;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);

        for (int i = 0; i < samplesRead; i++)
        {
            // Get current input sample
            float inputSample = buffer[offset + i];

            // Read delayed sample from circular buffer
            float delayedSample = delayBuffer[bufferPosition];

            // Mix: output = dry + (delayed * wet)
            float outputSample = inputSample + (delayedSample * wetLevel);

            // Write to output
            buffer[offset + i] = outputSample;

            // Write to delay buffer with decay (feedback)
            // This creates the repeating echo effect
            delayBuffer[bufferPosition] = inputSample + (delayedSample * decay);

            // Advance circular buffer position
            bufferPosition++;
            if (bufferPosition >= delayBuffer.Length)
                bufferPosition = 0;
        }

        return samplesRead;
    }
}
