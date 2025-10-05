using System;
using System.IO;
using System.Text.Json;

namespace SimpleLoop.Services
{
    /// <summary>
    /// Configuration service for managing API keys and TTS settings
    /// </summary>
    public class TtsConfiguration
    {
        private const string CONFIG_FILE = "tts_config.json";
        
        public string OpenAiApiKey { get; set; } = "";
        public string DefaultVoice { get; set; } = "alloy";
        public float DefaultSpeed { get; set; } = 1.0f;
        public bool AutoGenerateAudio { get; set; } = true;
        public bool AutoPlayAudio { get; set; } = true;
        public string VoicesDirectory { get; set; } = "voices";
        public int MaxConcurrentRequests { get; set; } = 3;
        public bool EnableAudioEffects { get; set; } = true;
        
        /// <summary>
        /// Load configuration from file or create defaults
        /// </summary>
        public static TtsConfiguration Load()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    var json = File.ReadAllText(CONFIG_FILE);
                    var config = JsonSerializer.Deserialize<TtsConfiguration>(json);
                    return config ?? new TtsConfiguration();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Error loading TTS configuration: {ex.Message}");
            }
            
            // Return defaults if file doesn't exist or loading failed
            return new TtsConfiguration();
        }
        
        /// <summary>
        /// Save current configuration to file
        /// </summary>
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(CONFIG_FILE, json);
                
                Console.WriteLine($"[Config] TTS configuration saved to {CONFIG_FILE}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Error saving TTS configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the configuration is valid for TTS operations
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(OpenAiApiKey) && 
                   !string.IsNullOrWhiteSpace(VoicesDirectory) &&
                   DefaultSpeed >= 0.25f && DefaultSpeed <= 4.0f;
        }
        
        /// <summary>
        /// Get configuration status message
        /// </summary>
        public string GetStatusMessage()
        {
            if (string.IsNullOrWhiteSpace(OpenAiApiKey))
                return "OpenAI API Key not configured";
                
            if (DefaultSpeed < 0.25f || DefaultSpeed > 4.0f)
                return "Invalid default speed (must be 0.25-4.0)";
                
            return "Configuration valid";
        }
        
        /// <summary>
        /// Create voices directory if it doesn't exist
        /// </summary>
        public void EnsureVoicesDirectory()
        {
            try
            {
                if (!Directory.Exists(VoicesDirectory))
                {
                    Directory.CreateDirectory(VoicesDirectory);
                    Console.WriteLine($"[Config] Created voices directory: {VoicesDirectory}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Error creating voices directory: {ex.Message}");
            }
        }
    }
}