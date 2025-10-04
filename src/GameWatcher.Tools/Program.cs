using System.Text.Json;
using System.Text.RegularExpressions;
using GameWatcher.Tools.Author;
using GameWatcher.Tools.Tts;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        var cmd = args[0].ToLowerInvariant();
        if (cmd == "gen-voices")
        {
            var misses = GetArg(args, "--misses") ?? "data/misses.json";
            var mapPath = GetArg(args, "--map") ?? "assets/maps/dialogue.en.json";
            var speakersPath = GetArg(args, "--speakers") ?? "assets/maps/speakers.json";
            var voicesDir = GetArg(args, "--voices") ?? "assets/voices";
            var personaPath = GetArg(args, "--persona") ?? "assets/voices/persona.json";
            var max = int.TryParse(GetArg(args, "--max"), out var n) ? n : int.MaxValue;
            var overwrite = HasFlag(args, "--overwrite");
            var dry = HasFlag(args, "--dry-run");

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey) && !dry)
            {
                Console.Error.WriteLine("OPENAI_API_KEY not set. Set it or use --dry-run.");
                return 2;
            }

            if (!File.Exists(misses))
            {
                Console.Error.WriteLine($"Misses file not found: {misses}");
                return 2;
            }

            var defaultPersona = VoicePersona.Load(personaPath);
            var speakerMap = new SpeakerMap(speakersPath);

            Directory.CreateDirectory(Path.GetDirectoryName(mapPath)!);
            Directory.CreateDirectory(voicesDir);

            var existing = LoadMap(mapPath);
            var missList = JsonSerializer.Deserialize<List<MissEntry>>(File.ReadAllText(misses)) ?? new();

            int nextNum = NextLineNumber(voicesDir);
            int generated = 0;
            foreach (var m in missList)
            {
                if (generated >= max) break;
                var key = m.normalized;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (existing.ContainsKey(key)) continue;

                var file = $"line_{nextNum:0000}.wav";
                var outPath = Path.Combine(voicesDir, file);

                var speaker = speakerMap.Resolve(key);
                var personaForSpeaker = speaker == "default" ? defaultPersona : VoicePersona.Load(Path.Combine(voicesDir, "personas", speaker + ".json"));
                var client = dry ? null : new OpenAiTtsClient(apiKey!, personaForSpeaker.Model, personaForSpeaker.Voice);

                Console.WriteLine($"Generate: {file} <= [{speaker}] {key}");
                if (!dry)
                {
                    var audio = await client!.SynthesizeWavAsync(key);
                    if (File.Exists(outPath) && !overwrite)
                    {
                        Console.Error.WriteLine($"Exists, skipping: {outPath}");
                        continue;
                    }
                    await File.WriteAllBytesAsync(outPath, audio);
                }
                existing[key] = file;
                nextNum++;
                generated++;
            }

            SaveMap(mapPath, existing);
            Console.WriteLine($"Done. Generated {generated} voice(s). Updated map: {mapPath}");
            return 0;
        }

        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GameWatcher.Tools");
        Console.WriteLine("Commands:");
        Console.WriteLine("  gen-voices [--misses data/misses.json] [--map assets/maps/dialogue.en.json] [--voices assets/voices] [--persona assets/voices/persona.json] [--max N] [--overwrite] [--dry-run]");
        Console.WriteLine("Env:");
        Console.WriteLine("  OPENAI_API_KEY  Required unless --dry-run");
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    private static bool HasFlag(string[] args, string name) => args.Contains(name);

    private static Dictionary<string, string> LoadMap(string path)
    {
        if (!File.Exists(path)) return new();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
    }

    private static void SaveMap(string path, Dictionary<string, string> map)
    {
        var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static int NextLineNumber(string voicesDir)
    {
        var re = new Regex(@"^line_(\d{4})\.wav$", RegexOptions.IgnoreCase);
        int max = 0;
        if (!Directory.Exists(voicesDir)) return 1;
        foreach (var f in Directory.EnumerateFiles(voicesDir, "*.wav"))
        {
            var name = Path.GetFileName(f);
            var m = re.Match(name);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                if (n > max) max = n;
        }
        return max + 1;
    }

    private sealed class MissEntry
    {
        public string normalized { get; set; } = string.Empty;
    }
}
