using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameWatcher.Runtime.Services.Dialogue
{
    public class DialogueEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        
        private string _speaker = "Unknown";
        public string Speaker 
        { 
            get => _speaker; 
            set 
            { 
                if (_speaker != value) 
                { 
                    _speaker = value; 
                    OnPropertyChanged(); 
                } 
            } 
        }
        
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int SeenCount { get; set; } = 1;
        public string RawOcrText { get; set; } = "";
        
        private bool _hasAudio = false;
        public bool HasAudio 
        { 
            get => _hasAudio; 
            set 
            { 
                if (_hasAudio != value) 
                { 
                    _hasAudio = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(AudioStatus));
                    OnPropertyChanged(nameof(AudioStatusColor));
                } 
            } 
        }
        
        private string _audioPath = "";
        public string AudioPath 
        { 
            get => _audioPath; 
            set 
            { 
                if (_audioPath != value) 
                { 
                    _audioPath = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(AudioStatus));
                    OnPropertyChanged(nameof(AudioStatusColor));
                } 
            } 
        }
        
        private string _voiceProfile = "default";
        public string VoiceProfile 
        { 
            get => _voiceProfile; 
            set 
            { 
                if (_voiceProfile != value) 
                { 
                    _voiceProfile = value; 
                    OnPropertyChanged(); 
                } 
            } 
        }
        
        // TTS Preparation fields
        public bool IsReadyForTTS { get; set; } = false;
        public string EditedText { get; set; } = ""; // For manual text corrections
        public string PronunciationNotes { get; set; } = ""; // Special pronunciation guidance
        
        private string _ttsVoiceId = "";
        public string TtsVoiceId 
        { 
            get => _ttsVoiceId; 
            set 
            { 
                if (_ttsVoiceId != value) 
                { 
                    _ttsVoiceId = value; 
                    OnPropertyChanged(); 
                } 
            } 
        }
        
        public bool IsApproved { get; set; } = false; // Manual approval before TTS generation
        public DateTime? AudioGeneratedAt { get; set; } = null;
        
        // Audio status properties for UI
        public string AudioStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(AudioPath) && System.IO.File.Exists(AudioPath))
                {
                    var fileInfo = new System.IO.FileInfo(AudioPath);
                    return $"✓ {fileInfo.Length / 1024:N0}KB";
                }
                else if (HasAudio)
                {
                    return "❌ Missing";
                }
                else
                {
                    return "⏸ Not Generated";
                }
            }
        }
        
        public string AudioStatusColor
        {
            get
            {
                if (!string.IsNullOrEmpty(AudioPath) && System.IO.File.Exists(AudioPath))
                {
                    return "Green";
                }
                else if (HasAudio)
                {
                    return "Red";
                }
                else
                {
                    return "Gray";
                }
            }
        }
        
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