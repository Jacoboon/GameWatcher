using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GameWatcher.Runtime.Services.Capture;
using GameWatcher.Runtime.Services.OCR;
using GameWatcher.Runtime.Services.Dialogue;
using GameWatcher.Engine.Detection;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Runtime.Services.Capture
{
    /// <summary>
    /// Core game capture service for GameWatcher V2 Platform
    /// Uses configurable ITextboxDetector for game-agnostic detection
    /// </summary>
    public class GameCaptureService : IDisposable
    {
        private System.Threading.Timer? _captureTimer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        
        // Core components
        private readonly ITextboxDetector _detector;
        private readonly IOcrEngine _ocr;
        private readonly ILogger<GameCaptureService> _logger;
        
        // Configuration
        private readonly int _captureIntervalMs;
        private readonly bool _enableOptimization;
        private readonly int _notBusyThreshold;    // Frame similarity threshold when not busy
        private readonly int _busyThreshold;        // Frame similarity threshold when busy
        private readonly bool _enableDuplicateDetection;
        
        // Frame processing state
        private Bitmap? _lastFrame;
        private string _lastText = "";
        private readonly object _lockObject = new();
        
        // Simple processing state tracking
        private bool _isBusy = false;
        private Rectangle? _lastTextboxRect = null;
        private string _lastTextboxHash = "";
        
        // Performance tracking
        private int _frameCount = 0;
        private int _processedFrames = 0;
        private int _textboxesFound = 0;
        private DateTime _startTime = DateTime.Now;
        private long _totalProcessingTime = 0;
        
        // Events for integration with Activity Monitor
        public event EventHandler<CaptureProgressEventArgs>? ProgressReported;
        public event EventHandler<DialogueDetectedEventArgs>? DialogueDetected;
        
        public GameCaptureService(
            ITextboxDetector detector, 
            IOcrEngine ocr, 
            ILogger<GameCaptureService> logger, 
            int captureFps = 15,
            bool enableOptimization = true,
            double optimizationThreshold = 0.85,
            bool enableDuplicateDetection = true)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Calculate capture interval from FPS (e.g., 10 FPS = 100ms, 15 FPS = 67ms)
            _captureIntervalMs = captureFps > 0 ? (int)(1000.0 / captureFps) : 67;
            
            // Store optimization settings
            _enableOptimization = enableOptimization;
            _enableDuplicateDetection = enableDuplicateDetection;
            
            // Convert optimization threshold (0.0 - 1.0) to pixel difference thresholds
            // Higher threshold = more tolerant of differences (fewer pixels need to match)
            // Lower threshold = stricter matching (more pixels must match)
            // 
            // Example: 0.85 threshold means 15% of pixels can differ
            // For 1920x1080 image (2,073,600 pixels), 15% = ~310,000 pixel difference
            // We scale this to a simpler threshold for the AreImagesSimilar function
            //
            // Not-busy threshold: More tolerant (500 default) to detect new textbox appearances
            // Busy threshold: Very strict (50 default) to catch small text changes
            _notBusyThreshold = (int)(500 * (1.0 - optimizationThreshold + 0.15)); // Scale: 0.85 → 500, 0.5 → 825
            _busyThreshold = (int)(50 * (1.0 - optimizationThreshold + 0.15));     // Scale: 0.85 → 50, 0.5 → 82
            
            _logger.LogInformation(
                "GameCaptureService initialized - {Fps} FPS ({Interval}ms), Optimization: {OptEnabled} (threshold: {OptValue:F2}, not-busy: {NotBusy}, busy: {Busy}), Duplicate Detection: {DupEnabled}", 
                captureFps, _captureIntervalMs, 
                enableOptimization ? "ON" : "OFF", optimizationThreshold, _notBusyThreshold, _busyThreshold,
                enableDuplicateDetection ? "ON" : "OFF");
            
            InitializeComponents();
        }
        
        private void InitializeComponents()
        {
            try
            {
                _logger.LogInformation("GameCaptureService components initialized successfully");
                
                // Test capture to show what window we're detecting
                var testCapture = ScreenCapture.CaptureGameWindow();
                _logger.LogInformation("Capture resolution: {Width}x{Height}", testCapture.Width, testCapture.Height);
                testCapture.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing GameCaptureService");
            }
        }
        
        public bool IsRunning => _isRunning;
        
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
        
        public Task<bool> StartCaptureAsync()
        {
            if (_isRunning)
            {
                _logger.LogWarning("StartCaptureAsync called but service is already running");
                return Task.FromResult(false);
            }
            
            try
            {
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                _startTime = DateTime.Now;
                
                // Reset statistics
                _frameCount = 0;
                _processedFrames = 0;
                _textboxesFound = 0;
                _totalProcessingTime = 0;
                _isBusy = false;
                
                // Start capture timer with configured interval
                _captureTimer = new System.Threading.Timer(CaptureAndProcess, null, 0, _captureIntervalMs);
                
                var actualFps = 1000.0 / _captureIntervalMs;
                _logger.LogInformation("GameCaptureService started - {Fps:F1} FPS ({Interval}ms intervals)", 
                    actualFps, _captureIntervalMs);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting GameCaptureService");
                _isRunning = false;
                return Task.FromResult(false);
            }
        }
        
        public Task<bool> StopCaptureAsync()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("StopCaptureAsync called but service is not running");
                return Task.FromResult(false);
            }

            try
            {
                _logger.LogInformation("Stopping GameCaptureService...");
                
                // Set running flag first to stop new timer callbacks
                _isRunning = false;
                
                // Cancel any ongoing operations
                _cancellationTokenSource?.Cancel();
                
                // Stop and dispose the timer - wait for completion
                if (_captureTimer != null)
                {
                    using var waitHandle = new ManualResetEvent(false);
                    _captureTimer.Dispose(waitHandle);
                    waitHandle.WaitOne(TimeSpan.FromSeconds(2)); // Wait up to 2 seconds
                    _captureTimer = null;
                }
                
                // Wait a bit for any in-flight callbacks to finish
                Thread.Sleep(100);
                
                // Cleanup resources
                lock (_lockObject)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }
                
                _logger.LogInformation("GameCaptureService stopped successfully");
                
                // Report final statistics
                var stats = GetStatistics();
                ProgressReported?.Invoke(this, new CaptureProgressEventArgs(stats));
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping GameCaptureService");
                return Task.FromResult(false);
            }
        }        private void CaptureAndProcess(object? state)
        {
            // Check running state first
            if (!_isRunning) 
            {
                _logger.LogDebug("Capture callback received but service already stopped");
                return;
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Step 1: Capture game window
                var currentFrame = ScreenCapture.CaptureGameWindow();
                _frameCount++;

                // Step 2: Smart frame processing logic with dynamic similarity thresholds
                lock (_lockObject)
                {
                    bool frameMatches;
                    
                    // Skip optimization if disabled
                    if (!_enableOptimization)
                    {
                        frameMatches = false; // Always process frames when optimization is off
                        _logger.LogTrace("Frame optimization disabled - processing frame");
                    }
                    else if (!_isBusy)
                    {
                        // Not busy = use lower threshold (more tolerant) to find new textbox appearances 
                        frameMatches = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, _notBusyThreshold);
                        if (frameMatches)
                            _logger.LogTrace("Frame matches (not-busy threshold: {Threshold})", _notBusyThreshold);
                    }
                    else
                    {
                        // Busy = use very high threshold (99% similar) to catch text changes
                        frameMatches = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, _busyThreshold);
                        if (frameMatches)
                            _logger.LogTrace("Frame matches (busy threshold: {Threshold})", _busyThreshold);
                    }
                    
                    if (frameMatches && !_isBusy)
                    {
                        // Fuzzy match + not busy = new stable frame detected, process it
                        _isBusy = true;
                        _logger.LogDebug("Stable frame detected - processing for textbox");
                        // Continue to textbox detection below
                    }
                    else if (frameMatches && _isBusy)
                    {
                        // Exact match + busy = same text as before, skip processing
                        currentFrame.Dispose();
                        return;
                    }
                    else
                    {
                        // No match = frame has changed, reset busy state and update last frame
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

                // Step 3: Check stable frame for textbox
                var textboxRect = _detector?.DetectTextbox(currentFrame);
                
                if (textboxRect.HasValue)
                {
                    _textboxesFound++;
                    
                    // Step 4: Check if this textbox position has changed
                    bool textboxPositionChanged = !_lastTextboxRect.HasValue || 
                        Math.Abs(_lastTextboxRect.Value.X - textboxRect.Value.X) > 5 ||
                        Math.Abs(_lastTextboxRect.Value.Y - textboxRect.Value.Y) > 5 ||
                        Math.Abs(_lastTextboxRect.Value.Width - textboxRect.Value.Width) > 10 ||
                        Math.Abs(_lastTextboxRect.Value.Height - textboxRect.Value.Height) > 10;
                    
                    if (textboxPositionChanged)
                    {
                        _logger.LogDebug("Textbox detected at X:{X}, Y:{Y}, W:{W}, H:{H}", 
                            textboxRect.Value.X, textboxRect.Value.Y, textboxRect.Value.Width, textboxRect.Value.Height);
                        _lastTextboxRect = textboxRect.Value;
                    }

                    // Step 5: Crop textbox area and check for content changes
                    var textboxImage = CropImage(currentFrame, textboxRect.Value);
                    var textboxHash = GetImageHash(textboxImage);
                    
                    // Only process OCR if textbox content has actually changed (unless duplicate detection is disabled)
                    bool shouldProcessOcr = !_enableDuplicateDetection || textboxHash != _lastTextboxHash;
                    
                    if (shouldProcessOcr)
                    {
                        if (_enableDuplicateDetection)
                        {
                            _lastTextboxHash = textboxHash;
                            _logger.LogDebug("Unique textbox detected, processing OCR");
                        }
                        else
                        {
                            _logger.LogDebug("Duplicate detection disabled - processing OCR regardless");
                        }
                        
                        // Create copies for async processing
                        var textboxCopy = new Bitmap(textboxImage);
                        
                        // Step 6: OCR the text (async to not block the loop)
                        Task.Run(async () => {
                            try 
                            {
                                _logger.LogDebug("Running OCR on textbox...");
                                
                                var rawText = _ocr?.ExtractTextFast(textboxCopy) ?? "";
                                _logger.LogDebug("Raw OCR result: '{Text}' (length: {Length})", rawText, rawText.Length);
                                
                                var cleanedText = CleanOCRText(rawText);
                                _logger.LogDebug("Cleaned text: '{Text}' (length: {Length})", cleanedText, cleanedText.Length);
                                
                                if (!string.IsNullOrWhiteSpace(cleanedText))
                                {
                                    await ProcessNewDialogue(cleanedText);
                                }
                                else
                                {
                                    _logger.LogDebug("No meaningful text extracted from textbox");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "OCR processing failed");
                            }
                            finally
                            {
                                textboxCopy?.Dispose();
                            }
                        });
                    }

                    textboxImage.Dispose();
                }
                else
                {
                    // No textbox detected - only save debug snapshot if this is a state change
                    if (_lastTextboxRect.HasValue)
                    {
                        _logger.LogDebug("Textbox disappeared from frame");
                        _lastTextboxRect = null;
                        _lastTextboxHash = "";
                    }
                    
                    // Enhanced debugging for textbox detection failures
                    if (_frameCount % 50 == 0) // Log every 50 frames instead of 200
                    {
                        _logger.LogDebug("No textbox found in stable frame (frame {Count}) - Frame size: {Width}x{Height}", 
                            _frameCount, currentFrame.Width, currentFrame.Height);
                    }
                }

                currentFrame.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in capture loop");
            }

            stopwatch.Stop();
            _totalProcessingTime += stopwatch.ElapsedMilliseconds;
            
            // Enhanced performance monitoring with better thresholds
            var processingTime = stopwatch.ElapsedMilliseconds;
            if (processingTime > 100)
            {
                _logger.LogWarning("SLOW: Frame processing took {Time}ms", processingTime);
            }
            
            // Report progress every 30 frames (~2 seconds at 15fps)
            if (_frameCount % 30 == 0)
            {
                var stats = GetStatistics();
                ProgressReported?.Invoke(this, new CaptureProgressEventArgs(stats));
            }
        }
        
        private Task ProcessNewDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || 
                text == "[OCR not available]" || 
                text == "[OCR Error]" || 
                text == "[REJECTED: Low Quality OCR]" ||
                text == _lastText)
            {
                return Task.CompletedTask;
            }
                
            _lastText = text;
            
            _logger.LogInformation("🎉 >>> NEW DIALOGUE DETECTED: \"{Text}\"", text);
            
            // Create a basic dialogue entry for V2 platform integration
            var entry = new DialogueEntry
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                Speaker = "Unknown", // TODO: Implement speaker detection
                FirstSeen = DateTime.Now,
                LastSeen = DateTime.Now,
                RawOcrText = text
            };
            
            // Notify listeners of new dialogue (Activity Monitor will pick this up)
            DialogueDetected?.Invoke(this, new DialogueDetectedEventArgs(entry));
            
            return Task.CompletedTask;
        }
        
        #region Image Processing Methods
        
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
        
        private string CleanOCRText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            if (IsOCRGarbage(text))
            {
                _logger.LogDebug("OCR quality filter: Rejecting garbage text");
                return "[REJECTED: Low Quality OCR]";
            }
            
            var cleaned = text;
            
            // Apply basic cleaning rules
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
        
        private string GetImageHash(Bitmap image)
        {
            try
            {
                int hash = image.Width * 31 + image.Height;
                
                if (image.Width > 40 && image.Height > 20)
                {
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
                        image.GetPixel(centerX - textWidth/2, centerY),
                        image.GetPixel(centerX + textWidth/2, centerY),
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
        
        private bool IsOCRGarbage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            
            var cleanText = text.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            
            if (cleanText.Length < 3) return true;
            if (cleanText.Length > 500) return true;
            
            int letters = 0;
            int digits = 0;
            int symbols = 0;
            
            foreach (char c in cleanText)
            {
                if (char.IsLetter(c)) letters++;
                else if (char.IsDigit(c)) digits++;
                else symbols++;
            }
            
            int totalChars = cleanText.Length;
            
            if ((double)letters / totalChars < 0.4) return true;
            if ((double)symbols / totalChars > 0.3) return true;
            if ((double)digits / totalChars > 0.3) return true;
            
            return false;
        }
        
        #endregion
        
        public void Dispose()
        {
            try
            {
                _isRunning = false;
                
                _captureTimer?.Dispose();
                _captureTimer = null;
                
                _cancellationTokenSource?.Cancel();
                
                lock (_lockObject)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }
                
                _cancellationTokenSource?.Dispose();
                _ocr?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during GameCaptureService disposal");
            }
        }
    }
    
    /// <summary>
    /// Progress event arguments for capture statistics
    /// </summary>
    public class CaptureProgressEventArgs : EventArgs
    {
        public CaptureStatistics Statistics { get; }
        
        public CaptureProgressEventArgs(CaptureStatistics statistics)
        {
            Statistics = statistics;
        }
    }
    
    /// <summary>
    /// Event arguments for dialogue detection
    /// </summary>
    public class DialogueDetectedEventArgs : EventArgs
    {
        public DialogueEntry DialogueEntry { get; }
        
        public DialogueDetectedEventArgs(DialogueEntry dialogueEntry)
        {
            DialogueEntry = dialogueEntry;
        }
    }
    
    /// <summary>
    /// Statistics for capture performance
    /// </summary>
    public class CaptureStatistics
    {
        public int FrameCount { get; set; }
        public int ProcessedFrames { get; set; }
        public int TextboxesFound { get; set; }
        public double ActualFps { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public TimeSpan Runtime { get; set; }
    }
}