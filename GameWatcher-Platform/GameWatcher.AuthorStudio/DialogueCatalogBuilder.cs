using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameWatcher.AuthorStudio
{
    public class DialogueEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; } = string.Empty;
        public string NormalizedText => Text?.Trim() ?? string.Empty;
        public string? Speaker { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DialogueCatalogBuilder
    {
        private readonly List<DialogueEntry> _entries = new();

        public IReadOnlyList<DialogueEntry> Entries => _entries.AsReadOnly();

        public Task AddOcrResultAsync(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return Task.CompletedTask;

            var entry = new DialogueEntry { Text = rawText };
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<DialogueEntry>> GetAllAsync()
        {
            return Task.FromResult<IEnumerable<DialogueEntry>>(_entries);
        }

        public Task<int> CountAsync()
        {
            return Task.FromResult(_entries.Count);
        }

        public Task ClearAsync()
        {
            _entries.Clear();
            return Task.CompletedTask;
        }
    }
}