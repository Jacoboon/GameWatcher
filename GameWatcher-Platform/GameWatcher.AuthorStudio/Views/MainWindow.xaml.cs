using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using GameWatcher.AuthorStudio.ViewModels;
using GameWatcher.AuthorStudio.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using MessageBox = System.Windows.MessageBox;

namespace GameWatcher.AuthorStudio.Views;

/// <summary>
/// Main window for GameWatcher Author Studio.
/// Uses MVVM pattern with MainWindowViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly MainWindowViewModel _viewModel;
    private readonly OcrFixesStore _ocrFixes;
    private readonly AudioStore _audioStore;
    private readonly OpenAiTtsService _ttsService;
    private readonly SpeakerStore _speakerStore;
    private readonly AuthorSettingsService _settingsService;

    public MainWindow(
        ILogger<MainWindow> logger,
        MainWindowViewModel viewModel,
        OcrFixesStore ocrFixes,
        AudioStore audioStore,
        OpenAiTtsService ttsService,
        SpeakerStore speakerStore,
        AuthorSettingsService settingsService)
    {
        _logger = logger;
        _viewModel = viewModel;
        _ocrFixes = ocrFixes;
        _audioStore = audioStore;
        _ttsService = ttsService;
        _speakerStore = speakerStore;
        _settingsService = settingsService;

        InitializeComponent();
        
        DataContext = _viewModel;
        
        _logger.LogInformation("MainWindow initialized");
    }

    // Event handlers for file dialogs (MVVM-friendly approach)
    private async void OpenPack_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Pack Folder (contains Configuration and Catalog)"
        };
        
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            await _viewModel.PackBuilderViewModel.OpenPackAsync(dialog.SelectedPath);
        }
    }

    private async void ImportSpeakers_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Speakers JSON|speakers.json;*.json|All Files|*.*",
            Title = "Import Speakers"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await _viewModel.SpeakersViewModel.ImportSpeakersFromFileAsync(dialog.FileName);
        }
    }

    private async void ExportSpeakers_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "speakers.json",
            Filter = "Speakers JSON|*.json|All Files|*.*",
            Title = "Export Speakers"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await _viewModel.SpeakersViewModel.ExportSpeakersToFileAsync(dialog.FileName);
        }
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(logsDir);
            
            // Open in Windows Explorer
            Process.Start("explorer.exe", logsDir);
            
            _logger.LogInformation("Opened logs folder: {LogsDir}", logsDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder");
            MessageBox.Show($"Failed to open logs folder: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles when user finishes editing a cell in the Discovery grid.
    /// Compares original OCR text with edited text and auto-generates OCR fix rules.
    /// </summary>
    private async void DiscoveryGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // Only process if they edited the Text column
        if (e.Column.Header?.ToString() != "Dialogue Text") return;
        if (e.EditAction == DataGridEditAction.Cancel) return;

        var entry = e.Row.Item as PendingDialogueEntry;
        if (entry == null) return;

        // Get the new text from the editing element
        var textBox = e.EditingElement as System.Windows.Controls.TextBox;
        if (textBox == null) return;

        var editedText = textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(editedText)) return;

        // Compare against original OCR text
        var originalText = entry.OriginalOcrText?.Trim();
        if (string.IsNullOrWhiteSpace(originalText)) return;
        if (editedText == originalText) return; // No changes made

        _logger.LogInformation("User edited dialogue: '{Original}' -> '{Edited}'", originalText, editedText);

        // Extract word-level differences and auto-generate fixes
        await GenerateOcrFixesAsync(originalText, editedText);
    }

    /// <summary>
    /// Compares original vs edited text, finds word-level differences, and generates OCR fix rules.
    /// </summary>
    private async Task GenerateOcrFixesAsync(string original, string edited)
    {
        try
        {
            // Tokenize both strings by whitespace
            var originalWords = original.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var editedWords = edited.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Simple approach: if same word count, do 1-to-1 mapping
            // This handles cases like "Cornella" -> "Corneria", "l" -> "I", etc.
            if (originalWords.Length == editedWords.Length)
            {
                var fixesGenerated = 0;
                var fixDescriptions = new List<string>();

                for (int i = 0; i < originalWords.Length; i++)
                {
                    var orig = originalWords[i].Trim();
                    var edit = editedWords[i].Trim();

                    if (orig != edit && !string.IsNullOrWhiteSpace(orig) && !string.IsNullOrWhiteSpace(edit))
                    {
                        await _ocrFixes.AddFixAsync(orig, edit);
                        _logger.LogInformation("Auto-generated OCR fix: '{From}' -> '{To}'", orig, edit);
                        
                        fixesGenerated++;
                        fixDescriptions.Add($"'{orig}' → '{edit}'");
                    }
                }

                // Show feedback in Activity Log
                if (fixesGenerated > 0)
                {
                    AddActivityLogNotification($"✓ Learned {fixesGenerated} OCR correction{(fixesGenerated > 1 ? "s" : "")}: {string.Join(", ", fixDescriptions)}");
                }
            }
            else
            {
                // Different word counts - user likely added/removed words
                // In this case, we could use more sophisticated diff algorithms (Levenshtein, Myers diff)
                // For now, just log it and skip auto-fix generation
                _logger.LogInformation("Word count mismatch ({OrigCount} vs {EditCount}), skipping auto-fix generation",
                    originalWords.Length, editedWords.Length);
                
                AddActivityLogNotification($"ℹ️ Complex edit detected (word count changed). Manual OCR fixes may be needed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate OCR fixes from edit");
            AddActivityLogNotification($"⚠️ Failed to generate OCR fixes: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a notification message to the Discovery Activity Log with visual feedback.
    /// </summary>
    private void AddActivityLogNotification(string message)
    {
        // Add to Discovery ViewModel's log lines
        if (_viewModel?.DiscoveryViewModel?.LogLines != null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _viewModel.DiscoveryViewModel.LogLines.Add($"[{timestamp}] {message}");
            
            // Scroll to bottom of log (if we can access the ScrollViewer)
            // For now, just adding to collection will auto-scroll in most cases
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("MainWindow closing");
        _viewModel?.Dispose();
        base.OnClosed(e);
    }

    private void AudioFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedValue is string format)
        {
            _viewModel?.SettingsViewModel?.UpdateAudioFormatCommand?.Execute(format);
        }
    }

    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FolderBrowserDialog
        {
            Description = "Select Output Folder for Pack Export",
            SelectedPath = _viewModel.PackBuilderViewModel.OutputFolder
        };
        
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _viewModel.PackBuilderViewModel.OutputFolder = dialog.SelectedPath;
        }
    }

    private async void AttachAudioFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PendingDialogueEntry entry)
            return;

        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // Use AudioStore to copy file to pack and update manifest
                var relativePath = await _audioStore.SetAudioAsync(entry.Text, openFileDialog.FileName, isGenerated: false);
                entry.AudioPath = relativePath;
                entry.TtsVoice = null; // Mark as user-imported
                
                _logger.LogInformation("Attached audio file: {Path} for dialogue: {Text}", relativePath, entry.Text);
                AddActivityLogNotification($"📎 Attached: {Path.GetFileName(openFileDialog.FileName)} → \"{TruncateForLog(entry.Text)}\"");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to attach audio file");
                MessageBox.Show($"Failed to attach audio file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void GenerateTts_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PendingDialogueEntry entry)
            return;

        if (!_ttsService.IsConfigured)
        {
            MessageBox.Show("OpenAI API key not configured. Please set up your API key in Settings.", 
                "TTS Not Configured", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Find speaker profile to get voice
        var speaker = _speakerStore.Speakers.FirstOrDefault(s => s.Id == entry.SpeakerId);
        if (speaker == null)
        {
            MessageBox.Show("Please assign a speaker to this dialogue entry first.", 
                "Speaker Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var voiceName = speaker.Voice ?? "alloy";
        var format = _settingsService.Settings.AudioFormat ?? "mp3";

        try
        {
            button.IsEnabled = false;
            var originalContent = button.Content;
            button.Content = "⏳";

            AddActivityLogNotification($"🎤 Generating TTS for: \"{TruncateForLog(entry.Text)}\"");

            // Generate to temp file
            var tempFile = Path.GetTempFileName();
            var tempOutputPath = Path.ChangeExtension(tempFile, format);
            
            var success = await _ttsService.GenerateAsync(entry.Text, voiceName, 1.0, format, tempOutputPath);
            
            if (success && File.Exists(tempOutputPath))
            {
                // Use AudioStore to move file to pack and update manifest
                var relativePath = await _audioStore.SetAudioAsync(entry.Text, tempOutputPath, isGenerated: true, voiceName: voiceName);
                entry.AudioPath = relativePath;
                entry.TtsVoice = voiceName;
                
                _logger.LogInformation("Generated TTS audio: {Voice} for dialogue: {Text}", voiceName, entry.Text);
                AddActivityLogNotification($"✓ Generated TTS ({voiceName}): \"{TruncateForLog(entry.Text)}\"");
            }
            else
            {
                _logger.LogError("TTS generation failed for: {Text}", entry.Text);
                AddActivityLogNotification($"❌ TTS generation failed: \"{TruncateForLog(entry.Text)}\"");
                MessageBox.Show("Failed to generate TTS audio. Check logs for details.", 
                    "Generation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            button.Content = originalContent;
            button.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during TTS generation");
            MessageBox.Show($"TTS generation error:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            button.IsEnabled = true;
        }
    }

    private void RegenerateAudio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PendingDialogueEntry entry)
            return;

        _logger.LogInformation("Regenerating audio for: {Text}", entry.Text);
        
        // TODO: Wire up to TTS service to actually regenerate
        MessageBox.Show($"Audio regeneration will be implemented in future update.\n\nEntry: {entry.Text}", 
            "Regenerate Audio", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteDiscoveryEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PendingDialogueEntry entry)
            return;

        var result = MessageBox.Show($"Delete this entry?\n\n\"{entry.Text}\"", 
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DiscoveryViewModel.DiscoveredDialogue.Remove(entry);
            _logger.LogInformation("Deleted discovery entry: {Text}", entry.Text);
            
            // User feedback in Activity Log
            AddActivityLogNotification($"❌ Deleted: \"{TruncateForLog(entry.Text)}\"");
        }
    }

    private void AcceptDiscoveryEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PendingDialogueEntry entry)
            return;

        _viewModel.DiscoveryViewModel.AcceptEntry(entry);
        _logger.LogInformation("Accepted discovery entry: {Text}", entry.Text);
        
        // User feedback in Activity Log
        AddActivityLogNotification($"✓ Accepted: \"{TruncateForLog(entry.Text)}\"");
    }

    private void DemoteToDiscovery_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PendingDialogueEntry entry)
            return;

        _viewModel.DiscoveryViewModel.DemoteEntry(entry);
        _logger.LogInformation("Demoted entry back to discovery: {Text}", entry.Text);
        
        // User feedback in Activity Log
        AddActivityLogNotification($"ℹ️ Demoted back to Discovery: \"{TruncateForLog(entry.Text)}\"");
    }

    /// <summary>
    /// Truncates long text for display in Activity Log.
    /// </summary>
    private string TruncateForLog(string text, int maxLength = 60)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength) + "...";
    }
}
