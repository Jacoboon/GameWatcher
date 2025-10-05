using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SimpleLoop
{
    public class DialogueCatalog
    {
        private readonly string _catalogPath;
        private readonly Dictionary<string, DialogueEntry> _entries;
        private readonly object _lockObject = new object();

        public DialogueCatalog(string catalogPath = "dialogue_catalog.json")
        {
            _catalogPath = catalogPath;
            _entries = new Dictionary<string, DialogueEntry>();
            LoadCatalog();
        }

        public DialogueEntry? AddOrUpdateDialogue(string text, string rawOcrText = "")
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            lock (_lockObject)
            {
                var entry = new DialogueEntry 
                { 
                    Text = text.Trim(),
                    RawOcrText = rawOcrText,
                    Speaker = DetermineSpeaker(text)
                };
                
                entry.Id = entry.GenerateId();

                if (_entries.ContainsKey(entry.Id))
                {
                    // Update existing entry
                    var existing = _entries[entry.Id];
                    existing.LastSeen = DateTime.Now;
                    existing.SeenCount++;
                    
                    Console.WriteLine($"🔄 Updated dialogue: {existing.Id} (seen {existing.SeenCount} times)");
                    return existing;
                }
                else
                {
                    // New dialogue entry
                    _entries[entry.Id] = entry;
                    Console.WriteLine($"🆕 New dialogue: {entry.Id} - Speaker: {entry.Speaker}");
                    SaveCatalog();
                    return entry;
                }
            }
        }

        private string DetermineSpeaker(string text)
        {
            // Smart speaker detection based on FF1 dialogue patterns
            var lowerText = text.ToLower();
            
            // Specific character detection
            if (lowerText.Contains("astos") || lowerText.Contains("king") && lowerText.Contains("elves"))
                return "Astos";
            
            if (lowerText.Contains("i am a sage") || lowerText.Contains("future is revealed"))
                return "Sage";
                
            if (lowerText.Contains("princess") || lowerText.Contains("your highness"))
                return "Princess";
                
            if (lowerText.Contains("garland") || lowerText.Contains("will knock you all down"))
                return "Garland";
            
            // Context-based detection
            if (lowerText.Contains("welcome") || lowerText.Contains("can i help"))
                return "Shopkeeper";
                
            if (lowerText.Contains("light warriors") || lowerText.Contains("chosen ones"))
                return "Elder";
            
            // Formal/royal speech patterns
            if (lowerText.Contains("thou") || lowerText.Contains("thee") || lowerText.Contains("thy"))
                return "Royal";
                
            // Default classifications
            if (text.Length > 100)
                return "Narrator";  // Long exposition text
            else if (lowerText.Contains("..."))
                return "Mysterious";  // Dramatic pauses
            else
                return "NPC";  // Generic townsperson
        }

        public DialogueEntry? GetDialogue(string id)
        {
            lock (_lockObject)
            {
                return _entries.TryGetValue(id, out var entry) ? entry : null;
            }
        }

        public List<DialogueEntry> GetAllDialogue()
        {
            lock (_lockObject)
            {
                return new List<DialogueEntry>(_entries.Values);
            }
        }

        public List<DialogueEntry> GetDialoguesBySpeaker(string speaker)
        {
            lock (_lockObject)
            {
                var result = new List<DialogueEntry>();
                foreach (var entry in _entries.Values)
                {
                    if (entry.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                        result.Add(entry);
                }
                return result;
            }
        }

        private void LoadCatalog()
        {
            try
            {
                if (File.Exists(_catalogPath))
                {
                    var json = File.ReadAllText(_catalogPath);
                    var entries = JsonSerializer.Deserialize<DialogueEntry[]>(json);
                    
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            _entries[entry.Id] = entry;
                        }
                        Console.WriteLine($"📚 Loaded {_entries.Count} dialogue entries from catalog");
                    }
                }
                else
                {
                    Console.WriteLine($"📚 Creating new dialogue catalog: {_catalogPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error loading catalog: {ex.Message}");
            }
        }

        public void SaveCatalog()
        {
            try
            {
                var entries = new List<DialogueEntry>(_entries.Values);
                var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(_catalogPath, json);
                Console.WriteLine($"💾 Saved {entries.Count} dialogue entries to catalog");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error saving catalog: {ex.Message}");
            }
        }

        public void PrintStats()
        {
            lock (_lockObject)
            {
                var speakers = new Dictionary<string, int>();
                foreach (var entry in _entries.Values)
                {
                    speakers[entry.Speaker] = speakers.GetValueOrDefault(entry.Speaker, 0) + 1;
                }

                Console.WriteLine($"\n📊 Dialogue Catalog Stats:");
                Console.WriteLine($"   Total entries: {_entries.Count}");
                foreach (var speaker in speakers)
                {
                    Console.WriteLine($"   {speaker.Key}: {speaker.Value} lines");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Get all dialogue entries for GUI display
        /// </summary>
        public IEnumerable<DialogueEntry> GetAllEntries()
        {
            lock (_lockObject)
            {
                return _entries.Values.ToList(); // Return a copy to avoid threading issues
            }
        }
        
        /// <summary>
        /// Remove a dialogue entry by its text content
        /// </summary>
        public bool RemoveDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            lock (_lockObject)
            {
                // Find the entry with matching text
                var entryToRemove = _entries.Values.FirstOrDefault(e => e.Text == text.Trim());
                if (entryToRemove != null)
                {
                    var removed = _entries.Remove(entryToRemove.Id);
                    if (removed)
                    {
                        SaveCatalog();
                    }
                    return removed;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Remove a dialogue entry by its ID
        /// </summary>
        public bool RemoveDialogueById(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId)) return false;

            lock (_lockObject)
            {
                var removed = _entries.Remove(entryId);
                if (removed)
                {
                    SaveCatalog();
                }
                return removed;
            }
        }
        
        /// <summary>
        /// Get dialogue entry count
        /// </summary>
        public int Count => _entries.Count;
    }
}