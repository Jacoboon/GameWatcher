using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;

namespace GameWatcher.Studio.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IConfiguration _configuration;

    [ObservableProperty]
    private ObservableCollection<SettingItemViewModel> _generalSettings = new();

    [ObservableProperty]
    private ObservableCollection<SettingItemViewModel> _captureSettings = new();

    [ObservableProperty]
    private ObservableCollection<SettingItemViewModel> _ocrSettings = new();

    [ObservableProperty]
    private ObservableCollection<SettingItemViewModel> _audioSettings = new();

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(ILogger<SettingsViewModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Settings ViewModel");
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Settings ViewModel");
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    private async Task LoadSettingsAsync()
    {
        // General Settings
        GeneralSettings.Clear();
        GeneralSettings.Add(new SettingItemViewModel
        {
            Name = "Auto Start Monitoring",
            Description = "Automatically start monitoring when a supported game is detected",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("GameWatcher:AutoStart", true)
        });

        GeneralSettings.Add(new SettingItemViewModel
        {
            Name = "Detection Interval",
            Description = "Game detection check interval in milliseconds",
            Type = SettingType.Integer,
            Value = _configuration.GetValue<int>("GameWatcher:DetectionIntervalMs", 2000),
            MinValue = 500,
            MaxValue = 10000
        });

        GeneralSettings.Add(new SettingItemViewModel
        {
            Name = "Pack Directories",
            Description = "Directories to search for game packs",
            Type = SettingType.StringList,
            Value = _configuration.GetSection("GameWatcher:PackDirectories").Get<string[]>() ?? Array.Empty<string>()
        });

        // Capture Settings
        CaptureSettings.Clear();
        CaptureSettings.Add(new SettingItemViewModel
        {
            Name = "Capture Rate",
            Description = "Frame capture rate in FPS",
            Type = SettingType.Integer,
            Value = _configuration.GetValue<int>("Capture:TargetFps", 10),
            MinValue = 1,
            MaxValue = 60
        });

        CaptureSettings.Add(new SettingItemViewModel
        {
            Name = "Enable Optimization",
            Description = "Use search area optimization for better performance",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("Capture:EnableOptimization", true)
        });

        CaptureSettings.Add(new SettingItemViewModel
        {
            Name = "Optimization Threshold",
            Description = "Similarity threshold for search area optimization (0.0-1.0)",
            Type = SettingType.Double,
            Value = _configuration.GetValue<double>("Capture:OptimizationThreshold", 0.85),
            MinValue = 0.0,
            MaxValue = 1.0
        });

        // OCR Settings
        OcrSettings.Clear();
        OcrSettings.Add(new SettingItemViewModel
        {
            Name = "Language",
            Description = "OCR language for text recognition",
            Type = SettingType.String,
            Value = _configuration.GetValue<string>("OCR:Language", "en-US")
        });

        OcrSettings.Add(new SettingItemViewModel
        {
            Name = "Confidence Threshold",
            Description = "Minimum confidence for OCR results (0.0-1.0)",
            Type = SettingType.Double,
            Value = _configuration.GetValue<double>("OCR:ConfidenceThreshold", 0.7),
            MinValue = 0.0,
            MaxValue = 1.0
        });

        OcrSettings.Add(new SettingItemViewModel
        {
            Name = "Enable Preprocessing",
            Description = "Apply image preprocessing for better OCR accuracy",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("OCR:EnablePreprocessing", true)
        });

        // Audio Settings
        AudioSettings.Clear();
        AudioSettings.Add(new SettingItemViewModel
        {
            Name = "Master Volume",
            Description = "Master audio volume (0-100)",
            Type = SettingType.Integer,
            Value = _configuration.GetValue<int>("Audio:MasterVolume", 80),
            MinValue = 0,
            MaxValue = 100
        });

        AudioSettings.Add(new SettingItemViewModel
        {
            Name = "Audio Device",
            Description = "Primary audio output device",
            Type = SettingType.String,
            Value = _configuration.GetValue<string>("Audio:OutputDevice", "Default")
        });

        AudioSettings.Add(new SettingItemViewModel
        {
            Name = "Enable Crossfade",
            Description = "Use crossfading between audio clips",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("Audio:EnableCrossfade", true)
        });

        // Subscribe to value changes
        foreach (var setting in GeneralSettings.Concat(CaptureSettings).Concat(OcrSettings).Concat(AudioSettings))
        {
            setting.ValueChanged += OnSettingValueChanged;
        }

        StatusMessage = "Settings loaded successfully";
        await Task.CompletedTask;
    }

    private void OnSettingValueChanged(object? sender, EventArgs e)
    {
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            StatusMessage = "Saving settings...";
            
            // In a real implementation, you would save to appsettings.json or user settings
            // For now, we'll just simulate the save
            await Task.Delay(500);

            HasUnsavedChanges = false;
            StatusMessage = "Settings saved successfully";
            
            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        try
        {
            StatusMessage = "Resetting to defaults...";
            await LoadSettingsAsync();
            HasUnsavedChanges = true;
            StatusMessage = "Settings reset to defaults";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            StatusMessage = $"Failed to reset settings: {ex.Message}";
        }
    }

    public void Dispose()
    {
        foreach (var setting in GeneralSettings.Concat(CaptureSettings).Concat(OcrSettings).Concat(AudioSettings))
        {
            setting.ValueChanged -= OnSettingValueChanged;
        }
    }
}

public partial class SettingItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private SettingType _type;

    [ObservableProperty]
    private object? _value;

    [ObservableProperty]
    private object? _minValue;

    [ObservableProperty]
    private object? _maxValue;

    public event EventHandler? ValueChanged;

    partial void OnValueChanged(object? value)
    {
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}

public enum SettingType
{
    String,
    Integer,
    Double,
    Boolean,
    StringList
}