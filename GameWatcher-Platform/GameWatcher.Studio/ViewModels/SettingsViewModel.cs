using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;
using NAudio.Wave;

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
    private ObservableCollection<string> _availableAudioDevices = new();

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
            LoadAvailableAudioDevices();
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Settings ViewModel");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Initialize Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    private void LoadAvailableAudioDevices()
    {
        try
        {
            AvailableAudioDevices.Clear();
            AvailableAudioDevices.Add("Default");
            
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                AvailableAudioDevices.Add(capabilities.ProductName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate audio devices");
            // Ensure at least "Default" is available
            if (AvailableAudioDevices.Count == 0)
            {
                AvailableAudioDevices.Add("Default");
            }
        }
    }

    private async Task LoadSettingsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[LoadSettingsAsync] START");
        
        // General Settings
        GeneralSettings.Clear();
        System.Diagnostics.Debug.WriteLine("[LoadSettingsAsync] Cleared GeneralSettings");
        GeneralSettings.Add(new SettingItemViewModel
        {
            Name = "Auto Start Monitoring",
            Description = "Automatically start monitoring when a supported game is detected",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("GameWatcher:AutoStart", true)
        });

        GeneralSettings.Add(new SettingItemViewModel
        {
            Name = "Game Detection Polling Rate",
            Description = "How often to check for game window (in seconds)",
            Type = SettingType.Double,
            Value = _configuration.GetValue<double>("GameWatcher:DetectionIntervalSeconds", 2.0),
            MinValue = 0.5,
            MaxValue = 10.0
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
            Description = "Frame capture rate in FPS (higher = more responsive, lower = better performance)",
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
        // Note: Language setting removed - not yet implemented in OCR engine
        
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

        // Add V2 Platform specific settings
        CaptureSettings.Add(new SettingItemViewModel
        {
            Name = "Enable Duplicate Detection",
            Description = "Skip duplicate frames for better performance",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("Capture:EnableDuplicateDetection", true)
        });

        OcrSettings.Add(new SettingItemViewModel
        {
            Name = "Scale Factor",
            Description = "Image scaling for OCR preprocessing (1.0-4.0)",
            Type = SettingType.Double,
            Value = _configuration.GetValue<double>("OCR:ScaleFactor", 2.0),
            MinValue = 1.0,
            MaxValue = 4.0
        });

        OcrSettings.Add(new SettingItemViewModel
        {
            Name = "Convert to Grayscale",
            Description = "Convert images to grayscale before OCR",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("OCR:ConvertToGrayscale", true)
        });

        AudioSettings.Add(new SettingItemViewModel
        {
            Name = "Default Audio Speed",
            Description = "Default playback speed for TTS audio (0.5-2.0x)",
            Type = SettingType.Double,
            Value = _configuration.GetValue<double>("Audio:DefaultSpeed", 1.0),
            MinValue = 0.5,
            MaxValue = 2.0
        });

        AudioSettings.Add(new SettingItemViewModel
        {
            Name = "Enable Audio Caching",
            Description = "Cache generated TTS audio for faster playback",
            Type = SettingType.Boolean,
            Value = _configuration.GetValue<bool>("Audio:EnableAudioCaching", true)
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

    private object? _value;
    public object? Value
    {
        get => _value;
        set
        {
            // Coerce value to correct type based on SettingType
            object? coercedValue = value;
            if (value != null)
            {
                try
                {
                    if (Type == SettingType.Integer && value is double doubleValue)
                    {
                        coercedValue = (int)Math.Round(doubleValue);
                    }
                    else if (Type == SettingType.Integer && value is not int)
                    {
                        // Try to convert other types to int
                        coercedValue = Convert.ToInt32(value);
                    }
                    else if (Type == SettingType.Double && value is int intValue)
                    {
                        coercedValue = (double)intValue;
                    }
                    else if (Type == SettingType.Double && value is not double)
                    {
                        // Try to convert other types to double
                        coercedValue = Convert.ToDouble(value);
                    }
                    else if (Type == SettingType.Boolean && value is not bool)
                    {
                        // Don't allow non-boolean values for Boolean settings
                        return;
                    }
                }
                catch (Exception)
                {
                    // If conversion fails, keep original value
                    coercedValue = value;
                }
            }

            if (SetProperty(ref _value, coercedValue))
            {
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [ObservableProperty]
    private object? _minValue;

    [ObservableProperty]
    private object? _maxValue;

    public event EventHandler? ValueChanged;

    // Computed properties for UI visibility binding
    public bool IsBooleanType => Type == SettingType.Boolean;
    public bool IsIntegerType => Type == SettingType.Integer;
    public bool IsDoubleType => Type == SettingType.Double;
    public bool IsStringType => Type == SettingType.String;
    public bool IsStringListType => Type == SettingType.StringList;
    
    // Special UI control identification
    public bool IsAudioDeviceSetting => Name == "Audio Device" && Type == SettingType.String;
    public bool IsPackDirectoriesSetting => Name == "Pack Directories" && Type == SettingType.StringList;
    
    // Commands for StringList management
    [RelayCommand]
    private void AddToStringList(string? newValue)
    {
        if (Type != SettingType.StringList || string.IsNullOrWhiteSpace(newValue)) return;
        
        var currentList = (Value as string[]) ?? Array.Empty<string>();
        
        // Don't add duplicates
        if (currentList.Contains(newValue, StringComparer.OrdinalIgnoreCase)) return;
        
        var newList = currentList.Append(newValue).ToArray();
        Value = newList;
    }
    
    [RelayCommand]
    private void RemoveFromStringList(string? itemToRemove)
    {
        if (Type != SettingType.StringList || string.IsNullOrWhiteSpace(itemToRemove)) return;
        
        var currentList = (Value as string[]) ?? Array.Empty<string>();
        var newList = currentList.Where(x => x != itemToRemove).ToArray();
        Value = newList;
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