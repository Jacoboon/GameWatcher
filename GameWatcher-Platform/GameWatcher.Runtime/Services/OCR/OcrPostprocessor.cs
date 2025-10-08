using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameWatcher.Runtime.Services.OCR;

public class OcrPostprocessor
{
    private readonly Dictionary<string, string> _fixes = new();

    private class FixItem
    {
        [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
    }

    private class FixFile
    {
        [JsonPropertyName("fixes")] public List<FixItem> Fixes { get; set; } = new();
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
        for (int i = 0; i < tokens.Length; i++)
        {
            var key = tokens[i].Trim().ToLowerInvariant();
            if (_fixes.TryGetValue(key, out var to))
            {
                tokens[i] = to;
            }
        }
        return string.Join(" ", tokens);
    }
}
