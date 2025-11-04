using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace GameWatcher.Studio.ViewModels;

public partial class ActivityMonitorViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ActivityMonitorViewModel> _logger;
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

    // DEPRECATED: These methods were for GameCaptureService which is now replaced by IDetectionLoop
    // TODO: Remove or update for IDetectionLoop if Activity Monitor UI is needed
    /*
    public void AttachCaptureService(IDetectionLoop detectionLoop)
    {
        try
        {
            DetachCaptureService();
            // TODO: Wire up IDetectionLoop events
            MonitoringStatus = "Monitoring";
            AddLogEntry("Connected to detection loop", ActivityLogLevel.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach detection loop");
        }
    }

    public void DetachCaptureService()
    {
        MonitoringStatus = "Stopped";
        AddLogEntry("Disconnected from detection loop", ActivityLogLevel.Info);
    }
    */

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

    // DEPRECATED: Event handlers for GameCaptureService (removed)
    /*
    private void OnCaptureProgress(object? sender, CaptureProgressEventArgs e)
    {
        FramesProcessed = e.Statistics.FrameCount;
        TextboxesFound = e.Statistics.TextboxesFound;
        CurrentFps = e.Statistics.ActualFps;
        AverageProcessingTime = e.Statistics.AverageProcessingTimeMs;
        LastActivityTime = DateTime.Now;

        if (e.Statistics.FrameCount % 30 == 0)
        {
            AddLogEntry($"Frame {e.Statistics.FrameCount}: {e.Statistics.ActualFps:F1} FPS, {e.Statistics.Textboxes Found} textboxes", ActivityLogLevel.Debug);
        }
    }

    private void OnDialogueDetected(object? sender, DialogueDetectedEventArgs e)
    {
        TextDetections++;
        LastDetectedText = e.DialogueEntry.Text;
        LastActivityTime = DateTime.Now;

        AddLogEntry($"Dialogue: \"{e.DialogueEntry.Text}\" ({e.DialogueEntry.Speaker})", ActivityLogLevel.Info);
    }
    */

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
        // DetachCaptureService(); // DEPRECATED
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

