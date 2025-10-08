using System.Collections.Generic;

namespace GameWatcher.Engine.Packs;

/// <summary>
/// Interface for managing speaker profiles and voice matching
/// </summary>
public interface ISpeakerCollection
{
    /// <summary>
    /// Match dialogue text to the most appropriate speaker
    /// </summary>
    SpeakerProfile MatchSpeaker(string dialogue);
    
    /// <summary>
    /// Get all available speaker profiles
    /// </summary>
    IEnumerable<SpeakerProfile> GetAllSpeakers();
    
    /// <summary>
    /// Get the default/fallback speaker
    /// </summary>
    SpeakerProfile GetDefaultSpeaker();
    
    /// <summary>
    /// Add or update a speaker profile
    /// </summary>
    void UpdateSpeaker(SpeakerProfile speaker);
}

/// <summary>
/// Speaker profile with voice configuration and matching rules
/// </summary>
public class SpeakerProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Voice { get; set; } = "";
    public double Speed { get; set; } = 1.0;
    public double Stability { get; set; } = 0.8;
    public double Clarity { get; set; } = 0.8;
    public string[] Keywords { get; set; } = System.Array.Empty<string>();
    public int Priority { get; set; } = 1;
    public bool IsDefault { get; set; }
    public string Color { get; set; } = "#4A90E2";  // For UI display
    public string? Effects { get; set; }
    
    /// <summary>
    /// Calculate match score for given dialogue text
    /// </summary>
    public double CalculateMatchScore(string dialogue)
    {
        if (string.IsNullOrEmpty(dialogue)) return 0.0;
        
        var score = 0.0;
        var lowerDialogue = dialogue.ToLowerInvariant();
        
        foreach (var keyword in Keywords)
        {
            if (lowerDialogue.Contains(keyword.ToLowerInvariant()))
            {
                score += Priority * 10.0; // Weighted by priority
            }
        }
        
        return score;
    }
}
