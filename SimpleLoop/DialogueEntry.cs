using System;

namespace SimpleLoop
{
    public class DialogueEntry
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string Speaker { get; set; } = "Unknown";
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int SeenCount { get; set; } = 1;
        public string RawOcrText { get; set; } = "";
        public bool HasAudio { get; set; } = false;
        public string AudioPath { get; set; } = "";
        public string VoiceProfile { get; set; } = "default";
        
        // TTS Preparation fields
        public bool IsReadyForTTS { get; set; } = false;
        public string EditedText { get; set; } = ""; // For manual text corrections
        public string PronunciationNotes { get; set; } = ""; // Special pronunciation guidance
        public string TtsVoiceId { get; set; } = ""; // OpenAI voice ID (alloy, echo, fable, onyx, nova, shimmer)
        public bool IsApproved { get; set; } = false; // Manual approval before TTS generation
        public DateTime? AudioGeneratedAt { get; set; } = null;
        
        public string GetTextForTTS()
        {
            // Return edited text if available, otherwise original text
            return !string.IsNullOrWhiteSpace(EditedText) ? EditedText : Text;
        }
        
        public string GenerateId()
        {
            // Generate consistent ID based on cleaned text
            var hash = Text.GetHashCode();
            return $"dialogue_{Math.Abs(hash):X8}";
        }
    }
}