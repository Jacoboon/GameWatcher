using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using GameWatcher.Runtime.Services;
using GameWatcher.Runtime.Services.Capture;

namespace GameWatcher.Studio.ViewModels;

public partial class ActivityMonitorViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ActivityMonitorViewModel> _logger;
    private GameCaptureService? _captureService;
    private readonly DispatcherTimer _metricsTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;

    [ObservableProperty]
    private ObservableCollection<ActivityLogEntry> _activityLog = new();

    [ObservableProperty]
    private int _framesProcessed;

    [ObservableProperty]
    private int _textDetections;

    [ObservableProperty]
    private int _textboxesFound;

    [ObservableProperty]
    private double _currentFps;

    [ObservableProperty]
    private double _averageProcessingTime;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _memoryUsage;

    [ObservableProperty]
    private string _lastDetectedText = string.Empty;

    [ObservableProperty]
    private DateTime _lastActivityTime = DateTime.Now;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _monitoringStatus = "Stopped";

    private readonly Queue<double> _processingTimes = new();
    private const int MaxLogEntries = 100;
    private const int MaxProcessingTimeSamples = 50;

    public ActivityMonitorViewModel(ILogger<ActivityMonitorViewModel> logger)
    {
        _logger = logger;

        _metricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _metricsTimer.Tick += UpdateMetrics;

        // Initialize performance counters (may fail on some systems)
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Performance counters not available");
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing ActivityMonitor ViewModel");
            
            IsMonitoring = true;
            _metricsTimer.Start();

            MonitoringStatus = "Ready";
            AddLogEntry("Activity monitoring initialized", ActivityLogLevel.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ActivityMonitor ViewModel");
        }
    }

    public void AttachCaptureService(GameCaptureService captureService)
    {
        try
        {
            // Unsubscribe from previous service
            DetachCaptureService();

            _captureService = captureService;
            
            // Subscribe to capture service events
            _captureService.ProgressReported += OnCaptureProgress;
            _captureService.DialogueDetected += OnDialogueDetected;

            MonitoringStatus = "Monitoring";
            AddLogEntry("Connected to capture service", ActivityLogLevel.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach capture service");
        }
    }

    public void DetachCaptureService()
    {
        if (_captureService != null)
        {
            _captureService.ProgressReported -= OnCaptureProgress;
            _captureService.DialogueDetected -= OnDialogueDetected;
            _captureService = null;

            MonitoringStatus = "Stopped";
            AddLogEntry("Disconnected from capture service", ActivityLogLevel.Info);
        }
    }

    public void RefreshMetrics()
    {
        try
        {
            // Update CPU usage
            if (_cpuCounter != null)
            {
                CpuUsage = _cpuCounter.NextValue();
            }

            // Update memory usage
            if (_memoryCounter != null)
            {
                var availableMB = _memoryCounter.NextValue();
                var totalMB = GC.GetTotalMemory(false) / (1024 * 1024);
                MemoryUsage = totalMB;
            }

            // Update average processing time
            if (_processingTimes.Count > 0)
            {
                AverageProcessingTime = _processingTimes.Average();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metrics");
        }
    }

    private void UpdateMetrics(object? sender, EventArgs e)
    {
        RefreshMetrics();
    }

    private void OnCaptureProgress(object? sender, CaptureProgressEventArgs e)
    {
        // Update metrics from capture statistics
        FramesProcessed = e.Statistics.FrameCount;
        TextboxesFound = e.Statistics.TextboxesFound;
        CurrentFps = e.Statistics.ActualFps;
        AverageProcessingTime = e.Statistics.AverageProcessingTimeMs;
        
        LastActivityTime = DateTime.Now;

        // Only log every 30 frames to avoid spam
        if (e.Statistics.FrameCount % 30 == 0)
        {
            AddLogEntry($"Frame {e.Statistics.FrameCount}: {e.Statistics.ActualFps:F1} FPS, {e.Statistics.TextboxesFound} textboxes", ActivityLogLevel.Debug);
        }
    }

    private void OnDialogueDetected(object? sender, DialogueDetectedEventArgs e)
    {
        TextDetections++;
        LastDetectedText = e.DialogueEntry.Text;
        LastActivityTime = DateTime.Now;

        AddLogEntry($"Dialogue: \"{e.DialogueEntry.Text}\" ({e.DialogueEntry.Speaker})", ActivityLogLevel.Info);
    }

    private void AddLogEntry(string message, ActivityLogLevel level)
    {
        var entry = new ActivityLogEntry
        {
            Timestamp = DateTime.Now,
            Message = message,
            Level = level
        };

        // Add to beginning of collection for newest-first display
        ActivityLog.Insert(0, entry);

        // Limit log size
        while (ActivityLog.Count > MaxLogEntries)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    public void Dispose()
    {
        _metricsTimer?.Stop();
        DetachCaptureService();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
    }
}

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public ActivityLogLevel Level { get; set; }
    
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");
}

public enum ActivityLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

