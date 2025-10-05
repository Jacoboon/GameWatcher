using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SimpleLoop
{
    /// <summary>
    /// Manages speaker profiles and automatic speaker detection
    /// </summary>
    public class SpeakerCatalog
    {
        private readonly string catalogPath;
        private List<SpeakerProfile> speakers = new();
        
        public SpeakerCatalog(string catalogPath = "speaker_catalog.json")
        {
            this.catalogPath = catalogPath;
            LoadCatalog();
            InitializeDefaultSpeakers();
        }
        
        /// <summary>
        /// Load speaker profiles from JSON file
        /// </summary>
        public void LoadCatalog()
        {
            if (File.Exists(catalogPath))
            {
                try
                {
                    var json = File.ReadAllText(catalogPath);
                    speakers = JsonConvert.DeserializeObject<List<SpeakerProfile>>(json) ?? new List<SpeakerProfile>();
                    Console.WriteLine($"📚 Loaded {speakers.Count} speaker profiles from catalog");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error loading speaker catalog: {ex.Message}");
                    speakers = new List<SpeakerProfile>();
                }
            }
            else
            {
                speakers = new List<SpeakerProfile>();
                Console.WriteLine("ℹ️ No speaker catalog found, will create new one");
            }
        }
        
        /// <summary>
        /// Save speaker profiles to JSON file
        /// </summary>
        public void SaveCatalog()
        {
            try
            {
                var json = JsonConvert.SerializeObject(speakers, Formatting.Indented);
                File.WriteAllText(catalogPath, json);
                Console.WriteLine($"💾 Saved {speakers.Count} speaker profiles to catalog");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving speaker catalog: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Initialize common FF1 character archetypes
        /// </summary>
        private void InitializeDefaultSpeakers()
        {
            if (speakers.Count > 0) return; // Don't overwrite existing speakers
            
            // Create default FF1 character profiles
            var defaultSpeakers = new List<SpeakerProfile>
            {
                new SpeakerProfile
                {
                    Name = "King of Cornelia",
                    Description = "A wise and noble ruler, large and commanding presence",
                    Location = "Cornelia Castle",
                    Gender = "Male",
                    Age = "Middle-aged",
                    PersonalityTrait = "Wise",
                    CharacterType = "King",
                    TtsVoiceId = "onyx", // Deep male voice
                    TtsSpeed = 0.9f, // Slightly slower for gravitas
                    NameKeywords = { "Your Majesty", "My King", "Sire", "Your Highness" },
                    DialoguePatterns = { "I am the King", "My kingdom", "Cornelia" },
                    Effects = new AudioEffects()
                },
                
                new SpeakerProfile
                {
                    Name = "Sage of Elfheim",
                    Description = "An ancient elven sage with mystical knowledge",
                    Location = "Elfheim",
                    Gender = "Male",
                    Age = "Ancient",
                    PersonalityTrait = "Mystical",
                    CharacterType = "Sage",
                    TtsVoiceId = "echo", // Ethereal voice
                    TtsSpeed = 0.8f, // Slow and deliberate
                    NameKeywords = { "sage", "ancient one", "wise one" },
                    DialoguePatterns = { "I am a sage", "When the time is right", "future is revealed" },
                    Effects = new AudioEffects()
                },
                
                new SpeakerProfile
                {
                    Name = "Princess Sara",
                    Description = "The kidnapped princess of Cornelia, young and brave",
                    Location = "Cornelia Castle",
                    Gender = "Female", 
                    Age = "Young",
                    PersonalityTrait = "Brave",
                    CharacterType = "Princess",
                    TtsVoiceId = "nova", // Clear female voice
                    TtsSpeed = 1.1f, // Slightly faster, youthful energy
                    NameKeywords = { "Princess", "Sara", "My Lady" },
                    DialoguePatterns = { "Thank you", "brave warriors", "Garland" },
                    Effects = new AudioEffects()
                },
                
                new SpeakerProfile
                {
                    Name = "Mysterious Voice", 
                    Description = "Unknown speaker, possibly magical or otherworldly",
                    Location = "Unknown",
                    Gender = "Unknown",
                    Age = "Unknown", 
                    PersonalityTrait = "Mysterious",
                    CharacterType = "Mysterious",
                    TtsVoiceId = "shimmer", // Ethereal, gender-neutral
                    TtsSpeed = 0.85f,
                    NameKeywords = { },
                    DialoguePatterns = { "help", "don't know", "please" },
                    Effects = new AudioEffects()
                }
            };
            
            // Apply environment effects to King (throne room reverb)
            defaultSpeakers[0].Effects.ApplyEnvironmentPreset("throne_room");
            
            // Apply mystical effects to Sage
            defaultSpeakers[1].Effects.ApplyEnvironmentPreset("mystical");
            
            // Generate IDs for all default speakers
            foreach (var speaker in defaultSpeakers)
            {
                speaker.Id = speaker.GenerateId();
            }
            
            speakers.AddRange(defaultSpeakers);
            SaveCatalog();
            
            Console.WriteLine($"🎭 Initialized {defaultSpeakers.Count} default FF1 speaker profiles");
        }
        
        /// <summary>
        /// Find the best matching speaker for given dialogue
        /// </summary>
        public SpeakerProfile? IdentifySpeaker(string dialogue, string contextInfo = "")
        {
            if (speakers.Count == 0) return null;
            
            var scores = speakers.Select(s => new { Speaker = s, Score = s.CalculateMatchScore(dialogue, contextInfo) })
                               .Where(x => x.Score > 0)
                               .OrderByDescending(x => x.Score)
                               .ToList();
            
            if (scores.Any())
            {
                var bestMatch = scores.First();
                Console.WriteLine($"🎯 Matched speaker: {bestMatch.Speaker.Name} (score: {bestMatch.Score})");
                
                // Update usage stats
                bestMatch.Speaker.LastUsed = DateTime.Now;
                bestMatch.Speaker.UsageCount++;
                
                return bestMatch.Speaker;
            }
            
            // No match found, return generic NPC profile
            return GetOrCreateGenericSpeaker("NPC");
        }
        
        /// <summary>
        /// Get or create a generic speaker profile for unmatched dialogue
        /// </summary>
        public SpeakerProfile GetOrCreateGenericSpeaker(string speakerType)
        {
            var existing = speakers.FirstOrDefault(s => s.Name == $"Generic {speakerType}");
            if (existing != null) return existing;
            
            var generic = new SpeakerProfile
            {
                Name = $"Generic {speakerType}",
                Description = $"Generic {speakerType.ToLower()} character",
                Location = "Various",
                CharacterType = speakerType,
                TtsVoiceId = GetDefaultVoiceForType(speakerType),
                Effects = new AudioEffects()
            };
            
            generic.Id = generic.GenerateId();
            speakers.Add(generic);
            
            Console.WriteLine($"🆕 Created generic speaker profile: {generic.Name}");
            return generic;
        }
        
        /// <summary>
        /// Get default TTS voice based on character type
        /// </summary>
        private string GetDefaultVoiceForType(string characterType)
        {
            return characterType.ToLower() switch
            {
                "king" => "onyx",     // Deep authoritative
                "queen" => "nova",    // Elegant female  
                "sage" => "echo",     // Mystical
                "merchant" => "fable", // Friendly
                "guard" => "onyx",    // Stern male
                "villager" => "alloy", // Neutral
                _ => "alloy"          // Default neutral
            };
        }
        
        /// <summary>
        /// Add or update a speaker profile
        /// </summary>
        public void AddOrUpdateSpeaker(SpeakerProfile speaker)
        {
            var existing = speakers.FirstOrDefault(s => s.Id == speaker.Id);
            if (existing != null)
            {
                speakers.Remove(existing);
            }
            
            speakers.Add(speaker);
            SaveCatalog();
        }
        
        /// <summary>
        /// Get all speaker profiles
        /// </summary>
        public List<SpeakerProfile> GetAllSpeakers() => speakers.ToList();
        
        /// <summary>
        /// Get speaker by ID
        /// </summary>
        public SpeakerProfile? GetSpeakerById(string id)
        {
            return speakers.FirstOrDefault(s => s.Id == id);
        }
        
        /// <summary>
        /// Get speaker by name
        /// </summary>
        public SpeakerProfile? GetSpeakerByName(string name)
        {
            return speakers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Remove a speaker profile
        /// </summary>
        public bool RemoveSpeaker(string id)
        {
            var speaker = GetSpeakerById(id);
            if (speaker != null)
            {
                speakers.Remove(speaker);
                SaveCatalog();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get usage statistics
        /// </summary>
        public void ShowStatistics()
        {
            Console.WriteLine("=== SPEAKER CATALOG STATISTICS ===");
            Console.WriteLine($"Total speakers: {speakers.Count}");
            
            var mostUsed = speakers.OrderByDescending(s => s.UsageCount).Take(5);
            Console.WriteLine("\nMost used speakers:");
            foreach (var speaker in mostUsed)
            {
                Console.WriteLine($"  {speaker.Name}: {speaker.UsageCount} times");
            }
            
            var recentlyUsed = speakers.Where(s => s.LastUsed > DateTime.Now.AddHours(-1)).OrderByDescending(s => s.LastUsed);
            Console.WriteLine("\nRecently active:");
            foreach (var speaker in recentlyUsed)
            {
                Console.WriteLine($"  {speaker.Name}: {speaker.LastUsed:HH:mm:ss}");
            }
        }
    }
}