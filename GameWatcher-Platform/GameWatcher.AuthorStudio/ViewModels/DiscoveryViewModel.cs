using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.Models;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Manages discovery sessions - capturing dialogue while playing a game.
/// </summary>
public partial class DiscoveryViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<DiscoveryViewModel> _logger;
    private readonly DiscoveryService _discoveryService;
    private readonly SpeakerStore _speakerStore;

    [ObservableProperty]
    private ObservableCollection<PendingDialogueEntry> _discoveredDialogue;

    [ObservableProperty]
    private ObservableCollection<string> _logLines;

    [ObservableProperty]
    private string _sessionStatus = "Stopped";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _uniqueLinesFound;

    public DiscoveryViewModel(
        ILogger<DiscoveryViewModel> logger,
        DiscoveryService discoveryService,
        SpeakerStore speakerStore)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _speakerStore = speakerStore;

        // Wire up service collections to ViewModels
        _discoveredDialogue = _discoveryService.Discovered;
        _logLines = _discoveryService.LogLines;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Discovery ViewModel");
        
        // Update counts
        UniqueLinesFound = DiscoveredDialogue.Count;
        
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task StartDiscoveryAsync()
    {
        try
        {
            _logger.LogInformation("Starting discovery session");
            await _discoveryService.StartAsync();
            IsRunning = true;
            SessionStatus = "Running";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start discovery");
            SessionStatus = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PauseDiscoveryAsync()
    {
        try
        {
            _logger.LogInformation("Pausing discovery session");
            await _discoveryService.PauseAsync();
            SessionStatus = "Paused";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause discovery");
        }
    }

    [RelayCommand]
    private async Task StopDiscoveryAsync()
    {
        try
        {
            _logger.LogInformation("Stopping discovery session");
            await _discoveryService.StopAsync();
            IsRunning = false;
            SessionStatus = "Stopped";
            UniqueLinesFound = DiscoveredDialogue.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop discovery");
        }
    }

    public void Dispose()
    {
        _discoveryService?.Dispose();
    }
}
