using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GameWatcher.AuthorStudio.Services;

namespace GameWatcher.AuthorStudio.Services
{
    public class PackLoader
    {
        private class Manifest
        {
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
            [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
        }

        private class DialogueEntryModel
        {
            [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
            [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
            [JsonPropertyName("normalized")] public string? Normalized { get; set; }
            [JsonPropertyName("speakerId")] public string? SpeakerId { get; set; }
            [JsonPropertyName("audio")] public string? AudioPath { get; set; }
        }

        private class DialogueFile
        {
            [JsonPropertyName("entries")] public List<DialogueEntryModel> Entries { get; set; } = new();
        }

        public async Task<(string name, string display, string version, List<PendingDialogueEntry> entries)> LoadAsync(string packFolder)
        {
            var configDir = Path.Combine(packFolder, "Configuration");
            var catalogDir = Path.Combine(packFolder, "Catalog");

            var manifestPath = Path.Combine(configDir, "pack.json");
            var speakersPath = Path.Combine(configDir, "speakers.json");
            var dialoguePath = Path.Combine(catalogDir, "dialogue.json");

            var manifest = new Manifest();
            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                manifest = JsonSerializer.Deserialize<Manifest>(json) ?? new Manifest();
            }

            var entries = new List<PendingDialogueEntry>();
            if (File.Exists(dialoguePath))
            {
                var json = await File.ReadAllTextAsync(dialoguePath);
                var file = JsonSerializer.Deserialize<DialogueFile>(json);
                if (file != null)
                {
                    foreach (var e in file.Entries)
                    {
                        entries.Add(new PendingDialogueEntry
                        {
                            Text = e.Text,
                            OriginalOcrText = e.Text, // Loaded text is already correct
                            SpeakerId = e.SpeakerId,
                            Approved = true,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            return (manifest.Name, manifest.DisplayName, manifest.Version, entries);
        }
    }
}

