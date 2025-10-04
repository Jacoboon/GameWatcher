using System.Text.Json.Serialization;

namespace GameWatcher.App.Events;

internal abstract class GameEvent
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class DialogueEvent : GameEvent
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty; // hash of normalized text
    [JsonPropertyName("normalized")] public string Normalized { get; init; } = string.Empty;
    [JsonPropertyName("raw")] public string Raw { get; init; } = string.Empty;
    [JsonPropertyName("rect")] public int[] Rect { get; init; } = Array.Empty<int>(); // x,y,w,h
    [JsonPropertyName("audio")] public string? Audio { get; init; } // filename or null
    [JsonPropertyName("hasAudio")] public bool HasAudio => !string.IsNullOrWhiteSpace(Audio);
}

