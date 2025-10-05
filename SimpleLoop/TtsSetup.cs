using System;
using System.Threading.Tasks;
using SimpleLoop.Services;

namespace SimpleLoop
{
    /// <summary>
    /// TTS Setup utility for configuring OpenAI API key and testing TTS functionality
    /// </summary>
    public static class TtsSetup
    {
        public static async Task<bool> SetupTtsAsync()
        {
            Console.WriteLine("=== GameWatcher TTS Setup ===");
            Console.WriteLine();
            
            var config = TtsConfiguration.Load();
            
            // Check if already configured
            if (!string.IsNullOrWhiteSpace(config.OpenAiApiKey))
            {
                Console.WriteLine($"OpenAI API Key: {MaskApiKey(config.OpenAiApiKey)} (already configured)");
                Console.WriteLine($"Default Voice: {config.DefaultVoice}");
                Console.WriteLine($"Voices Directory: {config.VoicesDirectory}");
                Console.WriteLine();
                
                Console.Write("Do you want to update the configuration? (y/n): ");
                var updateResponse = Console.ReadLine()?.ToLower();
                if (updateResponse != "y" && updateResponse != "yes")
                {
                    return await TestCurrentConfiguration(config);
                }
            }
            
            // Get API key from user
            Console.WriteLine("Enter your OpenAI API Key:");
            Console.WriteLine("(You can get one from https://platform.openai.com/api-keys)");
            Console.Write("API Key: ");
            
            var apiKey = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("❌ No API key provided. TTS setup cancelled.");
                return false;
            }
            
            config.OpenAiApiKey = apiKey;
            
            // Configure voice settings
            Console.WriteLine();
            Console.WriteLine("Available Voices:");
            var voices = TtsService.GetAvailableVoices();
            var descriptions = TtsService.GetVoiceDescriptions();
            
            for (int i = 0; i < voices.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {voices[i]} - {descriptions[voices[i]]}");
            }
            
            Console.Write($"Select default voice (1-{voices.Length}) or press Enter for '{config.DefaultVoice}': ");
            var voiceInput = Console.ReadLine()?.Trim();
            
            if (int.TryParse(voiceInput, out int voiceIndex) && voiceIndex >= 1 && voiceIndex <= voices.Length)
            {
                config.DefaultVoice = voices[voiceIndex - 1];
            }
            
            // Configure speed
            Console.Write($"Enter default speech speed (0.25-4.0) or press Enter for {config.DefaultSpeed}: ");
            var speedInput = Console.ReadLine()?.Trim();
            
            if (float.TryParse(speedInput, out float speed) && speed >= 0.25f && speed <= 4.0f)
            {
                config.DefaultSpeed = speed;
            }
            
            // Configure auto-play settings
            Console.Write($"Enable auto-play audio during capture? (y/n) [default: {(config.AutoPlayAudio ? "y" : "n")}]: ");
            var autoPlayInput = Console.ReadLine()?.ToLower().Trim();
            if (autoPlayInput == "y" || autoPlayInput == "yes")
            {
                config.AutoPlayAudio = true;
            }
            else if (autoPlayInput == "n" || autoPlayInput == "no")
            {
                config.AutoPlayAudio = false;
            }
            
            // Configure auto-generation
            Console.Write($"Enable auto-generate audio for new dialogue? (y/n) [default: {(config.AutoGenerateAudio ? "y" : "n")}]: ");
            var autoGenInput = Console.ReadLine()?.ToLower().Trim();
            if (autoGenInput == "y" || autoGenInput == "yes")
            {
                config.AutoGenerateAudio = true;
            }
            else if (autoGenInput == "n" || autoGenInput == "no")
            {
                config.AutoGenerateAudio = false;
            }
            
            // Save configuration
            config.Save();
            config.EnsureVoicesDirectory();
            
            Console.WriteLine();
            Console.WriteLine("✅ Configuration saved!");
            Console.WriteLine();
            
            // Test the configuration
            return await TestCurrentConfiguration(config);
        }
        
        private static async Task<bool> TestCurrentConfiguration(TtsConfiguration config)
        {
            Console.WriteLine("🧪 Testing OpenAI TTS API connection...");
            
            try
            {
                using var ttsService = new TtsService(config.OpenAiApiKey, config.VoicesDirectory);
                
                var isValid = await ttsService.ValidateApiKeyAsync();
                
                if (isValid)
                {
                    Console.WriteLine("✅ API key is valid!");
                    
                    // Offer to generate test audio
                    Console.Write("Generate test audio? (y/n): ");
                    var testResponse = Console.ReadLine()?.ToLower().Trim();
                    
                    if (testResponse == "y" || testResponse == "yes")
                    {
                        await GenerateTestAudio(ttsService, config);
                    }
                    
                    return true;
                }
                else
                {
                    Console.WriteLine("❌ API key validation failed. Please check your key and try again.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing API: {ex.Message}");
                return false;
            }
        }
        
        private static async Task GenerateTestAudio(TtsService ttsService, TtsConfiguration config)
        {
            Console.WriteLine("Generating test audio...");
            
            // Create test dialogue and speaker
            var testDialogue = new DialogueEntry
            {
                Id = "test_001",
                Text = "Welcome to the GameWatcher TTS system! Your audio generation is working perfectly.",
                Speaker = "Test Speaker"
            };
            
            var testSpeaker = new SpeakerProfile
            {
                Name = "Test Speaker",
                TtsVoiceId = config.DefaultVoice,
                TtsSpeed = config.DefaultSpeed,
                Description = "Test speaker for TTS validation"
            };
            
            try
            {
                var audioPath = await ttsService.GenerateAudioAsync(testDialogue, testSpeaker);
                
                if (!string.IsNullOrEmpty(audioPath))
                {
                    Console.WriteLine($"✅ Test audio generated: {audioPath}");
                    Console.WriteLine("You can play this file to verify audio quality.");
                    
                    // Offer to play if on Windows
                    if (OperatingSystem.IsWindows())
                    {
                        Console.Write("Play test audio now? (y/n): ");
                        var playResponse = Console.ReadLine()?.ToLower().Trim();
                        
                        if (playResponse == "y" || playResponse == "yes")
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = audioPath,
                                    UseShellExecute = true
                                });
                                Console.WriteLine("🔊 Playing test audio...");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Could not play audio automatically: {ex.Message}");
                                Console.WriteLine($"Please manually play: {audioPath}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ Test audio generation failed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating test audio: {ex.Message}");
            }
        }
        
        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
                return "***";
                
            return apiKey.Substring(0, 4) + "..." + apiKey.Substring(apiKey.Length - 4);
        }
        
        public static void ShowTtsStatus()
        {
            Console.WriteLine("=== TTS Status ===");
            
            var config = TtsConfiguration.Load();
            
            Console.WriteLine($"Configuration: {(config.IsValid() ? "✅ Valid" : "❌ Invalid")}");
            
            if (!config.IsValid())
            {
                Console.WriteLine($"Issue: {config.GetStatusMessage()}");
            }
            else
            {
                Console.WriteLine($"API Key: {MaskApiKey(config.OpenAiApiKey)}");
                Console.WriteLine($"Default Voice: {config.DefaultVoice}");
                Console.WriteLine($"Default Speed: {config.DefaultSpeed}");
                Console.WriteLine($"Auto-Play: {(config.AutoPlayAudio ? "Enabled" : "Disabled")}");
                Console.WriteLine($"Auto-Generate: {(config.AutoGenerateAudio ? "Enabled" : "Disabled")}");
                Console.WriteLine($"Voices Directory: {config.VoicesDirectory}");
            }
            
            Console.WriteLine();
        }
    }
}