using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimpleLoop.Services;

namespace SimpleLoop
{
    /// <summary>
    /// Provides a reusable service for capturing game frames and processing text detection
    /// Extracted from Program.cs to enable GUI integration with background processing
    /// </summary>
    public class CaptureService : IDisposable
    {
        private System.Threading.Timer? _captureTimer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        
        // Core components
        private ITextboxDetector? _detector;
        private IOcrEngine? _ocr;
        private DialogueCatalog? _catalog;
        private SpeakerCatalog? _speakerCatalog;
        
        // Frame processing state
        private Bitmap? _lastFrame;
        private Bitmap? _lastTextbox;
        private string _lastText = "";
        private readonly object _lockObject = new();
        
        // Enhanced stability detection state
        private bool _waitingForStableFrame = true;
        private DateTime _frameStableStartTime = DateTime.MinValue;
        private Rectangle? _lastTextboxRect = null;
        private string _lastTextboxHash = "";
        private const int STABILITY_DELAY_MS = 300; // Reduced to 300ms for more responsive detection
        
        // Performance tracking
        private int _frameCount = 0;
        private int _processedFrames = 0;
        private int _textboxesFound = 0;
        private DateTime _startTime = DateTime.Now;
        private long _totalProcessingTime = 0;
        
        // Events for progress reporting
        public event EventHandler<CaptureProgressEventArgs>? ProgressReported;
        public event EventHandler<DialogueDetectedEventArgs>? DialogueDetected;
        
        // File-based logging to prevent GUI thread contention
        private CaptureLogger? _logger;
        public CaptureLogger? Logger => _logger;
        
        // Debug snapshot manager - DISABLED for production
        // private SnapshotManager? _snapshotManager;
        
        // TTS integration
        private TtsManager? _ttsManager;
        
        public CaptureService() : this(null, null)
        {
        }
        
        public CaptureService(string? speakerCatalogPath = null, string? dialogueCatalogPath = null)
        {
            InitializeComponents(speakerCatalogPath, dialogueCatalogPath);
        }
        
        private void InitializeComponents(string? speakerCatalogPath = null, string? dialogueCatalogPath = null)
        {
            try
            {
                // Create logs in a predictable location relative to the SimpleLoop directory
                var logDirectory = Path.Combine(Path.GetDirectoryName(speakerCatalogPath ?? "speaker_catalog.json") ?? ".", "logs");
                Console.WriteLine($"[CaptureService] Creating logs in: {Path.GetFullPath(logDirectory)}");
                _logger = new CaptureLogger(logDirectory);
        // _snapshotManager = null; // Disabled: No debug snapshots
        _detector = new DynamicTextboxDetector();
        _ocr = new WindowsOCR();                // Use provided paths or defaults for data files
                var speakerPath = speakerCatalogPath ?? "speaker_catalog.json";
                var dialoguePath = dialogueCatalogPath ?? "dialogue_catalog.json";
                
                _speakerCatalog = new SpeakerCatalog(speakerPath);
                _catalog = new DialogueCatalog(dialoguePath, _speakerCatalog);
                
                // Initialize TTS manager if configuration is available
                try
                {
                    _ttsManager = new TtsManager(_catalog, _speakerCatalog);
                    if (_ttsManager.IsReady)
                    {
                        _logger.LogMessage("TTS Manager initialized successfully");
                        
                        // Subscribe to TTS events
                        _ttsManager.AudioGenerated += OnAudioGenerated;
                        _ttsManager.TtsError += OnTtsError;
                    }
                    else
                    {
                        _logger.LogMessage("TTS Manager not ready - check tts_config.json", LogLevel.Warning);
                    }
                }
                catch (Exception ttsEx)
                {
                    _logger.LogError("TTS Manager initialization failed", ttsEx);
                    _ttsManager = null;
                }
                
                _logger.LogMessage("Capture service initialized successfully");
                // Debug snapshots disabled for production
                Console.WriteLine($"[CaptureService] Initialized successfully. Log file: {_logger.LogFilePath}");
                
                // Test capture to show what window we're detecting
                var testCapture = ScreenCapture.CaptureGameWindow();
                _logger.LogMessage($"Capture resolution: {testCapture.Width}x{testCapture.Height}");
                testCapture.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error initializing capture service", ex);
                Console.WriteLine($"Error initializing capture service: {ex.Message}");
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
                Runtime = elapsed,
                DialogueCount = _catalog?.Count ?? 0,
                SpeakerCount = _speakerCatalog?.GetAllSpeakers().Count ?? 0
            };
        }
        
        public Task<bool> StartCaptureAsync()
        {
            if (_isRunning)
            {
                _logger?.LogMessage("Capture service is already running");
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
                _waitingForStableFrame = true;
                
                // Start capture timer - 15 FPS (67ms intervals)
                _captureTimer = new System.Threading.Timer(CaptureAndProcess, null, 0, 67);
                
                _logger?.LogMessage("Capture service started (15 FPS)");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error starting capture service", ex);
                _isRunning = false;
                return Task.FromResult(false);
            }
        }
        
        public Task<bool> StopCaptureAsync()
        {
            if (!_isRunning)
            {
                _logger?.LogMessage("Capture service is not running");
                return Task.FromResult(false);
            }
            
            try
            {
                _isRunning = false;
                
                // Stop the timer
                _captureTimer?.Dispose();
                _captureTimer = null;
                
                // Cancel any ongoing operations
                _cancellationTokenSource?.Cancel();
                
                // Cleanup resources
                lock (_lockObject)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                    _lastTextbox?.Dispose();
                    _lastTextbox = null;
                }
                
                _logger?.LogMessage("Capture service stopped");
                
                // Report final statistics
                var stats = GetStatistics();
                ProgressReported?.Invoke(this, new CaptureProgressEventArgs(stats));
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error stopping capture service", ex);
                return Task.FromResult(false);
            }
        }
        
        private void CaptureAndProcess(object? state)
        {
            if (!_isRunning) return;
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Step 1: Capture game window
                var currentFrame = ScreenCapture.CaptureGameWindow();
                _frameCount++;
                
                // Log capture progress every 100 frames for monitoring
                if (_frameCount % 100 == 0)
                {
                    _logger?.LogMessage($"Frame #{_frameCount} captured: {currentFrame.Width}x{currentFrame.Height}", LogLevel.Debug);
                }

                // Step 2: Fast early exit checks before expensive operations
                lock (_lockObject)
                {
                    // Quick frame similarity check with optimized threshold
                    bool frameIsSimilar = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 750);
                    
                    // Log frame similarity status periodically for monitoring
                    if (_frameCount % 100 == 0)
                    {
                        _logger?.LogMessage($"Frame similarity: similar={frameIsSimilar}, waiting={_waitingForStableFrame}", LogLevel.Debug);
                    }
                    
                    if (frameIsSimilar)
                    {
                        // Frame is stable - check if we need to wait longer
                        if (_waitingForStableFrame)
                        {
                            if (_frameStableStartTime == DateTime.MinValue)
                            {
                                // First stable frame - start timer
                                _frameStableStartTime = DateTime.Now;
                                _logger?.LogMessage("Frame stability started - waiting for text to settle...", LogLevel.Debug);
                            }
                            
                            var stableTime = (DateTime.Now - _frameStableStartTime).TotalMilliseconds;
                            if (stableTime < STABILITY_DELAY_MS)
                            {
                                // Still waiting for stability - early exit
                                currentFrame.Dispose();
                                return;
                            }
                            
                            // Stability period complete - process this frame
                            _waitingForStableFrame = false;
                            _logger?.LogMessage($"Stable frame detected after {stableTime:0}ms - processing for textbox", LogLevel.Debug);
                        }
                        else
                        {
                            // Frame is stable and we already processed it recently - early exit
                            currentFrame.Dispose();
                            return;
                        }
                    }
                    else
                    {
                        // Frame has changed - reset stability tracking and early exit
                        _waitingForStableFrame = true;
                        _frameStableStartTime = DateTime.MinValue;
                        _lastTextboxRect = null;
                        
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
                        _logger?.LogMessage($"Textbox detected at {textboxRect.Value}");
                        _lastTextboxRect = textboxRect.Value;
                    }

                    // Step 5: Crop textbox area and check for content changes
                    var textboxImage = CropImage(currentFrame, textboxRect.Value);
                    var textboxHash = GetImageHash(textboxImage);
                    
                    // Only process OCR if textbox content has actually changed
                    if (textboxHash != _lastTextboxHash)
                    {
                        _lastTextboxHash = textboxHash;
                        _logger?.LogMessage("Unique textbox detected, processing OCR");
                        
                        // Check for continuation indicators (like FF1's white triangle)
                        bool hasContinuation = HasContinuationIndicator(textboxImage);
                        if (hasContinuation)
                        {
                            _logger?.LogMessage("Continuation indicator detected - more text expected", LogLevel.Debug);
                        }
                        
                        // IMPORTANT: Reset stability state to allow processing of next text change
                        lock (_lockObject)
                        {
                            _waitingForStableFrame = true;
                            _frameStableStartTime = DateTime.MinValue;
                        }
                        
                        // Debug snapshots disabled for production
                        // _snapshotManager?.SaveFullscreenWithDetection(currentFrame, textboxRect, _frameCount);
                        
                        // Create copies for async processing - EnhancedOCR will handle preprocessing
                        var textboxCopy = new Bitmap(textboxImage);
                        
                        // Step 6: OCR the text (async to not block the loop)
                        Task.Run(async () => {
                            try 
                            {
                                _logger?.LogMessage("Running enhanced OCR on textbox...");
                                
                                // Debug textbox snapshots disabled for production
                                // _snapshotManager?.SaveTextboxCrop(textboxCopy, "debug", _frameCount);
                                
                                var rawText = _ocr?.ExtractTextFast(textboxCopy) ?? "";
                                _logger?.LogMessage($"Raw OCR result: '{rawText}' (length: {rawText.Length})");
                                
                                var cleanedText = CleanOCRText(rawText);
                                _logger?.LogMessage($"Cleaned text: '{cleanedText}' (length: {cleanedText.Length})");
                                
                                if (!string.IsNullOrWhiteSpace(cleanedText))
                                {
                                    await ProcessNewDialogue(cleanedText);
                                }
                                else
                                {
                                    _logger?.LogMessage("No meaningful text extracted from textbox");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("OCR processing failed", ex);
                            }
                            finally
                            {
                                textboxCopy?.Dispose();
                            }
                        });
                    }
                    else
                    {
                        // Textbox content unchanged - skip OCR processing
                    }

                    textboxImage.Dispose();
                }
                else
                {
                    // No textbox detected - only save debug snapshot if this is a state change
                    if (_lastTextboxRect.HasValue)
                    {
                        _logger?.LogMessage("Textbox disappeared from frame");
                        // Debug snapshots disabled for production
                        // _snapshotManager?.SaveFullscreenWithDetection(currentFrame, null, _frameCount);
                        _lastTextboxRect = null;
                        _lastTextboxHash = "";
                    }
                    
                    // Enhanced debugging for textbox detection failures
                    if (_frameCount % 50 == 0) // Log every 50 frames instead of 200
                    {
                        _logger?.LogMessage($"No textbox found in stable frame (frame {_frameCount}) - Frame size: {currentFrame.Width}x{currentFrame.Height}", LogLevel.Debug);
                        
                        // Additional debug info about the detection process
                        if (_detector != null)
                        {
                            _logger?.LogMessage($"Detector type: {_detector.GetType().Name}", LogLevel.Debug);
                        }
                    }
                }

                currentFrame.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in capture loop", ex);
            }

            stopwatch.Stop();
            _totalProcessingTime += stopwatch.ElapsedMilliseconds;
            
            // Enhanced performance monitoring with better thresholds
            var processingTime = stopwatch.ElapsedMilliseconds;
            if (processingTime > 100)
            {
                _logger?.LogPerformance("Frame processing", processingTime);
            }
            else if (processingTime < 100)
            {
                _logger?.LogMessage($"Frame processing: {processingTime}ms", LogLevel.Debug);
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
            
            // Identify speaker using advanced profile matching
            var speakerProfile = _speakerCatalog?.IdentifySpeaker(text) ?? _speakerCatalog?.GetOrCreateGenericSpeaker("NPC");
            
            // Add to dialogue catalog with enhanced speaker info
            var entry = _catalog?.AddOrUpdateDialogue(text);
            if (entry != null && speakerProfile != null)
            {
                entry.Speaker = speakerProfile.Name;
                entry.VoiceProfile = speakerProfile.TtsVoiceId;
                
                _logger?.LogDialogue(text, speakerProfile.Name, speakerProfile.TtsVoiceId);
                
                // Process with TTS system if available
                if (_ttsManager?.IsReady == true)
                {
                    // Check if we already have audio for this dialogue
                    if (entry.HasAudio && !string.IsNullOrEmpty(entry.AudioPath) && File.Exists(entry.AudioPath))
                    {
                        // Play existing audio immediately
                        _logger?.LogMessage($"🔄 Playing cached audio: {entry.AudioPath}");
                        _ = Task.Run(async () => {
                            try
                            {
                                await _ttsManager.PlayExistingAudioAsync(entry.AudioPath, entry, speakerProfile);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("Cached audio playback error", ex);
                            }
                        });
                    }
                    else
                    {
                        // Generate new TTS audio in background (don't block capture)
                        _logger?.LogMessage($"🎤 Generating new TTS for: '{entry.Text}' ({speakerProfile.Name})");
                        _ = Task.Run(async () => {
                            try
                            {
                                await _ttsManager.ProcessDialogueAsync(entry, speakerProfile);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("TTS processing error", ex);
                            }
                        });
                    }
                }
                
                // Notify listeners of new dialogue
                DialogueDetected?.Invoke(this, new DialogueDetectedEventArgs(entry, speakerProfile));
            }
            
            return Task.CompletedTask;
        }
        
        private bool HasContinuationIndicator(Bitmap textboxImage)
        {
            // Detect FF1's white continuation triangle in bottom-right corner
            try
            {
                if (textboxImage.Width < 50 || textboxImage.Height < 30) return false;
                
                // Look for white pixels in the bottom-right area where FF1 places the triangle
                int rightEdge = textboxImage.Width - 1;
                int bottomEdge = textboxImage.Height - 1;
                int searchArea = 30; // 30x30 pixel search area
                
                int whitePixels = 0;
                int totalPixels = 0;
                
                // Sample the bottom-right corner area
                for (int x = Math.Max(0, rightEdge - searchArea); x < rightEdge; x += 3)
                {
                    for (int y = Math.Max(0, bottomEdge - searchArea); y < bottomEdge; y += 3)
                    {
                        try
                        {
                            var pixel = textboxImage.GetPixel(x, y);
                            totalPixels++;
                            
                            // Check for white/light colored pixels (continuation indicator)
                            if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
                            {
                                whitePixels++;
                            }
                        }
                        catch { continue; }
                    }
                }
                
                // If more than 15% of sampled pixels in corner are white, likely a continuation indicator
                return totalPixels > 0 && (whitePixels * 100 / totalPixels) > 15;
            }
            catch
            {
                return false; // Safe fallback
            }
        }
        
        public string? GetLogFilePath() => _logger?.LogFilePath;
        
        public string[] GetRecentLogLines(int maxLines = 500) => _logger?.ReadRecentLogLines(maxLines) ?? Array.Empty<string>();
        
        #region Image Processing Methods (extracted from Program.cs)
        
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



        private bool IsUniqueTextbox(Bitmap textbox)
        {
            if (_lastTextbox == null)
            {
                _lastTextbox = new Bitmap(textbox);
                return true;
            }
            
            if (textbox.Width != _lastTextbox.Width || textbox.Height != _lastTextbox.Height)
            {
                _lastTextbox?.Dispose();
                _lastTextbox = new Bitmap(textbox);
                return true;
            }
            
            bool isDifferent = false;
            for (int y = 0; y < textbox.Height && !isDifferent; y += 10)
            {
                for (int x = 0; x < textbox.Width && !isDifferent; x += 10)
                {
                    var pixel1 = textbox.GetPixel(x, y);
                    var pixel2 = _lastTextbox.GetPixel(x, y);
                    
                    if (Math.Abs(pixel1.R - pixel2.R) > 30 ||
                        Math.Abs(pixel1.G - pixel2.G) > 30 ||
                        Math.Abs(pixel1.B - pixel2.B) > 30)
                    {
                        isDifferent = true;
                    }
                }
            }
            
            if (isDifferent)
            {
                _lastTextbox?.Dispose();
                _lastTextbox = new Bitmap(textbox);
                return true;
            }
            
            return false;
        }
        
        private string CleanOCRText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            if (IsOCRGarbage(text))
            {
                _logger?.LogMessage("OCR quality filter: Rejecting garbage text", LogLevel.Warning);
                return "[REJECTED: Low Quality OCR]";
            }
            
            var cleaned = text;
            
            // Apply the same cleaning rules from Program.cs
            cleaned = cleaned.Replace("Il ", "I ");
            cleaned = cleaned.Replace(" Il ", " I ");
            cleaned = cleaned.Replace("15", "is");
            cleaned = cleaned.Replace("1s", "is");
            cleaned = cleaned.Replace("0", "o");
            cleaned = cleaned.Replace("5", "s");
            cleaned = cleaned.Replace("3", "e");
            cleaned = cleaned.Replace("1", "i");
            
            // Add more cleaning rules here as needed...
            
            return cleaned.Trim();
        }
        
        private string GetImageHash(Bitmap image)
        {
            // Hash focused on TEXT CONTENT area to detect multi-part dialogue changes
            try
            {
                int hash = image.Width * 31 + image.Height;
                
                if (image.Width > 40 && image.Height > 20)
                {
                    // Focus sampling on the TEXT AREA (center region) instead of borders
                    // This captures text changes while ignoring static border colors
                    
                    int centerX = image.Width / 2;
                    int centerY = image.Height / 2;
                    int textWidth = (int)(image.Width * 0.6);   // 60% of width for text area
                    int textHeight = (int)(image.Height * 0.6); // 60% of height for text area
                    
                    // Sample multiple points across the text content area
                    var samples = new[]
                    {
                        image.GetPixel(centerX - textWidth/4, centerY - textHeight/4),  // Upper left text
                        image.GetPixel(centerX + textWidth/4, centerY - textHeight/4),  // Upper right text
                        image.GetPixel(centerX - textWidth/4, centerY + textHeight/4),  // Lower left text
                        image.GetPixel(centerX + textWidth/4, centerY + textHeight/4),  // Lower right text
                        image.GetPixel(centerX, centerY),                               // Center text
                        image.GetPixel(centerX - textWidth/2, centerY),                // Left text
                        image.GetPixel(centerX + textWidth/2, centerY),                // Right text
                    };
                    
                    // Combine all text area samples for content-sensitive hash
                    foreach (var pixel in samples)
                    {
                        hash = hash * 31 + pixel.ToArgb();
                    }
                }
                
                return hash.ToString();
            }
            catch
            {
                // Fallback to timestamp-based hash to ensure changes are always detected
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
        
        // TTS event handlers
        private void OnAudioGenerated(object? sender, TtsGenerationEventArgs e)
        {
            _logger?.LogMessage($"Audio generated: {e.AudioPath} for \"{e.DialogueEntry.Text}\"");
        }
        
        private void OnTtsError(object? sender, string error)
        {
            _logger?.LogMessage($"TTS Error: {error}", LogLevel.Warning);
        }
        
        // TTS control methods
        public void SkipCurrentAudio() => _ttsManager?.SkipCurrentAudio();
        public void ReplayCurrentAudio() => _ttsManager?.ReplayCurrentAudio();
        public void ClearAudioQueue() => _ttsManager?.ClearAudioQueue();
        
        public bool IsTtsReady => _ttsManager?.IsReady ?? false;
        public TtsStatistics? GetTtsStatistics() => _ttsManager?.GetStatistics();
        
        public bool AutoPlayEnabled 
        { 
            get => _ttsManager?.AutoPlayEnabled ?? false;
            set { if (_ttsManager != null) _ttsManager.AutoPlayEnabled = value; }
        }
        
        public bool AutoGenerateEnabled 
        { 
            get => _ttsManager?.AutoGenerateEnabled ?? false;
            set { if (_ttsManager != null) _ttsManager.AutoGenerateEnabled = value; }
        }
        
        public void Dispose()
        {
            try
            {
                // Force stop without waiting
                _isRunning = false;
                
                // Dispose timer immediately
                _captureTimer?.Dispose();
                _captureTimer = null;
                
                // Cancel operations
                _cancellationTokenSource?.Cancel();
                
                // Cleanup resources
                lock (_lockObject)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                    _lastTextbox?.Dispose();
                    _lastTextbox = null;
                }
                
                _ttsManager?.Dispose();
                _cancellationTokenSource?.Dispose();
                _logger?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during CaptureService disposal: {ex.Message}");
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
        public SpeakerProfile SpeakerProfile { get; }
        
        public DialogueDetectedEventArgs(DialogueEntry dialogueEntry, SpeakerProfile speakerProfile)
        {
            DialogueEntry = dialogueEntry;
            SpeakerProfile = speakerProfile;
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
        public int DialogueCount { get; set; }
        public int SpeakerCount { get; set; }
    }
}