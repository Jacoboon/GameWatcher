using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameWatcher.Runtime.Services.Dialogue;

public class DialogueCatalogService
{
    private readonly Dictionary<string, DialogueEntry> _byNormalized = new();

    private class DialogueFile
    {
        [JsonPropertyName("entries")] public List<DialogueEntryModel> Entries { get; set; } = new();
    }

    private class DialogueEntryModel
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("normalized")] public string? Normalized { get; set; }
        [JsonPropertyName("speakerId")] public string? SpeakerId { get; set; }
        [JsonPropertyName("audio")] public string? AudioPath { get; set; }
    }

    public void Clear()
    {
        _byNormalized.Clear();
    }

    public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories)
    {
        _byNormalized.Clear();
        foreach (var dir in directories)
        {
            try
            {
                var path = Path.Combine(dir, "Catalog", "dialogue.json");
                if (!File.Exists(path)) continue;
                var json = await File.ReadAllTextAsync(path);
                var file = JsonSerializer.Deserialize<DialogueFile>(json);
                if (file == null) continue;
                foreach (var e in file.Entries)
                {
                    var key = (e.Normalized ?? e.Text).Trim().ToLowerInvariant();
                    _byNormalized[key] = new DialogueEntry
                    {
                        Id = e.Id,
                        Text = e.Text,
                        Speaker = e.SpeakerId ?? "",
                        RawOcrText = e.Text,
                        VoiceProfile = e.SpeakerId ?? "",
                        IsApproved = true,
                        HasAudio = !string.IsNullOrWhiteSpace(e.AudioPath),
                        AudioPath = e.AudioPath ?? string.Empty
                    };
                }
            }
            catch
            {
                // ignore bad files for now
            }
        }
    }

    public bool TryLookup(string normalized, out DialogueEntry entry)
    {
        return _byNormalized.TryGetValue(normalized, out entry!);
    }
}

