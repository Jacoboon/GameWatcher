using System;
using System.IO;

namespace GameWatcher.Engine.Audio
{
    public static class VoicePreviewStore
    {
        public static string GetRootDirectory()
        {
            var asmPath = typeof(VoicePreviewStore).Assembly.Location;
            var dir = Path.GetDirectoryName(asmPath)!;
            var root = Path.Combine(dir, "Voices");
            try
            {
                Directory.CreateDirectory(root);
                // quick write test
                var testPath = Path.Combine(root, ".write_test");
                File.WriteAllText(testPath, "ok");
                File.Delete(testPath);
                return root;
            }
            catch
            {
                // Fallback to user profile
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var userRoot = Path.Combine(appData, "GameWatcher", "Engine", "Voices");
                Directory.CreateDirectory(userRoot);
                return userRoot;
            }
        }

        public static string GetPreviewPath(string voice, double speed, string format)
        {
            var safeVoice = string.Join("_", voice.Split(Path.GetInvalidFileNameChars()));
            var ext = string.Equals(format, "mp3", StringComparison.OrdinalIgnoreCase) ? ".mp3" : ".wav";
            return Path.Combine(GetRootDirectory(), $"{safeVoice}-{speed:0.00}{ext}");
        }
    }
}
