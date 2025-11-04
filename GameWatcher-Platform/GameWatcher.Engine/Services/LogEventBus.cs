using System;
using System.Collections.Concurrent;

namespace GameWatcher.Engine.Services;

/// <summary>
/// Centralized event bus for forwarding log entries to UI components.
/// Allows Activity Logs in Studio and AuthorStudio to display real-time logs
/// from Engine services without coupling UI to logging infrastructure.
/// </summary>
public class LogEventBus
{
    private static readonly Lazy<LogEventBus> _instance = new(() => new LogEventBus());
    public static LogEventBus Instance => _instance.Value;

    private readonly ConcurrentBag<Action<LogEntry>> _subscribers = new();

    private LogEventBus() { }

    /// <summary>
    /// Subscribe to log events. Returns IDisposable for cleanup.
    /// </summary>
    public IDisposable Subscribe(Action<LogEntry> handler)
    {
        _subscribers.Add(handler);
        return new Subscription(() => { /* ConcurrentBag doesn't support removal */ });
    }

    /// <summary>
    /// Publish a log entry to all subscribers.
    /// </summary>
    public void Publish(LogEntry entry)
    {
        foreach (var subscriber in _subscribers)
        {
            try
            {
                subscriber(entry);
            }
            catch
            {
                // Swallow exceptions from subscribers to prevent cascading failures
            }
        }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _dispose;
        public Subscription(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}

/// <summary>
/// Log entry for UI display.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Category { get; set; }
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
