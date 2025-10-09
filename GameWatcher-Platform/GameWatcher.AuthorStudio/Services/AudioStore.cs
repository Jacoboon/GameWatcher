using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameWatcher.AuthorStudio.Services
{
    /// <summary>
    /// Manages the audio file lifecycle and maintains a manifest mapping dialogue IDs to audio paths.
    /// Ensures audio files are consolidated in the pack's Audio folder and prevents orphaned files.
    /// </summary>
    public class AudioStore
    {
        private readonly Dictionary<string, AudioManifestEntry> _audioMap = new();
        private string? _packFolder;

        /// <summary>
        /// Sets the current pack folder and loads its audio manifest.
        /// </summary>
        public async Task SetPackFolderAsync(string packFolder)
        {
            _packFolder = packFolder;
            _audioMap.Clear();
            await LoadManifestAsync();
        }

        /// <summary>
        /// Registers audio for a dialogue entry. Copies/moves file to pack's Audio folder.
        /// </summary>
        /// <param name="dialogueText">The dialogue text (used to generate stable ID)</param>
        /// <param name="sourcePath">Path to the source audio file</param>
        /// <param name="isGenerated">True if TTS-generated (will move), false if user-imported (will copy)</param>
        /// <param name="voiceName">Voice name if TTS-generated</param>
        /// <returns>Relative path to the audio file within the pack</returns>
        public async Task<string> SetAudioAsync(string dialogueText, string sourcePath, bool isGenerated, string? voiceName = null)
        {
            if (string.IsNullOrEmpty(_packFolder))
                throw new InvalidOperationException("Pack folder not set. Call SetPackFolderAsync first.");

            var dialogueId = GenerateId(dialogueText);
            var audioFolder = Path.Combine(_packFolder, "Audio");
            Directory.CreateDirectory(audioFolder);

            var ext = Path.GetExtension(sourcePath);
            var destFile = Path.Combine(audioFolder, $"{dialogueId}{ext}");

            if (isGenerated)
            {
                // Move TTS-generated files (they're temporary)
                if (File.Exists(destFile))
                    File.Delete(destFile);
                File.Move(sourcePath, destFile);
            }
            else
            {
                // Copy user-imported files (preserve original)
                File.Copy(sourcePath, destFile, overwrite: true);
            }

            var relativePath = Path.Combine("Audio", Path.GetFileName(destFile)).Replace('\\', '/');
            
            _audioMap[dialogueId] = new AudioManifestEntry
            {
                Path = relativePath,
                VoiceName = voiceName,
                IsGenerated = isGenerated,
                UpdatedAt = DateTime.UtcNow
            };

            await SaveManifestAsync();

            return relativePath;
        }

        /// <summary>
        /// Gets audio path for a dialogue entry (if it exists).
        /// </summary>
        public string? GetAudio(string dialogueText)
        {
            var id = GenerateId(dialogueText);
            return _audioMap.TryGetValue(id, out var entry) ? entry.Path : null;
        }

        /// <summary>
        /// Gets audio manifest entry with metadata (voice, generation status, etc).
        /// </summary>
        public AudioManifestEntry? GetAudioEntry(string dialogueText)
        {
            var id = GenerateId(dialogueText);
            return _audioMap.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>
        /// Removes audio for a dialogue entry.
        /// </summary>
        public async Task RemoveAudioAsync(string dialogueText)
        {
            if (string.IsNullOrEmpty(_packFolder))
                return;

            var id = GenerateId(dialogueText);
            if (_audioMap.TryGetValue(id, out var entry))
            {
                var fullPath = Path.Combine(_packFolder, entry.Path);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                _audioMap.Remove(id);
                await SaveManifestAsync();
            }
        }

        /// <summary>
        /// Generates a stable 16-character ID for dialogue text using SHA256.
        /// </summary>
        private string GenerateId(string text)
        {
            var normalized = TextNormalizer.Normalize(text);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Loads the audio manifest from pack's Configuration/audio-manifest.json.
        /// </summary>
        private async Task LoadManifestAsync()
        {
            if (string.IsNullOrEmpty(_packFolder))
                return;

            var manifestPath = Path.Combine(_packFolder, "Configuration", "audio-manifest.json");
            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<Dictionary<string, AudioManifestEntry>>(json);
                if (manifest != null)
                {
                    foreach (var kvp in manifest)
                    {
                        _audioMap[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Saves the audio manifest to pack's Configuration/audio-manifest.json.
        /// </summary>
        private async Task SaveManifestAsync()
        {
            if (string.IsNullOrEmpty(_packFolder))
                return;

            var configDir = Path.Combine(_packFolder, "Configuration");
            Directory.CreateDirectory(configDir);

            var manifestPath = Path.Combine(configDir, "audio-manifest.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_audioMap, options);
            await File.WriteAllTextAsync(manifestPath, json);
        }

        /// <summary>
        /// Gets all audio entries for diagnostics/cleanup tools.
        /// </summary>
        public IReadOnlyDictionary<string, AudioManifestEntry> GetAllEntries() => _audioMap;

        /// <summary>
        /// Validates that all manifest entries have corresponding files.
        /// Returns list of missing audio file paths.
        /// </summary>
        public List<string> ValidateManifest()
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(_packFolder))
                return missing;

            foreach (var entry in _audioMap.Values)
            {
                var fullPath = Path.Combine(_packFolder, entry.Path);
                if (!File.Exists(fullPath))
                {
                    missing.Add(entry.Path);
                }
            }

            return missing;
        }

        /// <summary>
        /// Finds orphaned audio files (files in Audio folder not in manifest).
        /// </summary>
        public List<string> FindOrphanedFiles()
        {
            var orphaned = new List<string>();
            if (string.IsNullOrEmpty(_packFolder))
                return orphaned;

            var audioFolder = Path.Combine(_packFolder, "Audio");
            if (!Directory.Exists(audioFolder))
                return orphaned;

            var manifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _audioMap.Values)
            {
                manifestPaths.Add(Path.GetFullPath(Path.Combine(_packFolder, entry.Path)));
            }

            foreach (var file in Directory.GetFiles(audioFolder, "*.*", SearchOption.AllDirectories))
            {
                if (!manifestPaths.Contains(Path.GetFullPath(file)))
                {
                    orphaned.Add(Path.GetRelativePath(_packFolder, file).Replace('\\', '/'));
                }
            }

            return orphaned;
        }
    }

    /// <summary>
    /// Represents an entry in the audio manifest with metadata.
    /// </summary>
    public class AudioManifestEntry
    {
        public string Path { get; set; } = string.Empty;
        public string? VoiceName { get; set; }
        public bool IsGenerated { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
