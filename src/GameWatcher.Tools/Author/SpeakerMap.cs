using System.Text.Json;

namespace GameWatcher.Tools.Author;

internal sealed class SpeakerMap
{
    private readonly Dictionary<string, string> _map;

    public SpeakerMap(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        else
        {
            _map = new();
        }
    }

    public string Resolve(string normalized) => _map.TryGetValue(normalized, out var s) ? s : "default";
}

