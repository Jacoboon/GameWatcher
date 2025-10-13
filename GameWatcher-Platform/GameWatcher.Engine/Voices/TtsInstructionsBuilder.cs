using System;
using System.Collections.Generic;

namespace GameWatcher.Engine.Voices
{
    /// <summary>
    /// Builds context-aware instructions for OpenAI gpt-4o-mini-tts model
    /// to improve voice acting quality and emotional delivery.
    /// </summary>
    public static class TtsInstructionsBuilder
    {
        private static readonly Dictionary<string, string> RoleInstructions = new()
        {
            // Royalty
            ["King"] = "Speak with regal authority and wisdom, like a noble king addressing subjects.",
            ["Queen"] = "Speak with dignified grace and authority, like a queen commanding respect.",
            ["Prince"] = "Speak with youthful confidence and nobility.",
            ["Princess"] = "Speak with gentle kindness and royal poise.",
            
            // Warriors & Knights
            ["Knight"] = "Speak with honorable conviction and martial discipline.",
            ["Warrior"] = "Speak with battle-hardened strength and determination.",
            ["Soldier"] = "Speak with military discipline and respect.",
            
            // Wise Characters
            ["Sage"] = "Speak mysteriously and wisely, like an ancient scholar sharing secrets.",
            ["Elder"] = "Speak with the wisdom of age and experience.",
            ["Scholar"] = "Speak intellectually with careful consideration.",
            
            // Villains
            ["Garland"] = "Speak menacingly with evil intent, like a dark knight.",
            ["Lich"] = "Speak with undead malevolence and ancient power.",
            ["Chaos"] = "Speak with terrifying cosmic power and darkness.",
            ["Villain"] = "Speak with sinister intent and malicious confidence.",
            
            // Common Folk
            ["Villager"] = "Speak plainly like an ordinary citizen.",
            ["Merchant"] = "Speak cheerfully with business enthusiasm.",
            ["Guard"] = "Speak with dutiful professionalism.",
            
            // Mystical
            ["Fairy"] = "Speak whimsically with magical lightness.",
            ["Spirit"] = "Speak ethereally with otherworldly presence.",
        };

        private static readonly Dictionary<string, string> EmotionInstructions = new()
        {
            ["excited"] = "Deliver with high energy and excitement.",
            ["urgent"] = "Speak quickly with pressing urgency.",
            ["panicked"] = "Speak frantically with fear and alarm.",
            ["calm"] = "Speak peacefully with measured composure.",
            ["sad"] = "Speak with deep sorrow and grief.",
            ["angry"] = "Speak with intense fury and rage.",
            ["mysterious"] = "Speak enigmatically with intrigue.",
            ["fearful"] = "Speak with trembling fear and worry.",
            ["confident"] = "Speak with bold self-assurance.",
            ["worried"] = "Speak with anxious concern.",
            ["grateful"] = "Speak with heartfelt appreciation.",
            ["shocked"] = "Speak with stunned surprise.",
            ["determined"] = "Speak with resolute conviction.",
            ["defeated"] = "Speak with weary resignation.",
            ["hopeful"] = "Speak with optimistic anticipation.",
        };

        /// <summary>
        /// Builds TTS instructions from speaker role and optional emotion.
        /// </summary>
        /// <param name="speakerRole">The character's role (King, Sage, Villager, etc.)</param>
        /// <param name="emotion">Optional emotion hint (excited, sad, angry, etc.)</param>
        /// <param name="customInstructions">Optional custom instructions to append</param>
        /// <returns>Complete instructions string for OpenAI TTS</returns>
        public static string Build(string? speakerRole = null, string? emotion = null, string? customInstructions = null)
        {
            var parts = new List<string>();

            // Add role-based instruction
            if (!string.IsNullOrWhiteSpace(speakerRole) && 
                RoleInstructions.TryGetValue(speakerRole, out var roleInstruction))
            {
                parts.Add(roleInstruction);
            }

            // Add emotion-based instruction
            if (!string.IsNullOrWhiteSpace(emotion) && 
                EmotionInstructions.TryGetValue(emotion.ToLowerInvariant(), out var emotionInstruction))
            {
                parts.Add(emotionInstruction);
            }

            // Add custom instructions
            if (!string.IsNullOrWhiteSpace(customInstructions))
            {
                parts.Add(customInstructions);
            }

            // If no instructions, provide a basic default
            if (parts.Count == 0)
            {
                return "You are a skilled voice actor performing dialogue for a video game. Deliver the line with appropriate emotion and emphasis to bring the character to life.";
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Builds instructions specifically for the "voice actor audition" approach.
        /// </summary>
        public static string BuildAuditionStyle(string speakerName, string speakerRole, string text, string? emotion = null)
        {
            var role = !string.IsNullOrWhiteSpace(speakerRole) ? speakerRole : speakerName;
            var emotionHint = !string.IsNullOrWhiteSpace(emotion) ? $" The emotion is {emotion}." : "";
            
            return $"You are a voice actor auditioning for the role of {role}. Your line is: \"{text}\".{emotionHint} Perform the line with conviction, emotion, and emphasis to land the part.";
        }

        /// <summary>
        /// Adds pause/timing instructions (e.g., "Add 1 second of silence at the end").
        /// </summary>
        public static string WithPause(string baseInstructions, double secondsAtStart = 0, double secondsAtEnd = 0)
        {
            var parts = new List<string> { baseInstructions };

            if (secondsAtStart > 0)
            {
                parts.Add($"Add {secondsAtStart:F1} second{(secondsAtStart != 1 ? "s" : "")} of silence at the beginning.");
            }

            if (secondsAtEnd > 0)
            {
                parts.Add($"Add {secondsAtEnd:F1} second{(secondsAtEnd != 1 ? "s" : "")} of silence at the end.");
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Gets all available OpenAI TTS voice names (9 total).
        /// Voices: alloy, coral, echo, fable, nova, onyx, sage, shimmer, verse
        /// </summary>
        public static string[] GetAvailableVoices() => OpenAiVoices.All;

        /// <summary>
        /// Gets all available role instruction keys.
        /// </summary>
        public static IEnumerable<string> GetAvailableRoles() => RoleInstructions.Keys;

        /// <summary>
        /// Gets all available emotion instruction keys.
        /// </summary>
        public static IEnumerable<string> GetAvailableEmotions() => EmotionInstructions.Keys;
    }
}
