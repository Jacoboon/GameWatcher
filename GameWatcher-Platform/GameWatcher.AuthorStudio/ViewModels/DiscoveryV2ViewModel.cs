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

    /// <summary>
    /// Returns true if the selected dialogue is in the Accepted list
    /// </summary>
    public bool IsSelectedDialogueAccepted => SelectedDialogue != null && AcceptedDialogue.Contains(SelectedDialogue);

    public DiscoveryV2ViewModel(
        ILogger<DiscoveryV2ViewModel> logger,
        DiscoveryService discoveryService,
        SpeakerStore speakerStore,
        SessionStore sessionStore,
        OcrFixesStore ocrFixesStore)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _speakerStore = speakerStore;
        _sessionStore = sessionStore;
        _ocrFixesStore = ocrFixesStore;

        // Wire up service collections to ViewModels
        _discoveredDialogue = _discoveryService.Discovered;
        _acceptedDialogue = new ObservableCollection<PendingDialogueEntry>();

        _logLines = _discoveryService.LogLines;

        _discoveredDialogue.CollectionChanged += (_, _) => UpdateUniqueLinesFound();
        
        // Wire up duplicate detection callback so DiscoveryService can check Accepted list
        _discoveryService.IsAlreadyAccepted = (originalOcrText) =>
        {
            return AcceptedDialogue.Any(entry => 
                string.Equals(entry.OriginalOcrText, originalOcrText, StringComparison.Ordinal));
        };
    }

    partial void OnSelectedDialogueChanged(PendingDialogueEntry? value)
    {
        if (value == null)
        {
            ClearOcrFix();
            OnPropertyChanged(nameof(IsSelectedDialogueAccepted));
            return;
        }

        // Check if there's an existing OCR fix for this dialogue
        CheckForExistingOcrFix();
        OnPropertyChanged(nameof(IsSelectedDialogueAccepted));
        
        // Notify that HasOcrErrors may have changed (needed for XAML binding)
        OnPropertyChanged(nameof(SelectedDialogue));
    }

    /// <summary>
    /// Called when the user edits dialogue text. Re-checks for OCR fixes.
    /// </summary>
    public void OnDialogueTextChanged()
    {
        if (SelectedDialogue != null)
        {
            CheckForExistingOcrFix();
        }
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

    /// <summary>
    /// Loads a previous session for the given pack path.
    /// Called by PackBuilderViewModel when opening a pack.
    /// </summary>
    public async Task LoadPackSessionAsync(string packPath)
    {
        _sessionStore.SetCurrentPack(packPath);
        
        var entries = await _sessionStore.LoadSessionAsync();
        
        // Clear current lists
        DiscoveredDialogue.Clear();
        AcceptedDialogue.Clear();
        
        // Split entries by Approved flag
        var discovered = entries.Where(e => !e.Approved).ToList();
        var accepted = entries.Where(e => e.Approved).ToList();
        
        // Load discovered entries - add directly (Text already has fixes applied from previous session)
        foreach (var entry in discovered)
        {
            DiscoveredDialogue.Add(entry);
        }
        
        // Load accepted entries - add directly (Text already has fixes applied from previous session)
        foreach (var entry in accepted)
        {
            AcceptedDialogue.Add(entry);
        }
        
        _logger.LogInformation("Loaded pack session: {DiscoveredCount} discovered, {AcceptedCount} accepted",
            discovered.Count, accepted.Count);
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

        var original = SelectedDialogue.OriginalOcrText ?? string.Empty;
        var current = SelectedDialogue.Text ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(original))
        {
            ClearOcrFix();
            return;
        }

        var allFixes = _ocrFixesStore.GetAll();
        
        // First check if ANY words in the original text have existing OCR fixes
        var appliedFixes = new List<(string from, string to)>();
        
        // Check for multi-word patterns FIRST (patterns containing spaces)
        foreach (var fix in allFixes.Where(f => f.Key.Contains(' ')))
        {
            if (original.Contains(fix.Key, StringComparison.OrdinalIgnoreCase))
            {
                appliedFixes.Add((fix.Key, fix.Value));
            }
        }
        
        // Then check for single-word patterns
        var originalWords = original.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in originalWords)
        {
            var cleanWord = new string(word.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(cleanWord)) continue;
            
            // Check case-insensitive match (skip multi-word patterns, already checked)
            var matchingFix = allFixes
                .Where(f => !f.Key.Contains(' '))
                .FirstOrDefault(kvp => 
                    string.Equals(kvp.Key, cleanWord, StringComparison.OrdinalIgnoreCase));
            
            if (!string.IsNullOrEmpty(matchingFix.Key))
            {
                appliedFixes.Add((matchingFix.Key, matchingFix.Value));
            }
        }
        
        // If we found existing fixes that were applied, show them
        if (appliedFixes.Count > 0)
        {
            DetectedOcrFixes = appliedFixes;
            CurrentFixIndex = 0;
            
            var (from, to) = appliedFixes[0];
            OcrFixFrom = from;
            OcrFixTo = to;
            IsExistingOcrFix = true;
            
            if (appliedFixes.Count > 1)
            {
                MultipleFixesMessage = $"📋 {appliedFixes.Count} existing fixes applied (showing 1 of {appliedFixes.Count})";
            }
            else
            {
                MultipleFixesMessage = null;
            }
            
            OcrFixStatusMessage = "✅ This OCR fix was already applied to this line";
            return;
        }
        
        // No existing fixes found - check if user edited the text to create NEW fixes
        if (!string.Equals(original, current, StringComparison.Ordinal))
        {
            // Detect ALL word differences for potential new rules
            DetectedOcrFixes = DetectWordDifferences(original, current);
            
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
                IsExistingOcrFix = allFixes.Any(kvp => 
                    string.Equals(kvp.Key, from, StringComparison.OrdinalIgnoreCase));
                
                if (IsExistingOcrFix)
                {
                    OcrFixStatusMessage = "ℹ️ This OCR fix already exists";
                }
                else
                {
                    OcrFixStatusMessage = "💡 Detected potential OCR fix - click Save to apply";
                }
                return;
            }
        }
        
        // No fixes and no edits
        ClearOcrFix();
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
    private void UnacceptDialogue()
    {
        if (SelectedDialogue == null) return;

        DemoteEntry(SelectedDialogue);
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
            // Combine both lists into single collection for persistence
            var allEntries = DiscoveredDialogue.Concat(AcceptedDialogue).ToList();
            await _sessionStore.SaveSessionAsync(allEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session");
        }
    }

    /// <summary>
    /// Detects differences between original and corrected text.
    /// Uses a simple approach: find contiguous sequences of words that differ.
    /// Returns list of (from, to) tuples for potential OCR fix rules.
    /// </summary>
    private List<(string from, string to)> DetectWordDifferences(string original, string corrected)
    {
        var differences = new List<(string, string)>();

        // Split into words
        var originalWords = original.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var correctedWords = corrected.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        int i = 0, j = 0;
        while (i < originalWords.Length && j < correctedWords.Length)
        {
            if (string.Equals(originalWords[i], correctedWords[j], StringComparison.Ordinal))
            {
                // Words match, move forward
                i++;
                j++;
            }
            else
            {
                // Found a difference - collect consecutive mismatched words
                var origSequence = new List<string>();
                var corrSequence = new List<string>();
                
                int startI = i, startJ = j;
                
                // Scan ahead to find where they sync up again
                while (i < originalWords.Length || j < correctedWords.Length)
                {
                    bool foundSync = false;
                    
                    // Try to find a matching word ahead
                    for (int lookAhead = 1; lookAhead <= Math.Min(5, Math.Max(originalWords.Length - i, correctedWords.Length - j)); lookAhead++)
                    {
                        if (i + lookAhead < originalWords.Length && j + lookAhead < correctedWords.Length &&
                            string.Equals(originalWords[i + lookAhead], correctedWords[j + lookAhead], StringComparison.Ordinal))
                        {
                            // Found sync point
                            for (int k = 0; k < lookAhead; k++)
                            {
                                if (i < originalWords.Length) origSequence.Add(originalWords[i++]);
                                if (j < correctedWords.Length) corrSequence.Add(correctedWords[j++]);
                            }
                            foundSync = true;
                            break;
                        }
                    }
                    
                    if (foundSync) break;
                    
                    // No sync found nearby, just take one word from each
                    if (i < originalWords.Length) origSequence.Add(originalWords[i++]);
                    if (j < correctedWords.Length) corrSequence.Add(correctedWords[j++]);
                    
                    if (i >= originalWords.Length && j >= correctedWords.Length) break;
                }
                
                if (origSequence.Count > 0 && corrSequence.Count > 0)
                {
                    var fromText = string.Join(" ", origSequence);
                    var toText = string.Join(" ", corrSequence);
                    differences.Add((fromText, toText));
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
