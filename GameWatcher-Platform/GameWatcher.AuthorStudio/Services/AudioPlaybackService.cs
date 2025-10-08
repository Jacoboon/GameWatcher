using System.Media;
using System.IO;

namespace GameWatcher.AuthorStudio.Services
{
    public class AudioPlaybackService
    {
        public void Play(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            try
            {
                using var player = new SoundPlayer(filePath);
                player.Play();
            }
            catch { }
        }
    }
}
