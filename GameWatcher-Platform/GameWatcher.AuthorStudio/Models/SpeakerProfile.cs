using System.Collections.Generic;

namespace GameWatcher.AuthorStudio.Models
{
    public class SpeakerProfile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
        public double Speed { get; set; } = 1.0;
        public double Stability { get; set; } = 0.85;
        public double Clarity { get; set; } = 0.85;
        public List<string> Keywords { get; set; } = new();
        public int Priority { get; set; } = 1;
        public bool IsDefault { get; set; }
        public string Color { get; set; } = "#FFFFFF";

        // Convenience binding property
        public string KeywordsCsv
        {
            get => string.Join(", ", Keywords);
            set => Keywords = string.IsNullOrWhiteSpace(value) 
                ? new List<string>() 
                : new List<string>(value.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

