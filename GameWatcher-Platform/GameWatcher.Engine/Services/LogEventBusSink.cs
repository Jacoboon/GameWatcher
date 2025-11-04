using System;
using Serilog.Core;
using Serilog.Events;
using GameWatcher.Engine.Services;

namespace GameWatcher.Engine.Services;

/// <summary>
/// Custom Serilog sink that forwards log events to LogEventBus for UI consumption.
/// </summary>
public class LogEventBusSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;

    public LogEventBusSink(IFormatProvider? formatProvider = null)
    {
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp.DateTime,
            Level = MapLogLevel(logEvent.Level),
            Message = logEvent.RenderMessage(_formatProvider),
            Source = ExtractSourceContext(logEvent),
            Category = ExtractCategory(logEvent)
        };

        LogEventBus.Instance.Publish(entry);
    }

    private static LogLevel MapLogLevel(LogEventLevel serilogLevel)
    {
        return serilogLevel switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    private static string? ExtractSourceContext(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            return sourceContext.ToString().Trim('"');
        }
        return null;
    }

    private static string? ExtractCategory(LogEvent logEvent)
    {
        // Try to infer category from source context
        var source = ExtractSourceContext(logEvent);
        if (source == null) return null;

        if (source.Contains("Capture")) return "CAPTURE";
        if (source.Contains("OCR")) return "OCR";
        if (source.Contains("Detection") || source.Contains("Textbox")) return "DETECTION";
        if (source.Contains("Audio") || source.Contains("TTS")) return "AUDIO";
        if (source.Contains("Pack")) return "PACK";
        if (source.Contains("Settings")) return "SETTINGS";

        return "SYSTEM";
    }
}

/// <summary>
/// Extension methods for easily adding LogEventBusSink to Serilog configuration.
/// </summary>
public static class LogEventBusSinkExtensions
{
    public static Serilog.LoggerConfiguration LogEventBus(
        this Serilog.Configuration.LoggerSinkConfiguration sinkConfiguration,
        IFormatProvider? formatProvider = null)
    {
        return sinkConfiguration.Sink(new LogEventBusSink(formatProvider));
    }
}
