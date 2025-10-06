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
                // Look in multiple possible locations for the config file
                var searchPaths = new[]
                {
                    CONFIG_FILE, // Current directory
                    Path.Combine("..", CONFIG_FILE), // Parent directory  
                    Path.Combine("..", "..", CONFIG_FILE), // Two levels up
                    Path.Combine("SimpleLoop", CONFIG_FILE), // SimpleLoop subdirectory
                    Path.Combine("..", "SimpleLoop", CONFIG_FILE), // ../SimpleLoop/
                };
                
                string? configPath = null;
                foreach (var searchPath in searchPaths)
                {
                    if (File.Exists(searchPath))
                    {
                        configPath = searchPath;
                        break;
                    }
                }
                
                // Try to write debug info to a temp file since console output isn't visible in WPF
                try 
                {
                    var debugInfo = $"[Config] Current directory: {Directory.GetCurrentDirectory()}\n[Config] Found config at: {configPath ?? "NOT FOUND"}\n";
                    File.WriteAllText("tts_debug.log", debugInfo);
                }
                catch { } // Ignore errors in debug logging
                
                if (configPath != null)
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<TtsConfiguration>(json);
                    var result = config ?? new TtsConfiguration();
                    
                    // Set unified voices directory - find repo root and use voices/ there
                    result.VoicesDirectory = FindRepoVoicesDirectory();
                    
                    try 
                    {
                        var debugInfo = $"[Config] Config valid: {result.IsValid()}, API Key present: {!string.IsNullOrWhiteSpace(result.OpenAiApiKey)}, Voices Dir: {result.VoicesDirectory}\n";
                        File.AppendAllText("tts_debug.log", debugInfo);
                    }
                    catch { }
                    
                    return result;
                }
                else
                {
                    try 
                    {
                        File.AppendAllText("tts_debug.log", "[Config] Config file not found in any search paths, using defaults\n");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                try 
                {
                    File.AppendAllText("tts_debug.log", $"[Config] Error loading TTS configuration: {ex.Message}\n");
                }
                catch { }
            }
            
            // Return defaults if file doesn't exist or loading failed
            var defaultConfig = new TtsConfiguration();
            defaultConfig.VoicesDirectory = FindRepoVoicesDirectory();
            return defaultConfig;
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
        /// Find the repo root and return the unified voices directory path
        /// </summary>
        private static string FindRepoVoicesDirectory()
        {
            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                
                // Look for repo indicators (GameWatcher.sln or .git)
                var searchDir = currentDir;
                for (int i = 0; i < 5; i++) // Search up to 5 levels up
                {
                    if (File.Exists(Path.Combine(searchDir, "GameWatcher.sln")) ||
                        Directory.Exists(Path.Combine(searchDir, ".git")))
                    {
                        var repoVoicesDir = Path.Combine(searchDir, "voices");
                        try 
                        {
                            File.AppendAllText("tts_debug.log", $"[Config] Found repo root at: {searchDir}, using voices dir: {repoVoicesDir}\n");
                        }
                        catch { }
                        return repoVoicesDir;
                    }
                    
                    var parentDir = Directory.GetParent(searchDir);
                    if (parentDir == null) break;
                    searchDir = parentDir.FullName;
                }
                
                // Fallback to current directory + voices if repo root not found
                var fallbackDir = Path.Combine(currentDir, "voices");
                try 
                {
                    File.AppendAllText("tts_debug.log", $"[Config] Could not find repo root, using fallback: {fallbackDir}\n");
                }
                catch { }
                return fallbackDir;
            }
            catch (Exception ex)
            {
                try 
                {
                    File.AppendAllText("tts_debug.log", $"[Config] Error finding repo voices directory: {ex.Message}\n");
                }
                catch { }
                return "voices"; // Ultimate fallback
            }
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