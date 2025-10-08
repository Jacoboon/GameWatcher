using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SimpleLoop
{
    public class DialogueCatalog
    {
        private readonly string _catalogPath;
        private readonly Dictionary<string, DialogueEntry> _entries;
        private readonly object _lockObject = new object();
        private readonly SpeakerCatalog? _speakerCatalog;

        public DialogueCatalog(string catalogPath = "dialogue_catalog.json", SpeakerCatalog? speakerCatalog = null)
        {
            _catalogPath = catalogPath;
            _entries = new Dictionary<string, DialogueEntry>();
            _speakerCatalog = speakerCatalog;
            LoadCatalog();
        }

        public DialogueEntry? AddOrUpdateDialogue(string text, string rawOcrText = "")
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            lock (_lockObject)
            {
                var cleanText = text.Trim();
                
                // FIRST: Look for existing dialogue by exact text match (ignore speaker)
                var existing = _entries.Values.FirstOrDefault(e => e.Text.Equals(cleanText, StringComparison.OrdinalIgnoreCase));
                
                // SECOND: If no exact match, look for fuzzy matches (OCR variations)
                if (existing == null)
                {
                    existing = _entries.Values.FirstOrDefault(e => IsCloseMatch(e.Text, cleanText));
                    if (existing != null)
                    {
                        Console.WriteLine($"🔍 Found fuzzy match: '{existing.Text}' ~= '{cleanText}'");
                    }
                }
                
                if (existing != null)
                {
                    // Found existing dialogue - update it and use its saved speaker
                    existing.LastSeen = DateTime.Now;
                    existing.SeenCount++;
                    existing.RawOcrText = rawOcrText; // Update raw OCR if provided
                    
                    Console.WriteLine($"🔄 Found existing dialogue by text: {existing.Id} - Speaker: {existing.Speaker} (seen {existing.SeenCount} times)");
                    SaveCatalog();
                    return existing;
                }
                else
                {
                    // Text not found - create new entry with speaker determination
                    var newEntry = new DialogueEntry 
                    { 
                        Text = cleanText,
                        RawOcrText = rawOcrText,
                        Speaker = DetermineSpeaker(cleanText)
                    };
                    
                    newEntry.Id = newEntry.GenerateId();
                    _entries[newEntry.Id] = newEntry;
                    
                    Console.WriteLine($"🆕 New dialogue: {newEntry.Id} - Speaker: {newEntry.Speaker}");
                    SaveCatalog();
                    return newEntry;
                }
            }
        }

        private string DetermineSpeaker(string text)
        {
            // Use SpeakerCatalog if available for consistent speaker identification
            if (_speakerCatalog != null)
            {
                var speaker = _speakerCatalog.IdentifySpeaker(text);
                if (speaker != null)
                {
                    return speaker.Name;
                }
            }
            
            // Fallback to hardcoded patterns for backward compatibility
            var lowerText = text.ToLower();
            
            // Specific character detection
            if (lowerText.Contains("astos") || lowerText.Contains("king") && lowerText.Contains("elves"))
                return "Astos";
            
            if (lowerText.Contains("i am a sage") || lowerText.Contains("future is revealed"))
                return "Sage of Elfheim";  // Updated to match speaker catalog
                
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
        
        /// <summary>
        /// Check if two text strings are close matches (fuzzy matching for OCR variations)
        /// </summary>
        private static bool IsCloseMatch(string existing, string newText)
        {
            if (string.IsNullOrEmpty(existing) || string.IsNullOrEmpty(newText)) return false;
            
            // Normalize both texts for comparison
            var text1 = NormalizeForComparison(existing);
            var text2 = NormalizeForComparison(newText);
            
            // If normalized texts are identical, it's a match
            if (text1.Equals(text2, StringComparison.OrdinalIgnoreCase)) return true;
            
            // Calculate similarity using Levenshtein distance
            var similarity = CalculateSimilarity(text1, text2);
            
            // Consider it a match if similarity is high (80%+ for dialogue)
            // Lower threshold accounts for OCR character corruption
            const double SIMILARITY_THRESHOLD = 0.80;
            bool isMatch = similarity >= SIMILARITY_THRESHOLD;
            
            if (isMatch)
            {
                Console.WriteLine($"🎯 Fuzzy match found: {similarity:P1} similarity");
                Console.WriteLine($"   Existing: '{existing}'");
                Console.WriteLine($"   New OCR:  '{newText}'");
            }
            
            return isMatch;
        }
        
        /// <summary>
        /// Normalize text for fuzzy comparison by removing OCR artifacts
        /// </summary>
        private static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            var normalized = text.Trim();
            
            // Remove common OCR punctuation variations
            normalized = normalized.TrimEnd('.', ',', '!', '?', ';', ':');
            
            // Remove OCR garbage patterns at end of text (common OCR failure mode)
            normalized = RemoveOcrGarbageAtEnd(normalized);
            
            // Fix common OCR character substitutions before calculating similarity
            normalized = FixCommonOcrErrors(normalized);
            
            // Normalize whitespace (multiple spaces -> single space)
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
            
            // Remove common OCR artifacts
            normalized = normalized.Replace("  ", " ");  // Double spaces
            normalized = normalized.Replace(" .", ".");   // Space before period
            normalized = normalized.Replace(" ,", ",");   // Space before comma
            normalized = normalized.Replace(" !", "!");   // Space before exclamation
            normalized = normalized.Replace(" ?", "?");   // Space before question mark
            
            // Normalize smart quotes and apostrophes
            normalized = normalized.Replace("'", "'").Replace("'", "'");
            normalized = normalized.Replace(""", "\"").Replace(""", "\"");
            
            return normalized.Trim();
        }
        
        /// <summary>
        /// Remove OCR garbage patterns typically found at the end of text
        /// </summary>
        private static string RemoveOcrGarbageAtEnd(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Pattern: Remove trailing garbage like " p I e,eSe", " qw er ty", etc.
            // This handles cases where OCR adds random characters at the end
            var patterns = new[]
            {
                @"\s+[a-zA-Z]\s+[a-zA-Z]\s+[a-zA-Z,.\s]*$",    // " p I e,eSe" pattern
                @"\s+[a-zA-Z]{1,2}\s+[a-zA-Z]{1,2}[\s,.\-!]*$", // Short character sequences
                @"\s+[^\w\s]{2,}$",                              // Multiple symbols at end
                @"\s+\w\s+\w[\s\W]*$",                          // Single chars with spaces/symbols
                @"\s+[a-zA-Z]\s*[,.\-!]{1,3}[a-zA-Z]*[Ss3]e$", // Patterns like " p I e,eSe"
                @"\s+[a-zA-Z]{1,2}\s*[0-9]{1,2}[a-zA-Z]*$",    // " p I 3Se" type patterns
                @"\s+[a-zA-Z]\s+[a-zA-Z]\s*[,.\-!\s]*$"        // More flexible single char patterns
            };
            
            foreach (var pattern in patterns)
            {
                var cleaned = System.Text.RegularExpressions.Regex.Replace(text, pattern, "", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (cleaned.Length < text.Length)
                {
                    Console.WriteLine($"   Removed OCR garbage: '{text.Substring(cleaned.Length)}' from end");
                    text = cleaned.Trim();
                }
            }
            
            return text;
        }
        
        /// <summary>
        /// Fix common OCR character recognition errors
        /// </summary>
        private static string FixCommonOcrErrors(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Common OCR character substitutions
            var fixes = new Dictionary<string, string>
            {
                // Word-level fixes (case sensitive, most common FF1 OCR errors)
                {"Ijeapons", "Weapons"},
                {"Yau'", "You'll"},
                {"Yau' II", "You'll"},
                {"Ije ", "We "},        // Very common: "Ije" -> "We"
                {"Rstos", "Astos"},     // Common: R -> A confusion
                {"Ijhen", "When"},      // Common: Ij -> W confusion
                {" Il ", " I "},        // Il -> I
                {" Il'", " I'"},        // Il' -> I'
                
                // Character-level fixes (more aggressive for FF1 text)
                {" II ", " ll "},       // Roman numeral II -> ll
                {"0", "o"},             // Zero to lowercase O
                {"5", "s"},             // 5 to S  
                {"1", "l"},             // 1 to lowercase L (but be careful with "I")
                {"3", "e"},             // 3 to e
                {"rn", "m"},            // rn combination often misread as m
                {"vv", "w"},            // Double v often misread as w
                {"lj", "W"},            // lj often misread as W at start of words
                {"ij", "w"},            // ij often misread as w in middle of words
            };
            
            var result = text;
            foreach (var fix in fixes)
            {
                if (result.Contains(fix.Key))
                {
                    result = result.Replace(fix.Key, fix.Value);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculate text similarity using Levenshtein distance
        /// </summary>
        private static double CalculateSimilarity(string text1, string text2)
        {
            if (text1 == text2) return 1.0;
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return 0.0;
            
            var maxLength = Math.Max(text1.Length, text2.Length);
            if (maxLength == 0) return 1.0;
            
            var distance = CalculateLevenshteinDistance(text1, text2);
            return 1.0 - (double)distance / maxLength;
        }
        
        /// <summary>
        /// Calculate Levenshtein distance between two strings
        /// </summary>
        private static int CalculateLevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            
            if (len1 == 0) return len2;
            if (len2 == 0) return len1;
            
            var matrix = new int[len1 + 1, len2 + 1];
            
            // Initialize first row and column
            for (int i = 0; i <= len1; i++) matrix[i, 0] = i;
            for (int j = 0; j <= len2; j++) matrix[0, j] = j;
            
            // Calculate distances
            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    
                    matrix[i, j] = Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1,      // Deletion
                            matrix[i, j - 1] + 1),     // Insertion
                        matrix[i - 1, j - 1] + cost   // Substitution
                    );
                }
            }
            
            return matrix[len1, len2];
        }
    }
}