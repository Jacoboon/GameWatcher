using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Studio.Services;

/// <summary>
/// Studio (Player) settings model - persisted to %AppData%\GameWatcher\Studio\settings.json
/// </summary>
public class StudioSettings
{
    // General Settings
    public bool AutoStart { get; set; } = true;
    public double DetectionIntervalSeconds { get; set; } = 2.0;
    public string[] PackDirectories { get; set; } = Array.Empty<string>();

    // Capture Settings
    public int CaptureRate { get; set; } = 10;
    public bool EnableOptimization { get; set; } = true;
    public double OptimizationThreshold { get; set; } = 0.85;
    public bool EnableDuplicateDetection { get; set; } = true;

    // OCR Settings
    public double ConfidenceThreshold { get; set; } = 0.7;
    public bool EnablePreprocessing { get; set; } = true;
    public double ScaleFactor { get; set; } = 2.0;
    public bool ConvertToGrayscale { get; set; } = true;

    // Audio Settings
    public int MasterVolume { get; set; } = 80;
    public string OutputDevice { get; set; } = "Default";
    public bool EnableCrossfade { get; set; } = true;
    public bool EnableAudioCaching { get; set; } = true;
}

/// <summary>
/// Service for persisting Studio (Player) settings.
/// Settings are stored at: %AppData%\GameWatcher\Studio\settings.json
/// </summary>
public class StudioSettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger<StudioSettingsService>? _logger;
    
    public StudioSettings Settings { get; private set; } = new StudioSettings();

    public StudioSettingsService(ILogger<StudioSettingsService> logger)
    {
        _logger = logger;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appData, "GameWatcher", "Studio");
        Directory.CreateDirectory(settingsDir);
        
        _settingsPath = Path.Combine(settingsDir, "settings.json");
        
        _logger?.LogInformation("StudioSettingsService initialized - settings path: {Path}", _settingsPath);
        Load();
    }

    /// <summary>
    /// Loads settings from disk. Creates default settings if file doesn't exist.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<StudioSettings>(json) ?? new StudioSettings();
                Settings = loaded;
                _logger?.LogInformation("Settings loaded from file - CaptureRate: {Rate}, MasterVolume: {Volume}", 
                    Settings.CaptureRate, Settings.MasterVolume);
            }
            else
            {
                _logger?.LogInformation("No settings file found, using defaults");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load settings, using defaults");
            Settings = new StudioSettings();
        }
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            var json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(_settingsPath, json);
            
            _logger?.LogInformation("Settings saved - CaptureRate: {Rate}, MasterVolume: {Volume}", 
                Settings.CaptureRate, Settings.MasterVolume);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save settings");
        }
    }
}
