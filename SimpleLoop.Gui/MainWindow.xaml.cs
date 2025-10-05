using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
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

namespace SimpleLoop.Gui;

/// <summary>
/// SimpleLoop GUI - Main window for dialogue catalog and speaker profile management
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private DialogueCatalog? _dialogueCatalog;
    private SpeakerCatalog? _speakerCatalog;
    private bool _isCapturing = false;
    
    // NEW: CaptureService integration
    private CaptureService? _captureService;
    private DispatcherTimer? _statsUpdateTimer;
    
    // Observable collections for data binding
    public ObservableCollection<DialogueEntry> DialogueEntries { get; } = new();
    public ObservableCollection<SpeakerProfile> SpeakerProfiles { get; } = new();
    
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
        
        InitializeCatalogs();
        InitializeCaptureService();
        SetupUI();
        LoadData();
    }

    private void InitializeCatalogs()
    {
        try
        {
            // Use the SimpleLoop directory for catalog files  
            var currentDir = Directory.GetCurrentDirectory();
            var projectRoot = Directory.GetParent(currentDir)?.FullName ?? "";
            var simpleLoopDir = System.IO.Path.Combine(projectRoot, "SimpleLoop");
            var dialoguePath = System.IO.Path.Combine(simpleLoopDir, "dialogue_catalog.json");
            var speakerPath = System.IO.Path.Combine(simpleLoopDir, "speaker_catalog.json");
            
            Console.WriteLine($"Loading catalogs from:");
            Console.WriteLine($"  Dialogue: {dialoguePath}");
            Console.WriteLine($"  Speaker: {speakerPath}");
            Console.WriteLine($"  Files exist: Dialogue={File.Exists(dialoguePath)}, Speaker={File.Exists(speakerPath)}");
            
            _dialogueCatalog = new DialogueCatalog(dialoguePath);
            _speakerCatalog = new SpeakerCatalog(speakerPath);
            
            Console.WriteLine($"Catalogs created successfully");
            
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
            var cliDirectory = System.IO.Path.GetFullPath("../SimpleLoop");
            var speakerCatalogPath = System.IO.Path.Combine(cliDirectory, "speaker_catalog.json");
            var dialogueCatalogPath = System.IO.Path.Combine(cliDirectory, "dialogue_catalog.json");
            _captureService = new CaptureService(speakerCatalogPath, dialogueCatalogPath);            // Subscribe to capture service events
            _captureService.ProgressReported += OnCaptureProgressReported;
            _captureService.DialogueDetected += OnDialogueDetected;
            
            // Setup stats update timer (every second)
            _statsUpdateTimer = new DispatcherTimer();
            _statsUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _statsUpdateTimer.Tick += UpdateLiveStats;
            
            // Setup log refresh timer (every 2 seconds for file-based logs)
            var logUpdateTimer = new DispatcherTimer();
            logUpdateTimer.Interval = TimeSpan.FromSeconds(2);
            logUpdateTimer.Tick += UpdateLogDisplay;
            logUpdateTimer.Start();
            
            StatusText.Text = "Capture service initialized";
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
        // Setup DataGrid bindings
        DialogueDataGrid.ItemsSource = DialogueEntries;
        SpeakerListBox.ItemsSource = SpeakerProfiles;
        
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
        LoadDialogueEntries();
        LoadSpeakerProfiles();
        UpdateStatistics();
    }

    private void LoadDialogueEntries()
    {
        if (_dialogueCatalog == null) 
        {
            Console.WriteLine("_dialogueCatalog is null!");
            return;
        }
        
        DialogueEntries.Clear();
        
        // Load all dialogue entries (assuming we need to expose the internal dictionary)
        // This may require adding a public method to DialogueCatalog to get all entries
        try
        {
            var entries = _dialogueCatalog.GetAllEntries(); // We'll need to add this method
            Console.WriteLine($"Retrieved {entries.Count()} entries from catalog");
            foreach (var entry in entries)
            {
                DialogueEntries.Add(entry);
            }
            
            Console.WriteLine($"Added {DialogueEntries.Count} entries to UI collection");
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dialogue entries: {ex}");
            StatusText.Text = $"Error loading dialogue: {ex.Message}";
        }
    }    private void LoadSpeakerProfiles()
    {
        if (_speakerCatalog == null) return;

        SpeakerProfiles.Clear();
        SpeakerFilterBox.Items.Clear();
        SpeakerFilterBox.Items.Add("All Speakers");
        
        try
        {
            var speakers = _speakerCatalog.GetAllSpeakers(); // We'll need to add this method
            foreach (var speaker in speakers)
            {
                SpeakerProfiles.Add(speaker);
                SpeakerFilterBox.Items.Add(speaker.Name);
            }
            
            SpeakerFilterBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
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

    private void PreviewVoice_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpeaker == null) return;

        try
        {
            // NOTE: TTS preview implementation pending - requires OpenAI TTS API integration
            // Future implementation will generate and play audio using SelectedSpeaker.TtsVoiceId and TtsSpeed
            var sampleText = "Hello, this is a preview of my voice settings.";
            StatusText.Text = $"Voice preview (simulation) for {SelectedSpeaker.Name}";
            
            // Show voice settings preview until TTS is implemented
            MessageBox.Show($"Voice Preview (Simulation):\nSpeaker: {SelectedSpeaker.Name}\nVoice: {SelectedSpeaker.TtsVoiceId}\nSpeed: {SelectedSpeaker.TtsSpeed}\nSample: \"{sampleText}\"", 
                           "Voice Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating voice preview: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            if (logLines.Length == 0) return;
            
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
    
    protected override void OnClosing(CancelEventArgs e)
    {
        // Stop capture service when closing
        if (_captureService != null && _captureService.IsRunning)
        {
            var task = _captureService.StopCaptureAsync();
            task.Wait(TimeSpan.FromSeconds(5)); // Wait up to 5 seconds for graceful shutdown
        }
        
        _captureService?.Dispose();
        base.OnClosing(e);
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