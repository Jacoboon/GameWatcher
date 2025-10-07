using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace GameWatcher.Engine.Services;

/// <summary>
/// Interface for capture services that coordinate frame capture, textbox detection, and OCR processing.
/// </summary>
public interface ICaptureService : IDisposable
{
    /// <summary>
    /// Initialize the capture service with game-specific configuration.
    /// </summary>
    Task InitializeAsync(string gameName);
    
    /// <summary>
    /// Start the capture loop with V1 performance optimizations.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Stop the capture loop and clean up resources.
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Check if the service is currently running.
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Get the most recent frame captured.
    /// </summary>
    Bitmap? GetLastFrame();
    
    /// <summary>
    /// Get the most recent textbox region detected.
    /// </summary>
    Bitmap? GetLastTextbox();
    
    /// <summary>
    /// Get the most recent text extracted from OCR.
    /// </summary>
    string GetLastText();
}