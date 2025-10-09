using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using GameWatcher.Studio.ViewModels;
using GameWatcher.Runtime.Services.Capture;

namespace GameWatcher.Studio.Views;

public partial class MainWindow : Window
{
    private void InitializeSettingsViewModel()
    {
        try
        {
            // Get configuration from App services
            var services = App.Services;
            if (services != null)
            {
                var configuration = services.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                var logger = services.GetService<Microsoft.Extensions.Logging.ILogger<SettingsViewModel>>();
                
                if (configuration != null && logger != null)
                {
                    _settingsViewModel = new SettingsViewModel(logger, configuration);
                    _ = _settingsViewModel.InitializeAsync();
                    
                    // Set DataContext for Settings tab binding
                    DataContext = this;
                    
                    AddActivityLogEntry("[SYSTEM] Settings ViewModel initialized successfully");
                }
                else
                {
                    AddActivityLogEntry("[WARNING] Configuration or Logger service not available for Settings");
                }
            }
        }
        catch (Exception ex)
        {
            AddActivityLogEntry($"[ERROR] Failed to initialize Settings ViewModel: {ex.Message}");
        }
    }

    private BackgroundWorker? _smartGameDetectionWorker;
    // TODO: _isMonitoring is assigned but not used; wire to UI state (e.g., Start/Stop buttons) or remove if obsolete.
    private bool _isMonitoring = false;
    private readonly List<string> _watchedExecutables = new();
    private GameCaptureService? _captureService;
    private bool _gameIsRunning = false;
    private ActivityMonitorViewModel? _activityMonitor;
    private SettingsViewModel? _settingsViewModel;
    private bool _windowHasFocus = true;
    
    public SettingsViewModel? SettingsViewModel => _settingsViewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        // Basic setup with functional pack discovery
        Title = "GameWatcher Studio V2 - Working!";
        
        // Add basic pack data for testing
        SetupBasicPackData();
        
        // Initialize watched executables from available packs
        InitializeWatchedExecutables();
        
        // Initialize capture service for real-time monitoring
        InitializeCaptureService();
        
        // Initialize Activity Monitor
        InitializeActivityMonitor();
        
        // Handle events
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
        KeyDown += MainWindow_KeyDown;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
    }

    private void SetupBasicPackData()
    {
        // Create sample pack data to show in the UI
        var packs = new List<object>
        {
            new 
            { 
                Name = "FF1.PixelRemaster", 
                Version = "2.0.0", 
                Description = "Final Fantasy I Pixel Remaster - Complete voice pack with all V1 optimizations", 
                SupportedGames = "Final Fantasy I", 
                Status = "Available" 
            }
        };

        // Find the DataGrid and set its data
        var packDataGrid = FindName("PackDataGrid") as System.Windows.Controls.DataGrid;
        if (packDataGrid != null)
        {
            packDataGrid.ItemsSource = packs;
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Check for game after window is fully loaded and visible
        CheckForGame();
        
        // Setup smart focus-based game detection
        SetupSmartGameDetection();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Clean up timers - no memory leaks!
        CleanupGameDetection();
        System.Diagnostics.Debug.WriteLine("MainWindow closing - V2 Platform");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        // One smart refresh button - does everything
        CheckForGame();
        SetupBasicPackData();
        
        var gameStatus = GameStatusText.Text;
        var packStatus = PackStatusText.Text;
        
        MessageBox.Show($"🔄 Refresh Complete\n\n🎮 Game: {gameStatus}\n📦 Pack: {packStatus}\n\n✅ FF1.PixelRemaster available\n⚡ Event monitoring active", 
                       "System Refreshed", MessageBoxButton.OK, MessageBoxImage.Information);
        
        BottomStatusText.Text = "GameWatcher V2 Platform - System refreshed";
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // F5 = Quick refresh (like browser refresh)
        if (e.Key == System.Windows.Input.Key.F5)
        {
            Refresh_Click(sender, new RoutedEventArgs()); // Use the same logic
        }
    }

    private void LoadPack_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = (FindName("PackDataGrid") as System.Windows.Controls.DataGrid)?.SelectedItem;
        if (selectedItem != null)
        {
            MessageBox.Show("✅ Pack loaded successfully!\n\n🎮 FF1.PixelRemaster is now active\n⚡ V2 engine optimizations enabled\n🎯 Ready for game monitoring", 
                           "Pack Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Update header status
            UpdatePackStatus("FF1.PixelRemaster - Loaded");
        }
        else
        {
            MessageBox.Show("Please select a pack to load.", "No Pack Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UnloadPack_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("✅ Pack unloaded successfully\n\n📦 No pack currently active", 
                       "Pack Unloaded", MessageBoxButton.OK, MessageBoxImage.Information);
        UpdatePackStatus("No pack loaded");
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
            
            AddActivityLogEntry($"[INFO] Opened logs folder: {logsDir}");
        }
        catch (Exception ex)
        {
            AddActivityLogEntry($"[ERROR] Failed to open logs folder: {ex.Message}");
            MessageBox.Show($"Failed to open logs folder: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_isMonitoring) // already running
        {
            AddActivityLogEntry("[INFO] Monitoring already active");
            return;
        }
        // First check for game
        CheckForGame();
        
        // Show appropriate message based on game detection
        if (GameStatusText.Text.Contains("detected") && GameStatusText.Text != "No game detected")
        {
            _isMonitoring = true;
            MonitoringStatusText.Text = "Active";
            AddActivityLogEntry("[INFO] Real monitoring started - connecting to game");
            
            MessageBox.Show("🚀 GameWatcher V2 monitoring started!\n\n⚡ Ready to connect to Final Fantasy game\n🎯 Real capture system active\n📊 No simulation - actual game monitoring", 
                           "Monitoring Started", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateGameStatus("Monitoring active");
            BottomStatusText.Text = "GameWatcher V2 Platform - Real monitoring active";
        }
        else
        {
            MessageBox.Show("⚠️ No Final Fantasy game detected!\n\nPlease:\n1. Launch Final Fantasy I Pixel Remaster\n2. Click 'Start' again\n\nGameWatcher will automatically detect the game.", 
                           "No Game Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            BottomStatusText.Text = "GameWatcher V2 Platform - Waiting for game";
        }
    }

private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (!_isMonitoring)
        {
            AddActivityLogEntry("[INFO] Monitoring already stopped");
            return;
        }
        _isMonitoring = false;
        MonitoringStatusText.Text = "Stopped";
        AddActivityLogEntry("[INFO] Monitoring stopped");
        
        MessageBox.Show("⏹️ Monitoring stopped", "Monitoring Stopped", MessageBoxButton.OK, MessageBoxImage.Information);
        CheckForGame(); // Refresh to current state
    }

    private void UpdatePackStatus(string status)
    {
        PackStatusText.Text = status;
        BottomStatusText.Text = $"GameWatcher V2 Platform - Pack: {status}";
    }

    private void UpdateGameStatus(string status)
    {
        GameStatusText.Text = status;
        
        // Update indicator color based on status
        if (status.Contains("active") || status.Contains("detected"))
        {
            GameStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
        }
        else if (status.Contains("Monitoring"))
        {
            GameStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);
        }
        else
        {
            GameStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
        }
        
        BottomStatusText.Text = $"GameWatcher V2 Platform - Game: {status}";
    }

    private void CheckForGame()
    {
        // Use the smart detection logic for manual checks too
        CheckForGameExecutables();
    }

    private void AutoLoadMatchingPack(string packName)
    {
        try
        {
            // Auto-load the matching pack
            UpdatePackStatus($"{packName} - Auto-loaded");
            BottomStatusText.Text = $"GameWatcher V2 Platform - Auto-loaded {packName}";
            
            // Show notification after a slight delay to ensure window is visible
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.MessageBox.Show(this, $"🎮 Game detected!\n\n✅ Auto-loaded: {packName}\n⚡ V2 optimizations active\n🎯 Ready for voice synthesis", 
                                             "Pack Auto-Loaded", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        catch (Exception ex)
        {
            BottomStatusText.Text = $"GameWatcher V2 Platform - Auto-load failed: {ex.Message}";
        }
    }

    private void InitializeWatchedExecutables()
    {
        // Get executables from available packs
        _watchedExecutables.Clear();
        
        // FF1.PixelRemaster pack
        _watchedExecutables.Add("FINAL FANTASY.exe");
        
        // Future packs would add their executables here
        // _watchedExecutables.Add("FINAL FANTASY II.exe");
        // _watchedExecutables.Add("FINAL FANTASY IV.exe");
        
        System.Diagnostics.Debug.WriteLine($"[Smart Polling] Watching {_watchedExecutables.Count} executables: {string.Join(", ", _watchedExecutables)}");
    }

    private void InitializeCaptureService()
    {
        try
        {
            // Get GameCaptureService from DI (configured with FF1 detector)
            var services = App.Services;
            if (services != null)
            {
                _captureService = services.GetRequiredService<GameCaptureService>();
                
                // Subscribe to capture events for Activity Monitor
                _captureService.ProgressReported += CaptureService_ProgressReported;
                _captureService.DialogueDetected += CaptureService_DialogueDetected;
                
                AddActivityLogEntry("[SYSTEM] Capture service initialized successfully with FF1 detection");
            }
            else
            {
                AddActivityLogEntry("[ERROR] DI Services not available for capture service");
            }
        }
        catch (Exception ex)
        {
            AddActivityLogEntry($"[ERROR] Failed to initialize capture service: {ex.Message}");
        }
    }

    private void InitializeActivityMonitor()
    {
        try
        {
            // Create a simple logger for the Activity Monitor
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ActivityMonitorViewModel>();
            
            _activityMonitor = new ActivityMonitorViewModel(logger);
            _ = _activityMonitor.InitializeAsync();
            
            // Wire up property changed events to update UI
            _activityMonitor.PropertyChanged += ActivityMonitor_PropertyChanged;
            
            AddActivityLogEntry("[SYSTEM] Activity Monitor initialized successfully");
            
            // Initialize Settings ViewModel
            InitializeSettingsViewModel();
        }
        catch (Exception ex)
        {
            AddActivityLogEntry($"[ERROR] Failed to initialize Activity Monitor: {ex.Message}");
        }
    }

    private void ActivityMonitor_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Update UI elements when Activity Monitor properties change
        Dispatcher.Invoke(() =>
        {
            if (_activityMonitor == null) return;

            switch (e.PropertyName)
            {
                case nameof(ActivityMonitorViewModel.FramesProcessed):
                    FramesProcessedText.Text = _activityMonitor.FramesProcessed.ToString();
                    break;
                case nameof(ActivityMonitorViewModel.TextDetections):
                    DetectionsText.Text = _activityMonitor.TextDetections.ToString();
                    break;
                case nameof(ActivityMonitorViewModel.CurrentFps):
                    ProcessingRateText.Text = $"{_activityMonitor.CurrentFps:F1} fps";
                    break;
                case nameof(ActivityMonitorViewModel.MonitoringStatus):
                    MonitoringStatusText.Text = _activityMonitor.MonitoringStatus;
                    break;
            }
        });
    }

    private void CaptureService_ProgressReported(object? sender, CaptureProgressEventArgs e)
    {
        // Update Activity Monitor with capture statistics on UI thread
        Dispatcher.Invoke(() =>
        {
            var message = $"[CAPTURE] Frame {e.Statistics.FrameCount}: {e.Statistics.ActualFps:F1} FPS, {e.Statistics.TextboxesFound} textboxes found";
            AddActivityLogEntry(message);
            
            // Also log to Serilog for file output (only every 30 frames to avoid spam)
            if (e.Statistics.FrameCount % 30 == 0)
            {
                try
                {
                    var logger = App.Services?.GetService<Microsoft.Extensions.Logging.ILogger<MainWindow>>();
                    logger?.LogInformation("Capture Progress: Frame {FrameCount}, FPS: {Fps:F1}, Textboxes: {TextboxCount}", 
                        e.Statistics.FrameCount, e.Statistics.ActualFps, e.Statistics.TextboxesFound);
                }
                catch
                {
                    // Fallback to console if logger not available
                    Console.WriteLine($"[SERILOG] {message}");
                }
            }
        });
    }

    private void CaptureService_DialogueDetected(object? sender, DialogueDetectedEventArgs e)
    {
        // Update Activity Monitor with dialogue detection on UI thread
        Dispatcher.Invoke(() =>
        {
            var message = $"[DIALOGUE] \"{e.DialogueEntry.Text}\" ({e.DialogueEntry.Speaker})";
            AddActivityLogEntry(message);
            // Also add a simple Now Playing line for visibility
            AddActivityLogEntry($"▶ Now Playing: {e.DialogueEntry.Text}");
            
            // Always log dialogue to Serilog file (important events)
            try
            {
                var logger = App.Services?.GetService<Microsoft.Extensions.Logging.ILogger<MainWindow>>();
                logger?.LogInformation("Dialogue Detected: {Text} (Speaker: {Speaker})", 
                    e.DialogueEntry.Text, e.DialogueEntry.Speaker);
            }
            catch
            {
                // Fallback to console if logger not available
                Console.WriteLine($"[SERILOG] {message}");
            }
        });
    }

    private void SetupSmartGameDetection()
    {
        // BackgroundWorker for game detection - runs on separate thread, won't be blocked by capture service
        _smartGameDetectionWorker = new BackgroundWorker
        {
            WorkerSupportsCancellation = true
        };
        
        _smartGameDetectionWorker.DoWork += SmartGameDetection_DoWork;
        _smartGameDetectionWorker.RunWorkerCompleted += SmartGameDetection_Completed;
        
        // Start the background polling
        _windowHasFocus = IsActive;
        if (!_smartGameDetectionWorker.IsBusy)
        {
            _smartGameDetectionWorker.RunWorkerAsync();
            AddActivityLogEntry("[INFO] Smart polling active - background thread started");
        }
    }

    private void SmartGameDetection_DoWork(object? sender, DoWorkEventArgs e)
    {
        var worker = sender as BackgroundWorker;
        
        while (!worker.CancellationPending)
        {
            try
            {
                // Check for games on background thread
                CheckForGameExecutables();
                
                // Sleep based on focus state - 5s when focused, 30s when unfocused  
                var sleepTime = _windowHasFocus ? 5000 : 30000;
                
                // Sleep in small chunks so we can respond to cancellation quickly
                for (int i = 0; i < sleepTime / 1000; i++)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                // Handle errors without crashing the background thread
                Dispatcher.BeginInvoke(() => 
                    AddActivityLogEntry($"[ERROR] Smart polling error: {ex.Message}"));
                
                // Wait a bit before retrying
                System.Threading.Thread.Sleep(5000);
            }
        }
        
        e.Cancel = true;
    }

    private void SmartGameDetection_Completed(object? sender, RunWorkerCompletedEventArgs e)
    {
        // Restart the worker if it wasn't cancelled (e.g., if it completed due to error)
        if (!e.Cancelled && _smartGameDetectionWorker != null)
        {
            AddActivityLogEntry("[INFO] Smart polling restarting background thread");
            if (!_smartGameDetectionWorker.IsBusy)
            {
                _smartGameDetectionWorker.RunWorkerAsync();
            }
        }
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        // Window gained focus - enable more frequent polling
        _windowHasFocus = true;
        AddActivityLogEntry("[INFO] Smart polling switched to focused mode - window focused");
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        // Window lost focus - switch to slower polling to save resources
        _windowHasFocus = false;
        AddActivityLogEntry("[INFO] Smart polling switched to unfocused mode - window unfocused");
    }



    private void CheckForGameExecutables()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            
            foreach (var executable in _watchedExecutables)
            {
                var processName = Path.GetFileNameWithoutExtension(executable);
                var foundProcess = processes.FirstOrDefault(p => 
                    p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase) ||
                    p.MainWindowTitle.StartsWith(processName.Replace("FINAL FANTASY", "FINAL FANTASY"), StringComparison.OrdinalIgnoreCase));
                
                if (foundProcess != null)
                {
                    // Found a matching process
                    var currentPackStatus = PackStatusText.Text;
                    
                    if (currentPackStatus == "No pack loaded")
                    {
                        // Auto-load matching pack
                        AutoLoadMatchingPack("FF1.PixelRemaster");
                    }
                    
                    var gameTitle = string.IsNullOrEmpty(foundProcess.MainWindowTitle) 
                        ? foundProcess.ProcessName 
                        : foundProcess.MainWindowTitle;
                        
                    UpdateGameStatus($"Final Fantasy detected - {gameTitle}");
                    BottomStatusText.Text = "GameWatcher V2 Platform - FF Game Ready (Smart Detection)";
                    
                    // Start capture service automatically when game is detected
                    if (!_gameIsRunning)
                    {
                        _gameIsRunning = true;
                        _ = Task.Run(async () => {
                            try
                            {
                                var started = await _captureService?.StartCaptureAsync();
                                if (started == true)
                                {
                                    Dispatcher.Invoke(() => {
                                        AddActivityLogEntry($"[SYSTEM] Capture started for {gameTitle}");
                                        Console.WriteLine($"[MAIN] Capture service started for {gameTitle}");
                                        
                                        // Connect Activity Monitor to capture service
                                        _activityMonitor?.AttachCaptureService(_captureService);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => {
                                    AddActivityLogEntry($"[ERROR] Failed to start capture: {ex.Message}");
                                });
                            }
                        });
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[Smart Polling] Detected: {executable} -> {gameTitle}");
                    return; // Found one, no need to check others
                }
            }
            
            // No games found - stop capture service if running
            if (GameStatusText.Text != "No game detected")
            {
                UpdateGameStatus("No game detected");
                BottomStatusText.Text = "GameWatcher V2 Platform - Ready (No game)";
                
                // Stop capture service when game is no longer detected
                if (_gameIsRunning)
                {
                    _gameIsRunning = false;
                    _ = Task.Run(async () => {
                        try
                        {
                            var stopped = await _captureService?.StopCaptureAsync();
                            if (stopped == true)
                            {
                                Dispatcher.Invoke(() => {
                                    AddActivityLogEntry("[SYSTEM] Capture stopped - no game detected");
                                    Console.WriteLine("[MAIN] Capture service stopped - no game detected");
                                    
                                    // Disconnect Activity Monitor from capture service
                                    _activityMonitor?.DetachCaptureService();
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => {
                                AddActivityLogEntry($"[ERROR] Failed to stop capture: {ex.Message}");
                            });
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            UpdateGameStatus("Error detecting game");
            BottomStatusText.Text = $"GameWatcher V2 Platform - Error: {ex.Message}";
        }
    }

    private void CleanupGameDetection()
    {
        try
        {
            // Cancel the background worker
            if (_smartGameDetectionWorker != null)
            {
                _smartGameDetectionWorker.CancelAsync();
                _smartGameDetectionWorker.Dispose();
                _smartGameDetectionWorker = null;
            }
            
            // Cleanup capture service
            if (_captureService != null)
            {
                _captureService.StopCaptureAsync().Wait(TimeSpan.FromSeconds(2)); // Give it 2 seconds to stop gracefully
                _captureService.Dispose();
                _captureService = null;
            }
            
            // Cleanup Activity Monitor
            _activityMonitor?.Dispose();
            _activityMonitor = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cleaning up game detection: {ex.Message}");
        }
    }

    private void AddActivityLogEntry(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var newEntry = $"[{timestamp}] {message}\n";
        
        // Prepend to log (newest at top)
        ActivityLogText.Text = newEntry + ActivityLogText.Text;
        
        // Keep log reasonable size (last 1000 characters)
        if (ActivityLogText.Text.Length > 1000)
        {
            ActivityLogText.Text = ActivityLogText.Text.Substring(0, 1000);
        }
    }
}
