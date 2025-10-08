using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GameWatcher.Engine.Detection;
using GameWatcher.Engine.Ocr;
using GameWatcher.Runtime.Services.Capture; // ScreenCapture helper

namespace GameWatcher.AuthorStudio.Services
{
    public class DiscoveryService : IDisposable
    {
        private readonly ITextboxDetector _detector;
        private readonly IOcrEngine _ocr;
        private Timer? _timer;
        private bool _running;
        private string _lastText = string.Empty;

        public ObservableCollection<PendingDialogueEntry> Discovered { get; } = new();

        public DiscoveryService()
        {
            _detector = new DynamicTextboxDetector();
            _ocr = new WindowsOcrEngine();
        }

        public bool IsRunning => _running;

        public Task StartAsync()
        {
            if (_running) return Task.CompletedTask;
            _running = true;
            _timer = new Timer(CaptureTick, null, 0, 150); // ~6-7 FPS for MVP
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            if (!_running) return Task.CompletedTask;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _running = false;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _ = PauseAsync();
            ClearTransientState();
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
                if (string.IsNullOrWhiteSpace(text)) return;
                if (string.Equals(text, _lastText, StringComparison.Ordinal)) return;

                _lastText = text;
                App.Current?.Dispatcher.Invoke(() =>
                {
                    Discovered.Add(new PendingDialogueEntry
                    {
                        Text = text,
                        Timestamp = DateTime.UtcNow,
                        Approved = false
                    });
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
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

