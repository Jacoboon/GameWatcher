using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GameWatcher.AuthorStudio
{
    public enum DiscoveryMode
    {
        Passive,
        Active,
        Assisted
    }

    public class PendingDialogueEntry
    {
        /// <summary>
        /// The current dialogue text. Initially populated from OCR, user can edit inline.
        /// </summary>
        public string Text { get; set; } = string.Empty;
        
        /// <summary>
        /// The original OCR text before any user edits. Used to detect corrections for auto-fix generation.
        /// </summary>
        public string OriginalOcrText { get; set; } = string.Empty;
        
        public string? SpeakerId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public byte[]? ScreenshotPng { get; set; }
        public bool Approved { get; set; } = false;
        public string? AudioPath { get; set; }
    }

    public class DiscoverySession : IDisposable
    {
        public DiscoveryMode Mode { get; set; } = DiscoveryMode.Passive;
        public TimeSpan SessionDuration { get; private set; } = TimeSpan.Zero;
        public int UniqueDialogueFound => DiscoveredDialogue.Count;
        public ObservableCollection<PendingDialogueEntry> DiscoveredDialogue { get; } = new();

        private bool _isRunning = false;
        private DateTime _startedAt;

        public Task StartDiscoveryAsync()
        {
            if (_isRunning) return Task.CompletedTask;
            _isRunning = true;
            _startedAt = DateTime.UtcNow;
            // In a later iteration this will hook into capture service events
            return Task.CompletedTask;
        }

        public Task PauseDiscoveryAsync()
        {
            if (!_isRunning) return Task.CompletedTask;
            _isRunning = false;
            SessionDuration += DateTime.UtcNow - _startedAt;
            return Task.CompletedTask;
        }

        public Task StopDiscoveryAsync()
        {
            if (!_isRunning) return Task.CompletedTask;
            _isRunning = false;
            SessionDuration += DateTime.UtcNow - _startedAt;
            return Task.CompletedTask;
        }

        public Task<PackBuildResult> BuildPackAsync()
        {
            // Placeholder implementation - real pack builder lives in PackBuilder
            return Task.FromResult(new PackBuildResult { Success = true, PackPath = string.Empty });
        }

        public void Dispose()
        {
            // Cleanup resources if needed
        }
    }

    public class PackBuildResult
    {
        public bool Success { get; set; }
        public string PackPath { get; set; } = string.Empty;
        public string[] Warnings { get; set; } = Array.Empty<string>();
    }
}
