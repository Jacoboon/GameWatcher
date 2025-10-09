using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameWatcher.AuthorStudio.Services
{
    /// <summary>
    /// Manages persistence of discovery session data (Discovered and Accepted dialogue lists).
    /// Sessions are tied to the specific pack folder being authored to prevent cross-contamination.
    /// </summary>
    public class SessionStore
    {
        private readonly ILogger<SessionStore> _logger;
        private readonly string _sessionsDirectory;
        private string? _currentPackPath;
        private string? _currentSessionFile;

        private class SessionModel
        {
            [JsonPropertyName("packPath")] public string? PackPath { get; set; }
            [JsonPropertyName("lastSaved")] public DateTime LastSaved { get; set; }
            [JsonPropertyName("game")] public string? Game { get; set; }
            [JsonPropertyName("notes")] public string? Notes { get; set; }
            [JsonPropertyName("discovered")] public List<PendingDialogueEntry> Discovered { get; set; } = new();
            [JsonPropertyName("accepted")] public List<PendingDialogueEntry> Accepted { get; set; } = new();
        }

        public SessionStore(ILogger<SessionStore> logger)
        {
            _logger = logger;
            
            // Store sessions in %AppData%/GameWatcher/AuthorStudio/sessions/
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _sessionsDirectory = Path.Combine(appData, "GameWatcher", "AuthorStudio", "sessions");
            Directory.CreateDirectory(_sessionsDirectory);
            
            _logger.LogInformation("SessionStore initialized. Sessions directory: {Dir}", _sessionsDirectory);
        }

        /// <summary>
        /// Sets the current pack being authored. This determines which session file to use.
        /// Call this when a pack is opened or created.
        /// </summary>
        public void SetCurrentPack(string? packPath)
        {
            if (string.IsNullOrWhiteSpace(packPath))
            {
                _currentPackPath = null;
                _currentSessionFile = null;
                _logger.LogInformation("Cleared current pack context");
                return;
            }

            _currentPackPath = packPath;
            
            // Create a safe filename from the pack path (hash it to avoid path length issues)
            var packHash = GetStableHash(packPath);
            _currentSessionFile = Path.Combine(_sessionsDirectory, $"session_{packHash}.json");
            
            _logger.LogInformation("Set current pack: {PackPath} -> Session file: {SessionFile}", 
                packPath, _currentSessionFile);
        }

        /// <summary>
        /// Loads the session data for the current pack (Discovered and Accepted lists).
        /// Returns empty lists if no session exists or if no pack is set.
        /// </summary>
        public async Task<(List<PendingDialogueEntry> discovered, List<PendingDialogueEntry> accepted)> LoadSessionAsync()
        {
            if (string.IsNullOrEmpty(_currentSessionFile))
            {
                _logger.LogWarning("Cannot load session: no pack is set");
                return (new List<PendingDialogueEntry>(), new List<PendingDialogueEntry>());
            }

            if (!File.Exists(_currentSessionFile))
            {
                _logger.LogInformation("No existing session file found at {Path}", _currentSessionFile);
                return (new List<PendingDialogueEntry>(), new List<PendingDialogueEntry>());
            }

            try
            {
                var json = await File.ReadAllTextAsync(_currentSessionFile);
                var model = JsonSerializer.Deserialize<SessionModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                var discovered = model?.Discovered ?? new List<PendingDialogueEntry>();
                var accepted = model?.Accepted ?? new List<PendingDialogueEntry>();
                
                _logger.LogInformation("Loaded session: {DiscoveredCount} discovered, {AcceptedCount} accepted", 
                    discovered.Count, accepted.Count);
                
                return (discovered, accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session from {Path}", _currentSessionFile);
                return (new List<PendingDialogueEntry>(), new List<PendingDialogueEntry>());
            }
        }

        /// <summary>
        /// Saves the current session data (Discovered and Accepted lists).
        /// </summary>
        public async Task SaveSessionAsync(
            IEnumerable<PendingDialogueEntry> discovered, 
            IEnumerable<PendingDialogueEntry> accepted)
        {
            if (string.IsNullOrEmpty(_currentSessionFile))
            {
                _logger.LogWarning("Cannot save session: no pack is set");
                return;
            }

            try
            {
                var model = new SessionModel
                {
                    PackPath = _currentPackPath,
                    LastSaved = DateTime.UtcNow,
                    Discovered = new List<PendingDialogueEntry>(discovered),
                    Accepted = new List<PendingDialogueEntry>(accepted)
                };

                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                await File.WriteAllTextAsync(_currentSessionFile, json);
                
                _logger.LogInformation("Saved session: {DiscoveredCount} discovered, {AcceptedCount} accepted to {Path}", 
                    model.Discovered.Count, model.Accepted.Count, _currentSessionFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session to {Path}", _currentSessionFile);
            }
        }

        /// <summary>
        /// Clears the session for the current pack (deletes the session file).
        /// Use when starting a fresh session or resetting.
        /// </summary>
        public async Task ClearSessionAsync()
        {
            if (string.IsNullOrEmpty(_currentSessionFile))
            {
                return;
            }

            try
            {
                if (File.Exists(_currentSessionFile))
                {
                    File.Delete(_currentSessionFile);
                    _logger.LogInformation("Cleared session file: {Path}", _currentSessionFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear session file: {Path}", _currentSessionFile);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Legacy method for compatibility - saves session to explicit path.
        /// </summary>
        [Obsolete("Use SaveSessionAsync with current pack context instead")]
        public async Task SaveAsync(string path, IEnumerable<PendingDialogueEntry> entries, string? game = null, string? notes = null)
        {
            var model = new SessionModel
            {
                Game = game,
                Notes = notes,
                Discovered = new List<PendingDialogueEntry>(entries)
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            await File.WriteAllTextAsync(path, json);
        }

        /// <summary>
        /// Legacy method for compatibility - loads session from explicit path.
        /// </summary>
        [Obsolete("Use LoadSessionAsync with current pack context instead")]
        public async Task<IReadOnlyList<PendingDialogueEntry>> LoadAsync(string path)
        {
            var json = await File.ReadAllTextAsync(path);
            var model = JsonSerializer.Deserialize<SessionModel>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return model?.Discovered ?? new List<PendingDialogueEntry>();
        }

        /// <summary>
        /// Creates a stable hash from a pack path for use as a filename.
        /// Uses SHA256 for deterministic hashing across sessions.
        /// </summary>
        private static string GetStableHash(string input)
        {
            // Normalize path separators and case for consistency
            var normalized = input.Replace('/', '\\').ToLowerInvariant();
            
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
            var hash = sha.ComputeHash(bytes);
            
            // Take first 16 bytes and convert to hex string
            return BitConverter.ToString(hash, 0, 16).Replace("-", "").ToLowerInvariant();
        }
    }
}
