using System.Text.Json;

namespace GameWatcher.App.Events;

internal sealed class EventEmitter
{
    private readonly string _eventsDir;
    private readonly string _logPath;
    private readonly JsonSerializerOptions _opts = new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly object _lock = new object();

    public EventEmitter(string dataRoot)
    {
        _eventsDir = Path.Combine(dataRoot, "events");
        Directory.CreateDirectory(_eventsDir);
        _logPath = Path.Combine(_eventsDir, "events.ndjson");
    }

    public void Emit(GameEvent ev)
    {
        var line = JsonSerializer.Serialize(ev, ev.GetType(), _opts);
        lock (_lock)
        {
            File.AppendAllText(_logPath, line + "\n");
        }
    }
}

