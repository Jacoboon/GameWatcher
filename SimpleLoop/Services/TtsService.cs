using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleLoop.Services
{
    /// <summary>
    /// OpenAI TTS integration service for generating voiceovers from dialogue text
    /// </summary>
    public class TtsService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _voicesDirectory;
        private const string OPENAI_TTS_ENDPOINT = "https://api.openai.com/v1/audio/speech";
        
        public TtsService(string apiKey, string voicesDirectory = "voices")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _voicesDirectory = voicesDirectory;
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            
            // Ensure voices directory exists
            Directory.CreateDirectory(_voicesDirectory);
        }
        
        /// <summary>
        /// Generate TTS audio for a dialogue entry using the speaker's voice profile
        /// </summary>
        /// <param name="dialogueEntry">The dialogue to convert to speech</param>
        /// <param name="speakerProfile">Speaker profile containing TTS settings</param>
        /// <returns>Path to the generated audio file, or null if generation failed</returns>
        public async Task<string?> GenerateAudioAsync(DialogueEntry dialogueEntry, SpeakerProfile speakerProfile)
        {
            if (dialogueEntry == null || speakerProfile == null)
                return null;
                
            try
            {
                var textToSpeak = dialogueEntry.GetTextForTTS();
                if (string.IsNullOrWhiteSpace(textToSpeak))
                {
                    Console.WriteLine($"[TTS] Skipping empty text for dialogue {dialogueEntry.Id}");
                    return null;
                }
                
                // Create speaker-specific directory
                var speakerDir = Path.Combine(_voicesDirectory, SanitizeFileName(speakerProfile.Name));
                Directory.CreateDirectory(speakerDir);
                
                // Generate unique filename based on dialogue content
                var audioFileName = GenerateAudioFileName(dialogueEntry, speakerProfile);
                var audioFilePath = Path.Combine(speakerDir, audioFileName);
                
                // Skip generation if audio already exists
                if (File.Exists(audioFilePath))
                {
                    Console.WriteLine($"[TTS] Audio already exists: {audioFilePath}");
                    return audioFilePath;
                }
                
                Console.WriteLine($"[TTS] Generating audio for: \"{textToSpeak}\" (Voice: {speakerProfile.TtsVoiceId})");
                
                // Prepare OpenAI TTS request
                var requestData = new
                {
                    model = "tts-1", // Use tts-1 for faster generation, tts-1-hd for higher quality
                    input = textToSpeak,
                    voice = speakerProfile.TtsVoiceId,
                    response_format = "mp3",
                    speed = Math.Clamp(speakerProfile.TtsSpeed, 0.25f, 4.0f)
                };
                
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Make API request
                var response = await _httpClient.PostAsync(OPENAI_TTS_ENDPOINT, content);
                
                if (response.IsSuccessStatusCode)
                {
                    // Save audio file
                    var audioData = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(audioFilePath, audioData);
                    
                    Console.WriteLine($"[TTS] Audio generated successfully: {audioFilePath} ({audioData.Length} bytes)");
                    
                    // Update dialogue entry
                    dialogueEntry.AudioPath = audioFilePath;
                    dialogueEntry.HasAudio = true;
                    dialogueEntry.AudioGeneratedAt = DateTime.Now;
                    
                    return audioFilePath;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[TTS] API Error {response.StatusCode}: {error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TTS] Error generating audio: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Generate multiple audio files for a batch of dialogue entries
        /// </summary>
        /// <param name="dialogues">Dialogue entries with their speaker profiles</param>
        /// <param name="maxConcurrent">Maximum concurrent API calls</param>
        /// <returns>Number of successfully generated audio files</returns>
        public async Task<int> GenerateBatchAudioAsync(
            IEnumerable<(DialogueEntry dialogue, SpeakerProfile speaker)> dialogues, 
            int maxConcurrent = 3)
        {
            var semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
            var tasks = new List<Task<bool>>();
            
            foreach (var (dialogue, speaker) in dialogues)
            {
                tasks.Add(GenerateWithSemaphore(dialogue, speaker, semaphore));
            }
            
            var results = await Task.WhenAll(tasks);
            return results.Count(success => success);
        }
        
        private async Task<bool> GenerateWithSemaphore(DialogueEntry dialogue, SpeakerProfile speaker, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await GenerateAudioAsync(dialogue, speaker);
                return result != null;
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        /// <summary>
        /// Generate filename for audio based on dialogue content and speaker
        /// </summary>
        private string GenerateAudioFileName(DialogueEntry dialogueEntry, SpeakerProfile speakerProfile)
        {
            // Create a hash of the text content for unique filenames
            var textHash = Math.Abs(dialogueEntry.GetTextForTTS().GetHashCode()).ToString("X8");
            var speakerName = SanitizeFileName(speakerProfile.Name);
            
            // Include voice settings in filename for uniqueness
            var voiceSettings = $"{speakerProfile.TtsVoiceId}_{speakerProfile.TtsSpeed:F1}";
            
            return $"{speakerName}_{textHash}_{voiceSettings}.mp3";
        }
        
        /// <summary>
        /// Sanitize filename to be filesystem-safe
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "unknown";
                
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder();
            
            foreach (char c in fileName)
            {
                if (!invalid.Contains(c))
                    sanitized.Append(c);
                else
                    sanitized.Append('_');
            }
            
            var result = sanitized.ToString();
            
            // Ensure it's not too long and not empty
            if (result.Length > 50)
                result = result.Substring(0, 50);
                
            return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
        }
        
        /// <summary>
        /// Check if OpenAI API key is valid by making a test request
        /// </summary>
        public async Task<bool> ValidateApiKeyAsync()
        {
            try
            {
                var testRequest = new
                {
                    model = "tts-1",
                    input = "test",
                    voice = "alloy",
                    response_format = "mp3"
                };
                
                var jsonContent = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(OPENAI_TTS_ENDPOINT, content);
                
                // Any non-401/403 response means the key is valid (even if request fails for other reasons)
                return response.StatusCode != System.Net.HttpStatusCode.Unauthorized && 
                       response.StatusCode != System.Net.HttpStatusCode.Forbidden;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Get available OpenAI TTS voices
        /// </summary>
        public static string[] GetAvailableVoices()
        {
            return new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer", "coral", "sage" };
        }
        
        /// <summary>
        /// Get voice characteristics for UI selection
        /// </summary>
        public static Dictionary<string, string> GetVoiceDescriptions()
        {
            return new Dictionary<string, string>
            {
                ["alloy"] = "Neutral, balanced voice suitable for most characters",
                ["echo"] = "Male voice with clear pronunciation, good for authoritative characters",
                ["fable"] = "British accent, sophisticated tone, ideal for wise or noble characters",
                ["onyx"] = "Deep male voice, suitable for powerful or mysterious characters", 
                ["nova"] = "Young female voice, energetic and friendly",
                ["shimmer"] = "Soft female voice, gentle and warm",
                ["coral"] = "Warm, engaging female voice with natural intonation",
                ["sage"] = "Mature, wise-sounding voice with gravitas"
            };
        }
        
        /// <summary>
        /// Estimate cost for TTS generation
        /// </summary>
        /// <param name="textLength">Length of text to convert</param>
        /// <returns>Estimated cost in USD</returns>
        public static decimal EstimateCost(int textLength)
        {
            // OpenAI TTS pricing: $0.015 per 1K characters
            const decimal costPer1K = 0.015m;
            return (textLength / 1000m) * costPer1K;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}