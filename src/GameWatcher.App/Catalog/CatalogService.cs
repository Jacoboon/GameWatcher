using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Drawing;

namespace GameWatcher.App.Catalog;

internal sealed class CatalogService
{
    private readonly string _root;
    private readonly string _indexPath;
    private readonly Dictionary<string, CatalogEntry> _index;
    private readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CatalogService(string root)
    {
        _root = root;
        Directory.CreateDirectory(Path.Combine(_root, "crops"));
        Directory.CreateDirectory(Path.Combine(_root, "pre"));
        Directory.CreateDirectory(Path.Combine(_root, "text"));
        _indexPath = Path.Combine(_root, "index.json");
        _index = LoadIndex(_indexPath);
    }

    public string ComputeId(string normalizedText)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(normalizedText);
        var hash = sha1.ComputeHash(bytes);
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    public void Record(string normalizedText, string rawText, Rectangle rect, string? audioFile, Bitmap crop, Bitmap preprocessed)
    {
        var id = ComputeId(normalizedText);
        var now = DateTimeOffset.UtcNow;

        var cropPath = Path.Combine(_root, "crops", id + ".png");
        var prePath = Path.Combine(_root, "pre", id + ".png");
        var txtPath = Path.Combine(_root, "text", id + ".txt");

        if (!File.Exists(cropPath)) crop.Save(cropPath);
        if (!File.Exists(prePath)) preprocessed.Save(prePath);
        if (!File.Exists(txtPath)) File.WriteAllText(txtPath, rawText);

        _index[id] = new CatalogEntry
        {
            Id = id,
            Normalized = normalizedText,
            Raw = rawText,
            Rect = new[] { rect.X, rect.Y, rect.Width, rect.Height },
            Audio = audioFile,
            FirstSeen = _index.TryGetValue(id, out var prev) ? prev.FirstSeen : now,
            LastSeen = now,
            SeenCount = _index.TryGetValue(id, out prev) ? prev.SeenCount + 1 : 1
        };

        SaveIndex();
        SaveMissesReport();
    }

    private Dictionary<string, CatalogEntry> LoadIndex(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, CatalogEntry>>(json, _jsonOpts);
                return dict ?? new();
            }
        }
        catch { }
        return new();
    }

    private void SaveIndex()
    {
        var json = JsonSerializer.Serialize(_index, _jsonOpts);
        File.WriteAllText(_indexPath, json);
    }

    private void SaveMissesReport()
    {
        var misses = _index.Values
            .Where(e => string.IsNullOrWhiteSpace(e.Audio))
            .OrderBy(e => e.Normalized)
            .ToList();
        var reportPath = Path.Combine(_root, "misses.json");
        var json = JsonSerializer.Serialize(misses, _jsonOpts);
        File.WriteAllText(reportPath, json);
    }
}

internal sealed class CatalogEntry
{
    public string Id { get; set; } = string.Empty;
    public string Normalized { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
    public int[] Rect { get; set; } = Array.Empty<int>();
    public string? Audio { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public int SeenCount { get; set; }
}

