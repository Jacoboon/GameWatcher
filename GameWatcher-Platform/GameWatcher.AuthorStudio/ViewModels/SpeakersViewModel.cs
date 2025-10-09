using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.Models;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Manages speaker profiles - voice selection, speed, effects.
/// </summary>
public partial class SpeakersViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<SpeakersViewModel> _logger;
    private readonly SpeakerStore _speakerStore;
    private readonly OpenAiTtsService _ttsService;
    private readonly AudioPlaybackService _audioService;

    [ObservableProperty]
    private ObservableCollection<SpeakerProfile> _speakers;

    [ObservableProperty]
    private SpeakerProfile? _selectedSpeaker;

    public SpeakersViewModel(
        ILogger<SpeakersViewModel> logger,
        SpeakerStore speakerStore,
        OpenAiTtsService ttsService,
        AudioPlaybackService audioService)
    {
        _logger = logger;
        _speakerStore = speakerStore;
        _ttsService = ttsService;
        _audioService = audioService;

        // Wire up service collection
        _speakers = _speakerStore.Speakers;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Speakers ViewModel");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void AddSpeaker()
    {
        var newSpeaker = new SpeakerProfile
        {
            Id = $"speaker_{Speakers.Count + 1}",
            Name = $"New Speaker {Speakers.Count + 1}",
            Voice = "alloy",
            Speed = 1.0
        };
        
        Speakers.Add(newSpeaker);
        SelectedSpeaker = newSpeaker;
        _logger.LogInformation("Added new speaker: {Id}", newSpeaker.Id);
    }

    [RelayCommand]
    private async Task ImportSpeakersAsync()
    {
        try
        {
            // Will be called from view with file path
            _logger.LogInformation("Import speakers requested - needs file dialog implementation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import speakers");
        }
    }

    // Called from View with file path
    public async Task ImportSpeakersFromFileAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Importing speakers from: {FilePath}", filePath);
            await _speakerStore.ImportAsync(filePath);
            _logger.LogInformation("Speakers imported successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import speakers");
        }
    }

    [RelayCommand]
    private async Task ExportSpeakersAsync()
    {
        try
        {
            _logger.LogInformation("Export speakers requested - needs file dialog implementation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export speakers");
        }
    }

    // Called from View with file path
    public async Task ExportSpeakersToFileAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Exporting speakers to: {FilePath}", filePath);
            await _speakerStore.ExportAsync(filePath);
            _logger.LogInformation("Speakers exported successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export speakers");
        }
    }

    [RelayCommand]
    private async Task PreviewSpeakerAsync(SpeakerProfile speaker)
    {
        if (speaker == null) return;

        try
        {
            _logger.LogInformation("Previewing speaker: {Speaker}", speaker.Name);
            
            // Generate preview using VoicePreviewStore
            var voice = string.IsNullOrWhiteSpace(speaker.Voice) ? "alloy" : speaker.Voice;
            var speed = speaker.Speed <= 0 ? 1.0 : speaker.Speed;
            
            // Use engine-level preview path
            var previewPath = GameWatcher.Engine.Audio.VoicePreviewStore.GetPreviewPath(voice, speed, "mp3");
            
            if (!File.Exists(previewPath))
            {
                // Generate if not exists
                var sampleText = $"Hi! I'm {speaker.Name ?? voice}. Calm. Excited! Curious? Let's begin.";
                await _ttsService.GenerateAsync(sampleText, voice, speed, "mp3", previewPath);
            }

            _audioService.Play(previewPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview speaker");
        }
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }
}
