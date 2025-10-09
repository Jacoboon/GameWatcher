using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameWatcher.Engine.Detection;
using GameWatcher.Runtime.Services.OCR;
using GameWatcher.Runtime.Services.Capture; // ScreenCapture helper
using Microsoft.Extensions.Logging;
using FF1.PixelRemaster.Detection;

namespace GameWatcher.AuthorStudio.Services
{
    public class DiscoveryService : IDisposable
    {
        private readonly ITextboxDetector _detector;
        private readonly IOcrEngine _ocr;
        private readonly OcrFixesStore _fixes;
        private readonly ILogger<DiscoveryService> _logger;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly AudioPlaybackService _audioPlayer;
        private readonly AudioStore _audioStore;
        private Timer? _timer;
        private bool _running;
        private string _lastText = string.Empty;
        private string _lastNormalized = string.Empty;
        private readonly System.Collections.Generic.HashSet<string> _seen = new();
        private string? _currentPackFolder = null;

        // V1 Performance optimizations
        private bool _isBusy = false;
        private Bitmap? _lastFrame = null;
        private string _lastTextboxHash = string.Empty;
        private readonly object _lockObject = new();

        public ObservableCollection<PendingDialogueEntry> Discovered { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

        public DiscoveryService(
            OcrFixesStore fixes, 
            ILogger<DiscoveryService> logger,
            AudioPlaybackService audioPlayer,
            AudioStore audioStore,
            ILoggerFactory? loggerFactory = null,
            ITextboxDetector? detector = null)
        {
            _fixes = fixes;
            _logger = logger;
            _audioPlayer = audioPlayer;
            _audioStore = audioStore;
            _loggerFactory = loggerFactory;
            
            // Use injected detector or create FF1 detector as default for V2
            // Future: This will come from IGamePack.CreateDetector()
            if (detector != null)
            {
                _detector = detector;
            }
            else
            {
                var detectorLogger = _loggerFactory?.CreateLogger<DynamicTextboxDetector>();
                _detector = new DynamicTextboxDetector(FF1DetectionConfig.GetConfig(), detectorLogger);
            }
            
            _ocr = new WindowsOCR();
        }

        public bool IsRunning => _running;

        public async Task LoadOcrFixesAsync(string packFolder)
        {
            _currentPackFolder = packFolder;
            await _fixes.LoadFromFolderAsync(packFolder);
            await _audioStore.SetPackFolderAsync(packFolder);
        }

        public Task StartAsync()
        {
            if (_running) return Task.CompletedTask;
            _running = true;
            _timer = new Timer(CaptureTick, null, 0, 67); // 15 FPS like V1 (was 150ms/6.7 FPS)
            Log("Discovery started (15 FPS)");
            _logger.LogInformation("[Activity] Discovery started (15 FPS)");
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            if (!_running) return Task.CompletedTask;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _running = false;
            Log("Discovery paused");
            _logger.LogInformation("[Activity] Discovery paused");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _ = PauseAsync();
            ClearTransientState();
            Log("Discovery stopped");
            _logger.LogInformation("[Activity] Discovery stopped");
            return Task.CompletedTask;
        }

        private void CaptureTick(object? state)
        {
            if (!_running) return;

            try
            {
                // Step 1: Capture game window
                var currentFrame = ScreenCapture.CaptureGameWindow();

                // Step 2: Smart frame processing with dynamic fuzzy matching (V1 optimization)
                lock (_lockObject)
                {
                    bool frameMatches;

                    if (!_isBusy)
                    {
                        // Not busy = use lower threshold (more tolerant) to find new textbox appearances
                        // Sample rate 500 = check every 500th pixel
                        frameMatches = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 500);
                    }
                    else
                    {
                        // Busy = use very high threshold (check every 50th pixel) to catch text changes
                        frameMatches = _lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 50);
                    }

                    if (frameMatches && !_isBusy)
                    {
                        // Fuzzy match + not busy = new stable frame detected, process it
                        _isBusy = true;
                        Log("📸 Stable frame detected - searching for textbox...");
                        _logger.LogDebug("[Activity] Stable frame detected - searching for textbox");
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
                        if (_isBusy)
                        {
                            Log("🔄 Scene change detected - resetting capture state");
                            _logger.LogDebug("[Activity] Scene change detected - resetting capture state");
                        }

                        _isBusy = false;
                        _lastTextboxHash = string.Empty;

                        _lastFrame?.Dispose();
                        _lastFrame = new Bitmap(currentFrame);
                        currentFrame.Dispose();
                        return;
                    }
                }

                // Step 3: Check stable frame for textbox
                var textboxRect = _detector.DetectTextbox(currentFrame);

                if (!textboxRect.HasValue)
                {
                    Log("⚠️ No textbox found in stable frame");
                    _logger.LogDebug("[Activity] No textbox found in stable frame");
                    currentFrame.Dispose();
                    return;
                }

                Log($"🎯 Textbox detected at {textboxRect.Value.X},{textboxRect.Value.Y} ({textboxRect.Value.Width}x{textboxRect.Value.Height})");
                _logger.LogDebug("[Activity] Textbox detected at {X},{Y} ({W}x{H})", 
                    textboxRect.Value.X, textboxRect.Value.Y, textboxRect.Value.Width, textboxRect.Value.Height);

                // Step 4: Crop textbox area and check for content changes (V1 optimization)
                using var textboxImage = CropImage(currentFrame, textboxRect.Value);
                var textboxHash = GetImageHash(textboxImage);

                // Only process OCR if textbox content has actually changed
                if (textboxHash == _lastTextboxHash)
                {
                    Log("⏭️ Textbox content unchanged - skipping OCR");
                    _logger.LogDebug("[Activity] Textbox content unchanged - skipping OCR");
                    currentFrame.Dispose();
                    return; // Same textbox content, skip OCR
                }

                _lastTextboxHash = textboxHash;
                Log("✨ New textbox content detected - running OCR...");
                _logger.LogDebug("[Activity] New textbox content detected - running OCR");

                // Step 5: OCR the text (async to not block the capture loop - V1 optimization)
                var textboxCopy = new Bitmap(textboxImage);
                currentFrame.Dispose();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var text = _ocr.ExtractTextFast(textboxCopy)?.Trim();
                        Log($"📝 OCR extracted: '{Truncate(text ?? string.Empty, 60)}'");
                        _logger.LogDebug("[Activity] OCR extracted: '{Text}'", Truncate(text ?? string.Empty, 100));
                        
                        if (string.IsNullOrWhiteSpace(text)) 
                        {
                            Log("⚠️ OCR returned empty text");
                            _logger.LogDebug("[Activity] OCR returned empty text");
                            return;
                        }

                        // Store original OCR text before applying fixes
                        var originalOcrText = text;
                        text = _fixes.Apply(text);
                        
                        if (text != originalOcrText)
                        {
                            Log($"🔧 OCR fixes applied: '{Truncate(text, 60)}'");
                            _logger.LogDebug("[Activity] OCR fixes applied: '{Before}' -> '{After}'", 
                                Truncate(originalOcrText, 100), Truncate(text, 100));
                        }

                        if (string.IsNullOrWhiteSpace(text)) return;
                        var norm = TextNormalizer.Normalize(text);

                        lock (_lockObject)
                        {
                            if (string.Equals(norm, _lastNormalized, StringComparison.Ordinal))
                            {
                                Log("⏭️ Duplicate dialogue (normalized match) - skipping");
                                _logger.LogDebug("[Activity] Duplicate dialogue (normalized match) - skipping");
                                return;
                            }
                            if (_seen.Contains(norm))
                            {
                                Log("⏭️ Duplicate dialogue (seen in session) - skipping");
                                _logger.LogDebug("[Activity] Duplicate dialogue (seen in session) - skipping");
                                return; // skip duplicates in this session
                            }

                            _lastText = text;
                            _lastNormalized = norm;
                            _seen.Add(norm);
                        }

                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var entry = new PendingDialogueEntry
                            {
                                Text = text,
                                OriginalOcrText = originalOcrText,
                                Timestamp = DateTime.UtcNow,
                                Approved = false
                            };
                            
                            Discovered.Add(entry);
                            Log($"✅ Found: {Truncate(text, 80)}");
                            _logger.LogInformation("[Activity] Found unique dialogue: {Text}", Truncate(text, 100));
                            
                            // Try to find and play existing audio for this dialogue
                            TryPlayExistingAudio(entry);
                        });
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ OCR error: {ex.Message}");
                        _logger.LogError(ex, "OCR processing error");
                    }
                    finally
                    {
                        textboxCopy?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"❌ Capture error: {ex.Message}");
                _logger.LogError(ex, "Capture tick error");
            }
        }

        private void ClearTransientState()
        {
            _lastText = string.Empty;
            _lastNormalized = string.Empty;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            lock (_lockObject)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
            }
        }

        private void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
                App.Current?.Dispatcher.Invoke(() => LogLines.Add(line));
            }
            catch { }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        /// <summary>
        /// Attempts to find and play existing audio for a dialogue entry using AudioStore.
        /// </summary>
        private void TryPlayExistingAudio(PendingDialogueEntry entry)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentPackFolder))
                {
                    _logger.LogDebug("No pack folder set - skipping audio lookup");
                    return;
                }

                // Check AudioStore for existing audio
                var audioEntry = _audioStore.GetAudioEntry(entry.Text);
                if (audioEntry != null)
                {
                    var fullPath = Path.Combine(_currentPackFolder, audioEntry.Path);
                    if (File.Exists(fullPath))
                    {
                        // Update entry with audio metadata
                        entry.AudioPath = audioEntry.Path;
                        entry.TtsVoice = audioEntry.VoiceName;
                        
                        // Play the audio
                        _audioPlayer.Play(fullPath);
                        
                        var voiceInfo = !string.IsNullOrEmpty(audioEntry.VoiceName) ? $" ({audioEntry.VoiceName})" : "";
                        Log($"🔊 Playing audio{voiceInfo}: {Path.GetFileName(fullPath)}");
                        _logger.LogInformation("[Activity] Playing existing audio for dialogue: {File}", Path.GetFileName(fullPath));
                    }
                    else
                    {
                        _logger.LogWarning("Audio manifest references missing file: {Path}", audioEntry.Path);
                    }
                }
                else
                {
                    _logger.LogDebug("No audio found in manifest for: {Text}", Truncate(entry.Text, 50));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to lookup/play audio for dialogue");
            }
        }

        #region V1 Performance Helper Methods

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
            // Hash focused on TEXT CONTENT area to detect multi-part dialogue changes
            try
            {
                int hash = image.Width * 31 + image.Height;

                if (image.Width > 40 && image.Height > 20)
                {
                    // Focus sampling on the TEXT AREA (center region) instead of borders
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

        #endregion
    }
}
