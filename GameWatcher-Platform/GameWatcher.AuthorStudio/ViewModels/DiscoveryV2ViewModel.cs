using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Linq;
using GameWatcher.AuthorStudio.Services;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Enhanced Discovery view with List + Details pane design.
/// Supports inline text editing, OCR debug image viewing, and smart OCR fix creation.
/// </summary>
public partial class DiscoveryV2ViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<DiscoveryV2ViewModel> _logger;
    private readonly DiscoveryService _discoveryService;
    private readonly SpeakerStore _speakerStore;
    private readonly SessionStore _sessionStore;
    private readonly OcrFixesStore _ocrFixesStore;
    private readonly PackBuilderViewModel _packBuilder;

    [ObservableProperty]
    private ObservableCollection<PendingDialogueEntry> _discoveredDialogue;

    [ObservableProperty]
    private ObservableCollection<PendingDialogueEntry> _acceptedDialogue;

    [ObservableProperty]
    private ObservableCollection<string> _logLines;

    [ObservableProperty]
    private PendingDialogueEntry? _selectedDialogue;

    [ObservableProperty]
    private string _ocrFixFrom = string.Empty;

    [ObservableProperty]
    private string _ocrFixTo = string.Empty;

    [ObservableProperty]
    private string? _ocrFixStatusMessage;

    [ObservableProperty]
    private bool _isExistingOcrFix;

    [ObservableProperty]
    private List<(string from, string to)> _detectedOcrFixes = new();

    [ObservableProperty]
    private int _currentFixIndex = 0;

    [ObservableProperty]
    private string? _multipleFixesMessage;

    [ObservableProperty]
    private string _sessionStatus = "Stopped";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _uniqueLinesFound;

    public DiscoveryV2ViewModel(
        ILogger<DiscoveryV2ViewModel> logger,
        DiscoveryService discoveryService,
        SpeakerStore speakerStore,
        SessionStore sessionStore,
        OcrFixesStore ocrFixesStore,
        PackBuilderViewModel packBuilder)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _speakerStore = speakerStore;
        _sessionStore = sessionStore;
        _ocrFixesStore = ocrFixesStore;
        _packBuilder = packBuilder;

        // Wire up service collections to ViewModels
        _discoveredDialogue = _discoveryService.Discovered;
        _acceptedDialogue = new ObservableCollection<PendingDialogueEntry>();

        _logLines = _discoveryService.LogLines;

        _discoveredDialogue.CollectionChanged += (_, _) => UpdateUniqueLinesFound();
    }

    partial void OnSelectedDialogueChanged(PendingDialogueEntry? value)
    {
        if (value == null)
        {
            ClearOcrFix();
            return;
        }

        // Check if there's an existing OCR fix for this dialogue
        CheckForExistingOcrFix();
    }

    partial void OnOcrFixFromChanged(string value)
    {
        UpdateOcrFixStatus();
    }

    partial void OnOcrFixToChanged(string value)
    {
        UpdateOcrFixStatus();
    }

    [RelayCommand]
    private async Task StartDiscoveryAsync()
    {
        if (IsRunning) return;

        try
        {
            await _discoveryService.StartAsync();
            IsRunning = true;
            SessionStatus = "Running";
            _logger.LogInformation("Discovery V2 started");
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
        if (!IsRunning) return;

        try
        {
            await _discoveryService.PauseAsync();
            IsRunning = false;
            SessionStatus = "Paused";
            _logger.LogInformation("Discovery V2 paused");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause discovery");
        }
    }

    [RelayCommand]
    private async Task StopDiscoveryAsync()
    {
        if (!IsRunning) return;

        try
        {
            await _discoveryService.StopAsync();
            IsRunning = false;
            SessionStatus = "Stopped";
            _logger.LogInformation("Discovery V2 stopped");

            // Save session
            await SaveSessionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop discovery");
        }
    }

    [RelayCommand]
    private async Task SaveOcrFixAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrFixFrom) || string.IsNullOrWhiteSpace(OcrFixTo))
        {
            OcrFixStatusMessage = "⚠️ Both From and To fields must be filled";
            return;
        }

        try
        {
            // Add to store and save
            await _ocrFixesStore.AddFixAsync(OcrFixFrom, OcrFixTo);
            await _ocrFixesStore.SaveAsync();
            
            OcrFixStatusMessage = $"✅ Saved: \"{OcrFixFrom}\" → \"{OcrFixTo}\"";
            
            // Update status to reflect it's now an existing fix
            UpdateOcrFixStatus();
            
            _logger.LogInformation("Saved OCR fix: '{From}' → '{To}'", OcrFixFrom, OcrFixTo);
        }
        catch (Exception ex)
        {
            OcrFixStatusMessage = $"❌ Error: {ex.Message}";
            _logger.LogError(ex, "Failed to save OCR fix");
        }
    }

    [RelayCommand]
    private async Task RemoveOcrFixAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrFixFrom))
        {
            OcrFixStatusMessage = "⚠️ Please specify which OCR fix to remove";
            return;
        }

        try
        {
            // Remove from store
            var removed = _ocrFixesStore.RemoveFix(OcrFixFrom);
            
            if (removed)
            {
                await _ocrFixesStore.SaveAsync();
                OcrFixStatusMessage = $"✅ Removed OCR fix: \"{OcrFixFrom}\"";
                IsExistingOcrFix = false;
                _logger.LogInformation("Removed OCR fix: '{From}'", OcrFixFrom);
            }
            else
            {
                OcrFixStatusMessage = $"⚠️ OCR fix not found: \"{OcrFixFrom}\"";
            }
        }
        catch (Exception ex)
        {
            OcrFixStatusMessage = $"❌ Error: {ex.Message}";
            _logger.LogError(ex, "Failed to remove OCR fix");
        }
    }

    private void ClearOcrFix()
    {
        OcrFixFrom = string.Empty;
        OcrFixTo = string.Empty;
        OcrFixStatusMessage = null;
        IsExistingOcrFix = false;
        DetectedOcrFixes = new();
        CurrentFixIndex = 0;
        MultipleFixesMessage = null;
    }

    [RelayCommand]
    private void NextOcrFix()
    {
        if (DetectedOcrFixes.Count == 0) return;
        
        CurrentFixIndex = (CurrentFixIndex + 1) % DetectedOcrFixes.Count;
        LoadCurrentFix();
    }

    [RelayCommand]
    private void PreviousOcrFix()
    {
        if (DetectedOcrFixes.Count == 0) return;
        
        CurrentFixIndex = (CurrentFixIndex - 1 + DetectedOcrFixes.Count) % DetectedOcrFixes.Count;
        LoadCurrentFix();
    }

    private void LoadCurrentFix()
    {
        if (CurrentFixIndex >= 0 && CurrentFixIndex < DetectedOcrFixes.Count)
        {
            var (from, to) = DetectedOcrFixes[CurrentFixIndex];
            OcrFixFrom = from;
            OcrFixTo = to;
            
            MultipleFixesMessage = $"📋 {DetectedOcrFixes.Count} potential fixes detected (showing {CurrentFixIndex + 1} of {DetectedOcrFixes.Count})";
            
            UpdateOcrFixStatus();
        }
    }

    private void CheckForExistingOcrFix()
    {
        if (SelectedDialogue == null) return;

        // Check if there's an existing fix by comparing OriginalOcrText with CorrectedText
        var original = SelectedDialogue.OriginalOcrText ?? string.Empty;
        var corrected = SelectedDialogue.CorrectedText ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(original) && !string.IsNullOrWhiteSpace(corrected) && 
            !string.Equals(original, corrected, StringComparison.Ordinal))
        {
            // Detect ALL word differences
            DetectedOcrFixes = DetectWordDifferences(original, corrected);
            
            if (DetectedOcrFixes.Count > 0)
            {
                // Show the first fix by default
                CurrentFixIndex = 0;
                var (from, to) = DetectedOcrFixes[0];
                OcrFixFrom = from;
                OcrFixTo = to;
                
                // Update multiple fixes message
                if (DetectedOcrFixes.Count > 1)
                {
                    MultipleFixesMessage = $"📋 {DetectedOcrFixes.Count} potential fixes detected (showing 1 of {DetectedOcrFixes.Count})";
                }
                else
                {
                    MultipleFixesMessage = null;
                }
                
                // Check if this fix already exists
                var allFixes = _ocrFixesStore.GetAll();
                IsExistingOcrFix = allFixes.ContainsKey(from);
                
                if (IsExistingOcrFix)
                {
                    OcrFixStatusMessage = "ℹ️ This OCR fix already exists";
                }
                else
                {
                    OcrFixStatusMessage = "💡 Detected potential OCR fix - click Save to apply";
                }
            }
        }
        else
        {
            ClearOcrFix();
        }
    }

    private void UpdateOcrFixStatus()
    {
        if (string.IsNullOrWhiteSpace(OcrFixFrom) || string.IsNullOrWhiteSpace(OcrFixTo))
        {
            OcrFixStatusMessage = null;
            IsExistingOcrFix = false;
            return;
        }

        // Check if this fix already exists
        var allFixes = _ocrFixesStore.GetAll();
        IsExistingOcrFix = allFixes.ContainsKey(OcrFixFrom);
        
        if (IsExistingOcrFix)
        {
            var existingTo = allFixes[OcrFixFrom];
            if (existingTo == OcrFixTo)
            {
                OcrFixStatusMessage = "ℹ️ This exact rule already exists";
            }
            else
            {
                OcrFixStatusMessage = $"⚠️ Rule exists with different target: \"{existingTo}\" (will be overwritten)";
            }
        }
        else
        {
            OcrFixStatusMessage = "💾 Ready to save new OCR fix rule";
        }
    }

    [RelayCommand]
    private void AcceptDialogue()
    {
        if (SelectedDialogue == null) return;

        AcceptEntry(SelectedDialogue);
    }

    [RelayCommand]
    private void DeleteDialogue()
    {
        if (SelectedDialogue == null) return;

        DeleteDiscoveryEntry(SelectedDialogue);
    }

    public void AcceptEntry(PendingDialogueEntry entry)
    {
        if (DiscoveredDialogue.Contains(entry))
        {
            DiscoveredDialogue.Remove(entry);
            entry.Approved = true;
            AcceptedDialogue.Add(entry);
            _logger.LogInformation("Accepted dialogue entry: {Text}", entry.Text.Substring(0, Math.Min(50, entry.Text.Length)));
            _ = SaveSessionAsync();
        }
    }

    public void DemoteEntry(PendingDialogueEntry entry)
    {
        if (AcceptedDialogue.Contains(entry))
        {
            AcceptedDialogue.Remove(entry);
            entry.Approved = false;
            DiscoveredDialogue.Add(entry);
            _logger.LogInformation("Demoted dialogue entry back to discovery: {Text}", entry.Text.Substring(0, Math.Min(50, entry.Text.Length)));
            _ = SaveSessionAsync();
        }
    }

    public void DeleteDiscoveryEntry(PendingDialogueEntry entry)
    {
        if (DiscoveredDialogue.Contains(entry))
        {
            DiscoveredDialogue.Remove(entry);
            _logger.LogInformation("Deleted discovery entry: {Text}", entry.Text);
            _ = SaveSessionAsync();
        }
    }

    private void UpdateUniqueLinesFound()
    {
        UniqueLinesFound = DiscoveredDialogue.Count;
    }

    private async Task SaveSessionAsync()
    {
        try
        {
            await _sessionStore.SaveSessionAsync(DiscoveredDialogue.ToList(), AcceptedDialogue.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session");
        }
    }

    /// <summary>
    /// Detects word-level differences between original and corrected text.
    /// Returns list of (from, to) tuples for potential OCR fix rules.
    /// </summary>
    private List<(string from, string to)> DetectWordDifferences(string original, string corrected)
    {
        var differences = new List<(string, string)>();

        // Simple word-by-word comparison
        var originalWords = original.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var correctedWords = corrected.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // Find mismatched words at same positions
        var minLength = Math.Min(originalWords.Length, correctedWords.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (!string.Equals(originalWords[i], correctedWords[i], StringComparison.Ordinal))
            {
                // Strip punctuation for comparison
                var origClean = new string(originalWords[i].Where(char.IsLetterOrDigit).ToArray());
                var corrClean = new string(correctedWords[i].Where(char.IsLetterOrDigit).ToArray());

                if (!string.IsNullOrEmpty(origClean) && !string.IsNullOrEmpty(corrClean))
                {
                    differences.Add((origClean, corrClean));
                }
            }
        }

        return differences;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
