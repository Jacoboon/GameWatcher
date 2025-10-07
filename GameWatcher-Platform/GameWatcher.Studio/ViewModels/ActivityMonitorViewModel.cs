using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using GameWatcher.Runtime.Services;

namespace GameWatcher.Studio.ViewModels;

public partial class ActivityMonitorViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ActivityMonitorViewModel> _logger;
    private readonly ProcessingPipeline _pipeline;
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
    private int _audioPlayed;

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

    private readonly Queue<double> _processingTimes = new();
    private const int MaxLogEntries = 100;
    private const int MaxProcessingTimeSamples = 50;

    public ActivityMonitorViewModel(ILogger<ActivityMonitorViewModel> logger, ProcessingPipeline pipeline)
    {
        _logger = logger;
        _pipeline = pipeline;

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
            
            // Subscribe to pipeline events
            _pipeline.FrameProcessed += OnFrameProcessed;
            _pipeline.TextDetected += OnTextDetected;
            _pipeline.AudioPlayed += OnAudioPlayed;

            IsMonitoring = true;
            _metricsTimer.Start();

            AddLogEntry("Activity monitoring started", ActivityLogLevel.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ActivityMonitor ViewModel");
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

    private void OnFrameProcessed(object? sender, FrameProcessedEventArgs e)
    {
        FramesProcessed++;
        
        // Track processing time
        _processingTimes.Enqueue(e.ProcessingTimeMs);
        if (_processingTimes.Count > MaxProcessingTimeSamples)
            _processingTimes.Dequeue();

        LastActivityTime = DateTime.Now;

        AddLogEntry($"Frame processed in {e.ProcessingTimeMs:F1}ms", ActivityLogLevel.Debug);
    }

    private void OnTextDetected(object? sender, TextDetectedEventArgs e)
    {
        TextDetections++;
        LastDetectedText = e.Text;
        LastActivityTime = DateTime.Now;

        AddLogEntry($"Text detected: {e.Text}", ActivityLogLevel.Info);
    }

    private void OnAudioPlayed(object? sender, AudioPlayedEventArgs e)
    {
        AudioPlayed++;
        LastActivityTime = DateTime.Now;

        AddLogEntry($"Audio played: {e.AudioFile}", ActivityLogLevel.Info);
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
        _pipeline.FrameProcessed -= OnFrameProcessed;
        _pipeline.TextDetected -= OnTextDetected;
        _pipeline.AudioPlayed -= OnAudioPlayed;
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

