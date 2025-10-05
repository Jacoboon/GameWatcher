using System;
using System.Collections.Generic;

namespace SimpleLoop
{
    /// <summary>
    /// Represents a unique character/speaker with voice profile and audio effects
    /// </summary>
    public class SpeakerProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = ""; // e.g., "Cornelia Castle", "Elfheim"
        
        // Character Metadata
        public string Gender { get; set; } = "Unknown"; // Male, Female, Non-binary, Unknown
        public string Age { get; set; } = "Unknown"; // Young, Middle-aged, Old, Ancient, Unknown
        public string PersonalityTrait { get; set; } = ""; // Wise, Mysterious, Cheerful, Gruff, etc.
        public string CharacterType { get; set; } = "NPC"; // King, Sage, Merchant, Guard, Villager, Boss, etc.
        
        // OpenAI TTS Settings
        public string TtsVoiceId { get; set; } = "alloy"; // alloy, echo, fable, onyx, nova, shimmer
        public float TtsSpeed { get; set; } = 1.0f; // 0.25 to 4.0
        public float TtsPitch { get; set; } = 1.0f; // Pitch adjustment (if supported)
        
        // NAudio Effects Chain
        public AudioEffects Effects { get; set; } = new();
        
        // Voice Consistency
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public int UsageCount { get; set; } = 0;
        public List<string> SampleDialogue { get; set; } = new(); // Example lines for this character
        
        // Auto-assignment rules (for detecting this speaker from dialogue)
        public List<string> NameKeywords { get; set; } = new(); // "Your Majesty", "My King", etc.
        public List<string> DialoguePatterns { get; set; } = new(); // Common phrases this character says
        
        public string GenerateId()
        {
            // Generate consistent ID based on name and character type
            var combined = $"{Name}_{CharacterType}".ToLower().Replace(" ", "_");
            return $"speaker_{combined}_{Math.Abs(combined.GetHashCode()):X6}";
        }
        
        /// <summary>
        /// Creates a voice description for AI-assisted voice selection
        /// </summary>
        public string GetVoiceDescription()
        {
            var desc = $"This is {Name}";
            if (!string.IsNullOrEmpty(Description))
            {
                desc += $", {Description}";
            }
            
            if (Gender != "Unknown")
            {
                desc += $". Gender: {Gender}";
            }
            
            if (Age != "Unknown")
            {
                desc += $", Age: {Age}";
            }
            
            if (!string.IsNullOrEmpty(PersonalityTrait))
            {
                desc += $", Personality: {PersonalityTrait}";
            }
            
            if (!string.IsNullOrEmpty(Location))
            {
                desc += $". Found in: {Location}";
            }
            
            return desc + ".";
        }
        
        /// <summary>
        /// Determines if this speaker profile matches given dialogue context
        /// </summary>
        public float CalculateMatchScore(string dialogue, string contextInfo = "")
        {
            float score = 0f;
            
            // Check for name keywords
            foreach (var keyword in NameKeywords)
            {
                if (dialogue.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10f;
                }
            }
            
            // Check for dialogue patterns
            foreach (var pattern in DialoguePatterns)
            {
                if (dialogue.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    score += 5f;
                }
            }
            
            // Location context boost
            if (!string.IsNullOrEmpty(Location) && contextInfo.Contains(Location, StringComparison.OrdinalIgnoreCase))
            {
                score += 3f;
            }
            
            return score;
        }
    }
    
    /// <summary>
    /// Audio effects configuration for NAudio processing
    /// </summary>
    public class AudioEffects
    {
        // Reverb/Echo Effects (for environment)
        public bool EnableReverb { get; set; } = false;
        public float ReverbRoomSize { get; set; } = 0.5f; // 0.0 to 1.0
        public float ReverbDamping { get; set; } = 0.5f;  // 0.0 to 1.0
        public float ReverbWetLevel { get; set; } = 0.3f; // 0.0 to 1.0
        public float ReverbDryLevel { get; set; } = 0.7f; // 0.0 to 1.0
        
        // Pitch/Tone Adjustments
        public bool EnablePitchShift { get; set; } = false;
        public float PitchShiftSemitones { get; set; } = 0f; // -12 to +12 semitones
        
        // EQ/Filtering
        public bool EnableLowPass { get; set; } = false;
        public float LowPassFrequency { get; set; } = 8000f; // Hz
        public bool EnableHighPass { get; set; } = false;
        public float HighPassFrequency { get; set; } = 80f; // Hz
        
        // Volume/Dynamics
        public float VolumeMultiplier { get; set; } = 1.0f; // 0.0 to 2.0
        public bool EnableCompression { get; set; } = false;
        public float CompressionRatio { get; set; } = 4.0f; // 1.0 to 20.0
        
        // Special Effects
        public bool EnableDistortion { get; set; } = false;
        public float DistortionGain { get; set; } = 1.0f;
        public bool EnableChorus { get; set; } = false;
        public float ChorusDepth { get; set; } = 0.3f;
        
        // Environment Presets
        public string EnvironmentPreset { get; set; } = "None"; // None, Cathedral, Cave, Forest, Underwater, etc.
        
        /// <summary>
        /// Apply a preset configuration for common environments
        /// </summary>
        public void ApplyEnvironmentPreset(string preset)
        {
            EnvironmentPreset = preset;
            
            switch (preset.ToLower())
            {
                case "cathedral":
                case "throne_room":
                    EnableReverb = true;
                    ReverbRoomSize = 0.9f;
                    ReverbDamping = 0.3f;
                    ReverbWetLevel = 0.6f;
                    ReverbDryLevel = 0.4f;
                    break;
                    
                case "cave":
                case "dungeon":
                    EnableReverb = true;
                    ReverbRoomSize = 0.8f;
                    ReverbDamping = 0.6f;
                    ReverbWetLevel = 0.4f;
                    EnableLowPass = true;
                    LowPassFrequency = 6000f;
                    break;
                    
                case "outdoor":
                case "field":
                    EnableReverb = true;
                    ReverbRoomSize = 0.3f;
                    ReverbDamping = 0.8f;
                    ReverbWetLevel = 0.2f;
                    break;
                    
                case "underwater":
                    EnableLowPass = true;
                    LowPassFrequency = 3000f;
                    EnableReverb = true;
                    ReverbRoomSize = 0.7f;
                    ReverbWetLevel = 0.8f;
                    break;
                    
                case "mystical":
                case "magic":
                    EnableChorus = true;
                    ChorusDepth = 0.4f;
                    EnableReverb = true;
                    ReverbRoomSize = 0.6f;
                    ReverbWetLevel = 0.3f;
                    break;
                    
                case "robotic":
                case "mechanical":
                    EnableDistortion = true;
                    DistortionGain = 1.3f;
                    EnableHighPass = true;
                    HighPassFrequency = 200f;
                    break;
                    
                default: // "none" or unknown
                    // Reset to defaults
                    EnableReverb = false;
                    EnablePitchShift = false;
                    EnableLowPass = false;
                    EnableHighPass = false;
                    EnableDistortion = false;
                    EnableChorus = false;
                    break;
            }
        }
    }
}