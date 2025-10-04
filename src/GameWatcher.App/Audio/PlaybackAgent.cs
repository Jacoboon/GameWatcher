using NAudio.Wave;

namespace GameWatcher.App.Audio;

internal sealed class PlaybackAgent : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;

    public Task PlayAsync(string path, CancellationToken ct = default)
    {
        Stop();
        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _output.PlaybackStopped += (_, __) => tcs.TrySetResult(true);
        _output.Init(_reader);
        _output.Play();
        ct.Register(() => { try { Stop(); } catch { } tcs.TrySetCanceled(); });
        return tcs.Task;
    }

    public void Stop()
    {
        if (_output != null)
        {
            try { _output.Stop(); } catch { }
            _output.Dispose();
            _output = null;
        }
        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }
    }

    public void Dispose() => Stop();
}

