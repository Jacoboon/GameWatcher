using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GameWatcher.AuthorStudio.Models;

namespace GameWatcher.AuthorStudio.Services
{
    public class PackExporter
    {
        private static string Normalize(string s) => TextNormalizer.Normalize(s);

        private class DialogueExport
        {
            [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
            [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
            [JsonPropertyName("normalized")] public string Normalized { get; set; } = string.Empty;
            [JsonPropertyName("speakerId")] public string? SpeakerId { get; set; }
            [JsonPropertyName("audio")] public string? AudioPath { get; set; }
        }

        private class DialogueFile
        {
            [JsonPropertyName("entries")] public List<DialogueExport> Entries { get; set; } = new();
        }

        private class PackManifest
        {
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
            [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
            [JsonPropertyName("description")] public string Description { get; set; } = "Voiceover pack";
            [JsonPropertyName("engineVersion")] public string EngineVersion { get; set; } = "2.0.0";
        }

        public async Task ExportAsync(string outputDir, string packName, string displayName, string version,
            IEnumerable<PendingDialogueEntry> dialogues, SpeakerStore speakerStore)
        {
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "Configuration"));
            Directory.CreateDirectory(Path.Combine(outputDir, "Catalog"));

            // Write manifest
            var manifest = new PackManifest
            {
                Name = packName,
                DisplayName = displayName,
                Version = version
            };
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(outputDir, "Configuration", "pack.json"), manifestJson);

            // Write speakers.json from store
            await speakerStore.ExportAsync(Path.Combine(outputDir, "Configuration", "speakers.json"));

            // Write dialogue catalog
            var file = new DialogueFile();
            foreach (var d in dialogues)
            {
                var text = d.EditedText ?? d.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!d.Approved) continue;
                var id = $"dialogue_{Math.Abs(text.GetHashCode()):X8}";
                file.Entries.Add(new DialogueExport
                {
                    Id = id,
                    Text = text,
                    Normalized = Normalize(text),
                    SpeakerId = d.SpeakerId,
                    AudioPath = null
                });
            }
            var dialogueJson = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(outputDir, "Catalog", "dialogue.json"), dialogueJson);
        }
    }
}
