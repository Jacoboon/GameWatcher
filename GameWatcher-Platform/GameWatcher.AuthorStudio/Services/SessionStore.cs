using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GameWatcher.AuthorStudio.Services
{
    public class SessionStore
    {
        private class SessionModel
        {
            [JsonPropertyName("game")] public string? Game { get; set; }
            [JsonPropertyName("notes")] public string? Notes { get; set; }
            [JsonPropertyName("dialogues")] public List<PendingDialogueEntry> Dialogues { get; set; } = new();
        }

        public async Task SaveAsync(string path, IEnumerable<PendingDialogueEntry> entries, string? game = null, string? notes = null)
        {
            var model = new SessionModel
            {
                Game = game,
                Notes = notes,
                Dialogues = new List<PendingDialogueEntry>(entries)
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            await File.WriteAllTextAsync(path, json);
        }

        public async Task<IReadOnlyList<PendingDialogueEntry>> LoadAsync(string path)
        {
            var json = await File.ReadAllTextAsync(path);
            var model = JsonSerializer.Deserialize<SessionModel>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return model?.Dialogues ?? new List<PendingDialogueEntry>();
        }
    }
}

