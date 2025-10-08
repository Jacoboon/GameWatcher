using System;
using System.Collections.Generic;
using System.Linq;

namespace GameWatcher.Engine.Packs;

/// <summary>
/// Default implementation of speaker collection with fuzzy matching
/// </summary>
public class SpeakerCollection : ISpeakerCollection
{
    private readonly Dictionary<string, SpeakerProfile> _speakers = new();
    private SpeakerProfile? _defaultSpeaker;
    
    public void AddSpeaker(SpeakerProfile speaker)
    {
        _speakers[speaker.Id] = speaker;
        
        if (speaker.IsDefault)
        {
            _defaultSpeaker = speaker;
        }
    }
    
    public SpeakerProfile MatchSpeaker(string dialogue)
    {
        if (string.IsNullOrWhiteSpace(dialogue))
        {
            return GetDefaultSpeaker();
        }
        
        // Calculate match scores for all speakers
        var matches = _speakers.Values
            .Select(speaker => new
            {
                Speaker = speaker,
                Score = speaker.CalculateMatchScore(dialogue)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Speaker.Priority)
            .ToList();
        
        // Return the best match, or default if no matches
        return matches.FirstOrDefault()?.Speaker ?? GetDefaultSpeaker();
    }
    
    public IEnumerable<SpeakerProfile> GetAllSpeakers()
    {
        return _speakers.Values.OrderByDescending(s => s.Priority).ThenBy(s => s.Name);
    }
    
    public SpeakerProfile GetDefaultSpeaker()
    {
        if (_defaultSpeaker != null)
        {
            return _defaultSpeaker;
        }
        
        // If no explicit default, use the highest priority speaker
        var fallback = _speakers.Values.OrderByDescending(s => s.Priority).FirstOrDefault();
        
        // If no speakers at all, create a basic default
        return fallback ?? new SpeakerProfile
        {
            Id = "default",
            Name = "Unknown Speaker",
            Voice = "fable",
            Speed = 1.0,
            IsDefault = true,
            Priority = 1
        };
    }
    
    public void UpdateSpeaker(SpeakerProfile speaker)
    {
        _speakers[speaker.Id] = speaker;
        
        if (speaker.IsDefault)
        {
            _defaultSpeaker = speaker;
        }
    }
    
    /// <summary>
    /// Load speakers from configuration data
    /// </summary>
    public void LoadFromConfiguration(SpeakerConfigurationData config)
    {
        _speakers.Clear();
        _defaultSpeaker = null;
        
        foreach (var speakerData in config.Speakers)
        {
            var speaker = new SpeakerProfile
            {
                Id = speakerData.Id,
                Name = speakerData.Name,
                Voice = speakerData.Voice,
                Speed = speakerData.Speed,
                Stability = speakerData.Stability,
                Clarity = speakerData.Clarity,
                Keywords = speakerData.Keywords ?? Array.Empty<string>(),
                Priority = speakerData.Priority,
                IsDefault = speakerData.IsDefault,
                Color = speakerData.Color ?? "#4A90E2",
                Effects = speakerData.Effects
            };
            
            AddSpeaker(speaker);
        }
        
        // Ensure we have a default speaker
        if (_defaultSpeaker == null && _speakers.Any())
        {
            var firstSpeaker = _speakers.Values.OrderByDescending(s => s.Priority).First();
            firstSpeaker.IsDefault = true;
            _defaultSpeaker = firstSpeaker;
        }
    }
}

/// <summary>
/// Configuration data structure for loading speaker profiles from JSON
/// </summary>
public class SpeakerConfigurationData
{
    public SpeakerData[] Speakers { get; set; } = Array.Empty<SpeakerData>();
    public VoiceMatchingConfig VoiceMatching { get; set; } = new();
}

public class SpeakerData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Voice { get; set; } = "";
    public double Speed { get; set; } = 1.0;
    public double Stability { get; set; } = 0.8;
    public double Clarity { get; set; } = 0.8;
    public string[]? Keywords { get; set; }
    public int Priority { get; set; } = 1;
    public bool IsDefault { get; set; }
    public string? Color { get; set; }
    public string? Effects { get; set; }
}

public class VoiceMatchingConfig
{
    public string Algorithm { get; set; } = "KeywordScoring";
    public bool FuzzyMatching { get; set; } = true;
    public bool FallbackToDefault { get; set; } = true;
    public bool CacheResults { get; set; } = true;
}
