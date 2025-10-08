using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Runtime.Services.OCR;

public class OcrPostprocessor
{
    private readonly Dictionary<string, string> _fixes = new();
    private readonly ILogger<OcrPostprocessor> _logger;

    private class FixItem
    {
        [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
    }

    private class FixFile
    {
        [JsonPropertyName("fixes")] public List<FixItem> Fixes { get; set; } = new();
    }

    public OcrPostprocessor(ILogger<OcrPostprocessor> logger)
    {
        _logger = logger;
    }

    public void Clear()
    {
        _fixes.Clear();
    }

    public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories)
    {
        _fixes.Clear();
        foreach (var dir in directories)
        {
            try
            {
                var path = Path.Combine(dir, "Configuration", "ocr_fixes.json");
                if (!File.Exists(path)) continue;
                var json = await File.ReadAllTextAsync(path);
                var file = JsonSerializer.Deserialize<FixFile>(json);
                if (file == null) continue;
                foreach (var f in file.Fixes)
                {
                    var from = f.From.Trim().ToLowerInvariant();
                    var to = f.To.Trim();
                    if (from.Length > 0)
                    {
                        _fixes[from] = to;
                    }
                }
            }
            catch
            {
                // ignore file errors for now
            }
        }
    }

    public string Apply(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        // Token-level replacement
        var tokens = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var replaced = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            var key = tokens[i].Trim().ToLowerInvariant();
            if (_fixes.TryGetValue(key, out var to))
            {
                if (!string.Equals(tokens[i], to, StringComparison.Ordinal)) replaced++;
                tokens[i] = to;
            }
        }
        var joined = string.Join(" ", tokens);
        var normalized = Normalize(joined);
        if (replaced > 0)
        {
            _logger.LogDebug("OCR postprocess applied {Count} token fixes to text: {Preview}", replaced, joined.Length > 60 ? joined[..60] + "..." : joined);
        }
        return normalized;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        s = s.Replace("\u201C", "\"")
             .Replace("\u201D", "\"")
             .Replace("\u2018", "'")
             .Replace("\u2019", "'")
             .Replace("…", "...")
             .Replace("\r\n", " ")
             .Replace("\n", " ")
             .Replace("\r", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ");
        return s.ToLowerInvariant().Trim();
    }
}
