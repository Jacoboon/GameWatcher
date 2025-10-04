using System.Text.Json;

namespace GameWatcher.App.Mapping;

internal sealed class DialogMapping
{
    private readonly Dictionary<string, string> _map;
    private readonly string _voicesDir;

    public DialogMapping(string mapsDir, string voicesDir)
    {
        _voicesDir = voicesDir;
        Directory.CreateDirectory(mapsDir);
        Directory.CreateDirectory(voicesDir);
        var path = Path.Combine(mapsDir, "dialogue.en.json");
        if (!File.Exists(path))
        {
            _map = new();
        }
        else
        {
            var json = File.ReadAllText(path);
            _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
    }

    public bool TryResolve(string normalizedText, out string audioPath)
    {
        audioPath = string.Empty;
        if (!_map.TryGetValue(normalizedText, out var file))
            return false;
        var full = Path.Combine(_voicesDir, file);
        if (!File.Exists(full))
            return false;
        audioPath = full;
        return true;
    }
}

