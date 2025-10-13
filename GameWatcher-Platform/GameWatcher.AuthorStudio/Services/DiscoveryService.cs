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
using DetectionEventArgs = GameWatcher.Engine.Detection.DialogueDetectedEventArgs;

namespace GameWatcher.AuthorStudio.Services
{
    /// <summary>
    /// Discovery service that uses the optimal IDetectionLoop for dialogue detection.
    /// Simplified to delegate all detection logic to the loop implementation.
    /// </summary>
    public class DiscoveryService : IDisposable
    {
        private readonly IDetectionLoop _detectionLoop;
        private readonly ILogger<DiscoveryService> _logger;
        private readonly AudioPlaybackService _audioPlayer;
        private readonly AudioStore _audioStore;
        private string? _currentPackFolder = null;

        public ObservableCollection<PendingDialogueEntry> Discovered { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

        public DiscoveryService(
            IDetectionLoop detectionLoop,
            ILogger<DiscoveryService> logger,
            AudioPlaybackService audioPlayer,
            AudioStore audioStore)
        {
            _detectionLoop = detectionLoop ?? throw new ArgumentNullException(nameof(detectionLoop));
            _logger = logger;
            _audioPlayer = audioPlayer;
            _audioStore = audioStore;
            
            // Subscribe to detection events
            _detectionLoop.DialogueDetected += OnDialogueDetected;
        }

        public bool IsRunning => _detectionLoop.IsRunning;
        public DetectionStatistics Statistics => _detectionLoop.Statistics;

        public async Task LoadOcrFixesAsync(string packFolder)
        {
            _currentPackFolder = packFolder;
            // OCR fixes are now handled by the detection loop
            await _audioStore.SetPackFolderAsync(packFolder);
        }

        public Task StartAsync()
        {
            Log("Discovery started (optimal loop)");
            _logger.LogInformation("[Activity] Discovery started (optimal loop)");
            return _detectionLoop.StartAsync();
        }

        public Task PauseAsync()
        {
            Log("Discovery paused");
            _logger.LogInformation("[Activity] Discovery paused");
            return _detectionLoop.PauseAsync();
        }

        public Task StopAsync()
        {
            Log("Discovery stopped");
            _logger.LogInformation("[Activity] Discovery stopped");
            return _detectionLoop.StopAsync();
        }

        private void OnDialogueDetected(object? sender, DetectionEventArgs e)
        {
            try
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    var entry = new PendingDialogueEntry
                    {
                        Text = e.Text,
                        OriginalOcrText = e.OriginalOcrText,
                        Timestamp = e.Timestamp,
                        Approved = false
                    };
                    
                    Discovered.Add(entry);
                    Log($"✅ Found: {Truncate(e.Text, 80)}");
                    _logger.LogInformation("[Activity] Found unique dialogue: {Text}", Truncate(e.Text, 100));
                    
                    // Try to find and play existing audio for this dialogue
                    TryPlayExistingAudio(entry);
                    
                    // Log statistics periodically
                    if (Statistics.TotalFramesProcessed % 100 == 0)
                    {
                        Log($"📊 Stats: {Statistics.TotalFramesProcessed} frames, " +
                            $"{Statistics.CacheHitRate:F1}% cache hits, " +
                            $"{Statistics.AverageFrameProcessingMs:F2}ms avg");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dialogue detection");
            }
        }

        public void Dispose()
        {
            _detectionLoop.DialogueDetected -= OnDialogueDetected;
            _detectionLoop?.Dispose();
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
    }
}
