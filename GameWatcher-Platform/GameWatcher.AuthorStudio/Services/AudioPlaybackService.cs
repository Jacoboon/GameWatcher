using System.IO;
using System.Windows.Media;

namespace GameWatcher.AuthorStudio.Services
{
    public class AudioPlaybackService
    {
        private readonly MediaPlayer _player = new MediaPlayer();

        public void Play(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            try
            {
                _player.Stop();
                _player.Open(new System.Uri(Path.GetFullPath(filePath)));
                _player.Volume = 1.0; // future: tie to settings
                _player.Play();
            }
            catch { }
        }
    }
}
