using GameWatcher.Engine.Packs;
using GameWatcher.Engine.Services;
using GameWatcher.Engine.Ocr;
using GameWatcher.Engine.Detection;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace GameWatcher.Runtime.Services;

/// <summary>
/// Real-time processing pipeline that coordinates capture, detection, OCR and pack-specific logic.
/// Maintains V1 performance optimizations while supporting multiple game packs.
/// </summary>
public interface IProcessingPipeline
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();
    bool IsRunning { get; }
    bool IsPaused { get; }
}

public record ProcessedDialogue(
    string Text,
    SpeakerProfile? Speaker,
    DateTime Timestamp,
    double Confidence,
    TimeSpan ProcessingTime,
    IGamePack Pack
);

public record ProcessingMetrics(
    TimeSpan DetectionTime,
    TimeSpan OcrTime,
    TimeSpan TotalProcessingTime,
    double DetectionConfidence,
    int QueuedItems,
    bool WasOptimized
);

public class ProcessingPipeline : IProcessingPipeline, IDisposable
{
    private readonly ILogger<ProcessingPipeline> _logger;
    private readonly ICaptureService _captureService;
    private readonly IOcrEngine _ocrEngine;
    private readonly IPackManager _packManager;
    private readonly IGameDetectionService _gameDetection;
    private readonly GameWatcher.Runtime.Services.Dialogue.DialogueCatalogService _catalog;
    private readonly GameWatcher.Runtime.Services.OCR.OcrPostprocessor _ocrPost;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;
    private volatile bool _isRunning;
    private volatile bool _isPaused;

    // Performance optimization state (V1 learnings)
    private readonly Dictionary<string, string> _lastTextByPack = new();
    private readonly Dictionary<string, DateTime> _lastDetectionByPack = new();
    private Rectangle? _lastTextboxRect;

    // Events for UI monitoring
    public event EventHandler<FrameProcessedEventArgs>? FrameProcessed;
    public event EventHandler<TextDetectedEventArgs>? TextDetected;
    public event EventHandler<AudioPlayedEventArgs>? AudioPlayed;

    public ProcessingPipeline(
        ILogger<ProcessingPipeline> logger,
        ICaptureService captureService,
        IOcrEngine ocrEngine,
        IPackManager packManager,
        IGameDetectionService gameDetection,
        GameWatcher.Runtime.Services.Dialogue.DialogueCatalogService catalog,
        GameWatcher.Runtime.Services.OCR.OcrPostprocessor ocrPost)
    {
        _logger = logger;
        _captureService = captureService;
        _ocrEngine = ocrEngine;
        _packManager = packManager;
        _gameDetection = gameDetection;
        _catalog = catalog;
        _ocrPost = ocrPost;
    }

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Processing pipeline is already running");
            return;
        }

        _logger.LogInformation("Starting GameWatcher V2 processing pipeline...");

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;
        _isPaused = false;

        // Start the main processing loop
        _processingTask = ProcessingLoopAsync(_cancellationTokenSource.Token);

        _logger.LogInformation("Processing pipeline started");
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("Stopping processing pipeline...");

        _isRunning = false;
        _cancellationTokenSource?.Cancel();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        _logger.LogInformation("Processing pipeline stopped");
    }

    public Task PauseAsync()
    {
        _logger.LogInformation("Pausing processing pipeline...");
        _isPaused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        _logger.LogInformation("Resuming processing pipeline...");
        _isPaused = false;
        return Task.CompletedTask;
    }

    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing loop started");
        var frameCount = 0;
        var startTime = DateTime.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                // Pause handling
                if (_isPaused)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                // Detect active game and ensure appropriate pack is loaded
                await EnsureActivePackLoadedAsync();

                var activePack = _packManager.GetActivePack();
                if (activePack == null)
                {
                    // No supported game running, wait and retry
                    _logger.LogDebug("No supported game detected, waiting...");
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                // Process single frame
                var processingTime = await ProcessFrameAsync(activePack, cancellationToken);
                
                // Emit frame processed event
                FrameProcessed?.Invoke(this, new FrameProcessedEventArgs { ProcessingTimeMs = processingTime });

                frameCount++;

                // Log processing stats every 100 frames
                if (frameCount % 100 == 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var fps = frameCount / elapsed.TotalSeconds;
                    _logger.LogDebug("Processed {FrameCount} frames, Average FPS: {Fps:F1}", 
                        frameCount, fps);
                }

                // V1 optimization: Dynamic frame rate based on activity
                var delay = activePack.Manifest.TargetFrameRate > 0 
                    ? 1000 / activePack.Manifest.TargetFrameRate 
                    : 66; // ~15fps default

                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in processing loop");
            throw;
        }

        _logger.LogInformation("Processing loop finished. Processed {FrameCount} frames", frameCount);
    }

    private async Task EnsureActivePackLoadedAsync()
    {
        var activePack = _packManager.GetActivePack();
        var detectedGame = await _gameDetection.DetectActiveGameAsync();

        if (detectedGame == null)
        {
            // No game detected - unload current pack if any
            if (activePack != null)
            {
                _logger.LogInformation("No supported game running, unloading current pack");
                await _packManager.UnloadPackAsync(activePack.Manifest.Name);
            }
            return;
        }

        // If detected game doesn't match current pack, switch
        if (activePack == null || activePack.Manifest.Name != detectedGame.Pack.Manifest.Name)
        {
            _logger.LogInformation("Switching to pack: {PackId} for game: {GameName}", 
                detectedGame.Pack.Manifest.Name, detectedGame.ProcessName);

            await _packManager.LoadPackAsync(detectedGame.Pack);
        }
    }

    private async Task<double> ProcessFrameAsync(IGamePack pack, CancellationToken cancellationToken)
    {
        var processingStart = DateTime.UtcNow;

        try
        {
            // Capture screenshot
            var screenshot = await CaptureScreenshotAsync(pack);
            if (screenshot == null)
            {
                return 0;
            }

            using (screenshot)
            {
                // Detect textbox using pack-specific strategy
                var detectionResult = await DetectTextboxAsync(pack, screenshot);
                if (!detectionResult.HasTextbox)
                {
                    return (DateTime.UtcNow - processingStart).TotalMilliseconds;
                }

                // Extract and process text
                var dialogue = await ProcessDialogueAsync(pack, screenshot, detectionResult, processingStart);
                if (dialogue != null)
                {
                    _logger.LogInformation("Processed dialogue: {Speaker} - {Text}", 
                        dialogue.Speaker?.Name ?? "Unknown", 
                        dialogue.Text.Length > 50 ? dialogue.Text.Substring(0, 50) + "..." : dialogue.Text);

                    // Emit text detected event
                    TextDetected?.Invoke(this, new TextDetectedEventArgs { Text = dialogue.Text });

                    // TODO: Queue audio generation and playback
                    // For now, simulate audio playback
                    var audioFile = $"{dialogue.Speaker?.Name ?? "Unknown"}.wav";
                    AudioPlayed?.Invoke(this, new AudioPlayedEventArgs { AudioFile = audioFile });

                    // TODO: Update streaming overlays
                    // TODO: Emit events for external integrations
                }

                return (DateTime.UtcNow - processingStart).TotalMilliseconds;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing frame for pack {PackId}", pack.Manifest.Name);
            return (DateTime.UtcNow - processingStart).TotalMilliseconds;
        }
    }

    private async Task<Bitmap?> CaptureScreenshotAsync(IGamePack pack)
    {
        try
        {
            // Use pack-specific capture settings if available
            var windowTitle = pack.Manifest.WindowTitle ?? "Final Fantasy";
            await _captureService.InitializeAsync(windowTitle);
            
            return _captureService.GetLastFrame();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to capture screenshot for pack {PackId}", pack.Manifest.Name);
            return null;
        }
    }

    private async Task<DetectionResult> DetectTextboxAsync(IGamePack pack, Bitmap screenshot)
    {
        var detectionStart = DateTime.UtcNow;

        try
        {
            // Get pack-specific textbox detector
            var detector = pack.CreateDetectionStrategy();
            
            // Use targeted detection if pack supports it (V1 optimization)
            var textboxRect = detector.DetectTextbox(screenshot);
            
            var detectionTime = DateTime.UtcNow - detectionStart;
            
            return new DetectionResult
            {
                HasTextbox = textboxRect.HasValue,
                TextboxRect = textboxRect,
                DetectionTime = detectionTime,
                Confidence = textboxRect.HasValue ? 0.95 : 0.0 // TODO: Implement proper confidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Textbox detection failed for pack {PackId}", pack.Manifest.Name);
            return new DetectionResult { HasTextbox = false };
        }
    }

    private async Task<ProcessedDialogue?> ProcessDialogueAsync(
        IGamePack pack, 
        Bitmap screenshot, 
        DetectionResult detection, 
        DateTime processingStart)
    {
        if (!detection.HasTextbox || !detection.TextboxRect.HasValue)
        {
            return null;
        }

        var ocrStart = DateTime.UtcNow;

        try
        {
            // Extract text from detected textbox area
            using var textboxImage = CropImageToTextbox(screenshot, detection.TextboxRect.Value);
            var rawText = await _ocrEngine.ExtractTextAsync(textboxImage);
            // Apply author-curated OCR postprocess fixes
            rawText = _ocrPost.Apply(rawText);
            
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return null;
            }

            var ocrTime = DateTime.UtcNow - ocrStart;

            // Normalize text (remove artifacts, standardize formatting)
            var normalizedText = NormalizeText(rawText);
            
            // Check for duplicates (V1 optimization)
            var packId = pack.Manifest.Name;
            if (_lastTextByPack.TryGetValue(packId, out var lastText) && 
                string.Equals(lastText, normalizedText, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogTrace("Skipping duplicate text for pack {PackId}", packId);
                return null;
            }

            _lastTextByPack[packId] = normalizedText;
            _lastDetectionByPack[packId] = DateTime.UtcNow;

            // Lookup dialogue in catalog for stable ID/speaker mapping
            if (_catalog.TryLookup(normalizedText.ToLowerInvariant(), out var catalogEntry))
            {
                // Override normalized text to the catalog version's text if needed
                if (!string.IsNullOrWhiteSpace(catalogEntry.Text))
                {
                    normalizedText = NormalizeText(catalogEntry.Text);
                }
            }

            // Match speaker using pack's speaker collection (catalog speakerId takes precedence)
            var speakers = pack.GetSpeakers();
            var matchedSpeaker = speakers.MatchSpeaker(normalizedText);
            if (_catalog.TryLookup(normalizedText.ToLowerInvariant(), out var entry2))
            {
                if (!string.IsNullOrWhiteSpace(entry2.VoiceProfile))
                {
                    // Attempt to prefer catalog-defined speaker by name/id
                    var preferred = speakers.GetAllSpeakers().FirstOrDefault(s =>
                        string.Equals(s.Id, entry2.VoiceProfile, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.Name, entry2.VoiceProfile, StringComparison.OrdinalIgnoreCase));
                    if (preferred != null) matchedSpeaker = preferred;
                }
            }

            var totalProcessingTime = DateTime.UtcNow - processingStart;

            return new ProcessedDialogue(
                normalizedText,
                matchedSpeaker,
                DateTime.UtcNow,
                detection.Confidence,
                totalProcessingTime,
                pack
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process dialogue for pack {PackId}", pack.Manifest.Name);
            return null;
        }
    }

    private string NormalizeText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        return rawText
            .Trim()
            .Replace("\u201C", "\"") // Left double quotation mark
            .Replace("\u201D", "\"") // Right double quotation mark  
            .Replace("\u2018", "'")  // Left single quotation mark
            .Replace("\u2019", "'")  // Right single quotation mark
            .Replace("…", "...")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private Bitmap CropImageToTextbox(Bitmap screenshot, Rectangle textboxRect)
    {
        var croppedImage = new Bitmap(textboxRect.Width, textboxRect.Height);
        using var graphics = Graphics.FromImage(croppedImage);
        graphics.DrawImage(screenshot, new Rectangle(0, 0, textboxRect.Width, textboxRect.Height), 
            textboxRect, GraphicsUnit.Pixel);
        return croppedImage;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
    }

    private record DetectionResult
    {
        public bool HasTextbox { get; init; }
        public Rectangle? TextboxRect { get; init; }
        public TimeSpan DetectionTime { get; init; }
        public double Confidence { get; init; }
    }
}

// Event args for pipeline events
public class FrameProcessedEventArgs : EventArgs
{
    public double ProcessingTimeMs { get; set; }
}

public class TextDetectedEventArgs : EventArgs
{
    public string Text { get; set; } = string.Empty;
}

public class AudioPlayedEventArgs : EventArgs
{
    public string AudioFile { get; set; } = string.Empty;
}
