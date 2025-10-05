using System.Text.Json;

namespace GameWatcher.App.Mapping;

internal sealed class DialogMapping
{
    private readonly Dictionary<string, string> _map;
    private readonly string _voicesDir;
    private readonly string _mapPath;
    private DateTime _lastWriteUtc;

    public DialogMapping(string mapsDir, string voicesDir)
    {
        _voicesDir = voicesDir;
        Directory.CreateDirectory(mapsDir);
        Directory.CreateDirectory(voicesDir);
        _mapPath = Path.Combine(mapsDir, "dialogue.en.json");
        (_map, _lastWriteUtc) = LoadMap(_mapPath);
    }

    public void RefreshIfChanged()
    {
        try
        {
            var ts = File.Exists(_mapPath) ? File.GetLastWriteTimeUtc(_mapPath) : DateTime.MinValue;
            if (ts > _lastWriteUtc)
            {
                var (map, last) = LoadMap(_mapPath);
                _map.Clear();
                foreach (var kv in map) _map[kv.Key] = kv.Value;
                _lastWriteUtc = last;
            }
        }
        catch { /* ignore */ }
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

    private static (Dictionary<string, string>, DateTime) LoadMap(string path)
    {
        if (!File.Exists(path)) return (new Dictionary<string, string>(), DateTime.MinValue);
        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            return (dict, File.GetLastWriteTimeUtc(path));
        }
        catch { return (new Dictionary<string, string>(), DateTime.MinValue); }
    }
}
