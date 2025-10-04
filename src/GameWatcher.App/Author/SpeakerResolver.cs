using System.Text.Json;

namespace GameWatcher.App.Author;

internal sealed class SpeakerResolver
{
    private readonly Dictionary<string, string> _map;
    public SpeakerResolver(string speakersPath)
    {
        if (File.Exists(speakersPath))
        {
            var json = File.ReadAllText(speakersPath);
            _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        else
        {
            _map = new();
        }
    }

    public string Resolve(string normalized) => _map.TryGetValue(normalized, out var s) ? s : "default";
}

