using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GameWatcher.AuthorStudio.Models;

namespace GameWatcher.AuthorStudio.Services
{
    public class SpeakerStore
    {
        public ObservableCollection<SpeakerProfile> Speakers { get; } = new();

        private class SpeakersFile
        {
            [JsonPropertyName("speakers")] public SpeakerProfile[] Speakers { get; set; } = Array.Empty<SpeakerProfile>();
            [JsonPropertyName("voiceMatching")] public JsonElement? VoiceMatching { get; set; } // preserved if present
        }

        public async Task ImportAsync(string path)
        {
            var json = await File.ReadAllTextAsync(path);
            var model = JsonSerializer.Deserialize<SpeakersFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (model == null) return;

            Speakers.Clear();
            foreach (var s in model.Speakers)
            {
                Speakers.Add(s);
            }
        }

        public async Task ExportAsync(string path)
        {
            var file = new SpeakersFile
            {
                Speakers = Speakers.ToArray(),
                VoiceMatching = null
            };

            var json = JsonSerializer.Serialize(file, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            await File.WriteAllTextAsync(path, json);
        }
    }
}

