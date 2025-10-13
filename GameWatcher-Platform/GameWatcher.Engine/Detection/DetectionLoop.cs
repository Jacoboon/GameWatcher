using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameWatcher.Engine.Ocr;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Optimal detection loop combining V1 proven algorithms with V2 architecture.
/// Key optimizations:
/// - Pre-crop textbox before comparison (30× faster)
/// - Textbox location caching (98% reduction in detection time)
/// - V1's 3-state stability machine (proven to work)
/// - Content hash check (skip unnecessary OCR)
/// Target: <3ms synchronous processing for 95% of frames
/// </summary>
public class DetectionLoop : IDetectionLoop
{
    private readonly DetectionLoopConfig _config;
    private readonly ITextboxDetector _detector;
    private readonly IOcrEngine _ocr;
    private readonly Func<string, string> _applyOcrFixes;
    private readonly Func<Bitmap?> _captureFrame;
    private readonly Func<Bitmap, Bitmap, int, bool> _compareImages;
    private readonly Func<string, string> _normalizeText;
    private readonly ILogger<DetectionLoop>? _logger;
    
    private Timer? _timer;
    private bool _running;
    private readonly object _lockObject = new();
    
    // V1 3-state machine (PROVEN)
    private bool _waitingForStableFrame = true;
    private bool _processingStableFrame = false;
    
    // Reentrancy guard (CRITICAL FIX)
    private int _isProcessing = 0;
    
    // Textbox caching (NEW optimization)
    private Rectangle? _cachedTextboxRect = null;
    private int _cacheAge = 0;
    
    // Comparison bitmaps (textbox area only)
    private Bitmap? _lastTextbox = null;
    private string _lastTextboxHash = string.Empty;
    
    // Deduplication
    private readonly HashSet<string> _seenNormalized = new();
    private string _lastNormalizedText = string.Empty;
    
    // Statistics
    private readonly DetectionStatistics _stats = new();
    private readonly System.Diagnostics.Stopwatch _frameTimer = new();
    private readonly System.Diagnostics.Stopwatch _sessionTimer = new();
    private readonly List<double> _frameTimes = new();
    
    public event EventHandler<DialogueDetectedEventArgs>? DialogueDetected;
    
    public bool IsRunning => _running;
    public DetectionStatistics Statistics => _stats;
    
    public DetectionLoop(
        DetectionLoopConfig config,
        ITextboxDetector detector,
        IOcrEngine ocr,
        Func<string, string> applyOcrFixes,
        Func<Bitmap?> captureFrame,
        Func<Bitmap, Bitmap, int, bool> compareImages,
        Func<string, string> normalizeText,
        ILogger<DetectionLoop>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _applyOcrFixes = applyOcrFixes ?? throw new ArgumentNullException(nameof(applyOcrFixes));
        _captureFrame = captureFrame ?? throw new ArgumentNullException(nameof(captureFrame));
        _compareImages = compareImages ?? throw new ArgumentNullException(nameof(compareImages));
        _normalizeText = normalizeText ?? throw new ArgumentNullException(nameof(normalizeText));
        _logger = logger;
    }
    
    public Task StartAsync()
    {
        if (_running) return Task.CompletedTask;
        
        _running = true;
        _sessionTimer.Restart();
        var intervalMs = 1000 / _config.TargetFps;
        _timer = new Timer(CaptureTick, null, 0, intervalMs);
        
        _logger?.LogInformation("Detection loop started at {Fps} FPS ({IntervalMs}ms interval)", 
            _config.TargetFps, intervalMs);
        
        return Task.CompletedTask;
    }
    
    public Task PauseAsync()
    {
        if (!_running) return Task.CompletedTask;
        
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _running = false;
        
        _logger?.LogInformation("Detection loop paused");
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        _ = PauseAsync();
        
        // Output performance summary
        if (_stats.TotalFramesProcessed > 0)
        {
            _sessionTimer.Stop();
            var sessionDuration = _sessionTimer.Elapsed;
            var avgFrameTime = _stats.AverageFrameProcessingMs;
            var minFrameTime = _frameTimes.Count > 0 ? _frameTimes.Min() : 0;
            var maxFrameTime = _frameTimes.Count > 0 ? _frameTimes.Max() : 0;
            var actualFps = sessionDuration.TotalSeconds > 0 
                ? _stats.TotalFramesProcessed / sessionDuration.TotalSeconds 
                : 0;
            var targetFps = 1000.0 / (1000.0 / _config.TargetFps);
            
            _logger?.LogInformation("═══════════════════════════════════════════════════");
            _logger?.LogInformation("Detection Loop Performance Summary");
            _logger?.LogInformation("═══════════════════════════════════════════════════");
            _logger?.LogInformation("Session Duration: {Duration:mm\\:ss}", sessionDuration);
            _logger?.LogInformation("Total Frames: {Frames}", _stats.TotalFramesProcessed);
            _logger?.LogInformation("Target FPS: {TargetFps} | Actual FPS: {ActualFps:F1}", 
                _config.TargetFps, actualFps);
            _logger?.LogInformation("Avg Frame Time: {AvgMs:F1}ms | Min: {MinMs:F1}ms | Max: {MaxMs:F1}ms",
                avgFrameTime, minFrameTime, maxFrameTime);
            _logger?.LogInformation("Textbox Cache Hit Rate: {Rate:F1}% ({Hits}/{Total})",
                _stats.CacheHitRate, _stats.CachedTextboxHits, _stats.TotalFramesProcessed);
            _logger?.LogInformation("Full Detections: {Count}", _stats.FullTextboxDetections);
            _logger?.LogInformation("OCR Skipped (Hash Match): {Count}", _stats.OcrSkippedDueToHashMatch);
            _logger?.LogInformation("Dialogues Detected: {Count}", _stats.DialoguesDetected);
            _logger?.LogInformation("═══════════════════════════════════════════════════");
        }
        
        // Clear transient state
        lock (_lockObject)
        {
            _lastNormalizedText = string.Empty;
            _seenNormalized.Clear();
        }
        
        _logger?.LogInformation("Detection loop stopped");
        
        return Task.CompletedTask;
    }
    
    private void CaptureTick(object? state)
    {
        // CRITICAL: Prevent reentrancy - if already processing, skip this tick
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
        {
            // Already processing a frame - skip this tick
            return;
        }
        
        try
        {
            ProcessFrame();
        }
        finally
        {
            // Always release the lock
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }
    
    private void ProcessFrame()
    {
        // Log every 10 frames to verify timer is firing
        if (_stats.TotalFramesProcessed % 10 == 0)
        {
            _logger?.LogDebug("CaptureTick fired for frame {Count}, running={Running}", _stats.TotalFramesProcessed + 1, _running);
        }
        
        if (!_running)
        {
            _logger?.LogDebug("CaptureTick called but _running=false, returning");
            return;
        }
        
        _frameTimer.Restart();
        
        try
        {
            // STEP 1: Capture frame (5-10ms)
            var currentFrame = _captureFrame();
            if (currentFrame == null)
            {
                _logger?.LogWarning("Failed to capture game window - captureFrame returned null");
                return;
            }
            
            _stats.TotalFramesProcessed++;
            
            // Log every 100 frames for diagnostics
            if (_stats.TotalFramesProcessed % 100 == 0)
            {
                _logger?.LogInformation("Frame {Count}: {Width}x{Height}, Cache hits: {CacheHits}, Dialogues: {Dialogues}", 
                    _stats.TotalFramesProcessed, currentFrame.Width, currentFrame.Height, 
                    _stats.CachedTextboxHits, _stats.DialoguesDetected);
            }
            
            // OPTIMIZATION 1: Pre-crop if textbox cached (0.2ms vs 10ms)
            Bitmap? currentTextbox = null;
            Rectangle? currentTextboxRect = null;
            
            if (_config.EnablePreCrop && _config.EnableTextboxCache && _cachedTextboxRect.HasValue)
            {
                // Fast path: Use cached textbox position (95% of frames)
                currentTextbox = CropImage(currentFrame, _cachedTextboxRect.Value);
                currentTextboxRect = _cachedTextboxRect.Value;
                _cacheAge++;
                _stats.CachedTextboxHits++;
                
                // Invalidate cache after threshold (prevent stale cache)
                if (_cacheAge > _config.CacheInvalidateAfterFrames)
                {
                    _cachedTextboxRect = null;
                    _cacheAge = 0;
                    _logger?.LogDebug("Cache invalidated after {Frames} frames", _config.CacheInvalidateAfterFrames);
                }
            }
            
            // OPTIMIZATION 2: Textbox-only comparison (0.1ms vs 9.3ms)
            bool shouldProcessOcr = false;
            
            lock (_lockObject)
            {
                if (currentTextbox != null && _lastTextbox != null)
                {
                    // Compare ONLY textbox areas (not full frames!)
                    var sampleRate = _waitingForStableFrame ? _config.StableSampleRate : _config.ChangeSampleRate;
                    var textboxMatches = _compareImages(currentTextbox, _lastTextbox, sampleRate);
                    
                    // V1 3-STATE MACHINE (PROVEN)
                    if (textboxMatches && _waitingForStableFrame && !_processingStableFrame)
                    {
                        // TEXTBOX STABLE - First stable frame detected
                        _waitingForStableFrame = false;
                        _processingStableFrame = true;
                        shouldProcessOcr = true;
                        
                        _logger?.LogInformation("✅ Textbox stable - processing OCR");
                    }
                    else if (textboxMatches && _processingStableFrame)
                    {
                        // ALREADY PROCESSED - Skip duplicate
                        currentFrame.Dispose();
                        currentTextbox?.Dispose();
                        RecordFrameTime();
                        return;
                    }
                    else if (!textboxMatches)
                    {
                        // TEXTBOX CHANGED - Reset for new detection
                        _waitingForStableFrame = true;
                        _processingStableFrame = false;
                        _lastTextbox?.Dispose();
                        _lastTextbox = (Bitmap)currentTextbox.Clone();
                        _cachedTextboxRect = null; // Force re-detection
                        _cacheAge = 0;
                        
                        currentFrame.Dispose();
                        currentTextbox?.Dispose();
                        RecordFrameTime();
                        return;
                    }
                }
                else if (_lastTextbox == null && currentTextbox != null)
                {
                    // First frame with cached textbox - store it
                    _lastTextbox = (Bitmap)currentTextbox.Clone();
                    _waitingForStableFrame = true;
                    _processingStableFrame = false;
                    
                    currentFrame.Dispose();
                    currentTextbox?.Dispose();
                    RecordFrameTime();
                    return;
                }
                // else: No cached textbox yet - continue to full detection below
            }
            
            // Only return early if we're not doing full detection
            if (shouldProcessOcr)
            {
                // We already have the textbox from cache and it's stable - skip to OCR
                // (This path handled after textbox detection section)
            }
            else if (currentTextbox != null)
            {
                // Have cached textbox but not ready to process - already handled above
                currentFrame.Dispose();
                currentTextbox?.Dispose();
                RecordFrameTime();
                return;
            }
            // else: Need full textbox detection - continue below
            
            // OPTIMIZATION 3: Skip detection if cached (0.2ms vs 10ms for 95% of frames)
            Rectangle textboxRect;
            
            if (currentTextboxRect.HasValue)
            {
                // Already cropped above using cached position
                textboxRect = currentTextboxRect.Value;
            }
            else
            {
                // Slow path: Full textbox detection (5% of frames)
                var detected = _detector.DetectTextbox(currentFrame);
                
                if (!detected.HasValue)
                {
                    // No textbox found - reset state
                    if (_stats.TotalFramesProcessed % 50 == 0) // Log every 50 frames
                    {
                        _logger?.LogDebug("No textbox detected in frame {Count}", _stats.TotalFramesProcessed);
                    }
                    
                    lock (_lockObject)
                    {
                        _processingStableFrame = false;
                        _waitingForStableFrame = true;
                    }
                    
                    currentFrame.Dispose();
                    currentTextbox?.Dispose();
                    RecordFrameTime();
                    return;
                }
                
                textboxRect = detected.Value;
                _cachedTextboxRect = textboxRect;
                _cacheAge = 0;
                _stats.FullTextboxDetections++;
                
                _logger?.LogInformation("🎯 Textbox found: {Rect}", textboxRect);
                
                // Crop the textbox now
                currentTextbox?.Dispose();
                currentTextbox = CropImage(currentFrame, textboxRect);
                
                // Store this as the last textbox for comparison on next frame
                lock (_lockObject)
                {
                    _lastTextbox?.Dispose();
                    _lastTextbox = (Bitmap)currentTextbox.Clone();
                    _waitingForStableFrame = true;
                    _processingStableFrame = false;
                }
                
                // Don't process OCR yet - wait for stability on next frame
                currentFrame.Dispose();
                currentTextbox?.Dispose();
                RecordFrameTime();
                return;
            }
            
            // OPTIMIZATION 4: Content hash check (0.1ms)
            if (_config.EnableHashCheck)
            {
                var hash = GetImageHash(currentTextbox!);
                
                if (hash == _lastTextboxHash)
                {
                    // Same content - skip OCR
                    lock (_lockObject)
                    {
                        _processingStableFrame = false;
                    }
                    
                    _stats.OcrSkippedDueToHashMatch++;
                    currentFrame.Dispose();
                    currentTextbox?.Dispose();
                    RecordFrameTime();
                    return;
                }
                
                _lastTextboxHash = hash;
            }
            
            // OPTIMIZATION 5: Async OCR (30-100ms, non-blocking)
            var textboxCopy = (Bitmap)currentTextbox!.Clone();
            
            lock (_lockObject)
            {
                _processingStableFrame = false; // Reset for next cycle
            }
            
            currentFrame.Dispose();
            currentTextbox?.Dispose();
            RecordFrameTime();
            
            // Process OCR asynchronously (don't block the capture loop)
            _ = Task.Run(async () => await ProcessOcrAsync(textboxCopy));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing frame");
            RecordFrameTime();
        }
    }
    
    private async Task ProcessOcrAsync(Bitmap textboxImage)
    {
        try
        {
            // Extract text
            var text = _ocr.ExtractTextFast(textboxImage)?.Trim();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            
            // Store original OCR text before fixes
            var originalOcrText = text;
            
            // Apply OCR fixes
            text = _applyOcrFixes(text);
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            
            // Normalize for deduplication
            var normalized = _normalizeText(text);
            
            // Check for duplicates
            lock (_lockObject)
            {
                if (string.Equals(normalized, _lastNormalizedText, StringComparison.Ordinal))
                {
                    return; // Exact duplicate
                }
                
                if (_seenNormalized.Contains(normalized))
                {
                    return; // Seen in this session
                }
                
                _lastNormalizedText = normalized;
                _seenNormalized.Add(normalized);
            }
            
            _stats.DialoguesDetected++;
            
            // Raise event
            var args = new DialogueDetectedEventArgs
            {
                Text = text,
                OriginalOcrText = originalOcrText,
                Timestamp = DateTime.UtcNow
            };
            
            DialogueDetected?.Invoke(this, args);
            
            _logger?.LogInformation("Dialogue detected: {Text}", text.Length > 60 ? text.Substring(0, 60) + "…" : text);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing OCR");
        }
        finally
        {
            textboxImage?.Dispose();
        }
    }
    
    private void RecordFrameTime()
    {
        _frameTimer.Stop();
        var ms = _frameTimer.Elapsed.TotalMilliseconds;
        
        _frameTimes.Add(ms);
        
        // Keep last 100 frame times for rolling average
        if (_frameTimes.Count > 100)
        {
            _frameTimes.RemoveAt(0);
        }
        
        _stats.AverageFrameProcessingMs = _frameTimes.Average();
    }
    
    #region Helper Methods
    
    private Bitmap CropImage(Bitmap source, Rectangle cropRect)
    {
        var actualRect = Rectangle.Intersect(cropRect, new Rectangle(0, 0, source.Width, source.Height));
        
        if (actualRect.IsEmpty)
            return new Bitmap(1, 1);
        
        var croppedImage = new Bitmap(actualRect.Width, actualRect.Height);
        using (var g = Graphics.FromImage(croppedImage))
        {
            g.DrawImage(source, 0, 0, actualRect, GraphicsUnit.Pixel);
        }
        return croppedImage;
    }
    
    private string GetImageHash(Bitmap image)
    {
        // Hash focused on TEXT CONTENT area (center 60%)
        try
        {
            int hash = image.Width * 31 + image.Height;
            
            if (image.Width > 40 && image.Height > 20)
            {
                int centerX = image.Width / 2;
                int centerY = image.Height / 2;
                int textWidth = (int)(image.Width * 0.6);
                int textHeight = (int)(image.Height * 0.6);
                
                // Sample 7 points across text content area
                var samples = new[]
                {
                    image.GetPixel(centerX - textWidth/4, centerY - textHeight/4),  // Upper left
                    image.GetPixel(centerX + textWidth/4, centerY - textHeight/4),  // Upper right
                    image.GetPixel(centerX - textWidth/4, centerY + textHeight/4),  // Lower left
                    image.GetPixel(centerX + textWidth/4, centerY + textHeight/4),  // Lower right
                    image.GetPixel(centerX, centerY),                               // Center
                    image.GetPixel(centerX - textWidth/2, centerY),                // Left
                    image.GetPixel(centerX + textWidth/2, centerY),                // Right
                };
                
                foreach (var pixel in samples)
                {
                    hash = hash * 31 + pixel.ToArgb();
                }
            }
            
            return hash.ToString();
        }
        catch
        {
            // Fallback to timestamp-based hash
            return DateTime.Now.Ticks.ToString();
        }
    }
    
    #endregion
    
    public void Dispose()
    {
        _timer?.Dispose();
        
        lock (_lockObject)
        {
            _lastTextbox?.Dispose();
            _lastTextbox = null;
        }
    }
}
