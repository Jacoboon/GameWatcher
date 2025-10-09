using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.Models;
using System.Collections.ObjectModel;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Settings - TTS API key, audio format, batch preview generation.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly AuthorSettingsService _settingsService;
    private readonly OpenAiTtsService _ttsService;
    private readonly OcrFixesStore _ocrFixesStore;

    [ObservableProperty]
    private string _audioFormat = "mp3";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isTtsConfigured;

    [ObservableProperty]
    private ObservableCollection<OcrFixEntry> _ocrFixes = new();

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        AuthorSettingsService settingsService,
        OpenAiTtsService ttsService,
        OcrFixesStore ocrFixesStore)
    {
        _logger = logger;
        _settingsService = settingsService;
        _ttsService = ttsService;
        _ocrFixesStore = ocrFixesStore;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Settings ViewModel");
        
        // Load current settings
        AudioFormat = _settingsService.Settings.AudioFormat ?? "mp3";
        IsTtsConfigured = _ttsService.IsConfigured;
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Loads OCR fixes from OcrFixesStore into the editable collection.
    /// Call this when a pack is opened or when refreshing the list.
    /// </summary>
    public void LoadOcrFixes()
    {
        try
        {
            OcrFixes.Clear();
            
            var fixes = _ocrFixesStore.GetAll();
            foreach (var fix in fixes.OrderBy(f => f.Key))
            {
                OcrFixes.Add(new OcrFixEntry(fix.Key, fix.Value));
            }
            
            StatusMessage = $"✓ Loaded {OcrFixes.Count} OCR fixes";
            _logger.LogInformation("Loaded {Count} OCR fixes into Settings view", OcrFixes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load OCR fixes");
            StatusMessage = $"⚠️ Failed to load OCR fixes: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddOcrFixAsync()
    {
        try
        {
            // Add a blank entry that user can edit
            var newEntry = new OcrFixEntry("", "");
            OcrFixes.Add(newEntry);
            
            StatusMessage = "➕ New OCR fix added - edit the From and To fields";
            _logger.LogInformation("Added new blank OCR fix entry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add OCR fix");
            StatusMessage = $"⚠️ Failed to add fix: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteOcrFixAsync(OcrFixEntry entry)
    {
        try
        {
            if (entry == null) return;

            OcrFixes.Remove(entry);
            await SaveOcrFixesAsync();
            
            StatusMessage = $"✓ Deleted OCR fix: '{entry.From}' → '{entry.To}'";
            _logger.LogInformation("Deleted OCR fix: '{From}' -> '{To}'", entry.From, entry.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete OCR fix");
            StatusMessage = $"⚠️ Failed to delete fix: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveOcrFixesAsync()
    {
        try
        {
            // Remove any empty entries
            var emptyEntries = OcrFixes.Where(f => string.IsNullOrWhiteSpace(f.From) || string.IsNullOrWhiteSpace(f.To)).ToList();
            foreach (var empty in emptyEntries)
            {
                OcrFixes.Remove(empty);
            }

            // Update OcrFixesStore with the current collection
            var fixes = OcrFixes.Select(f => new KeyValuePair<string, string>(f.From, f.To));
            _ocrFixesStore.SetAll(fixes);
            
            // Save to file
            await _ocrFixesStore.SaveAsync();
            
            StatusMessage = $"✓ Saved {OcrFixes.Count} OCR fixes";
            _logger.LogInformation("Saved {Count} OCR fixes", OcrFixes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OCR fixes");
            StatusMessage = $"⚠️ Failed to save fixes: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshOcrFixesAsync()
    {
        try
        {
            LoadOcrFixes();
            StatusMessage = "✓ OCR fixes refreshed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh OCR fixes");
            StatusMessage = $"⚠️ Failed to refresh: {ex.Message}";
        }

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
