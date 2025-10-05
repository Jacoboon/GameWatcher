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
    private DispatcherTimer? _captureTimer;
    private bool _isCapturing = false;
    
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

    private void StartCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturing) return;

        try
        {
            // TODO: Start SimpleLoop capture engine in background
            _isCapturing = true;
            StartCaptureButton.IsEnabled = false;
            StopCaptureButton.IsEnabled = true;
            StatusText.Text = "Capturing...";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
            
            LiveLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Starting SimpleLoop capture engine...\n");
            LiveLogTextBox.ScrollToEnd();
            
            // Start performance monitoring timer
            _captureTimer = new DispatcherTimer();
            _captureTimer.Interval = TimeSpan.FromSeconds(1);
            _captureTimer.Tick += UpdateLiveStats;
            _captureTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start capture: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
            ResetCaptureUI();
        }
    }

    private void StopCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCapturing) return;

        try
        {
            // TODO: Stop SimpleLoop capture engine
            ResetCaptureUI();
            
            LiveLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Capture stopped by user\n");
            LiveLogTextBox.ScrollToEnd();
            
            // Refresh data after capture session
            LoadData();
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
        
        _captureTimer?.Stop();
    }

    private void UpdateLiveStats(object? sender, EventArgs e)
    {
        if (!_isCapturing) return;

        // TODO: Get real stats from SimpleLoop engine
        // For now, simulate some values
        var runtime = DateTime.Now.Subtract(DateTime.Now.Date);
        RuntimeText.Text = $"{runtime:mm\\:ss}";
        
        // These would be real values from the capture engine
        FpsText.Text = "14.9";
        FrameCountText.Text = "1234";
        ProcessedText.Text = "67";
        TextboxFoundText.Text = "12";
        PerformanceText.Text = "2.1ms";
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
                // TODO: Remove from DialogueCatalog as well
            }
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
            // TODO: Remove from SpeakerCatalog as well
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

        // TODO: Save to SpeakerCatalog
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
            // TODO: Generate and play a preview TTS sample
            var sampleText = "Hello, this is a preview of my voice settings.";
            StatusText.Text = $"Playing voice preview for {SelectedSpeaker.Name}...";
            
            // For now, just show a message
            MessageBox.Show($"Voice Preview:\nSpeaker: {SelectedSpeaker.Name}\nVoice: {SelectedSpeaker.TtsVoiceId}\nSpeed: {SelectedSpeaker.TtsSpeed}\nText: \"{sampleText}\"", 
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

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}