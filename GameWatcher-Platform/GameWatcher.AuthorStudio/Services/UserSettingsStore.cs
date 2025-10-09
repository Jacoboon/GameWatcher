using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameWatcher.AuthorStudio.Models;
using Microsoft.Extensions.Logging;

namespace GameWatcher.AuthorStudio.Services;

/// <summary>
/// Manages user settings persistence to %AppData%\GameWatcher\AuthorStudio\user-settings.json.
/// </summary>
public class UserSettingsStore
{
    private readonly ILogger<UserSettingsStore> _logger;
    private readonly string _settingsPath;
    private UserSettings _settings;

    public UserSettingsStore(ILogger<UserSettingsStore> logger)
    {
        _logger = logger;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appData, "GameWatcher", "AuthorStudio");
        Directory.CreateDirectory(settingsDir);
        
        _settingsPath = Path.Combine(settingsDir, "user-settings.json");
        _settings = new UserSettings();
    }

    /// <summary>
    /// Current user settings (read-only reference).
    /// </summary>
    public UserSettings Settings => _settings;

    /// <summary>
    /// Loads user settings from disk. Creates default if not found.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                _logger.LogInformation("User settings loaded from: {Path}", _settingsPath);
            }
            else
            {
                _settings = new UserSettings();
                _logger.LogInformation("No user settings found, using defaults");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user settings, using defaults");
            _settings = new UserSettings();
        }
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_settingsPath, json);
            _logger.LogDebug("User settings saved to: {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user settings");
        }
    }

    /// <summary>
    /// Updates last pack path and adds to recent list.
    /// </summary>
    public async Task SetLastPackAsync(string packPath)
    {
        _settings.LastPackPath = packPath;

        // Add to recent list (unique, most recent first)
        _settings.RecentPacks.Remove(packPath);
        _settings.RecentPacks.Insert(0, packPath);

        // Trim to max size
        if (_settings.RecentPacks.Count > UserSettings.MaxRecentPacks)
        {
            _settings.RecentPacks = _settings.RecentPacks.Take(UserSettings.MaxRecentPacks).ToList();
        }

        await SaveAsync();
    }

    /// <summary>
    /// Updates window position and size.
    /// </summary>
    public async Task SetWindowBoundsAsync(double width, double height, double? left, double? top)
    {
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        _settings.WindowLeft = left;
        _settings.WindowTop = top;
        await SaveAsync();
    }

    /// <summary>
    /// Toggles auto-load last pack preference.
    /// </summary>
    public async Task SetAutoLoadLastPackAsync(bool enabled)
    {
        _settings.AutoLoadLastPack = enabled;
        await SaveAsync();
    }
}
