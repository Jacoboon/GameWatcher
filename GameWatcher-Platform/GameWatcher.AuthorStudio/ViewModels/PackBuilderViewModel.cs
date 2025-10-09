using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.IO;
using GameWatcher.AuthorStudio.Services;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Pack Builder - metadata editing, validation, export.
/// </summary>
public partial class PackBuilderViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<PackBuilderViewModel> _logger;
    private readonly PackExporter _packExporter;
    private readonly PackLoader _packLoader;
    private readonly DiscoveryService _discoveryService;
    private readonly SpeakerStore _speakerStore;

    [ObservableProperty]
    private string _packName = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _version = "1.0.0";

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isExporting;

    public PackBuilderViewModel(
        ILogger<PackBuilderViewModel> logger,
        PackExporter packExporter,
        PackLoader packLoader,
        DiscoveryService discoveryService,
        SpeakerStore speakerStore)
    {
        _logger = logger;
        _packExporter = packExporter;
        _packLoader = packLoader;
        _discoveryService = discoveryService;
        _speakerStore = speakerStore;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Pack Builder ViewModel");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportPackAsync()
    {
        if (string.IsNullOrWhiteSpace(PackName) || string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(OutputFolder))
        {
            StatusMessage = "Please fill Pack Name, Display Name, and Output Folder.";
            return;
        }

        try
        {
            IsExporting = true;
            StatusMessage = "Exporting pack...";
            
            _logger.LogInformation("Exporting pack: {PackName} to {OutputFolder}", PackName, OutputFolder);
            
            await _packExporter.ExportAsync(
                OutputFolder,
                PackName,
                DisplayName,
                string.IsNullOrWhiteSpace(Version) ? "1.0.0" : Version,
                _discoveryService.Discovered,
                _speakerStore);

            StatusMessage = $"✓ Pack exported to {OutputFolder}";
            _logger.LogInformation("Pack export completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export pack");
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task OpenPackAsync()
    {
        // Placeholder - actual file dialog handled in view
        _logger.LogInformation("Open pack requested");
    }

    // Called from View with folder path
    public async Task OpenPackAsync(string folderPath)
    {
        try
        {
            _logger.LogInformation("Opening pack from: {FolderPath}", folderPath);
            
            var (name, display, version, entries) = await _packLoader.LoadAsync(folderPath);
            
            PackName = name;
            DisplayName = display;
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
            OutputFolder = folderPath;

            // Load speakers if present
            var speakersPath = Path.Combine(folderPath, "Configuration", "speakers.json");
            if (File.Exists(speakersPath))
            {
                await _speakerStore.ImportAsync(speakersPath);
            }

            // Populate discovery list
            _discoveryService.Discovered.Clear();
            foreach (var entry in entries)
            {
                _discoveryService.Discovered.Add(entry);
            }

            StatusMessage = $"✓ Loaded pack: {display}";
            _logger.LogInformation("Pack loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open pack");
            StatusMessage = $"Open failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }
}
