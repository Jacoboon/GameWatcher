using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace GameWatcher.AuthorStudio.Services
{
    public class OcrFixesStore
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

        public void Clear() => _fixes.Clear();

        public async Task LoadFromFolderAsync(string packFolder)
        {
            _fixes.Clear();
            try
            {
                var path = Path.Combine(packFolder, "Configuration", "ocr_fixes.json");
                if (!File.Exists(path)) return;
                var json = await File.ReadAllTextAsync(path);
                var file = JsonSerializer.Deserialize<FixFile>(json);
                if (file == null) return;
                foreach (var f in file.Fixes)
                {
                    var from = (f.From ?? "").Trim().ToLowerInvariant();
                    if (from.Length == 0) continue;
                    _fixes[from] = (f.To ?? "").Trim();
                }
            }
            catch { }
        }

        public string Apply(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var tokens = input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
}
