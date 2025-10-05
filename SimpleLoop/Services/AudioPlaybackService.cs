using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimpleLoop.Services
{
    /// <summary>
    /// Audio playback service with queueing and real-time playback capabilities
    /// </summary>
    public class AudioPlaybackService : IDisposable
    {
        private readonly WaveOutEvent _waveOut;
        private readonly ConcurrentQueue<AudioPlaybackItem> _playbackQueue;
        private readonly object _playbackLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private AudioPlaybackItem? _currentItem;
        private bool _isPlaying = false;
        private bool _isDisposed = false;
        
        // Events
        public event EventHandler<AudioPlaybackEventArgs>? AudioStarted;
        public event EventHandler<AudioPlaybackEventArgs>? AudioCompleted;
        public event EventHandler<string>? PlaybackError;
        
        // Configuration
        public bool AutoPlayEnabled { get; set; } = true;
        public float Volume { get; set; } = 0.7f;
        public bool SkipRequested { get; private set; }
        
        public AudioPlaybackService()
        {
            _waveOut = new WaveOutEvent();
            _playbackQueue = new ConcurrentQueue<AudioPlaybackItem>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Configure NAudio for low-latency playback
            _waveOut.DesiredLatency = 100; // 100ms latency for responsive playback
            
            // Subscribe to playback completion events
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            
            // Start the playback worker task
            Task.Run(PlaybackWorkerAsync);
        }
        
        /// <summary>
        /// Queue audio for playback from dialogue detection
        /// </summary>
        /// <param name="audioPath">Path to the audio file</param>
        /// <param name="dialogueEntry">Associated dialogue entry</param>
        /// <param name="speakerProfile">Associated speaker profile</param>
        public void QueueAudio(string audioPath, DialogueEntry dialogueEntry, SpeakerProfile speakerProfile)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                Console.WriteLine($"[Audio] Cannot queue audio: file not found - {audioPath}");
                return;
            }
            
            var item = new AudioPlaybackItem
            {
                AudioPath = audioPath,
                DialogueEntry = dialogueEntry,
                SpeakerProfile = speakerProfile,
                QueuedAt = DateTime.Now
            };
            
            _playbackQueue.Enqueue(item);
            
            Console.WriteLine($"[Audio] Queued: \"{dialogueEntry.Text}\" ({speakerProfile.Name}) - Queue size: {_playbackQueue.Count}");
        }
        
        /// <summary>
        /// Skip current playback and move to next item in queue
        /// </summary>
        public void Skip()
        {
            lock (_playbackLock)
            {
                SkipRequested = true;
                
                if (_isPlaying)
                {
                    _waveOut.Stop();
                    Console.WriteLine("[Audio] Playback skipped by user");
                }
            }
        }
        
        /// <summary>
        /// Replay the current audio item
        /// </summary>
        public void Replay()
        {
            lock (_playbackLock)
            {
                if (_currentItem != null)
                {
                    // Stop current playback and re-queue the same item
                    _waveOut.Stop();
                    _playbackQueue.Enqueue(_currentItem);
                    Console.WriteLine($"[Audio] Replaying: \"{_currentItem.DialogueEntry.Text}\"");
                }
            }
        }
        
        /// <summary>
        /// Clear all queued audio items
        /// </summary>
        public void ClearQueue()
        {
            while (_playbackQueue.TryDequeue(out _)) { }
            Console.WriteLine("[Audio] Playback queue cleared");
        }
        
        /// <summary>
        /// Get current queue size
        /// </summary>
        public int GetQueueSize() => _playbackQueue.Count;
        
        /// <summary>
        /// Main playback worker that processes the audio queue
        /// </summary>
        private async Task PlaybackWorkerAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && !_isDisposed)
            {
                try
                {
                    // Check if auto-play is enabled and we have items to play
                    if (AutoPlayEnabled && _playbackQueue.TryDequeue(out var item) && !_isPlaying)
                    {
                        await PlayAudioItemAsync(item);
                    }
                    
                    // Brief pause to prevent busy-waiting
                    await Task.Delay(50, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Audio] Playback worker error: {ex.Message}");
                    PlaybackError?.Invoke(this, ex.Message);
                    
                    // Brief pause before retrying
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }
        
        /// <summary>
        /// Play a specific audio item with effects processing
        /// </summary>
        private async Task PlayAudioItemAsync(AudioPlaybackItem item)
        {
            if (_isDisposed) return;
            
            try
            {
                lock (_playbackLock)
                {
                    if (_isPlaying) return; // Already playing something
                    
                    _currentItem = item;
                    _isPlaying = true;
                    SkipRequested = false;
                }
                
                Console.WriteLine($"[Audio] Playing: \"{item.DialogueEntry.Text}\" ({item.SpeakerProfile.Name})");
                
                // Load audio file with NAudio
                using var audioReader = new AudioFileReader(item.AudioPath);
                
                // Apply speaker-specific effects
                ISampleProvider sampleProvider = ApplyAudioEffects(audioReader, item.SpeakerProfile);
                
                // Apply volume
                var volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = Volume * item.SpeakerProfile.Effects.VolumeMultiplier
                };
                
                // Initialize and start playback
                _waveOut.Init(volumeProvider);
                
                // Fire started event
                AudioStarted?.Invoke(this, new AudioPlaybackEventArgs(item));
                
                _waveOut.Play();
                
                // Wait for playback to complete or be stopped
                while (_waveOut.PlaybackState == PlaybackState.Playing && !SkipRequested)
                {
                    await Task.Delay(50);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Error playing {item.AudioPath}: {ex.Message}");
                PlaybackError?.Invoke(this, $"Error playing audio: {ex.Message}");
            }
            finally
            {
                lock (_playbackLock)
                {
                    _isPlaying = false;
                    _currentItem = null;
                }
            }
        }
        
        /// <summary>
        /// Apply audio effects based on speaker profile
        /// </summary>
        private ISampleProvider ApplyAudioEffects(ISampleProvider input, SpeakerProfile speaker)
        {
            ISampleProvider output = input;
            var effects = speaker.Effects;
            
            try
            {
                // Apply pitch shifting if enabled
                if (effects.EnablePitchShift && Math.Abs(effects.PitchShiftSemitones) > 0.1f)
                {
                    // Note: This is a simplified implementation
                    // For production, you'd want to use a more sophisticated pitch shifter
                    var pitchShifter = new SmbPitchShiftingSampleProvider(output);
                    pitchShifter.PitchFactor = (float)Math.Pow(2.0, effects.PitchShiftSemitones / 12.0);
                    output = pitchShifter;
                }
                
                // Apply EQ filtering (simplified - advanced filters can be added later)
                // Note: BiQuadFilter requires additional NAudio extensions
                // For now, we'll skip these filters and add them in a future update
                
                // Apply reverb if enabled (simplified implementation)
                if (effects.EnableReverb)
                {
                    // This is a basic reverb effect - for production you'd want a more sophisticated implementation
                    output = new ReverbSampleProvider(output, effects.ReverbRoomSize, effects.ReverbWetLevel, effects.ReverbDryLevel);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Error applying effects: {ex.Message}");
                // Return original input if effects fail
                return input;
            }
            
            return output;
        }
        
        /// <summary>
        /// Handle playback completion events
        /// </summary>
        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine($"[Audio] Playback stopped with error: {e.Exception.Message}");
                PlaybackError?.Invoke(this, e.Exception.Message);
            }
            
            if (_currentItem != null)
            {
                Console.WriteLine($"[Audio] Completed: \"{_currentItem.DialogueEntry.Text}\"");
                AudioCompleted?.Invoke(this, new AudioPlaybackEventArgs(_currentItem));
            }
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _cancellationTokenSource.Cancel();
            
            _waveOut?.Stop();
            _waveOut?.Dispose();
            
            _cancellationTokenSource?.Dispose();
        }
    }
    
    /// <summary>
    /// Represents an item in the audio playback queue
    /// </summary>
    public class AudioPlaybackItem
    {
        public string AudioPath { get; set; } = "";
        public DialogueEntry DialogueEntry { get; set; } = new();
        public SpeakerProfile SpeakerProfile { get; set; } = new();
        public DateTime QueuedAt { get; set; }
    }
    
    /// <summary>
    /// Event arguments for audio playback events
    /// </summary>
    public class AudioPlaybackEventArgs : EventArgs
    {
        public AudioPlaybackItem Item { get; }
        
        public AudioPlaybackEventArgs(AudioPlaybackItem item)
        {
            Item = item;
        }
    }
    
    /// <summary>
    /// Simple reverb effect implementation
    /// </summary>
    public class ReverbSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly float[] _delayBuffer;
        private readonly int _delayLength;
        private int _delayIndex;
        private readonly float _wetLevel;
        private readonly float _dryLevel;
        
        public WaveFormat WaveFormat => _source.WaveFormat;
        
        public ReverbSampleProvider(ISampleProvider source, float roomSize, float wetLevel, float dryLevel)
        {
            _source = source;
            _wetLevel = wetLevel;
            _dryLevel = dryLevel;
            
            // Calculate delay buffer size based on room size
            _delayLength = (int)(source.WaveFormat.SampleRate * roomSize * 0.1f); // Max 100ms delay
            _delayBuffer = new float[_delayLength];
        }
        
        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            
            for (int i = 0; i < samplesRead; i++)
            {
                float input = buffer[offset + i];
                
                // Get delayed sample
                float delayed = _delayBuffer[_delayIndex];
                
                // Store current sample with feedback
                _delayBuffer[_delayIndex] = input + delayed * 0.3f;
                
                // Mix dry and wet signals
                buffer[offset + i] = input * _dryLevel + delayed * _wetLevel;
                
                // Advance delay index
                _delayIndex = (_delayIndex + 1) % _delayLength;
            }
            
            return samplesRead;
        }
    }
}