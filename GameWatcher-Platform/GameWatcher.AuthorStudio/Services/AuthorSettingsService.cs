using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameWatcher.AuthorStudio.Services
{
    public class AuthorSettings
    {
        public string AudioFormat { get; set; } = "mp3"; // wav|mp3|flac (default mp3 for size)
        public double DefaultTtsSpeed { get; set; } = 1.0; // 0.25-4.0 per OpenAI API spec
    }

    public class AuthorSettingsService
    {
        private readonly string _path;
        private readonly ILogger<AuthorSettingsService>? _logger;
        public AuthorSettings Settings { get; private set; } = new AuthorSettings();

        public AuthorSettingsService(ILogger<AuthorSettingsService> logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "GameWatcher", "AuthorStudio");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
            
            _logger?.LogInformation("AuthorSettingsService initialized - settings path: {Path}", _path);
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    var loaded = JsonSerializer.Deserialize<AuthorSettings>(json) ?? new AuthorSettings();
                    Settings = loaded;
                    _logger?.LogInformation("Settings loaded from file - AudioFormat: {Format}, DefaultTtsSpeed: {Speed:F2}x", 
                        Settings.AudioFormat, Settings.DefaultTtsSpeed);
                }
                else
                {
                    _logger?.LogInformation("No settings file found, using defaults");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load settings, using defaults");
                Settings = new AuthorSettings();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
                _logger?.LogInformation("Settings saved - AudioFormat: {Format}, DefaultTtsSpeed: {Speed:F2}x", 
                    Settings.AudioFormat, Settings.DefaultTtsSpeed);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save settings");
            }
        }
    }
}
