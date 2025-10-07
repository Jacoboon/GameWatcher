using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GameWatcher.Runtime.Services.Capture;
using GameWatcher.Runtime.Services.Detection;
using GameWatcher.Runtime.Services.OCR;
using GameWatcher.Runtime.Services.Dialogue;

namespace GameWatcher.Runtime.Services.Capture
{
    /// <summary>
    /// Core game capture service for GameWatcher V2 Platform
    /// Simplified version of SimpleLoop CaptureService focused on real-time text detection
    /// </summary>
    public class GameCaptureService : IDisposable
    {
        private System.Threading.Timer? _captureTimer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        
        // Core components
        private ITextboxDetector _detector;
        private IOcrEngine _ocr;
        
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
        
        public GameCaptureService()
        {
            InitializeComponents();
        }
        
        private void InitializeComponents()
        {
            try
            {
                _detector = new DynamicTextboxDetector();
                _ocr = new WindowsOCR();
                
                Console.WriteLine("[GameCaptureService] Components initialized successfully");
                
                // Test capture to show what window we're detecting
                var testCapture = ScreenCapture.CaptureGameWindow();
                Console.WriteLine($"[GameCaptureService] Capture resolution: {testCapture.Width}x{testCapture.Height}");
                testCapture.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameCaptureService] Error initializing: {ex.Message}");
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
                Console.WriteLine("[GameCaptureService] Already running");
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
                
                // Start capture timer - 15 FPS (67ms intervals)
                _captureTimer = new System.Threading.Timer(CaptureAndProcess, null, 0, 67);
                
                Console.WriteLine("[GameCaptureService] Started (15 FPS)");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameCaptureService] Error starting: {ex.Message}");
                _isRunning = false;
                return Task.FromResult(false);
            }
        }
        
        public Task<bool> StopCaptureAsync()
        {
            if (!_isRunning)
            {
                Console.WriteLine("[GameCaptureService] Not running");
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
                }
                
                Console.WriteLine("[GameCaptureService] Stopped");
                
                // Report final statistics
                var stats = GetStatistics();
                ProgressReported?.Invoke(this, new CaptureProgressEventArgs(stats));
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameCaptureService] Error stopping: {ex.Message}");
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

                // Step 2: Smart frame processing logic with dynamic similarity thresholds
                lock (_lockObject)
                {
                    bool frameMatches;
                    
                    if (!_isBusy)
                    {
                        // Not busy = use lower threshold (more tolerant) to find new textbox appearances 
                        frameMatches = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 500);
                    }
                    else
                    {
                        // Busy = use very high threshold (99% similar) to catch text changes
                        frameMatches = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 50);
                    }
                    
                    if (frameMatches && !_isBusy)
                    {
                        // Fuzzy match + not busy = new stable frame detected, process it
                        _isBusy = true;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stable frame detected - processing for textbox");
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
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Textbox detected at {textboxRect.Value}");
                        _lastTextboxRect = textboxRect.Value;
                    }

                    // Step 5: Crop textbox area and check for content changes
                    var textboxImage = CropImage(currentFrame, textboxRect.Value);
                    var textboxHash = GetImageHash(textboxImage);
                    
                    // Only process OCR if textbox content has actually changed
                    if (textboxHash != _lastTextboxHash)
                    {
                        _lastTextboxHash = textboxHash;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unique textbox detected, processing OCR");
                        
                        // Create copies for async processing
                        var textboxCopy = new Bitmap(textboxImage);
                        
                        // Step 6: OCR the text (async to not block the loop)
                        Task.Run(async () => {
                            try 
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Running OCR on textbox...");
                                
                                var rawText = _ocr?.ExtractTextFast(textboxCopy) ?? "";
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Raw OCR result: '{rawText}' (length: {rawText.Length})");
                                
                                var cleanedText = CleanOCRText(rawText);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Cleaned text: '{cleanedText}' (length: {cleanedText.Length})");
                                
                                if (!string.IsNullOrWhiteSpace(cleanedText))
                                {
                                    await ProcessNewDialogue(cleanedText);
                                }
                                else
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No meaningful text extracted from textbox");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[GameCaptureService] OCR processing failed: {ex.Message}");
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
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Textbox disappeared from frame");
                        _lastTextboxRect = null;
                        _lastTextboxHash = "";
                    }
                    
                    // Enhanced debugging for textbox detection failures
                    if (_frameCount % 50 == 0) // Log every 50 frames instead of 200
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No textbox found in stable frame (frame {_frameCount}) - Frame size: {currentFrame.Width}x{currentFrame.Height}");
                    }
                }

                currentFrame.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameCaptureService] Error in capture loop: {ex.Message}");
            }

            stopwatch.Stop();
            _totalProcessingTime += stopwatch.ElapsedMilliseconds;
            
            // Enhanced performance monitoring with better thresholds
            var processingTime = stopwatch.ElapsedMilliseconds;
            if (processingTime > 100)
            {
                Console.WriteLine($"[GameCaptureService] SLOW: Frame processing took {processingTime}ms");
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
            
            Console.WriteLine($"🎉 >>> NEW DIALOGUE DETECTED: \"{text}\"");
            
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
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] OCR quality filter: Rejecting garbage text");
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
                Console.WriteLine($"[GameCaptureService] Error during disposal: {ex.Message}");
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