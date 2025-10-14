using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GameWatcher.AuthorStudio.Services;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Main window coordinator for AuthorStudio.
/// Orchestrates child ViewModels for Discovery, Speakers, Voice Lab, Pack Builder, and Settings.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly UserSettingsStore _userSettings;

    [ObservableProperty]
    private DiscoveryViewModel _discoveryViewModel;

    [ObservableProperty]
    private DiscoveryV2ViewModel _discoveryV2ViewModel;

    [ObservableProperty]
    private SpeakersViewModel _speakersViewModel;

    [ObservableProperty]
    private VoiceLabViewModel _voiceLabViewModel;

    [ObservableProperty]
    private PackBuilderViewModel _packBuilderViewModel;

    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private OverridesViewModel _overridesViewModel;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _ttsStatusText = "Checking TTS...";

    [ObservableProperty]
    private bool _isTtsAvailable;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        UserSettingsStore userSettings,
        DiscoveryViewModel discoveryViewModel,
        DiscoveryV2ViewModel discoveryV2ViewModel,
        SpeakersViewModel speakersViewModel,
        VoiceLabViewModel voiceLabViewModel,
        PackBuilderViewModel packBuilderViewModel,
        SettingsViewModel settingsViewModel,
        OverridesViewModel overridesViewModel)
    {
        _logger = logger;
        _userSettings = userSettings;
        _discoveryViewModel = discoveryViewModel;
        _discoveryV2ViewModel = discoveryV2ViewModel;
        _speakersViewModel = speakersViewModel;
        _voiceLabViewModel = voiceLabViewModel;
        _packBuilderViewModel = packBuilderViewModel;
        _settingsViewModel = settingsViewModel;
        _overridesViewModel = overridesViewModel;

        // Subscribe to pack changes to update overrides
        _packBuilderViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PackBuilderViewModel.OutputFolder))
            {
                var packPath = _packBuilderViewModel.OutputFolder;
                if (!string.IsNullOrEmpty(packPath))
                {
                    _overridesViewModel.SetPackDirectory(packPath);
                }
            }
        };

        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            _logger.LogInformation("Initializing MainWindow ViewModel");

            // Load user settings first
            await _userSettings.LoadAsync();

            // Initialize child view models
            await DiscoveryViewModel.InitializeAsync();
            await SpeakersViewModel.InitializeAsync();
            await VoiceLabViewModel.InitializeAsync();
            await PackBuilderViewModel.InitializeAsync(); // This will auto-load last pack
            await SettingsViewModel.InitializeAsync();

            // Update TTS status
            UpdateTtsStatus();

            StatusText = "Author Studio initialized";
            _logger.LogInformation("MainWindow ViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MainWindow ViewModel");
            StatusText = $"Initialization failed: {ex.Message}";
        }
    }

    private void UpdateTtsStatus()
    {
        // Check if TTS key is configured (will be wired to SettingsViewModel)
        var hasKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GWS_OPENAI_API_KEY"));
        IsTtsAvailable = hasKey;
        TtsStatusText = hasKey ? "TTS ready" : "TTS unavailable: Configure key";
    }

    [RelayCommand]
    private void RefreshTtsStatus()
    {
        UpdateTtsStatus();
        _logger.LogInformation("TTS status refreshed: {Status}", TtsStatusText);
    }

    public void Dispose()
    {
        DiscoveryViewModel?.Dispose();
        SpeakersViewModel?.Dispose();
        VoiceLabViewModel?.Dispose();
        PackBuilderViewModel?.Dispose();
        SettingsViewModel?.Dispose();
    }
}
