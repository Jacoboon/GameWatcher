using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleLoop
{
    /// <summary>
    /// Thread-safe file-based logger that prevents GUI thread contention
    /// </summary>
    public class CaptureLogger : IDisposable
    {
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly System.Threading.Timer _flushTimer;
        private readonly string _logFilePath;
        private readonly object _fileLock = new();
        private bool _disposed = false;

        public CaptureLogger(string logDirectory = "logs")
        {
            _logQueue = new ConcurrentQueue<LogEntry>();
            
            // Create logs directory if it doesn't exist
            Directory.CreateDirectory(logDirectory);
            
            // Create session log file
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logDirectory, $"capture_session_{timestamp}.log");
            
            // Flush to file every 500ms to prevent UI blocking
            _flushTimer = new System.Threading.Timer(FlushLogEntries, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
            
            LogMessage($"Capture session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            if (_disposed) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            _logQueue.Enqueue(entry);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message}: {exception.Message}" : message;
            LogMessage(fullMessage, LogLevel.Error);
        }

        public void LogPerformance(string operation, long milliseconds)
        {
            var message = milliseconds > 100 
                ? $"SLOW: {operation} took {milliseconds}ms" 
                : $"{operation}: {milliseconds}ms";
            
            LogMessage(message, milliseconds > 100 ? LogLevel.Warning : LogLevel.Debug);
        }

        public void LogDialogue(string dialogue, string speaker, string voice)
        {
            LogMessage($"DIALOGUE: \"{dialogue}\" → {speaker} ({voice})", LogLevel.Success);
        }

        private void FlushLogEntries(object? state)
        {
            if (_disposed || _logQueue.IsEmpty) return;

            try
            {
                var entries = new List<LogEntry>();
                
                // Drain the queue
                while (_logQueue.TryDequeue(out var entry) && entries.Count < 100) // Limit batch size
                {
                    entries.Add(entry);
                }

                if (entries.Count == 0) return;

                // Write to file in background thread to avoid blocking
                Task.Run(() => WriteLogEntries(entries));
            }
            catch (Exception ex)
            {
                // Avoid recursive logging - just write to console
                Console.WriteLine($"Error flushing log entries: {ex.Message}");
            }
        }

        private void WriteLogEntries(List<LogEntry> entries)
        {
            try
            {
                lock (_fileLock)
                {
                    using var writer = new StreamWriter(_logFilePath, append: true);
                    
                    foreach (var entry in entries)
                    {
                        var levelIcon = entry.Level switch
                        {
                            LogLevel.Error => "❌",
                            LogLevel.Warning => "⚠️",
                            LogLevel.Success => "✅",
                            LogLevel.Debug => "🔍",
                            _ => "ℹ️"
                        };
                        
                        writer.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] {levelIcon} {entry.Message}");
                    }
                    
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing log file: {ex.Message}");
            }
        }

        public string[] ReadRecentLogLines(int maxLines = 500)
        {
            try
            {
                lock (_fileLock)
                {
                    if (!File.Exists(_logFilePath)) return Array.Empty<string>();
                    
                    var allLines = File.ReadAllLines(_logFilePath);
                    
                    // Return the last N lines
                    if (allLines.Length <= maxLines)
                        return allLines;
                    
                    var recentLines = new string[maxLines];
                    Array.Copy(allLines, allLines.Length - maxLines, recentLines, 0, maxLines);
                    return recentLines;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading log file: {ex.Message}");
                return new[] { $"Error reading log: {ex.Message}" };
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            // Final flush
            FlushLogEntries(null);
            
            // Wait a bit for final write
            Thread.Sleep(100);
            
            _flushTimer?.Dispose();
            
            LogMessage("Capture session ended");
            FlushLogEntries(null); // Final final flush
        }

        public string LogFilePath => _logFilePath;
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = "";
    }

    public enum LogLevel
    {
        Debug,
        Info, 
        Success,
        Warning,
        Error
    }
}