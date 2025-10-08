using System.Media;

namespace GameWatcher.Runtime.Services.Audio;

public interface IAudioPlaybackService
{
    Task PlayAsync(string filePath);
}

public class AudioPlaybackService : IAudioPlaybackService
{
    public Task PlayAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Task.CompletedTask;
        }
        return Task.Run(() =>
        {
            try
            {
                using var player = new SoundPlayer(filePath);
                player.Play(); // fire-and-forget (gapless/queue is future work)
            }
            catch
            {
                // swallow audio errors for MVP
            }
        });
    }
}

