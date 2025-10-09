using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.IO;
using GameWatcher.AuthorStudio.Services;
using System.Linq;
using System.Collections.ObjectModel;

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
    private readonly UserSettingsStore _userSettings;
    private readonly DiscoveryViewModel _discoveryViewModel;
    private readonly SettingsViewModel _settingsViewModel;

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

    [ObservableProperty]
    private ObservableCollection<string> _recentPacks = new();

    public PackBuilderViewModel(
        ILogger<PackBuilderViewModel> logger,
        PackExporter packExporter,
        PackLoader packLoader,
        DiscoveryService discoveryService,
        SpeakerStore speakerStore,
        UserSettingsStore userSettings,
        DiscoveryViewModel discoveryViewModel,
        SettingsViewModel settingsViewModel)
    {
        _logger = logger;
        _packExporter = packExporter;
        _packLoader = packLoader;
        _discoveryService = discoveryService;
        _speakerStore = speakerStore;
        _userSettings = userSettings;
        _discoveryViewModel = discoveryViewModel;
        _settingsViewModel = settingsViewModel;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Pack Builder ViewModel");
        
        // Load recent packs list
        RecentPacks = new ObservableCollection<string>(_userSettings.Settings.RecentPacks);
        
        // Auto-load last pack if enabled
        if (_userSettings.Settings.AutoLoadLastPack && !string.IsNullOrWhiteSpace(_userSettings.Settings.LastPackPath))
        {
            if (Directory.Exists(_userSettings.Settings.LastPackPath))
            {
                _logger.LogInformation("Auto-loading last pack: {Path}", _userSettings.Settings.LastPackPath);
                await OpenPackAsync(_userSettings.Settings.LastPackPath);
            }
            else
            {
                _logger.LogWarning("Last pack path no longer exists: {Path}", _userSettings.Settings.LastPackPath);
            }
        }
        
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
            
            // Load the pack's session data first (Discovered and Accepted lists)
            await _discoveryViewModel.LoadPackSessionAsync(folderPath);
            
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

            // Merge pack entries with existing discoveries (preserve session captures)
            // Only add entries that don't already exist in the discovery list
            var existingTexts = new HashSet<string>(_discoveryService.Discovered.Select(d => d.Text), StringComparer.Ordinal);
            int addedCount = 0;
            
            foreach (var entry in entries)
            {
                if (!existingTexts.Contains(entry.Text))
                {
                    _discoveryService.Discovered.Add(entry);
                    addedCount++;
                }
            }

            var sessionCount = _discoveryService.Discovered.Count - addedCount;
            StatusMessage = $"✓ Loaded pack: {display} ({addedCount} from pack, {sessionCount} from session)";
            _logger.LogInformation("Pack loaded successfully: {AddedCount} entries from pack, {SessionCount} preserved from session", 
                addedCount, sessionCount);

            // Load OCR fixes into Settings view
            _settingsViewModel.LoadOcrFixes();

            // Save to user settings and update recent list
            await _userSettings.SetLastPackAsync(folderPath);
            RecentPacks = new ObservableCollection<string>(_userSettings.Settings.RecentPacks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open pack");
            StatusMessage = $"Open failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenRecentPackAsync(string packPath)
    {
        if (!string.IsNullOrWhiteSpace(packPath) && Directory.Exists(packPath))
        {
            await OpenPackAsync(packPath);
        }
        else
        {
            StatusMessage = $"Pack path no longer exists: {packPath}";
            _logger.LogWarning("Recent pack path not found: {Path}", packPath);
        }
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }
}
