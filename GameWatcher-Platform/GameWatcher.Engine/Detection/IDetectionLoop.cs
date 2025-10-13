using System;
using System.Threading.Tasks;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Interface for the optimal detection loop combining V1 stability and V2 architecture.
/// Manages frame capture, textbox detection, and dialogue OCR with optimal performance.
/// </summary>
public interface IDetectionLoop : IDisposable
{
    /// <summary>
    /// Raised when unique dialogue is detected and processed.
    /// </summary>
    event EventHandler<DialogueDetectedEventArgs>? DialogueDetected;
    
    /// <summary>
    /// Starts the detection loop with configured frame rate.
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// Pauses the detection loop without clearing state.
    /// </summary>
    Task PauseAsync();
    
    /// <summary>
    /// Stops the detection loop and clears transient state.
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Gets whether the loop is currently running.
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Gets performance statistics for the detection loop.
    /// </summary>
    DetectionStatistics Statistics { get; }
}

/// <summary>
/// Event args for dialogue detection events.
/// </summary>
public class DialogueDetectedEventArgs : EventArgs
{
    public required string Text { get; init; }
    public required string OriginalOcrText { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Performance statistics for the detection loop.
/// </summary>
public class DetectionStatistics
{
    public int TotalFramesProcessed { get; set; }
    public int DialoguesDetected { get; set; }
    public int CachedTextboxHits { get; set; }
    public int FullTextboxDetections { get; set; }
    public int OcrSkippedDueToHashMatch { get; set; }
    public double AverageFrameProcessingMs { get; set; }
    public double CacheHitRate => TotalFramesProcessed > 0 
        ? (double)CachedTextboxHits / TotalFramesProcessed * 100 
        : 0;
}
