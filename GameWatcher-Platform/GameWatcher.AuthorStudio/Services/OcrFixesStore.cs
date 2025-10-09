using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace GameWatcher.AuthorStudio.Services
{
    public class OcrFixesStore
    {
        private readonly Dictionary<string, string> _fixes = new();
        private readonly ILogger<OcrFixesStore> _logger;
        private string? _currentPackFolder;

        public OcrFixesStore(ILogger<OcrFixesStore> logger)
        {
            _logger = logger;
        }

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
            _currentPackFolder = packFolder;
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
                _logger.LogInformation("Loaded {Count} OCR fixes from {Path}", _fixes.Count, path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load OCR fixes from {Folder}", packFolder);
            }
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

        /// <summary>
        /// Adds a new OCR fix rule and saves it to the pack's ocr_fixes.json.
        /// </summary>
        public async Task AddFixAsync(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                _logger.LogWarning("Attempted to add invalid OCR fix: '{From}' -> '{To}'", from, to);
                return;
            }

            var key = from.Trim().ToLowerInvariant();
            var value = to.Trim();

            // Don't add if it's already correct or a duplicate
            if (_fixes.TryGetValue(key, out var existing) && existing == value)
            {
                return;
            }

            _fixes[key] = value;
            _logger.LogInformation("Added OCR fix: '{From}' -> '{To}'", from, to);

            await SaveAsync();
        }

        /// <summary>
        /// Saves all OCR fixes to the pack's ocr_fixes.json.
        /// </summary>
        public async Task SaveAsync()
        {
            if (string.IsNullOrEmpty(_currentPackFolder))
            {
                _logger.LogWarning("Cannot save OCR fixes: no pack folder set");
                return;
            }

            try
            {
                var configDir = Path.Combine(_currentPackFolder, "Configuration");
                Directory.CreateDirectory(configDir);

                var path = Path.Combine(configDir, "ocr_fixes.json");
                var file = new FixFile
                {
                    Fixes = _fixes.Select(kvp => new FixItem
                    {
                        From = kvp.Key,
                        To = kvp.Value
                    }).OrderBy(f => f.From).ToList()
                };

                var json = JsonSerializer.Serialize(file, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(path, json);
                _logger.LogInformation("Saved {Count} OCR fixes to {Path}", _fixes.Count, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save OCR fixes to {Folder}", _currentPackFolder);
            }
        }
    }
}
