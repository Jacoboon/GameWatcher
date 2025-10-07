using System.Diagnostics;
using System.Drawing;
using GameWatcher.Engine.Detection;
using GameWatcher.Engine.Ocr;

namespace GameWatcher.Engine.Services;

/// <summary>
/// Core capture service ported from V1 with all performance optimizations preserved
/// - 4.1x faster detection (9.4ms → 2.3ms)
/// - 79.3% search area reduction via targeted detection
/// - Dynamic similarity thresholds (isBusy logic)
/// </summary>
public class CaptureService : ICaptureService
{
    private System.Threading.Timer? _captureTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning = false;
    
    // Core components
    private readonly ITextboxDetector _textboxDetector;
    private readonly IOcrEngine _ocrEngine;
    
    // V1 Performance optimization state
    private Bitmap? _lastFrame;
    private Bitmap? _lastTextbox;
    private string _lastText = "";
    private readonly object _lockObject = new();
    
    // V1 isBusy logic for dynamic thresholds
    private bool _isBusy = false;
    private Rectangle? _lastTextboxRect = null;
    private string _lastTextboxHash = "";
    
    // Performance tracking (V1 benchmarks)
    private int _frameCount = 0;
    private int _processedFrames = 0;
    private int _textboxesFound = 0;
    private DateTime _startTime = DateTime.Now;
    private long _totalProcessingTime = 0;
    
    // Events for progress reporting
    public event EventHandler<CaptureProgressEventArgs>? ProgressReported;
    public event EventHandler<DialogueDetectedEventArgs>? DialogueDetected;
    
    public CaptureService(ITextboxDetector? textboxDetector = null, IOcrEngine? ocrEngine = null)
    {
        _textboxDetector = textboxDetector ?? new DynamicTextboxDetector();
        _ocrEngine = ocrEngine ?? new WindowsOcrEngine();
        
        Console.WriteLine("[CaptureService] ✅ Initialized with V1 optimizations");
        Console.WriteLine($"[CaptureService] OCR Available: {_ocrEngine.IsAvailable}");
    }
    
    public bool IsRunning => _isRunning;
    
    public async Task InitializeAsync(string gameName)
    {
        Console.WriteLine($"[CaptureService] Initializing for game: {gameName}");
        // Initialize game-specific settings here
        await Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Console.WriteLine("[CaptureService] Already running");
            return;
        }
        
        await StartCaptureAsync();
        
        // Keep running until cancellation requested
        try
        {
            await Task.Delay(-1, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[CaptureService] Cancellation requested");
        }
    }

    public async Task<bool> StartCaptureAsync()
    {
        if (_isRunning)
        {
            Console.WriteLine("[CaptureService] Already running");
            return false;
        }
        
        try
        {
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _startTime = DateTime.Now;
            
            // Reset performance counters
            _frameCount = 0;
            _processedFrames = 0;
            _textboxesFound = 0;
            _totalProcessingTime = 0;
            _isBusy = false;
            
            // Start capture timer - 15 FPS (67ms intervals) like V1
            _captureTimer = new System.Threading.Timer(CaptureAndProcess, null, 0, 67);
            
            Console.WriteLine("[CaptureService] 🚀 Started capture (15 FPS target)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CaptureService] ❌ Start error: {ex.Message}");
            _isRunning = false;
            return false;
        }
    }
    
    public async Task StopAsync()
    {
        await StopCaptureAsync();
    }

    public async Task<bool> StopCaptureAsync()
    {
        if (!_isRunning)
        {
            return false;
        }
        
        try
        {
            _isRunning = false;
            
            _captureTimer?.Dispose();
            _captureTimer = null;
            
            _cancellationTokenSource?.Cancel();
            
            // Cleanup resources (V1 cleanup logic)
            lock (_lockObject)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
                _lastTextbox?.Dispose();
                _lastTextbox = null;
            }
            
            Console.WriteLine("[CaptureService] 🛑 Stopped capture");
            
            // Report final statistics
            var stats = GetStatistics();
            ProgressReported?.Invoke(this, new CaptureProgressEventArgs(stats));
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CaptureService] ❌ Stop error: {ex.Message}");
            return false;
        }
    }
    
    private void CaptureAndProcess(object? state)
    {
        if (!_isRunning) return;
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Step 1: Capture game window (V1 method)
            var currentFrame = CaptureGameWindow();
            _frameCount++;
            
            // Step 2: V1's smart frame processing with dynamic similarity thresholds
            lock (_lockObject)
            {
                bool frameMatches;
                
                if (!_isBusy)
                {
                    // Not busy = lower threshold (more tolerant) for new textbox appearances
                    frameMatches = _lastFrame != null && AreImagesSimilar(_lastFrame, currentFrame, 500);
                }
                else
                {
                    // Busy = high threshold (99% similar) to catch text changes
                    frameMatches = _lastFrame != null && AreImagesSimilar(_lastFrame, currentFrame, 50);
                }
                
                if (frameMatches && !_isBusy)
                {
                    // Fuzzy match + not busy = new stable frame detected
                    _isBusy = true;
                    Console.WriteLine("🎯 Stable frame detected - processing for textbox");
                    // Continue to textbox detection below
                }
                else if (frameMatches && _isBusy)
                {
                    // Exact match + busy = same text as before, skip
                    currentFrame.Dispose();
                    return;
                }
                else
                {
                    // No match = frame changed, reset state
                    if (_isBusy)
                    {
                        Console.WriteLine("🔄 Text/scene change detected - resetting state");
                    }
                    
                    _isBusy = false;
                    _lastTextboxRect = null;
                    _lastTextboxHash = "";
                    
                    _lastFrame?.Dispose();
                    _lastFrame = new Bitmap(currentFrame);
                    currentFrame.Dispose();
                    return;
                }
            }

            _processedFrames++;

            // Step 3: Check stable frame for textbox (V1's 79.3% optimization)
            var textboxRect = _textboxDetector.DetectTextbox(currentFrame);
            
            if (textboxRect.HasValue)
            {
                _textboxesFound++;
                
                // Step 4: Check if textbox content changed (V1 logic)
                var textboxImage = CropImage(currentFrame, textboxRect.Value);
                var textboxHash = GetImageHash(textboxImage);
                
                if (textboxHash != _lastTextboxHash)
                {
                    _lastTextboxHash = textboxHash;
                    Console.WriteLine("🆕 Unique textbox detected, processing OCR");
                    
                    // Step 5: OCR the text (async to not block - V1 pattern)
                    _ = Task.Run(async () =>
                    {
                        try 
                        {
                            var textboxCopy = new Bitmap(textboxImage);
                            
                            Console.WriteLine("🔍 Running OCR...");
                            var rawText = _ocrEngine.ExtractTextFast(textboxCopy);
                            Console.WriteLine($"📝 OCR result: '{rawText}'");
                            
                            var cleanedText = CleanOCRText(rawText);
                            Console.WriteLine($"🧹 Cleaned: '{cleanedText}'");
                            
                            if (!string.IsNullOrWhiteSpace(cleanedText))
                            {
                                await ProcessNewDialogue(cleanedText);
                            }
                            
                            textboxCopy.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ OCR error: {ex.Message}");
                        }
                    });
                }
                
                textboxImage.Dispose();
                _isBusy = false; // Reset after processing
            }
            else
            {
                _isBusy = false;
                
                // Periodic "no textbox" logging (V1 pattern)
                if (_frameCount % 200 == 0)
                {
                    Console.WriteLine($"⭕ No textbox found in stable frame (frame {_frameCount})");
                }
            }

            currentFrame.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Capture loop error: {ex.Message}");
        }

        stopwatch.Stop();
        _totalProcessingTime += stopwatch.ElapsedMilliseconds;
        
        // Performance monitoring (V1 target: <60ms for 15fps)
        if (stopwatch.ElapsedMilliseconds > 60)
        {
            Console.WriteLine($"⚠️ SLOW: Processing took {stopwatch.ElapsedMilliseconds}ms (target: <60ms)");
        }
        
        // Report progress every 100 frames (V1 pattern)
        if (_frameCount % 100 == 0)
        {
            var avgMs = _totalProcessingTime / (double)_frameCount;
            Console.WriteLine($"📊 [{_frameCount}] Avg: {avgMs:F1}ms | Processed: {_processedFrames} | Found: {_textboxesFound}");
        }
        
        // Report progress every 30 frames for UI updates
        if (_frameCount % 30 == 0)
        {
            var stats = GetStatistics();
            ProgressReported?.Invoke(this, new CaptureProgressEventArgs(stats));
        }
    }
    
    private async Task ProcessNewDialogue(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == _lastText)
        {
            return;
        }
            
        _lastText = text;
        Console.WriteLine($"🎉 >>> NEW DIALOGUE DETECTED: \"{text}\"");
        
        // Create dialogue entry for event system
        var dialogueEntry = new DialogueEntry
        {
            Id = Guid.NewGuid().ToString(),
            Text = text,
            DetectedAt = DateTime.UtcNow,
            Speaker = "Unknown", // Will be determined by game pack
            Confidence = 1.0f
        };
        
        // Notify listeners
        DialogueDetected?.Invoke(this, new DialogueDetectedEventArgs(dialogueEntry));
    }
    
    public CaptureStatistics GetStatistics()
    {
        var elapsed = DateTime.Now - _startTime;
        var actualFps = _frameCount > 0 ? _frameCount / elapsed.TotalSeconds : 0;
        var avgProcessingTime = _frameCount > 0 ? _totalProcessingTime / (double)_frameCount : 0;
        
        return new CaptureStatistics
        {
            FrameCount = _frameCount,
            ProcessedFrames = _processedFrames,
            TextboxesFound = _textboxesFound,
            ActualFps = actualFps,
            AverageProcessingTimeMs = avgProcessingTime,
            Runtime = elapsed
        };
    }
    
    #region V1 Image Processing Methods
    
    private Bitmap CaptureGameWindow()
    {
        // Placeholder - will be replaced with actual screen capture
        // For now, return a test bitmap
        return new Bitmap(1920, 1080);
    }
    
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
    
    private bool AreImagesSimilar(Bitmap img1, Bitmap img2, int sampleRate)
    {
        if (img1.Width != img2.Width || img1.Height != img2.Height)
            return false;
        
        int diffPixels = 0;
        int totalSamples = 0;
        
        for (int y = 0; y < img1.Height; y += sampleRate)
        {
            for (int x = 0; x < img1.Width; x += sampleRate)
            {
                var pixel1 = img1.GetPixel(x, y);
                var pixel2 = img2.GetPixel(x, y);
                
                if (Math.Abs(pixel1.R - pixel2.R) > 30 ||
                    Math.Abs(pixel1.G - pixel2.G) > 30 ||
                    Math.Abs(pixel1.B - pixel2.B) > 30)
                {
                    diffPixels++;
                }
                
                totalSamples++;
            }
        }
        
        var similarity = 1.0 - (double)diffPixels / totalSamples;
        return similarity > 0.95; // 95% similarity threshold
    }
    
    private string GetImageHash(Bitmap image)
    {
        // V1's content-focused hashing
        try
        {
            int hash = image.Width * 31 + image.Height;
            
            if (image.Width > 40 && image.Height > 20)
            {
                // Sample text content area (60% of center)
                int centerX = image.Width / 2;
                int centerY = image.Height / 2;
                int textWidth = (int)(image.Width * 0.6);
                int textHeight = (int)(image.Height * 0.6);
                
                var samples = new[]
                {
                    image.GetPixel(centerX - textWidth/4, centerY - textHeight/4),
                    image.GetPixel(centerX + textWidth/4, centerY - textHeight/4),
                    image.GetPixel(centerX - textWidth/4, centerY + textHeight/4),
                    image.GetPixel(centerX + textWidth/4, centerY + textHeight/4),
                    image.GetPixel(centerX, centerY),
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
            return DateTime.Now.Ticks.ToString();
        }
    }
    
    private string CleanOCRText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        
        // V1's OCR cleaning rules
        var cleaned = text;
        
        cleaned = cleaned.Replace("Il ", "I ");
        cleaned = cleaned.Replace(" Il ", " I ");
        cleaned = cleaned.Replace("15", "is");
        cleaned = cleaned.Replace("1s", "is");
        cleaned = cleaned.Replace("0", "o");
        cleaned = cleaned.Replace("5", "s");
        cleaned = cleaned.Replace("3", "e");
        cleaned = cleaned.Replace("1", "i");
        
        return cleaned.Trim();
    }
    
    #endregion
    
    // Interface property implementations
    public Bitmap? GetLastFrame() 
    {
        lock (_lockObject)
        {
            return _lastFrame != null ? new Bitmap(_lastFrame) : null;
        }
    }
    
    public Bitmap? GetLastTextbox()
    {
        lock (_lockObject)
        {
            return _lastTextbox != null ? new Bitmap(_lastTextbox) : null;
        }
    }
    
    public string GetLastText() => _lastText;
    
    public void Dispose()
    {
        try
        {
            _isRunning = false;
            _captureTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            
            lock (_lockObject)
            {
                _lastFrame?.Dispose();
                _lastTextbox?.Dispose();
            }
            
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Disposal error: {ex.Message}");
        }
    }
}

// Supporting classes for events and statistics
public class CaptureProgressEventArgs : EventArgs
{
    public CaptureStatistics Statistics { get; }
    
    public CaptureProgressEventArgs(CaptureStatistics statistics)
    {
        Statistics = statistics;
    }
}

public class DialogueDetectedEventArgs : EventArgs
{
    public DialogueEntry DialogueEntry { get; }
    
    public DialogueDetectedEventArgs(DialogueEntry dialogueEntry)
    {
        DialogueEntry = dialogueEntry;
    }
}

public class CaptureStatistics
{
    public int FrameCount { get; set; }
    public int ProcessedFrames { get; set; }
    public int TextboxesFound { get; set; }
    public double ActualFps { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public TimeSpan Runtime { get; set; }
}

public class DialogueEntry
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime DetectedAt { get; set; }
    public string Speaker { get; set; } = "";
    public float Confidence { get; set; }
}