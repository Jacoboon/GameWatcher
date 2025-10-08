using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GameWatcher.Engine.Detection;
using GameWatcher.Runtime.Services.OCR;
using GameWatcher.Runtime.Services.Capture; // ScreenCapture helper

namespace GameWatcher.AuthorStudio.Services
{
    public class DiscoveryService : IDisposable
    {
        private readonly ITextboxDetector _detector;
        private readonly IOcrEngine _ocr;
        private readonly OcrFixesStore _fixes = new();
        private Timer? _timer;
        private bool _running;
        private string _lastText = string.Empty;
        private string _lastNormalized = string.Empty;
        private readonly System.Collections.Generic.HashSet<string> _seen = new();

        public ObservableCollection<PendingDialogueEntry> Discovered { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

        public DiscoveryService()
        {
            _detector = new DynamicTextboxDetector();
            _ocr = new WindowsOCR();
        }

        public bool IsRunning => _running;

        public async Task LoadOcrFixesAsync(string packFolder)
        {
            await _fixes.LoadFromFolderAsync(packFolder);
        }

        public Task StartAsync()
        {
            if (_running) return Task.CompletedTask;
            _running = true;
            _timer = new Timer(CaptureTick, null, 0, 150); // ~6-7 FPS for MVP
            Log("Discovery started");
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            if (!_running) return Task.CompletedTask;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _running = false;
            Log("Discovery paused");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _ = PauseAsync();
            ClearTransientState();
            Log("Discovery stopped");
            return Task.CompletedTask;
        }

        private void CaptureTick(object? state)
        {
            try
            {
                using var frame = ScreenCapture.CaptureGameWindow();
                var rect = _detector.DetectTextbox(frame);
                if (rect == null) return;

                using var crop = new Bitmap(rect.Value.Width, rect.Value.Height);
                using (var g = Graphics.FromImage(crop))
                {
                    g.DrawImage(frame, new Rectangle(0, 0, crop.Width, crop.Height), rect.Value, GraphicsUnit.Pixel);
                }

                var text = _ocr.ExtractTextFast(crop)?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = _fixes.Apply(text);
                }
                if (string.IsNullOrWhiteSpace(text)) return;
                var norm = TextNormalizer.Normalize(text);
                if (string.Equals(norm, _lastNormalized, StringComparison.Ordinal)) return;
                if (_seen.Contains(norm)) return; // skip duplicates in this session

                _lastText = text;
                _lastNormalized = norm;
                _seen.Add(norm);
                App.Current?.Dispatcher.Invoke(() =>
                {
                    Discovered.Add(new PendingDialogueEntry
                    {
                        Text = text,
                        Timestamp = DateTime.UtcNow,
                        Approved = false
                    });
                    Log($"Found: {Truncate(text, 80)}");
                });
            }
            catch
            {
                // swallow MVP errors to keep loop alive
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
    }
}
