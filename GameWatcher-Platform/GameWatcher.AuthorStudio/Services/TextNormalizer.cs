using System.Text;

namespace GameWatcher.AuthorStudio.Services
{
    public static class TextNormalizer
    {
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim();

            // Replace smart quotes and ellipsis
            s = s.Replace('\u2018'.ToString(), "'")
                 .Replace('\u2019'.ToString(), "'")
                 .Replace('\u201C'.ToString(), "\"")
                 .Replace('\u201D'.ToString(), "\"")
                 .Replace('\u2026'.ToString(), "...");

            // Collapse whitespace
            s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ");

            // Lowercase for matching key
            s = s.ToLowerInvariant();

            return s;
        }
    }
}

