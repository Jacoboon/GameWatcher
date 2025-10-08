using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GameWatcher.Runtime.Services.Audio;

public interface IAudioPlaybackService
{
    Task PlayAsync(string filePath, string? effects = null);
}

public class AudioPlaybackService : IAudioPlaybackService
{
    private readonly object _lock = new();
    private IWavePlayer? _output;
    private WaveStream? _reader;

    public Task PlayAsync(string filePath, string? effects = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Task.CompletedTask;
        }
        return Task.Run(() =>
        {
            try
            {
                var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
                WaveStream reader = ext == ".mp3" ? new Mp3FileReader(filePath) : new AudioFileReader(filePath);
                ISampleProvider samples = reader.ToSampleProvider();

                // Apply effects if requested
                samples = ApplyEffects(samples, reader.WaveFormat.SampleRate, reader.WaveFormat.Channels, effects);

                lock (_lock)
                {
                    _output?.Stop();
                    _output?.Dispose();
                    _reader?.Dispose();
                    _reader = reader;
                    _output = new WaveOutEvent();
                    _output.Init(samples);
                    _output.Play();
                }
            }
            catch
            {
                // swallow audio errors for MVP
            }
        });
    }

    private static ISampleProvider ApplyEffects(ISampleProvider input, int sampleRate, int channels, string? effects)
    {
        if (string.IsNullOrWhiteSpace(effects)) return input;
        var tags = effects.Split(new[] { ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        ISampleProvider current = input;

        foreach (var tag in tags)
        {
            var t = tag.ToLowerInvariant();
            if (t.StartsWith("random pitch"))
            {
                var range = ParseRange(t);
                var factor = Clamp(range.HasValue ? Random.Shared.NextDouble() * (range.Value.max - range.Value.min) + range.Value.min : 0.9 + Random.Shared.NextDouble() * 0.2, 0.5, 2.0);
                current = new SmbPitchShiftingSampleProvider(current) { PitchFactor = (float)factor };
            }
            else if (t.StartsWith("pitch "))
            {
                if (double.TryParse(t.Substring(6), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var f))
                {
                    current = new SmbPitchShiftingSampleProvider(current) { PitchFactor = (float)Clamp(f, 0.5, 2.0) };
                }
            }
            else if (t.Contains("squeaky"))
            {
                current = new SmbPitchShiftingSampleProvider(current) { PitchFactor = 1.35f };
            }
            else if (t.Contains("cave echo") || t.Contains("cave"))
            {
                current = new SimpleEchoProvider(current, sampleRate, channels, delayMs: 280, decay: 0.45f, wet: 0.35f);
            }
            else if (t.Contains("throne room") || t.Contains("hall") || t.Contains("reverb"))
            {
                // shorter, denser echo
                current = new SimpleEchoProvider(current, sampleRate, channels, delayMs: 120, decay: 0.30f, wet: 0.25f);
                current = new SimpleEchoProvider(current, sampleRate, channels, delayMs: 220, decay: 0.25f, wet: 0.20f);
            }
        }

        return current;
    }

    private static (double min, double max)? ParseRange(string text)
    {
        var idx = text.IndexOf(' ');
        if (idx < 0) return null;
        var rest = text.Substring(idx).Trim();
        var parts = rest.Split(':');
        if (parts.Length != 2) return null;
        if (double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var a) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var b))
        {
            var min = Math.Min(a, b);
            var max = Math.Max(a, b);
            return (min, max);
        }
        return null;
    }

    private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
}

internal class SimpleEchoProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly float _decay;
    private readonly float _wet;
    private readonly float[] _buffer;
    private int _writePos;

    public SimpleEchoProvider(ISampleProvider source, int sampleRate, int channels, int delayMs, float decay, float wet)
    {
        _source = source;
        _channels = channels;
        _decay = decay;
        _wet = wet;
        var delaySamples = (int)(sampleRate * delayMs / 1000.0) * channels;
        _buffer = new float[Math.Max(delaySamples, 1) * 4];
        WaveFormat = source.WaveFormat;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (int n = 0; n < read; n++)
        {
            var i = offset + n;
            var dry = buffer[i];
            var delayed = _buffer[_writePos];
            var wetSample = dry + delayed * _decay;
            _buffer[_writePos] = wetSample;
            buffer[i] = dry * (1 - _wet) + wetSample * _wet;
            _writePos++;
            if (_writePos >= _buffer.Length) _writePos = 0;
        }
        return read;
    }

    public WaveFormat WaveFormat { get; }
}
