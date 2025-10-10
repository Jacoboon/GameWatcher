using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameWatcher.AuthorStudio.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace GameWatcher.AuthorStudio.ViewModels;

public partial class OverridesViewModel : ObservableObject
{
    private readonly ILogger<OverridesViewModel> _logger;
    private string? _packDirectory;

    [ObservableProperty]
    private string _description = string.Empty;

    // Capture Settings
    [ObservableProperty]
    private bool _overrideCapture;

    [ObservableProperty]
    private int _captureTargetFps = 15;

    [ObservableProperty]
    private double _captureConfidenceThreshold = 0.85;

    [ObservableProperty]
    private bool _captureEnableOptimization = true;

    [ObservableProperty]
    private bool _captureEnableDuplicateDetection = true;

    // Audio Settings
    [ObservableProperty]
    private bool _overrideAudio;

    [ObservableProperty]
    private int _audioMasterVolume = 80;

    [ObservableProperty]
    private bool _audioEnableCrossfade = true;

    [ObservableProperty]
    private double _audioPlaybackSpeed = 1.0;

    [ObservableProperty]
    private bool _audioEnableCaching = true;

    // OCR Settings
    [ObservableProperty]
    private bool _overrideOcr;

    [ObservableProperty]
    private double _ocrConfidenceThreshold = 0.7;

    [ObservableProperty]
    private bool _ocrEnablePreprocessing = true;

    [ObservableProperty]
    private double _ocrScaleFactor = 2.0;

    [ObservableProperty]
    private bool _ocrConvertToGrayscale = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public OverridesViewModel(ILogger<OverridesViewModel> logger)
    {
        _logger = logger;
    }

    public void SetPackDirectory(string packDirectory)
    {
        _packDirectory = packDirectory;
        LoadOverrides();
    }

    private void LoadOverrides()
    {
        if (string.IsNullOrEmpty(_packDirectory)) return;

        try
        {
            var overridesPath = Path.Combine(_packDirectory, "Configuration", "player-overrides.json");
            
            if (!File.Exists(overridesPath))
            {
                StatusMessage = "No player-overrides.json found. Configure and save to create.";
                return;
            }

            var json = File.ReadAllText(overridesPath);
            var overrides = JsonSerializer.Deserialize<PackSettingsOverrides>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (overrides == null) return;

            Description = overrides.Description;

            // Load Capture overrides
            if (overrides.Overrides.Capture != null)
            {
                OverrideCapture = true;
                CaptureTargetFps = overrides.Overrides.Capture.TargetFps ?? 15;
                CaptureConfidenceThreshold = overrides.Overrides.Capture.ConfidenceThreshold ?? 0.85;
                CaptureEnableOptimization = overrides.Overrides.Capture.EnableOptimization ?? true;
                CaptureEnableDuplicateDetection = overrides.Overrides.Capture.EnableDuplicateDetection ?? true;
            }

            // Load Audio overrides
            if (overrides.Overrides.Audio != null)
            {
                OverrideAudio = true;
                AudioMasterVolume = overrides.Overrides.Audio.MasterVolume ?? 80;
                AudioEnableCrossfade = overrides.Overrides.Audio.EnableCrossfade ?? true;
                AudioPlaybackSpeed = overrides.Overrides.Audio.PlaybackSpeed ?? 1.0;
                AudioEnableCaching = overrides.Overrides.Audio.EnableCaching ?? true;
            }

            // Load OCR overrides
            if (overrides.Overrides.Ocr != null)
            {
                OverrideOcr = true;
                OcrConfidenceThreshold = overrides.Overrides.Ocr.ConfidenceThreshold ?? 0.7;
                OcrEnablePreprocessing = overrides.Overrides.Ocr.EnablePreprocessing ?? true;
                OcrScaleFactor = overrides.Overrides.Ocr.ScaleFactor ?? 2.0;
                OcrConvertToGrayscale = overrides.Overrides.Ocr.ConvertToGrayscale ?? true;
            }

            StatusMessage = "Player overrides loaded successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load player overrides");
            StatusMessage = $"Failed to load overrides: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveOverrides()
    {
        if (string.IsNullOrEmpty(_packDirectory))
        {
            StatusMessage = "No pack loaded. Cannot save overrides.";
            return;
        }

        try
        {
            var overrides = new PackSettingsOverrides
            {
                Version = "1.0",
                Description = Description,
                Overrides = new SettingsOverrides()
            };

            // Only include sections that are enabled
            if (OverrideCapture)
            {
                overrides.Overrides.Capture = new CaptureOverrides
                {
                    TargetFps = CaptureTargetFps,
                    ConfidenceThreshold = CaptureConfidenceThreshold,
                    EnableOptimization = CaptureEnableOptimization,
                    EnableDuplicateDetection = CaptureEnableDuplicateDetection
                };
            }

            if (OverrideAudio)
            {
                overrides.Overrides.Audio = new AudioOverrides
                {
                    MasterVolume = AudioMasterVolume,
                    EnableCrossfade = AudioEnableCrossfade,
                    PlaybackSpeed = AudioPlaybackSpeed,
                    EnableCaching = AudioEnableCaching
                };
            }

            if (OverrideOcr)
            {
                overrides.Overrides.Ocr = new OcrOverrides
                {
                    ConfidenceThreshold = OcrConfidenceThreshold,
                    EnablePreprocessing = OcrEnablePreprocessing,
                    ScaleFactor = OcrScaleFactor,
                    ConvertToGrayscale = OcrConvertToGrayscale
                };
            }

            var configDir = Path.Combine(_packDirectory, "Configuration");
            Directory.CreateDirectory(configDir);

            var overridesPath = Path.Combine(configDir, "player-overrides.json");
            var json = JsonSerializer.Serialize(overrides, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            File.WriteAllText(overridesPath, json);
            StatusMessage = $"✓ Saved to {Path.GetFileName(overridesPath)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save player overrides");
            StatusMessage = $"Failed to save: {ex.Message}";
            MessageBox.Show($"Failed to save player overrides: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void PreviewJson()
    {
        var overrides = new PackSettingsOverrides
        {
            Version = "1.0",
            Description = Description,
            Overrides = new SettingsOverrides()
        };

        if (OverrideCapture)
        {
            overrides.Overrides.Capture = new CaptureOverrides
            {
                TargetFps = CaptureTargetFps,
                ConfidenceThreshold = CaptureConfidenceThreshold,
                EnableOptimization = CaptureEnableOptimization,
                EnableDuplicateDetection = CaptureEnableDuplicateDetection
            };
        }

        if (OverrideAudio)
        {
            overrides.Overrides.Audio = new AudioOverrides
            {
                MasterVolume = AudioMasterVolume,
                EnableCrossfade = AudioEnableCrossfade,
                PlaybackSpeed = AudioPlaybackSpeed,
                EnableCaching = AudioEnableCaching
            };
        }

        if (OverrideOcr)
        {
            overrides.Overrides.Ocr = new OcrOverrides
            {
                ConfidenceThreshold = OcrConfidenceThreshold,
                EnablePreprocessing = OcrEnablePreprocessing,
                ScaleFactor = OcrScaleFactor,
                ConvertToGrayscale = OcrConvertToGrayscale
            };
        }

        var json = JsonSerializer.Serialize(overrides, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        MessageBox.Show(json, "JSON Preview", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ClearOverrides()
    {
        var result = MessageBox.Show(
            "This will reset all override settings to defaults. Continue?",
            "Clear Overrides",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Description = string.Empty;
            
            OverrideCapture = false;
            CaptureTargetFps = 15;
            CaptureConfidenceThreshold = 0.85;
            CaptureEnableOptimization = true;
            CaptureEnableDuplicateDetection = true;

            OverrideAudio = false;
            AudioMasterVolume = 80;
            AudioEnableCrossfade = true;
            AudioPlaybackSpeed = 1.0;
            AudioEnableCaching = true;

            OverrideOcr = false;
            OcrConfidenceThreshold = 0.7;
            OcrEnablePreprocessing = true;
            OcrScaleFactor = 2.0;
            OcrConvertToGrayscale = true;

            StatusMessage = "Overrides cleared. Click Save to apply.";
        }
    }
}
