using System.Text.Json;

namespace GameWatcher.App.Author;

internal sealed class SpeakerResolver
{
    private readonly Dictionary<string, string> _map;
    private readonly string _path;
    private DateTime _lastWriteUtc;

    public SpeakerResolver(string speakersPath)
    {
        _path = speakersPath;
        (_map, _lastWriteUtc) = Load(_path);
    }

    public void RefreshIfChanged()
    {
        try
        {
            var ts = File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue;
            if (ts > _lastWriteUtc)
            {
                var (map, last) = Load(_path);
                _map.Clear();
                foreach (var kv in map) _map[kv.Key] = kv.Value;
                _lastWriteUtc = last;
            }
        }
        catch { /* ignore */ }
    }

    public string Resolve(string normalized) => _map.TryGetValue(normalized, out var s) ? s : "default";

    private static (Dictionary<string, string>, DateTime) Load(string path)
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
