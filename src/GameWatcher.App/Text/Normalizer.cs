using System.Text;
using GameWatcher.Core;

namespace GameWatcher.App.Text;

internal sealed class SimpleNormalizer : INormalizer
{
    public string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var s = text.Trim();
        s = s.Replace('\u2018', '\'').Replace('\u2019', '\'')
             .Replace('\u201C', '"').Replace('\u201D', '"');
        s = s.Replace("…", "...");
        s = s.Replace("—", "-");
        s = s.ToLowerInvariant();
        // collapse whitespace lines
        var lines = s.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries)
                     .Select(l => string.Join(' ', l.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        return string.Join("\n", lines).Trim();
    }
}

