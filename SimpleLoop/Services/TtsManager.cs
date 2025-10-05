using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleLoop.Services
{
    /// <summary>
    /// Central TTS manager that orchestrates audio generation and playback
    /// </summary>
    public class TtsManager : IDisposable
    {
        private readonly TtsConfiguration _config;
        private readonly TtsService? _ttsService;
        private readonly AudioPlaybackService? _audioService;
        private readonly DialogueCatalog _dialogueCatalog;
        private readonly SpeakerCatalog _speakerCatalog;
        
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        
        // Events for monitoring TTS operations
        public event EventHandler<TtsGenerationEventArgs>? AudioGenerated;
        public event EventHandler<string>? TtsError;
        
        public TtsManager(DialogueCatalog dialogueCatalog, SpeakerCatalog speakerCatalog)
        {
            _dialogueCatalog = dialogueCatalog ?? throw new ArgumentNullException(nameof(dialogueCatalog));
            _speakerCatalog = speakerCatalog ?? throw new ArgumentNullException(nameof(speakerCatalog));
            
            _config = TtsConfiguration.Load();
            _config.EnsureVoicesDirectory();
            
            if (_config.IsValid())
            {
                try
                {
                    _ttsService = new TtsService(_config.OpenAiApiKey, _config.VoicesDirectory);
                    _audioService = new AudioPlaybackService
                    {
                        AutoPlayEnabled = _config.AutoPlayAudio,
                        Volume = 0.7f
                    };
                    
                    // Subscribe to audio events for logging
                    _audioService.AudioStarted += OnAudioStarted;
                    _audioService.AudioCompleted += OnAudioCompleted;
                    _audioService.PlaybackError += OnPlaybackError;
                    
                    _isInitialized = true;
                    Console.WriteLine("[TTS Manager] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TTS Manager] Initialization error: {ex.Message}");
                    _ttsService = null;
                    _audioService = null;
                }
            }
            else
            {
                Console.WriteLine($"[TTS Manager] Configuration invalid: {_config.GetStatusMessage()}");
            }
        }
        
        /// <summary>
        /// Check if TTS manager is ready for operations
        /// </summary>
        public bool IsReady => _isInitialized && !_isDisposed;
        
        /// <summary>
        /// Process new dialogue detected by the capture system
        /// </summary>
        public async Task ProcessDialogueAsync(DialogueEntry dialogueEntry, SpeakerProfile speakerProfile)
        {
            if (!IsReady || dialogueEntry == null || speakerProfile == null)
                return;
                
            try
            {
                // Skip if audio already exists
                if (dialogueEntry.HasAudio && !string.IsNullOrEmpty(dialogueEntry.AudioPath) && File.Exists(dialogueEntry.AudioPath))
                {
                    Console.WriteLine($"[TTS Manager] Audio exists for: \"{dialogueEntry.Text}\"");
                    
                if (_config.AutoPlayAudio && _audioService != null)
                {
                    _audioService.QueueAudio(dialogueEntry.AudioPath, dialogueEntry, speakerProfile);
                }                    return;
                }
                
                // Generate audio if auto-generation is enabled
                if (_config.AutoGenerateAudio)
                {
                    Console.WriteLine($"[TTS Manager] Generating audio for: \"{dialogueEntry.Text}\" ({speakerProfile.Name})");
                    
                    var audioPath = await _ttsService?.GenerateAudioAsync(dialogueEntry, speakerProfile);
                    
                    if (!string.IsNullOrEmpty(audioPath))
                    {
                // Update catalogs with audio information
                _dialogueCatalog.SaveCatalog();                        // Fire generation event
                        AudioGenerated?.Invoke(this, new TtsGenerationEventArgs(dialogueEntry, speakerProfile, audioPath));
                        
                        // Queue for playback if enabled
                        if (_config.AutoPlayAudio && _audioService != null)
                        {
                            _audioService.QueueAudio(audioPath, dialogueEntry, speakerProfile);
                        }
                        
                        Console.WriteLine($"[TTS Manager] Successfully processed dialogue with audio generation");
                    }
                    else
                    {
                        TtsError?.Invoke(this, $"Failed to generate audio for: {dialogueEntry.Text}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TTS Manager] Error processing dialogue: {ex.Message}");
                TtsError?.Invoke(this, ex.Message);
            }
        }
        
        /// <summary>
        /// Play existing audio file without regenerating TTS
        /// </summary>
        /// <param name="audioPath">Path to the existing audio file</param>
        /// <param name="dialogueEntry">The dialogue entry for context</param>
        /// <param name="speakerProfile">The speaker profile for audio processing</param>
        public async Task PlayExistingAudioAsync(string audioPath, DialogueEntry dialogueEntry, SpeakerProfile speakerProfile)
        {
            try
            {
                Console.WriteLine($"[TTS Manager] Playing existing audio: {audioPath}");
                
                if (!File.Exists(audioPath))
                {
                    Console.WriteLine($"[TTS Manager] Warning: Audio file not found: {audioPath}");
                    return;
                }
                
                // Queue the existing audio file for playback
                _audioService.QueueAudio(audioPath, dialogueEntry, speakerProfile);
                Console.WriteLine($"[TTS Manager] Successfully queued existing audio for playback");
                
                // Invoke completion events
                AudioGenerated?.Invoke(this, new TtsGenerationEventArgs(dialogueEntry, speakerProfile, audioPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TTS Manager] Error playing existing audio: {ex.Message}");
                TtsError?.Invoke(this, ex.Message);
            }
        }
        
        /// <summary>
        /// Generate audio for multiple dialogue entries in batch
        /// </summary>
        public async Task<int> GenerateBatchAudioAsync(IEnumerable<DialogueEntry> dialogueEntries)
        {
            if (!IsReady)
                return 0;
                
            var itemsToProcess = new List<(DialogueEntry dialogue, SpeakerProfile speaker)>();
            
            foreach (var dialogue in dialogueEntries)
            {
                // Skip if already has audio
                if (dialogue.HasAudio && !string.IsNullOrEmpty(dialogue.AudioPath) && File.Exists(dialogue.AudioPath))
                    continue;
                    
                // Find or create speaker profile
                var speaker = _speakerCatalog.GetSpeakerByName(dialogue.Speaker) ?? 
                             _speakerCatalog.GetOrCreateGenericSpeaker("NPC");
                             
                if (speaker != null)
                {
                    itemsToProcess.Add((dialogue, speaker));
                }
            }
            
            if (itemsToProcess.Any())
            {
                Console.WriteLine($"[TTS Manager] Batch generating audio for {itemsToProcess.Count} dialogues");
                
                var successCount = await (_ttsService?.GenerateBatchAudioAsync(itemsToProcess, _config.MaxConcurrentRequests) ?? Task.FromResult(0));
                
                // Save updated catalogue
                _dialogueCatalog.SaveCatalog();
                
                Console.WriteLine($"[TTS Manager] Batch generation complete: {successCount}/{itemsToProcess.Count} successful");
                return successCount;
            }
            
            return 0;
        }
        
        /// <summary>
        /// Validate OpenAI API key
        /// </summary>
        public async Task<bool> ValidateApiKeyAsync()
        {
            if (!IsReady)
                return false;
                
            try
            {
                return await (_ttsService?.ValidateApiKeyAsync() ?? Task.FromResult(false));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TTS Manager] API key validation error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get TTS statistics
        /// </summary>
        public TtsStatistics GetStatistics()
        {
            var allDialogues = _dialogueCatalog.GetAllDialogue();
            var withAudio = allDialogues.Count(d => d.HasAudio);
            var totalTextLength = allDialogues.Sum(d => d.GetTextForTTS().Length);
            var estimatedCost = TtsService.EstimateCost(totalTextLength);
            
            return new TtsStatistics
            {
                TotalDialogues = allDialogues.Count,
                DialoguesWithAudio = withAudio,
                DialoguesWithoutAudio = allDialogues.Count - withAudio,
                QueueSize = _audioService?.GetQueueSize() ?? 0,
                TotalTextLength = totalTextLength,
                EstimatedCost = estimatedCost
            };
        }
        
        /// <summary>
        /// Control playback
        /// </summary>
        public void SkipCurrentAudio() => _audioService?.Skip();
        public void ReplayCurrentAudio() => _audioService?.Replay();
        public void ClearAudioQueue() => _audioService?.ClearQueue();
        
        public bool AutoPlayEnabled 
        { 
            get => _audioService?.AutoPlayEnabled ?? false;
            set 
            { 
                if (_audioService != null) 
                    _audioService.AutoPlayEnabled = value;
                _config.AutoPlayAudio = value;
                _config.Save();
            }
        }
        
        public bool AutoGenerateEnabled 
        { 
            get => _config.AutoGenerateAudio;
            set 
            { 
                _config.AutoGenerateAudio = value;
                _config.Save();
            }
        }
        
        /// <summary>
        /// Update TTS configuration and save
        /// </summary>
        public void UpdateConfiguration(Action<TtsConfiguration> configUpdate)
        {
            configUpdate(_config);
            _config.Save();
        }
        
        // Event handlers for audio service events
        private void OnAudioStarted(object? sender, AudioPlaybackEventArgs e)
        {
            Console.WriteLine($"[TTS Manager] Audio started: \"{e.Item.DialogueEntry.Text}\" ({e.Item.SpeakerProfile.Name})");
        }
        
        private void OnAudioCompleted(object? sender, AudioPlaybackEventArgs e)
        {
            Console.WriteLine($"[TTS Manager] Audio completed: \"{e.Item.DialogueEntry.Text}\"");
        }
        
        private void OnPlaybackError(object? sender, string error)
        {
            Console.WriteLine($"[TTS Manager] Playback error: {error}");
            TtsError?.Invoke(this, $"Playback error: {error}");
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _ttsService?.Dispose();
            _audioService?.Dispose();
        }
    }
    
    /// <summary>
    /// Event arguments for TTS generation events
    /// </summary>
    public class TtsGenerationEventArgs : EventArgs
    {
        public DialogueEntry DialogueEntry { get; }
        public SpeakerProfile SpeakerProfile { get; }
        public string AudioPath { get; }
        
        public TtsGenerationEventArgs(DialogueEntry dialogueEntry, SpeakerProfile speakerProfile, string audioPath)
        {
            DialogueEntry = dialogueEntry;
            SpeakerProfile = speakerProfile;
            AudioPath = audioPath;
        }
    }
    
    /// <summary>
    /// TTS operation statistics
    /// </summary>
    public class TtsStatistics
    {
        public int TotalDialogues { get; set; }
        public int DialoguesWithAudio { get; set; }
        public int DialoguesWithoutAudio { get; set; }
        public int QueueSize { get; set; }
        public int TotalTextLength { get; set; }
        public decimal EstimatedCost { get; set; }
    }
}