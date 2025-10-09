using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GameWatcher.AuthorStudio.Services;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Settings - TTS API key, audio format, batch preview generation.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly AuthorSettingsService _settingsService;
    private readonly OpenAiTtsService _ttsService;

    [ObservableProperty]
    private string _audioFormat = "mp3";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isTtsConfigured;

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        AuthorSettingsService settingsService,
        OpenAiTtsService ttsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _ttsService = ttsService;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Settings ViewModel");
        
        // Load current settings
        AudioFormat = _settingsService.Settings.AudioFormat ?? "mp3";
        IsTtsConfigured = _ttsService.IsConfigured;
        
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusMessage = "Please enter a valid API key.";
            return;
        }

        try
        {
            Environment.SetEnvironmentVariable("GWS_OPENAI_API_KEY", apiKey, EnvironmentVariableTarget.User);
            _ttsService.ReloadApiKey();
            IsTtsConfigured = _ttsService.IsConfigured;
            StatusMessage = "✓ API key saved successfully";
            _logger.LogInformation("TTS API key saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save API key");
            StatusMessage = $"Failed to save key: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveApiKey()
    {
        try
        {
            Environment.SetEnvironmentVariable("GWS_OPENAI_API_KEY", null, EnvironmentVariableTarget.User);
            _ttsService.ReloadApiKey();
            IsTtsConfigured = _ttsService.IsConfigured;
            StatusMessage = "✓ API key removed";
            _logger.LogInformation("TTS API key removed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove API key");
            StatusMessage = $"Failed to remove key: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UpdateAudioFormat(string format)
    {
        try
        {
            var normalizedFormat = format?.ToLowerInvariant() ?? "mp3";
            if (normalizedFormat != "mp3" && normalizedFormat != "wav")
                normalizedFormat = "mp3";

            AudioFormat = normalizedFormat;
            _settingsService.Settings.AudioFormat = normalizedFormat;
            _settingsService.Save();
            
            StatusMessage = $"✓ Audio format set to {normalizedFormat}";
            _logger.LogInformation("Audio format updated to: {Format}", normalizedFormat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update audio format");
        }
    }

    public void Dispose()
    {
        // Save settings on dispose
        _settingsService.Save();
    }
}
