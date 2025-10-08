using System;
using System.IO;
using System.Text.Json;

namespace GameWatcher.AuthorStudio.Services
{
    public class AuthorSettings
    {
        public string AudioFormat { get; set; } = "mp3"; // wav|mp3 (default mp3 for size)
    }

    public class AuthorSettingsService
    {
        private readonly string _path;
        public AuthorSettings Settings { get; private set; } = new AuthorSettings();

        public AuthorSettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "GameWatcher", "AuthorStudio");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    Settings = JsonSerializer.Deserialize<AuthorSettings>(json) ?? new AuthorSettings();
                }
            }
            catch { Settings = new AuthorSettings(); }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch { }
        }
    }
}
