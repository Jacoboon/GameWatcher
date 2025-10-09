using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using GameWatcher.AuthorStudio.ViewModels;
using GameWatcher.AuthorStudio.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
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

    public MainWindow(
        ILogger<MainWindow> logger,
        MainWindowViewModel viewModel,
        OcrFixesStore ocrFixes)
    {
        _logger = logger;
        _viewModel = viewModel;
        _ocrFixes = ocrFixes;

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
                for (int i = 0; i < originalWords.Length; i++)
                {
                    var orig = originalWords[i].Trim();
                    var edit = editedWords[i].Trim();

                    if (orig != edit && !string.IsNullOrWhiteSpace(orig) && !string.IsNullOrWhiteSpace(edit))
                    {
                        await _ocrFixes.AddFixAsync(orig, edit);
                        _logger.LogInformation("Auto-generated OCR fix: '{From}' -> '{To}'", orig, edit);
                    }
                }
            }
            else
            {
                // Different word counts - user likely added/removed words
                // In this case, we could use more sophisticated diff algorithms (Levenshtein, Myers diff)
                // For now, just log it and skip auto-fix generation
                _logger.LogInformation("Word count mismatch ({OrigCount} vs {EditCount}), skipping auto-fix generation",
                    originalWords.Length, editedWords.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate OCR fixes from edit");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("MainWindow closing");
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
