using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SimpleLoop.Services;

namespace SimpleLoop.Gui;

/// <summary>
/// SimpleLoop GUI - Main window for dialogue catalog and speaker profile management
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Windows API for forceful process termination
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    private DialogueCatalog? _dialogueCatalog;
    private SpeakerCatalog? _speakerCatalog;
    private bool _isCapturing = false;
    
    // NEW: CaptureService integration
    private CaptureService? _captureService;
    private DispatcherTimer? _statsUpdateTimer;
    private DispatcherTimer? _logUpdateTimer;
    
    // Audio playback
    private System.Windows.Media.MediaPlayer? _mediaPlayer;
    
    // Observable collections for data binding
    public ObservableCollection<DialogueEntry> DialogueEntries { get; } = new();
    public ObservableCollection<SpeakerProfile> SpeakerProfiles { get; } = new();
    public ObservableCollection<string> AvailableSpeakers { get; } = new();
    
    private SpeakerProfile? _selectedSpeaker;
    public SpeakerProfile? SelectedSpeaker
    {
        get => _selectedSpeaker;
        set
        {
            _selectedSpeaker = value;
            OnPropertyChanged();
            LoadSpeakerDetails();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        
        // Initialize media player for in-app audio playback
        _mediaPlayer = new System.Windows.Media.MediaPlayer();
        _mediaPlayer.MediaEnded += (s, e) => StatusText.Text = "Audio playback completed";
        _mediaPlayer.MediaFailed += (s, e) => StatusText.Text = $"Audio playback failed: {e.ErrorException?.Message}";
        
        // Add multiple shutdown hooks
        this.Closing += MainWindow_Closing;
        this.Closed += MainWindow_Closed;
        Application.Current.SessionEnding += Application_SessionEnding;
        
        InitializeCatalogs();
        InitializeCaptureService();
        SetupUI();
        LoadData();
        
        Console.WriteLine($"[GUI] MainWindow initialized. AvailableSpeakers count: {AvailableSpeakers.Count}");
    }

    private void InitializeCatalogs()
    {
        var debugLog = @"c:\Code Projects\GameWatcher\SimpleLoop\gui_debug.log";
        
        try
        {
            // Use absolute path to SimpleLoop directory (same as CaptureService)
            var simpleLoopDir = @"c:\Code Projects\GameWatcher\SimpleLoop";
            var dialoguePath = System.IO.Path.Combine(simpleLoopDir, "dialogue_catalog.json");
            var speakerPath = System.IO.Path.Combine(simpleLoopDir, "speaker_catalog.json");
            
            Console.WriteLine($"[GUI] Loading catalogs from:");
            Console.WriteLine($"[GUI]   Dialogue: {dialoguePath}");
            Console.WriteLine($"[GUI]   Speaker: {speakerPath}");
            Console.WriteLine($"[GUI]   Files exist: Dialogue={File.Exists(dialoguePath)}, Speaker={File.Exists(speakerPath)}");
            
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] InitializeCatalogs started\n");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Dialogue path: {dialoguePath}\n");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Speaker path: {speakerPath}\n");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Files exist: Dialogue={File.Exists(dialoguePath)}, Speaker={File.Exists(speakerPath)}\n");
            
            // Also check file sizes for diagnostics
            if (File.Exists(dialoguePath))
            {
                var dialogueContent = File.ReadAllText(dialoguePath);
                Console.WriteLine($"[GUI]   Dialogue file size: {dialogueContent.Length} characters");
                File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Dialogue file size: {dialogueContent.Length} characters\n");
            }
            
            _speakerCatalog = new SpeakerCatalog(speakerPath);
            _dialogueCatalog = new DialogueCatalog(dialoguePath, _speakerCatalog);
            
            Console.WriteLine($"Catalogs created successfully");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Catalogs created successfully\n");
            
            // Load entries into UI collections
            LoadDialogueEntries();
            LoadSpeakerProfiles();
            
            StatusText.Text = "Catalogs loaded successfully";
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing catalogs: {ex}");
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to initialize catalogs: {ex.Message}\n\nTrying to load from: {AppDomain.CurrentDomain.BaseDirectory}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void InitializeCaptureService()
    {
        try 
        {
            // CLI is the single source of truth - GUI manages CLI's data files  
            // Use absolute path to avoid working directory issues
            var cliDirectory = @"c:\Code Projects\GameWatcher\SimpleLoop";
            var speakerCatalogPath = System.IO.Path.Combine(cliDirectory, "speaker_catalog.json");
            var dialogueCatalogPath = System.IO.Path.Combine(cliDirectory, "dialogue_catalog.json");
            Console.WriteLine($"[GUI] CLI directory: {cliDirectory}");
            Console.WriteLine($"[GUI] Speaker catalog exists: {System.IO.File.Exists(speakerCatalogPath)}");
            Console.WriteLine($"[GUI] About to create CaptureService with paths:");
            Console.WriteLine($"[GUI]   Speaker: {speakerCatalogPath}");
            Console.WriteLine($"[GUI]   Dialogue: {dialogueCatalogPath}");
            _captureService = new CaptureService(speakerCatalogPath, dialogueCatalogPath);            // Subscribe to capture service events
            _captureService.ProgressReported += OnCaptureProgressReported;
            _captureService.DialogueDetected += OnDialogueDetected;
            
            // Setup stats update timer (every second)
            _statsUpdateTimer = new DispatcherTimer();
            _statsUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _statsUpdateTimer.Tick += UpdateLiveStats;
            
            // Setup log refresh timer (every 2 seconds for file-based logs)
            _logUpdateTimer = new DispatcherTimer();
            _logUpdateTimer.Interval = TimeSpan.FromSeconds(2);
            _logUpdateTimer.Tick += UpdateLogDisplay;
            _logUpdateTimer.Start();
            
            StatusText.Text = "Capture service initialized";
            
            // Debug: Show log file location immediately
            var logPath = _captureService.GetLogFilePath();
            Console.WriteLine($"[GUI Debug] Capture service log path: {logPath}");
            
            // Trigger an immediate log update to test
            UpdateLogDisplay(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error initializing capture service: {ex.Message}";
            MessageBox.Show($"Failed to initialize capture service: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    


    private void SetupUI()
    {
        var debugLog = @"c:\Code Projects\GameWatcher\SimpleLoop\gui_debug.log";
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] SetupUI() called\n");
        
        // Setup DataGrid bindings
        DialogueDataGrid.ItemsSource = DialogueEntries;
        SpeakerListBox.ItemsSource = SpeakerProfiles;
        
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] DialogueEntries count: {DialogueEntries.Count}\n");
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] ItemsSource set to DialogueEntries\n");
        
        // Setup speaker filter dropdown
        SpeakerFilterBox.Items.Add("All Speakers");
        SpeakerFilterBox.SelectedIndex = 0;
        
        // Setup search box placeholder behavior
        DialogueSearchBox.GotFocus += (s, e) => {
            if (DialogueSearchBox.Text == "Search dialogue text...")
                DialogueSearchBox.Text = "";
        };
        DialogueSearchBox.LostFocus += (s, e) => {
            if (string.IsNullOrWhiteSpace(DialogueSearchBox.Text))
                DialogueSearchBox.Text = "Search dialogue text...";
        };
        DialogueSearchBox.Text = "Search dialogue text...";
        
        // Setup TTS speed slider display
        TtsSpeedSlider.ValueChanged += (s, e) => {
            // Could add a label to show current speed value
        };
    }

    private void LoadData()
    {
        var debugLog = @"c:\Code Projects\GameWatcher\SimpleLoop\gui_debug.log";
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] LoadData() called - DialogueEntries count before: {DialogueEntries.Count}\n");
        
        // Don't reload dialogue entries - they're already loaded by InitializeCatalogs()
        // LoadDialogueEntries();
        LoadSpeakerProfiles();
        UpdateStatistics();
        
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] LoadData() completed - DialogueEntries count after: {DialogueEntries.Count}\n");
    }

    private void LoadDialogueEntries()
    {
        // Debug file logging to see what's happening
        var debugLog = @"c:\Code Projects\GameWatcher\SimpleLoop\gui_debug.log";
        
        if (_dialogueCatalog == null) 
        {
            Console.WriteLine("[GUI] _dialogueCatalog is null!");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] _dialogueCatalog is null!\n");
            StatusText.Text = "Dialogue catalog not initialized";
            return;
        }
        
        DialogueEntries.Clear();
        Console.WriteLine("[GUI] Loading dialogue entries...");
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Loading dialogue entries...\n");
        
        try
        {
            var entries = _dialogueCatalog.GetAllEntries();
            Console.WriteLine($"[GUI] Retrieved {entries.Count()} entries from catalog");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Retrieved {entries.Count()} entries from catalog\n");
            
            foreach (var entry in entries)
            {
                DialogueEntries.Add(entry);
                var preview = entry.Text.Substring(0, Math.Min(50, entry.Text.Length));
                Console.WriteLine($"[GUI] Added: {preview}...");
                File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Added: {preview}...\n");
            }
            
            Console.WriteLine($"[GUI] Added {DialogueEntries.Count} entries to UI collection");
            Console.WriteLine($"[GUI] DataGrid ItemsSource count: {((System.Collections.ICollection?)DialogueDataGrid.ItemsSource)?.Count ?? 0}");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Added {DialogueEntries.Count} entries to UI collection\n");
            UpdateStatistics();
            
            // Force UI refresh
            DialogueDataGrid.Items.Refresh();
            
            // Debug UI thread and binding state
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] UI Thread: {System.Windows.Threading.Dispatcher.CurrentDispatcher.Thread.ManagedThreadId}\n");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] ItemsSource null: {DialogueDataGrid.ItemsSource == null}\n");
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] ItemsSource type: {DialogueDataGrid.ItemsSource?.GetType().Name}\n");
            
            if (DialogueDataGrid.ItemsSource is System.Collections.IEnumerable items)
            {
                var count = 0;
                foreach (var item in items) count++;
                File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] ItemsSource enumeration count: {count}\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dialogue entries: {ex}");
            StatusText.Text = $"Error loading dialogue: {ex.Message}";
        }
    }    private void LoadSpeakerProfiles()
    {
        var debugLog = @"c:\Code Projects\GameWatcher\SimpleLoop\gui_debug.log";
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] LoadSpeakerProfiles() called\n");
        
        if (_speakerCatalog == null) 
        {
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] _speakerCatalog is null!\n");
            return;
        }

        SpeakerProfiles.Clear();
        AvailableSpeakers.Clear();
        SpeakerFilterBox.Items.Clear();
        SpeakerFilterBox.Items.Add("All Speakers");
        
        try
        {
            var speakers = _speakerCatalog.GetAllSpeakers();
            Console.WriteLine($"[GUI] LoadSpeakerProfiles: Found {speakers.Count} speakers");
            
            foreach (var speaker in speakers)
            {
                SpeakerProfiles.Add(speaker);
                AvailableSpeakers.Add(speaker.Name);
                SpeakerFilterBox.Items.Add(speaker.Name);
                _captureService?.Logger?.LogMessage($"[GUI] Added speaker: {speaker.Name} with voice: {speaker.TtsVoiceId}");
            }
            
            Console.WriteLine($"[GUI] AvailableSpeakers collection count: {AvailableSpeakers.Count}");
            SpeakerFilterBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] Exception in LoadSpeakerProfiles: {ex.Message}\n");
            StatusText.Text = $"Error loading speakers: {ex.Message}";
        }
    }

    private void UpdateStatistics()
    {
        DialogueCountText.Text = DialogueEntries.Count.ToString();
        SpeakerCountText.Text = SpeakerProfiles.Count.ToString();
        
        // Also update status bar
        StatusDialogueCount.Text = DialogueEntries.Count.ToString();
        StatusSpeakerCount.Text = SpeakerProfiles.Count.ToString();
    }

    #region Capture Control Events

    private async void StartCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturing || _captureService == null) return;

        try
        {
            // Start the capture service
            var started = await _captureService.StartCaptureAsync();
            
            if (started)
            {
                _isCapturing = true;
                StartCaptureButton.IsEnabled = false;
                StopCaptureButton.IsEnabled = true;
                StatusText.Text = "Capturing...";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                CaptureStatusText.Text = "Capturing";
                CaptureStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                
                // Log will be updated by the file-based refresh timer
                
                // Start stats update timer
                _statsUpdateTimer?.Start();
            }
            else
            {
                MessageBox.Show("Failed to start capture service. Check the log for details.", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start capture: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
            ResetCaptureUI();
        }
    }

    private async void StopCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCapturing || _captureService == null) return;

        try
        {
            var stopped = await _captureService.StopCaptureAsync();
            
            if (stopped)
            {
                ResetCaptureUI();
                
                // Log will be updated by the file-based refresh timer
                
                // Refresh data after capture session
                LoadData();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error stopping capture: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetCaptureUI()
    {
        _isCapturing = false;
        StartCaptureButton.IsEnabled = true;
        StopCaptureButton.IsEnabled = false;
        StatusText.Text = "Ready";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(144, 238, 144)); // Light green
        CaptureStatusText.Text = "Ready";
        CaptureStatusText.Foreground = new SolidColorBrush(Color.FromRgb(144, 238, 144)); // Light green
        
        _statsUpdateTimer?.Stop();
    }

    private void UpdateLiveStats(object? sender, EventArgs e)
    {
        if (!_isCapturing || _captureService == null) return;

        try
        {
            // Get real stats from capture service
            var stats = _captureService.GetStatistics();
            
            // Update UI with real values
            Dispatcher.Invoke(() =>
            {
                RuntimeText.Text = $"{stats.Runtime:mm\\:ss}";
                FpsText.Text = stats.ActualFps.ToString("F1");
                FrameCountText.Text = stats.FrameCount.ToString();
                ProcessedText.Text = stats.ProcessedFrames.ToString();
                TextboxFoundText.Text = stats.TextboxesFound.ToString();
                PerformanceText.Text = $"{stats.AverageProcessingTimeMs:F1}ms";
                AvgTimeText.Text = $"{stats.AverageProcessingTimeMs:F1}ms";
                
                // Also update status bar and live displays
                StatusFps.Text = stats.ActualFps.ToString("F1");
                StatusRuntime.Text = $"{stats.Runtime:mm\\:ss}";
                LiveFpsDisplay.Text = stats.ActualFps.ToString("F1");
                LiveTextboxDisplay.Text = stats.TextboxesFound.ToString();
                
                // Update dialogue and speaker counts from stats
                DialogueCountText.Text = stats.DialogueCount.ToString();
                SpeakerCountText.Text = stats.SpeakerCount.ToString();
                StatusDialogueCount.Text = stats.DialogueCount.ToString();
                StatusSpeakerCount.Text = stats.SpeakerCount.ToString();
            });
        }
        catch (Exception ex)
        {
            // Log error but don't show message box (would interrupt capture)
            Console.WriteLine($"Error updating live stats: {ex.Message}");
        }
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LiveLogTextBox.Clear();
            LiveLogTextBox.Text = $"[{DateTime.Now:HH:mm:ss}] Log display cleared by user\n";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error clearing log: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    #region Dialogue Management Events

    private void DialogueSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterDialogueEntries();
    }

    private void SpeakerFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterDialogueEntries();
    }

    private void FilterDialogueEntries()
    {
        if (DialogueDataGrid.ItemsSource is not ICollectionView view) return;

        var searchText = DialogueSearchBox.Text;
        var selectedSpeaker = SpeakerFilterBox.SelectedItem?.ToString();

        if (searchText == "Search dialogue text...")
            searchText = "";

        view.Filter = item =>
        {
            if (item is not DialogueEntry entry) return false;

            bool matchesSearch = string.IsNullOrEmpty(searchText) || 
                               entry.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase);

            bool matchesSpeaker = selectedSpeaker == "All Speakers" || 
                                string.IsNullOrEmpty(selectedSpeaker) ||
                                entry.Speaker?.Contains(selectedSpeaker, StringComparison.OrdinalIgnoreCase) == true;

            return matchesSearch && matchesSpeaker;
        };
    }

    private void SearchDialogue_Click(object sender, RoutedEventArgs e)
    {
        FilterDialogueEntries();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        DialogueSearchBox.Text = "Search dialogue text...";
        SpeakerFilterBox.SelectedIndex = 0;
        FilterDialogueEntries();
    }

    private void RefreshData_Click(object sender, RoutedEventArgs e)
    {
        var debugLog = @"c:\Code Projects\GameWatcher\SimpleLoop\gui_debug.log";
        Console.WriteLine("[GUI] Manual refresh requested");
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] RefreshData_Click called\n");
        
        LoadDialogueEntries();
        LoadSpeakerProfiles(); 
        StatusText.Text = "Data refreshed manually";
        
        File.AppendAllText(debugLog, $"[{DateTime.Now:HH:mm:ss}] RefreshData_Click completed\n");
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Save both dialogue and speaker catalogs
            _dialogueCatalog?.SaveCatalog();
            _speakerCatalog?.SaveCatalog();
            
            StatusText.Text = "Changes saved successfully";
            MessageBox.Show("All changes have been saved to disk.", "Save Complete", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving changes: {ex.Message}";
            MessageBox.Show($"Failed to save changes:\n{ex.Message}", "Save Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SpeakerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _captureService?.Logger?.LogMessage("[GUI] SpeakerCombo_SelectionChanged triggered");
        
        if (sender is ComboBox combo && combo.DataContext is DialogueEntry entry)
        {
            var selectedSpeaker = combo.SelectedItem as string;
            _captureService?.Logger?.LogMessage($"[GUI] Selected speaker: '{selectedSpeaker}', Current entry speaker: '{entry.Speaker}'");
            
            if (!string.IsNullOrEmpty(selectedSpeaker))
            {
                _captureService?.Logger?.LogMessage($"[GUI] Processing speaker selection: '{selectedSpeaker}' (was '{entry.Speaker}')");
                
                // Always update the speaker name (even if it's the same)
                entry.Speaker = selectedSpeaker;
                
                // Find the speaker profile and update the voice
                var speaker = _speakerCatalog?.GetSpeakerByName(selectedSpeaker);
                _captureService?.Logger?.LogMessage($"[GUI] Found speaker profile: {speaker?.Name} with voice: {speaker?.TtsVoiceId}");
                
                if (speaker != null)
                {
                    var oldVoice = entry.VoiceProfile;
                    entry.VoiceProfile = speaker.TtsVoiceId;
                    entry.TtsVoiceId = speaker.TtsVoiceId;
                    _captureService?.Logger?.LogMessage($"[GUI] Updated entry voice from '{oldVoice}' to: {entry.VoiceProfile}");
                }
                else
                {
                    _captureService?.Logger?.LogMessage("[GUI] Speaker profile not found in catalog");
                }
                
                // Update status - no manual refresh needed if binding works properly
                StatusText.Text = $"Updated speaker to '{selectedSpeaker}' (voice: {entry.VoiceProfile})";
            }
            else
            {
                _captureService?.Logger?.LogMessage("[GUI] Empty speaker selection");
            }
        }
    }

    private void DeleteSelectedDialogue_Click(object sender, RoutedEventArgs e)
    {
        var selected = DialogueDataGrid.SelectedItems.Cast<DialogueEntry>().ToList();
        if (!selected.Any()) return;

        var result = MessageBox.Show($"Delete {selected.Count} selected dialogue entries?", 
                                   "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            foreach (var entry in selected)
            {
                DialogueEntries.Remove(entry);
                _dialogueCatalog?.RemoveDialogueById(entry.Id);
            }
            
            // Save changes to catalog
            SaveDialogueCatalog();
            UpdateStatistics();
        }
    }

    #endregion

    #region Speaker Management Events

    private void NewSpeaker_Click(object sender, RoutedEventArgs e)
    {
        var newSpeaker = new SpeakerProfile
        {
            Name = "New Speaker",
            CharacterType = "NPC",
            TtsVoiceId = "alloy",
            TtsSpeed = 1.0f,
            Description = "",
            Effects = new AudioEffects { EnvironmentPreset = "None" }
        };

        SpeakerProfiles.Add(newSpeaker);
        _speakerCatalog?.AddOrUpdateSpeaker(newSpeaker);
        SaveSpeakerCatalog();
        
        SpeakerListBox.SelectedItem = newSpeaker;
        SpeakerNameBox.Focus();
        SpeakerNameBox.SelectAll();
    }

    private void DeleteSpeaker_Click(object sender, RoutedEventArgs e)
    {
        if (SpeakerListBox.SelectedItem is not SpeakerProfile speaker) return;

        var result = MessageBox.Show($"Delete speaker '{speaker.Name}'?", 
                                   "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            SpeakerProfiles.Remove(speaker);
            _speakerCatalog?.RemoveSpeaker(speaker.Id);
            SaveSpeakerCatalog();
            UpdateStatistics();
        }
    }

    private void SpeakerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedSpeaker = SpeakerListBox.SelectedItem as SpeakerProfile;
        SpeakerEditorGroup.IsEnabled = SelectedSpeaker != null;
    }

    private void LoadSpeakerDetails()
    {
        if (SelectedSpeaker == null) return;

        SpeakerNameBox.Text = SelectedSpeaker.Name;
        CharacterTypeBox.Text = SelectedSpeaker.CharacterType;
        TtsVoiceBox.Text = SelectedSpeaker.TtsVoiceId;
        TtsSpeedSlider.Value = SelectedSpeaker.TtsSpeed;
        SpeakerDescriptionBox.Text = SelectedSpeaker.Description;
        AudioEffectsBox.Text = SelectedSpeaker.Effects.EnvironmentPreset;
    }

    private void SaveSpeaker_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpeaker == null) return;

        SelectedSpeaker.Name = SpeakerNameBox.Text;
        SelectedSpeaker.CharacterType = CharacterTypeBox.Text;
        SelectedSpeaker.TtsVoiceId = TtsVoiceBox.Text;
        SelectedSpeaker.TtsSpeed = (float)TtsSpeedSlider.Value;
        SelectedSpeaker.Description = SpeakerDescriptionBox.Text;
        SelectedSpeaker.Effects.EnvironmentPreset = AudioEffectsBox.Text;

        // Save changes to catalog
        _speakerCatalog?.AddOrUpdateSpeaker(SelectedSpeaker);
        SaveSpeakerCatalog();
        StatusText.Text = $"Saved speaker '{SelectedSpeaker.Name}'";

        // Refresh the list display
        SpeakerListBox.Items.Refresh();
        LoadSpeakerProfiles(); // Refresh filter dropdown
    }

    private async void PreviewVoice_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpeaker == null) return;
        
        try
        {
            StatusText.Text = $"Checking voice preview for {SelectedSpeaker.Name}...";
            _captureService?.Logger?.LogMessage($"[GUI] Voice preview requested for speaker: {SelectedSpeaker.Name}");
            
            // Create a temporary dialogue entry for the preview
            var sampleText = "Hello, this is a preview of my voice settings.";
            var previewEntry = new DialogueEntry
            {
                Id = "voice_preview",
                Text = sampleText,
                Speaker = SelectedSpeaker.Name,
                VoiceProfile = SelectedSpeaker.TtsVoiceId,
                TtsVoiceId = SelectedSpeaker.TtsVoiceId
            };
            
            // Generate/play preview (this will check for existing file first)
            await GenerateAndPlayPreview(previewEntry, SelectedSpeaker);
            
            StatusText.Text = $"Voice preview for {SelectedSpeaker.Name} completed";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating voice preview: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task GenerateAndPlayPreview(DialogueEntry previewEntry, SpeakerProfile speaker)
    {
        try
        {
            // Check if we have a TTS service available through capture service
            if (_captureService == null)
            {
                throw new InvalidOperationException("Capture service not available");
            }
            
            _captureService?.Logger?.LogMessage($"[GUI] Starting speaker preview for: {speaker.Name}");
            
            // Load TTS configuration
            var config = SimpleLoop.Services.TtsConfiguration.Load();
            
            // Create shared preview system - voice+speed based, not speaker based
            var previewsDir = System.IO.Path.Combine(config.VoicesDirectory, "previews");
            var previewFileName = $"{speaker.TtsVoiceId}_{speaker.TtsSpeed:F1}.mp3";
            var previewPath = System.IO.Path.Combine(previewsDir, previewFileName);
            
            // ALWAYS check for existing preview file first (even if TTS fails, we can use existing)
            if (File.Exists(previewPath))
            {
                _captureService?.Logger?.LogMessage($"[GUI] Using existing shared preview: {previewPath}");
                await PlayAudioFile(previewPath);
                return;
            }
            
            _captureService?.Logger?.LogMessage($"[GUI] No existing preview found at: {previewPath}");
            
            // Check if TTS config is valid before attempting generation
            if (!config.IsValid())
            {
                throw new InvalidOperationException($"TTS configuration is invalid and no existing preview found for {speaker.TtsVoiceId} at {speaker.TtsSpeed:F1} speed");
            }
            
            // Only generate if TTS is available - if not, we already returned above for existing files
            if (!_captureService?.IsTtsReady == true)
            {
                throw new InvalidOperationException("TTS system is not ready and no existing preview found");
            }
            
            // Generate new shared preview file
            var ttsService = new SimpleLoop.Services.TtsService(config.OpenAiApiKey, config.VoicesDirectory);
            
            // Create specific preview entry with shared preview path
            var sharedPreview = new DialogueEntry
            {
                Id = "voice_preview",
                Text = previewEntry.Text,
                Speaker = "Preview", // Generic speaker for preview
                VoiceProfile = speaker.TtsVoiceId,
                TtsVoiceId = speaker.TtsVoiceId,
                AudioPath = previewPath
            };
            
            // Ensure previews directory exists
            Directory.CreateDirectory(previewsDir);
            
            var audioPath = await ttsService.GenerateAudioAsync(sharedPreview, speaker);
            
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
                _captureService?.Logger?.LogMessage($"[GUI] New shared preview generated: {audioPath}");
                
                // Move to shared preview location if necessary
                if (audioPath != previewPath)
                {
                    if (File.Exists(previewPath)) File.Delete(previewPath);
                    File.Move(audioPath, previewPath);
                    _captureService?.Logger?.LogMessage($"[GUI] Moved to shared preview path: {previewPath}");
                }
                
                // Play the audio file
                await PlayAudioFile(previewPath);
            }
            else
            {
                throw new InvalidOperationException("Failed to generate shared preview audio file");
            }
        }
        catch (Exception ex)
        {
            _captureService?.Logger?.LogMessage($"[GUI] Error in voice preview: {ex.Message}");
            throw;
        }
    }
    

    
    private async Task PlayAudioFile(string audioPath)
    {
        try
        {
            // Convert relative path to absolute path
            var fullPath = System.IO.Path.IsPathRooted(audioPath) ? audioPath : System.IO.Path.GetFullPath(audioPath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Audio file not found: {fullPath}");
            }
            
            _captureService?.Logger?.LogMessage($"[GUI] Playing audio: {fullPath}");
            
            // Stop any current playback
            _mediaPlayer?.Stop();
            
            // Play audio using the persistent media player
            if (_mediaPlayer != null)
            {
                var uri = new Uri(fullPath);
                _mediaPlayer.Open(uri);
                _mediaPlayer.Volume = 0.7; // Set reasonable volume
                _mediaPlayer.Play();
                
                StatusText.Text = $"♪ Playing: {System.IO.Path.GetFileName(fullPath)}";
                
                // Wait a moment for playback to start
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            _captureService?.Logger?.LogMessage($"[GUI] Error playing audio: {ex.Message}");
            StatusText.Text = $"Audio playback failed: {ex.Message}";
            throw new InvalidOperationException($"Failed to play audio: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop any currently playing audio
    /// </summary>
    private void StopAudio()
    {
        try
        {
            _mediaPlayer?.Stop();
            StatusText.Text = "Audio playback stopped";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error stopping audio: {ex.Message}";
        }
    }

    #endregion

    #region Always on Top Events

    private void AlwaysOnTopCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        this.Topmost = true;
    }

    private void AlwaysOnTopCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        this.Topmost = false;
    }

    #endregion

    #region Capture Service Event Handlers
    
    private void OnCaptureProgressReported(object? sender, CaptureProgressEventArgs e)
    {
        // Progress updates are handled by the stats update timer
        // This event can be used for additional processing if needed
    }
    
    private void OnDialogueDetected(object? sender, DialogueDetectedEventArgs e)
    {
        // New dialogue detected - refresh the dialogue grid
        Dispatcher.Invoke(() =>
        {
            try
            {
                // Add the new dialogue to the collection
                DialogueEntries.Add(e.DialogueEntry);
                
                // Scroll to the new entry
                DialogueDataGrid.ScrollIntoView(e.DialogueEntry);
                
                // Update statistics
                UpdateStatistics();
                
                // Dialogue detection will be logged to file automatically
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling dialogue detection: {ex.Message}");
            }
        });
    }
    
    private void UpdateLogDisplay(object? sender, EventArgs e)
    {
        // Update log display from file (non-blocking, prevents UI thread issues)
        if (_captureService == null) return;
        
        try
        {
            var logLines = _captureService.GetRecentLogLines(500);
            
            if (logLines.Length == 0) 
            {
                // Debug: Show why no logs are found
                var logPath = _captureService.GetLogFilePath();
                if (string.IsNullOrEmpty(logPath))
                {
                    LiveLogTextBox.Text = "[DEBUG] No log file path available from capture service\n";
                }
                else if (!System.IO.File.Exists(logPath))
                {
                    LiveLogTextBox.Text = $"[DEBUG] Log file not found: {logPath}\n";
                }
                else
                {
                    LiveLogTextBox.Text = $"[DEBUG] Log file exists but no lines returned: {logPath}\n";
                }
                return;
            }
            
            // Only update if content has changed to prevent flickering
            var newContent = string.Join('\n', logLines);
            if (LiveLogTextBox.Text != newContent)
            {
                LiveLogTextBox.Text = newContent;
                LiveLogTextBox.ScrollToEnd();
            }
        }
        catch (Exception ex)
        {
            // Don't show message box during regular operation - just log to console
            Console.WriteLine($"Error updating log display: {ex.Message}");
        }
    }
    
    #endregion

    #region Data Persistence Helpers
    
    /// <summary>
    /// Save speaker catalog changes to disk
    /// </summary>
    private void SaveSpeakerCatalog()
    {
        try
        {
            _speakerCatalog?.SaveCatalog();
            Console.WriteLine("💾 Saved speaker catalog changes");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving speaker catalog: {ex.Message}";
            Console.WriteLine($"❌ Error saving speaker catalog: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Save dialogue catalog changes to disk
    /// </summary>
    private void SaveDialogueCatalog()
    {
        try
        {
            _dialogueCatalog?.SaveCatalog();
            Console.WriteLine("💾 Saved dialogue catalog changes");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving dialogue catalog: {ex.Message}";
            Console.WriteLine($"❌ Error saving dialogue catalog: {ex.Message}");
        }
    }
    
    #endregion

    #region Window Event Handlers
    
    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        Console.WriteLine("[GUI] MainWindow_Closing called");
        ForceShutdown();
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        Console.WriteLine("[GUI] MainWindow_Closed called");
        ForceShutdown();
        
        // Nuclear option - multiple termination methods
        Task.Run(async () =>
        {
            await Task.Delay(500); // Wait half second
            Console.WriteLine("[GUI] Attempting Environment.Exit(0)");
            Environment.Exit(0);
        });
        
        Task.Run(async () =>
        {
            await Task.Delay(1000); // Wait 1 second  
            Console.WriteLine("[GUI] Attempting Application.Current.Shutdown()");
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(0));
        });
        
        Task.Run(async () =>
        {
            await Task.Delay(1500); // Wait 1.5 seconds
            Console.WriteLine("[GUI] Attempting Windows API TerminateProcess()");
            var currentProcess = GetCurrentProcess();
            TerminateProcess(currentProcess, 0);
        });
        
        Task.Run(async () =>
        {
            await Task.Delay(2000); // Wait 2 seconds
            Console.WriteLine("[GUI] Attempting Process.GetCurrentProcess().Kill()");
            Process.GetCurrentProcess().Kill();
        });
    }

    private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Console.WriteLine("[GUI] Application_SessionEnding called");
        ForceShutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Console.WriteLine("[GUI] OnClosing called");
        try
        {
            ForceShutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex.Message}");
        }
        finally
        {
            base.OnClosing(e);
        }
    }

    private static bool _shutdownInProgress = false;

    private void ForceShutdown()
    {
        if (_shutdownInProgress) return;
        _shutdownInProgress = true;
        
        Console.WriteLine("[GUI] ForceShutdown started");
        
        // Stop all timers immediately - no try/catch, just force it
        try
        {
            if (_statsUpdateTimer != null)
            {
                _statsUpdateTimer.Stop();
                _statsUpdateTimer.Tick -= UpdateLiveStats;
                _statsUpdateTimer = null;
            }
            
            if (_logUpdateTimer != null)
            {
                _logUpdateTimer.Stop();
                _logUpdateTimer.Tick -= UpdateLogDisplay;
                _logUpdateTimer = null;
            }
            Console.WriteLine("[GUI] Timers stopped and disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping timers: {ex.Message}");
        }
        
        // Stop and dispose media player
        try
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Close();
                _mediaPlayer = null;
                Console.WriteLine("[GUI] Media player disposed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing media player: {ex.Message}");
        }
        
        // Stop capture service immediately - no waiting
        try
        {
            if (_captureService != null)
            {
                Console.WriteLine("[GUI] Disposing capture service...");
                // Try to stop first, but don't wait
                if (_captureService.IsRunning)
                {
                    _ = _captureService.StopCaptureAsync(); // Fire and forget
                }
                // Force dispose immediately
                _captureService.Dispose();
                Console.WriteLine("[GUI] Capture service disposed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing capture service: {ex.Message}");
        }
        
        // Clear all references immediately
        _captureService = null;
        _statsUpdateTimer = null;
        _logUpdateTimer = null;
        _dialogueCatalog = null;
        _speakerCatalog = null;
        
        // Quick save attempt - don't wait if it fails
        try
        {
            var dialogPath = @"c:\Code Projects\GameWatcher\SimpleLoop\dialogue_catalog.json";
            var speakerPath = @"c:\Code Projects\GameWatcher\SimpleLoop\speaker_catalog.json";
            Console.WriteLine($"[GUI] Attempting quick save to {dialogPath} and {speakerPath}");
        }
        catch { }
        
        Console.WriteLine("[GUI] ForceShutdown completed - scheduling force exit");
    }
    
    #endregion

    #region Dialogue Row Action Event Handlers

    private async void PlayAudio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DialogueEntry entry)
        {
            try 
            {
                // Find the audio file for this dialogue entry
                var audioFile = FindAudioFileForDialogue(entry);
                if (audioFile != null && File.Exists(audioFile))
                {
                    // Use in-app audio player instead of external media player
                    await PlayAudioFile(audioFile);
                }
                else
                {
                    MessageBox.Show($"No audio file found for this dialogue entry.\nExpected: {audioFile ?? "Unknown path"}", 
                                  "Audio Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing audio: {ex.Message}", "Playback Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void GenerateAudio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DialogueEntry entry)
        {
            var originalContent = button.Content;
            try 
            {
                button.IsEnabled = false;
                
                // Find the speaker profile for this dialogue
                var speaker = _speakerCatalog?.GetSpeakerByName(entry.Speaker);
                if (speaker == null)
                {
                    MessageBox.Show($"No speaker profile found for '{entry.Speaker}'. Please create a speaker profile first.", 
                                  "Speaker Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if audio already exists
                var existingAudio = FindAudioFileForDialogue(entry);
                
                if (existingAudio != null && File.Exists(existingAudio))
                {
                    // Audio exists - ask user what to do
                    var result = MessageBox.Show(
                        $"Audio already exists for this dialogue:\n{System.IO.Path.GetFileName(existingAudio)}\n\nDo you want to:\nYes = Regenerate (delete old)\nNo = Play existing\nCancel = Do nothing",
                        "Audio Already Exists", 
                        MessageBoxButton.YesNoCancel, 
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // User wants to regenerate - delete old file first
                        button.Content = "🗑 Deleting...";
                        await CleanupOldAudioFiles(entry);
                        
                        // Continue with generation below
                        button.Content = "⏳ Generating...";
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        // User wants to play existing
                        PlayExistingAudio(existingAudio);
                        return;
                    }
                    else
                    {
                        // User cancelled
                        return;
                    }
                }
                else
                {
                    // No existing audio - generate new
                    button.Content = "⏳ Generating...";
                }

                // Generate TTS audio using the TtsService
                var audioFile = await GenerateTtsAudioForEntry(entry, speaker);
                
                if (audioFile != null)
                {
                    // Update the entry with the generated audio file path
                    entry.AudioPath = audioFile;
                    entry.HasAudio = true;
                    entry.AudioGeneratedAt = DateTime.Now;
                    _dialogueCatalog?.SaveCatalog();
                    
                    MessageBox.Show("Audio generated successfully!", "Generation Complete", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating audio: {ex.Message}", "Generation Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = originalContent;
            }
        }
    }

    private void ManageAudio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DialogueEntry entry)
        {
            try 
            {
                // Find the speaker directory for this dialogue
                var speaker = _speakerCatalog?.GetSpeakerByName(entry.Speaker);
                if (speaker == null)
                {
                    MessageBox.Show($"No speaker profile found for '{entry.Speaker}'.", 
                                  "Speaker Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = SimpleLoop.Services.TtsConfiguration.Load();
                var speakerDir = System.IO.Path.Combine(config.VoicesDirectory, speaker.Name);
                
                if (!Directory.Exists(speakerDir))
                {
                    MessageBox.Show($"No audio files directory found for '{speaker.Name}'.", 
                                  "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get all audio files for this dialogue text
                var audioFiles = Directory.GetFiles(speakerDir, "*.mp3")
                    .Where(f => System.IO.Path.GetFileName(f).Contains(entry.Id) || 
                               System.IO.Path.GetFileName(f).Contains(entry.Text.GetHashCode().ToString("X8")))
                    .ToArray();

                if (audioFiles.Length == 0)
                {
                    MessageBox.Show($"No audio files found for this dialogue in '{speakerDir}'.", 
                                  "No Audio Files", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Show audio file management dialog
                ShowAudioFileManager(entry, audioFiles);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error managing audio files: {ex.Message}", "Management Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ShowAudioFileManager(DialogueEntry entry, string[] audioFiles)
    {
        var fileList = string.Join("\n", audioFiles.Select((f, i) => $"{i + 1}. {System.IO.Path.GetFileName(f)} ({new FileInfo(f).Length / 1024:N0}KB)"));
        
        var result = MessageBox.Show(
            $"Audio files for: '{entry.Text}'\n\nFiles found:\n{fileList}\n\nOpen audio folder?", 
            "Audio File Manager", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            // Open the speaker's audio folder in Windows Explorer
            var speakerDir = System.IO.Path.GetDirectoryName(audioFiles[0]);
            if (!string.IsNullOrEmpty(speakerDir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = speakerDir,
                    UseShellExecute = true
                });
            }
        }
    }

    private void DeleteDialogue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DialogueEntry entry)
        {
            var result = MessageBox.Show($"Are you sure you want to delete this dialogue entry?\n\n\"{entry.Text}\"", 
                                       "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Remove from catalog
                    _dialogueCatalog?.RemoveDialogue(entry.Text);
                    
                    // Remove from UI collection
                    DialogueEntries.Remove(entry);
                    
                    // Delete associated audio file if it exists
                    var audioFile = FindAudioFileForDialogue(entry);
                    if (audioFile != null && File.Exists(audioFile))
                    {
                        File.Delete(audioFile);
                    }
                    
                    UpdateStatistics();
                    StatusText.Text = "Dialogue entry deleted successfully";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting dialogue: {ex.Message}", "Delete Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private string? FindAudioFileForDialogue(DialogueEntry entry)
    {
        // First check if the entry has an AudioPath already set
        if (!string.IsNullOrEmpty(entry.AudioPath) && File.Exists(entry.AudioPath))
        {
            return entry.AudioPath;
        }
        
        // Look for audio file in voices/{Speaker}/{hash}_{voice}_{speed}.mp3 format
        var config = TtsConfiguration.Load();
        var voicesDir = System.IO.Path.Combine(config.VoicesDirectory, entry.Speaker);
        if (!Directory.Exists(voicesDir)) return null;
        
        // Find any audio file that matches this entry's text hash pattern
        var pattern = $"*{entry.Text.GetHashCode():X8}*.mp3";
        var files = Directory.GetFiles(voicesDir, pattern);
        return files.FirstOrDefault();
    }

    private async Task CleanupOldAudioFiles(DialogueEntry entry)
    {
        try
        {
            // Delete the direct AudioPath if it exists
            if (!string.IsNullOrEmpty(entry.AudioPath) && File.Exists(entry.AudioPath))
            {
                File.Delete(entry.AudioPath);
            }

            // Also search for and delete any files with matching text hash
            var config = TtsConfiguration.Load();
            var voicesDir = System.IO.Path.Combine(config.VoicesDirectory, entry.Speaker);
            if (Directory.Exists(voicesDir))
            {
                var pattern = $"*{entry.Text.GetHashCode():X8}*.mp3";
                var files = Directory.GetFiles(voicesDir, pattern);
                
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }

            // Clear the AudioPath since we deleted the files
            entry.AudioPath = null;
            entry.HasAudio = false;
            
            // Small delay to let file system catch up
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cleaning up old audio files: {ex.Message}");
            throw; // Re-throw so the UI can show the error
        }
    }

    private async void PlayExistingAudio(string audioPath)
    {
        try
        {
            // Use the unified in-app audio player
            await PlayAudioFile(audioPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error playing audio: {ex.Message}", "Playback Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<string?> GenerateTtsAudioForEntry(DialogueEntry entry, SpeakerProfile speaker)
    {
        try
        {
            // Load TTS configuration to get API key
            var ttsConfig = TtsConfiguration.Load();
            if (string.IsNullOrEmpty(ttsConfig.OpenAiApiKey) || ttsConfig.OpenAiApiKey == "YOUR_OPENAI_API_KEY_HERE")
            {
                throw new Exception("OpenAI API key not configured. Please set up your API key in tts_config.json");
            }
            
            // Initialize TTS service
            var ttsService = new TtsService(ttsConfig.OpenAiApiKey, ttsConfig.VoicesDirectory);
            
            // Generate the audio
            var audioFile = await ttsService.GenerateAudioAsync(entry, speaker);
            
            return audioFile;
        }
        catch (Exception ex)
        {
            throw new Exception($"TTS generation failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}