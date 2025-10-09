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
    private readonly SessionStore _sessionStore;

    [ObservableProperty]
    private ObservableCollection<PendingDialogueEntry> _discoveredDialogue;

    [ObservableProperty]
    private ObservableCollection<PendingDialogueEntry> _acceptedDialogue;

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
        SpeakerStore speakerStore,
        SessionStore sessionStore)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _speakerStore = speakerStore;
        _sessionStore = sessionStore;

        // Wire up service collections to ViewModels
        _discoveredDialogue = _discoveryService.Discovered;
        _acceptedDialogue = new ObservableCollection<PendingDialogueEntry>();
        _logLines = _discoveryService.LogLines;
        
        // Auto-save session when lists change
        _discoveredDialogue.CollectionChanged += (s, e) => _ = AutoSaveSessionAsync();
        _acceptedDialogue.CollectionChanged += (s, e) => _ = AutoSaveSessionAsync();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Discovery ViewModel");
        
        // Update counts
        UniqueLinesFound = DiscoveredDialogue.Count;
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Sets the current pack and loads its associated session data.
    /// Call this when a pack is opened.
    /// </summary>
    public async Task LoadPackSessionAsync(string packPath)
    {
        _sessionStore.SetCurrentPack(packPath);
        
        var (discovered, accepted) = await _sessionStore.LoadSessionAsync();
        
        // Clear current lists
        DiscoveredDialogue.Clear();
        AcceptedDialogue.Clear();
        
        // Load saved entries
        foreach (var entry in discovered)
        {
            DiscoveredDialogue.Add(entry);
        }
        
        foreach (var entry in accepted)
        {
            AcceptedDialogue.Add(entry);
        }
        
        UniqueLinesFound = DiscoveredDialogue.Count;
        
        _logger.LogInformation("Loaded pack session: {DiscoveredCount} discovered, {AcceptedCount} accepted",
            discovered.Count, accepted.Count);
        
        // User feedback in Activity Log
        if (discovered.Count > 0 || accepted.Count > 0)
        {
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ✓ Restored session: {discovered.Count} discovered, {accepted.Count} accepted");
        }
        else
        {
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ℹ️ Starting fresh session (no previous data found)");
        }
    }

    /// <summary>
    /// Clears the current pack context and session data.
    /// Call this when closing a pack or starting a new one.
    /// </summary>
    public async Task ClearPackSessionAsync()
    {
        DiscoveredDialogue.Clear();
        AcceptedDialogue.Clear();
        _sessionStore.SetCurrentPack(null);
        UniqueLinesFound = 0;
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Auto-saves the session in the background (called when lists change).
    /// </summary>
    private async Task AutoSaveSessionAsync()
    {
        try
        {
            await _sessionStore.SaveSessionAsync(DiscoveredDialogue, AcceptedDialogue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-save session");
        }
    }

    /// <summary>
    /// Moves an entry from the Discovered list to the Accepted list.
    /// </summary>
    public void AcceptEntry(PendingDialogueEntry entry)
    {
        if (entry == null) return;
        
        if (DiscoveredDialogue.Contains(entry))
        {
            DiscoveredDialogue.Remove(entry);
            AcceptedDialogue.Add(entry);
            UniqueLinesFound = DiscoveredDialogue.Count;
            _logger.LogInformation("Accepted dialogue: {Text}", entry.Text);
        }
    }

    /// <summary>
    /// Demotes an entry from the Accepted list back to the Discovered list.
    /// </summary>
    public void DemoteEntry(PendingDialogueEntry entry)
    {
        if (entry == null) return;
        
        if (AcceptedDialogue.Contains(entry))
        {
            AcceptedDialogue.Remove(entry);
            DiscoveredDialogue.Add(entry);
            UniqueLinesFound = DiscoveredDialogue.Count;
            _logger.LogInformation("Demoted entry back to discovery: {Text}", entry.Text);
        }
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
            
            // User feedback in Activity Log
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ✓ Discovery started - watching for dialogue...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start discovery");
            SessionStatus = $"Error: {ex.Message}";
            
            // User feedback in Activity Log
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ❌ Failed to start discovery: {ex.Message}");
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
            
            // User feedback in Activity Log
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ⏸️ Discovery paused");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause discovery");
            
            // User feedback in Activity Log
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ⚠️ Failed to pause: {ex.Message}");
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
            
            // User feedback in Activity Log
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ⏹️ Discovery stopped - captured {DiscoveredDialogue.Count} unique lines");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop discovery");
            
            // User feedback in Activity Log
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] ❌ Failed to stop: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _discoveryService?.Dispose();
    }
}
