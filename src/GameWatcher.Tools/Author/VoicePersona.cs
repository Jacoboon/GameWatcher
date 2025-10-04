using System.Text.Json;

namespace GameWatcher.Tools.Author;

internal sealed class VoicePersona
{
    public string Model { get; set; } = "gpt-4o-mini-tts";
    public string Voice { get; set; } = "alloy";

    public static VoicePersona Load(string path)
    {
        if (!File.Exists(path)) return new VoicePersona();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VoicePersona>(json) ?? new VoicePersona();
    }
}

