using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using GameWatcher.Runtime.Services;
using GameWatcher.Engine.Packs;

namespace GameWatcher.Studio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IGameDetectionService _gameDetection;
    private readonly IProcessingPipeline _pipeline;
    private readonly Runtime.Services.IPackManager _packManager;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private PackManagerViewModel _packManagerViewModel;

    [ObservableProperty]
    private ActivityMonitorViewModel _activityMonitorViewModel;

    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _currentGame = "No game detected";

    [ObservableProperty]
    private string _currentPack = "No pack loaded";

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IGameDetectionService gameDetection,
        IProcessingPipeline pipeline,
        Runtime.Services.IPackManager packManager,
        PackManagerViewModel packManagerViewModel,
        ActivityMonitorViewModel activityMonitorViewModel,
        SettingsViewModel settingsViewModel)
    {
        _logger = logger;
        _gameDetection = gameDetection;
        _pipeline = pipeline;
        _packManager = packManager;
        _packManagerViewModel = packManagerViewModel;
        _activityMonitorViewModel = activityMonitorViewModel;
        _settingsViewModel = settingsViewModel;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _refreshTimer.Tick += RefreshStatus;

        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            _logger.LogInformation("Initializing MainWindow ViewModel");
            
            // Game detection will be handled by the refresh timer

            // Initialize child view models
            await PackManagerViewModel.InitializeAsync();
            await ActivityMonitorViewModel.InitializeAsync();
            await SettingsViewModel.InitializeAsync();

            StatusText = "Initialized successfully";
            _logger.LogInformation("MainWindow ViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MainWindow ViewModel");
            StatusText = $"Initialization failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        try
        {
            if (IsRunning)
                return;

            _logger.LogInformation("Starting GameWatcher monitoring");
            
            await _pipeline.StartAsync();

            IsRunning = true;
            StatusText = "Monitoring active";
            _refreshTimer.Start();

            _logger.LogInformation("GameWatcher monitoring started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start monitoring");
            StatusText = $"Start failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try
        {
            if (!IsRunning)
                return;

            _logger.LogInformation("Stopping GameWatcher monitoring");

            _refreshTimer.Stop();
            await _pipeline.StopAsync();

            IsRunning = false;
            StatusText = "Monitoring stopped";
            CurrentGame = "No game detected";

            _logger.LogInformation("GameWatcher monitoring stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop monitoring");
            StatusText = $"Stop failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshPacksAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing game packs");
            await PackManagerViewModel.RefreshAsync();
            StatusText = "Packs refreshed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh packs");
            StatusText = $"Refresh failed: {ex.Message}";
        }
    }

    private async void RefreshStatus(object? sender, EventArgs e)
    {
        try
        {
            // Update current pack info
            var activePack = _packManager.GetActivePack();
            CurrentPack = activePack?.Manifest.Name ?? "No pack loaded";

            // Check for active game
            var detectedGame = await _gameDetection.DetectActiveGameAsync();
            CurrentGame = detectedGame?.ProcessName ?? "No game detected";

            // Update activity monitor
            ActivityMonitorViewModel.RefreshMetrics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing status");
        }
    }



    public void Dispose()
    {
        _refreshTimer?.Stop();
        PackManagerViewModel?.Dispose();
        ActivityMonitorViewModel?.Dispose();
        SettingsViewModel?.Dispose();
    }
}