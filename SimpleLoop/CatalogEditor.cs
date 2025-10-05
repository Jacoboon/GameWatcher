using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SimpleLoop
{
    public class CatalogEditor
    {
        private readonly string catalogPath;
        private List<DialogueEntry> entries = new();

        public CatalogEditor(string catalogPath = "dialogue_catalog.json")
        {
            this.catalogPath = catalogPath;
            LoadCatalog();
        }

        private void LoadCatalog()
        {
            if (File.Exists(catalogPath))
            {
                var json = File.ReadAllText(catalogPath);
                entries = JsonConvert.DeserializeObject<List<DialogueEntry>>(json) ?? new List<DialogueEntry>();
                Console.WriteLine($"✅ Loaded {entries.Count} dialogue entries from catalog");
            }
            else
            {
                entries = new List<DialogueEntry>();
                Console.WriteLine("ℹ️ No existing catalog found, starting fresh");
            }
        }

        public void SaveCatalog()
        {
            var json = JsonConvert.SerializeObject(entries, Formatting.Indented);
            File.WriteAllText(catalogPath, json);
            Console.WriteLine($"💾 Saved {entries.Count} entries to {catalogPath}");
        }

        public void ShowMainMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== DIALOGUE CATALOG EDITOR ===");
                Console.WriteLine($"📋 Total entries: {entries.Count}");
                Console.WriteLine($"🎤 Ready for TTS: {entries.Count(e => e.IsReadyForTTS)}");
                Console.WriteLine($"✅ Approved: {entries.Count(e => e.IsApproved)}");
                Console.WriteLine($"🔊 Has Audio: {entries.Count(e => e.HasAudio)}");
                Console.WriteLine();
                Console.WriteLine("1. List all entries");
                Console.WriteLine("2. Review/Edit entries");
                Console.WriteLine("3. Show entries by speaker");
                Console.WriteLine("4. Show entries ready for TTS");
                Console.WriteLine("5. Bulk approve entries");
                Console.WriteLine("6. Export for TTS generation");
                Console.WriteLine("7. Save and exit");
                Console.WriteLine("0. Exit without saving");
                Console.WriteLine();
                Console.Write("Choose an option: ");

                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": ListAllEntries(); break;
                    case "2": ReviewEntries(); break;
                    case "3": ShowEntriesBySpeaker(); break;
                    case "4": ShowReadyForTTS(); break;
                    case "5": BulkApprove(); break;
                    case "6": ExportForTTS(); break;
                    case "7": SaveAndExit(); return;
                    case "0": return;
                    default: Console.WriteLine("Invalid option. Press any key..."); Console.ReadKey(); break;
                }
            }
        }

        private void ListAllEntries()
        {
            Console.Clear();
            Console.WriteLine("=== ALL DIALOGUE ENTRIES ===");
            
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var status = GetEntryStatusIcon(entry);
                var preview = entry.Text.Length > 60 ? entry.Text.Substring(0, 57) + "..." : entry.Text;
                preview = preview.Replace("\n", " ");
                
                Console.WriteLine($"{i + 1:D2}. {status} [{entry.Speaker}] {preview}");
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ReviewEntries()
        {
            if (entries.Count == 0)
            {
                Console.WriteLine("No entries to review. Press any key...");
                Console.ReadKey();
                return;
            }

            int currentIndex = 0;
            while (currentIndex < entries.Count)
            {
                var entry = entries[currentIndex];
                ShowEntryDetails(entry, currentIndex + 1, entries.Count);
                
                Console.WriteLine("\nOptions:");
                Console.WriteLine("1. Edit text  2. Change speaker  3. Set voice  4. Add notes");
                Console.WriteLine("5. Toggle ready for TTS  6. Toggle approved");
                Console.WriteLine("N. Next  P. Previous  S. Skip to entry  Q. Back to menu");
                Console.Write("Choice: ");

                var choice = Console.ReadLine()?.ToUpper();
                switch (choice)
                {
                    case "1": EditEntryText(entry); break;
                    case "2": ChangeSpeaker(entry); break;
                    case "3": SetVoice(entry); break;
                    case "4": AddNotes(entry); break;
                    case "5": entry.IsReadyForTTS = !entry.IsReadyForTTS; break;
                    case "6": entry.IsApproved = !entry.IsApproved; break;
                    case "N": currentIndex = Math.Min(currentIndex + 1, entries.Count - 1); break;
                    case "P": currentIndex = Math.Max(currentIndex - 1, 0); break;
                    case "S": currentIndex = SkipToEntry() - 1; break;
                    case "Q": return;
                }
            }
        }

        private void ShowEntryDetails(DialogueEntry entry, int current, int total)
        {
            Console.Clear();
            Console.WriteLine($"=== ENTRY {current}/{total} ===");
            Console.WriteLine($"ID: {entry.Id}");
            Console.WriteLine($"Speaker: {entry.Speaker}");
            Console.WriteLine($"Status: {GetEntryStatusIcon(entry)} {GetEntryStatusText(entry)}");
            Console.WriteLine($"Seen: {entry.SeenCount} times (First: {entry.FirstSeen:MM/dd HH:mm})");
            Console.WriteLine();
            Console.WriteLine("Original Text:");
            Console.WriteLine($"  {entry.Text.Replace("\n", "\n  ")}");
            
            if (!string.IsNullOrWhiteSpace(entry.EditedText))
            {
                Console.WriteLine();
                Console.WriteLine("Edited Text (for TTS):");
                Console.WriteLine($"  {entry.EditedText.Replace("\n", "\n  ")}");
            }
            
            if (!string.IsNullOrWhiteSpace(entry.PronunciationNotes))
            {
                Console.WriteLine($"\nPronunciation Notes: {entry.PronunciationNotes}");
            }
            
            if (!string.IsNullOrWhiteSpace(entry.TtsVoiceId))
            {
                Console.WriteLine($"TTS Voice: {entry.TtsVoiceId}");
            }
        }

        private void EditEntryText(DialogueEntry entry)
        {
            Console.WriteLine("\nCurrent text:");
            Console.WriteLine(entry.Text);
            Console.WriteLine("\nEnter edited text (or press Enter to keep original):");
            Console.Write("> ");
            var newText = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(newText))
            {
                entry.EditedText = newText;
                Console.WriteLine("✅ Text updated");
            }
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private void ChangeSpeaker(DialogueEntry entry)
        {
            var speakers = entries.Select(e => e.Speaker).Distinct().OrderBy(s => s).ToList();
            Console.WriteLine("\nExisting speakers:");
            for (int i = 0; i < speakers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {speakers[i]}");
            }
            Console.WriteLine($"Current: {entry.Speaker}");
            Console.Write("Enter new speaker name or number from list: ");
            var input = Console.ReadLine();
            
            if (int.TryParse(input, out int index) && index > 0 && index <= speakers.Count)
            {
                entry.Speaker = speakers[index - 1];
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                entry.Speaker = input;
            }
            
            Console.WriteLine($"✅ Speaker set to: {entry.Speaker}");
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private void SetVoice(DialogueEntry entry)
        {
            var voices = new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };
            Console.WriteLine("\nAvailable OpenAI voices:");
            for (int i = 0; i < voices.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {voices[i]}");
            }
            Console.WriteLine($"Current: {entry.TtsVoiceId}");
            Console.Write("Enter voice number or name: ");
            var input = Console.ReadLine();
            
            if (int.TryParse(input, out int index) && index > 0 && index <= voices.Length)
            {
                entry.TtsVoiceId = voices[index - 1];
            }
            else if (!string.IsNullOrWhiteSpace(input) && voices.Contains(input.ToLower()))
            {
                entry.TtsVoiceId = input.ToLower();
            }
            
            Console.WriteLine($"✅ Voice set to: {entry.TtsVoiceId}");
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private void AddNotes(DialogueEntry entry)
        {
            Console.WriteLine($"Current notes: {entry.PronunciationNotes}");
            Console.Write("Enter pronunciation notes: ");
            entry.PronunciationNotes = Console.ReadLine() ?? "";
            Console.WriteLine("✅ Notes updated");
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private int SkipToEntry()
        {
            Console.Write($"Enter entry number (1-{entries.Count}): ");
            if (int.TryParse(Console.ReadLine(), out int num) && num > 0 && num <= entries.Count)
            {
                return num;
            }
            return 1;
        }

        private void ShowEntriesBySpeaker()
        {
            var grouped = entries.GroupBy(e => e.Speaker).OrderBy(g => g.Key);
            
            Console.Clear();
            Console.WriteLine("=== ENTRIES BY SPEAKER ===");
            foreach (var group in grouped)
            {
                Console.WriteLine($"\n🎭 {group.Key} ({group.Count()} entries):");
                foreach (var entry in group.Take(5))
                {
                    var status = GetEntryStatusIcon(entry);
                    var preview = entry.Text.Length > 50 ? entry.Text.Substring(0, 47) + "..." : entry.Text;
                    preview = preview.Replace("\n", " ");
                    Console.WriteLine($"  {status} {preview}");
                }
                if (group.Count() > 5)
                {
                    Console.WriteLine($"  ... and {group.Count() - 5} more");
                }
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ShowReadyForTTS()
        {
            var ready = entries.Where(e => e.IsReadyForTTS && e.IsApproved).ToList();
            
            Console.Clear();
            Console.WriteLine("=== ENTRIES READY FOR TTS ===");
            Console.WriteLine($"Found {ready.Count} approved entries ready for TTS generation");
            
            foreach (var entry in ready)
            {
                var text = entry.GetTextForTTS();
                var preview = text.Length > 60 ? text.Substring(0, 57) + "..." : text;
                preview = preview.Replace("\n", " ");
                Console.WriteLine($"🎤 [{entry.Speaker}] {entry.TtsVoiceId} - {preview}");
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void BulkApprove()
        {
            Console.WriteLine("Bulk approve options:");
            Console.WriteLine("1. Approve all entries");
            Console.WriteLine("2. Approve entries marked ready for TTS");
            Console.WriteLine("3. Approve entries by speaker");
            Console.Write("Choice: ");
            
            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    foreach (var entry in entries) entry.IsApproved = true;
                    Console.WriteLine($"✅ Approved all {entries.Count} entries");
                    break;
                case "2":
                    var readyCount = entries.Count(e => e.IsReadyForTTS);
                    foreach (var entry in entries.Where(e => e.IsReadyForTTS)) entry.IsApproved = true;
                    Console.WriteLine($"✅ Approved {readyCount} entries marked ready for TTS");
                    break;
                case "3":
                    Console.Write("Enter speaker name: ");
                    var speaker = Console.ReadLine();
                    var speakerCount = entries.Count(e => e.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
                    foreach (var entry in entries.Where(e => e.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase)))
                    {
                        entry.IsApproved = true;
                    }
                    Console.WriteLine($"✅ Approved {speakerCount} entries for speaker '{speaker}'");
                    break;
            }
            
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private void ExportForTTS()
        {
            var ready = entries.Where(e => e.IsReadyForTTS && e.IsApproved && !e.HasAudio).ToList();
            
            if (ready.Count == 0)
            {
                Console.WriteLine("No entries ready for TTS generation.");
                Console.WriteLine("Press any key...");
                Console.ReadKey();
                return;
            }
            
            var exportPath = "tts_queue.json";
            var exportData = ready.Select(e => new
            {
                Id = e.Id,
                Text = e.GetTextForTTS(),
                Speaker = e.Speaker,
                Voice = e.TtsVoiceId,
                Notes = e.PronunciationNotes
            }).ToList();
            
            var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(exportPath, json);
            
            Console.WriteLine($"📤 Exported {ready.Count} entries to {exportPath}");
            Console.WriteLine("Ready for TTS generation!");
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private void SaveAndExit()
        {
            SaveCatalog();
            Console.WriteLine("✅ Changes saved. Goodbye!");
            System.Threading.Thread.Sleep(1000);
        }

        private string GetEntryStatusIcon(DialogueEntry entry)
        {
            if (entry.HasAudio) return "🔊";
            if (entry.IsApproved && entry.IsReadyForTTS) return "🎤";
            if (entry.IsReadyForTTS) return "⏳";
            return "📝";
        }

        private string GetEntryStatusText(DialogueEntry entry)
        {
            if (entry.HasAudio) return "Has Audio";
            if (entry.IsApproved && entry.IsReadyForTTS) return "Ready for TTS";
            if (entry.IsReadyForTTS) return "Needs Approval";
            return "Needs Review";
        }
    }
}