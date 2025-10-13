using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameWatcher.Engine.Voices
{
    /// <summary>
    /// Official OpenAI voice names for gpt-4o-mini-tts model.
    /// Per API spec: https://platform.openai.com/docs/guides/text-to-speech
    /// 
    /// Language Support:
    /// Voices are optimized for English but support 50+ languages including:
    /// Afrikaans, Arabic, Chinese, French, German, Hindi, Italian, Japanese, Korean, 
    /// Portuguese, Russian, Spanish, and many more. Future voice packs can be localized
    /// to these languages by providing input text in the target language.
    /// See: https://platform.openai.com/docs/guides/text-to-speech#supported-languages
    /// </summary>
    public static class OpenAiVoices
    {
        public const string Alloy = "alloy";       // Neutral
        public const string Ash = "ash";           // Clear, expressive
        public const string Ballad = "ballad";     // Smooth, storytelling
        public const string Coral = "coral";       // Warm, upbeat
        public const string Echo = "echo";         // Masculine
        public const string Fable = "fable";       // British accent
        public const string Nova = "nova";         // Young, energetic
        public const string Onyx = "onyx";         // Deep, authoritative
        public const string Sage = "sage";         // Wise, calm
        public const string Shimmer = "shimmer";   // Soft, feminine
        public const string Verse = "verse";       // Neutral, measured

        public static readonly string[] All = { Alloy, Ash, Ballad, Coral, Echo, Fable, Nova, Onyx, Sage, Shimmer, Verse };

        public static bool IsValid(string voice) => All.Contains(voice, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// OpenAI TTS format options, filtered for NAudio compatibility.
    /// </summary>
    public static class OpenAiTtsFormats
    {
        // Supported by both OpenAI and NAudio
        public const string Wav = "wav";           // Uncompressed PCM - best for effects processing
        public const string Mp3 = "mp3";           // Lossy compression - smaller files
        public const string Flac = "flac";         // Lossless compression (requires NAudio.Flac)

        // NOT included (NAudio compatibility issues):
        // - "opus" (NAudio doesn't support)
        // - "aac" (requires Media Foundation, Windows only)
        // - "pcm" (raw PCM without header, requires custom reader)

        public static readonly string[] NAudioCompatible = { Wav, Mp3, Flac };

        public static bool IsNAudioCompatible(string format) 
            => NAudioCompatible.Contains(format, StringComparer.OrdinalIgnoreCase);
    }

    public class OpenAiTtsService
    {
        private readonly HttpClient _http = new();
        private string? _apiKey;

        public OpenAiTtsService()
        {
            ReloadApiKey();
        }

        public void ReloadApiKey()
        {
            _apiKey = TryLoadFromEnv() ?? TryLoadApiKeyFromSecrets();
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public Task<bool> GenerateWavAsync(string text, string voice, string outputPath)
            => GenerateAsync(text, voice, 1.0, OpenAiTtsFormats.Wav, outputPath);

        public Task<bool> GenerateWavAsync(string text, string voice, string instructions, string outputPath)
            => GenerateAsync(text, voice, 1.0, OpenAiTtsFormats.Wav, outputPath, instructions);

        public async Task<bool> GenerateAsync(string text, string voice, double speed, string format, string outputPath, string? instructions = null)
        {
            if (!IsConfigured) return false;

            // Validate speed range per OpenAI API spec (0.25 to 4.0)
            if (speed < 0.25 || speed > 4.0)
                throw new ArgumentOutOfRangeException(nameof(speed), speed, 
                    "Speed must be between 0.25 and 4.0 per OpenAI TTS API specification.");

            // Validate format is NAudio-compatible
            if (!OpenAiTtsFormats.IsNAudioCompatible(format))
                throw new ArgumentException(
                    $"Format '{format}' is not compatible with NAudio. Use: {string.Join(", ", OpenAiTtsFormats.NAudioCompatible)}", 
                    nameof(format));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Basic API call to OpenAI TTS (model name adaptable)
            var url = "https://api.openai.com/v1/audio/speech";

            async Task<bool> CallAsync(bool includeSpeed)
            {
                // Normalize format to lowercase for API (wav, mp3, flac)
                var fmt = format.ToLowerInvariant();
                
                // Build payload with optional instructions parameter
                object payload;
                if (!string.IsNullOrWhiteSpace(instructions))
                {
                    payload = includeSpeed
                        ? new { model = "gpt-4o-mini-tts", voice = voice, input = text, format = fmt, speed = speed, instructions = instructions }
                        : new { model = "gpt-4o-mini-tts", voice = voice, input = text, format = fmt, instructions = instructions };
                }
                else
                {
                    payload = includeSpeed
                        ? new { model = "gpt-4o-mini-tts", voice = voice, input = text, format = fmt, speed = speed }
                        : new { model = "gpt-4o-mini-tts", voice = voice, input = text, format = fmt };
                }
                
                var json = JsonSerializer.Serialize(payload);
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return false;
                using var fs = File.Create(outputPath);
                await resp.Content.CopyToAsync(fs);
                return true;
            }

            // Try including speed; if the API rejects it, retry without
            var ok = await CallAsync(includeSpeed: speed != 1.0);
            if (!ok && speed != 1.0)
            {
                ok = await CallAsync(includeSpeed: false);
            }
            return ok;
        }

        private static string? TryLoadFromEnv()
        {
            // Preferred: user-level env var configurable via GUI
            var key = Environment.GetEnvironmentVariable("GWS_OPENAI_API_KEY", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(key))
            {
                // Fallback to process env if already set
                key = Environment.GetEnvironmentVariable("GWS_OPENAI_API_KEY");
            }
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }

        private static string? TryLoadApiKeyFromSecrets()
        {
            try
            {
                var fromEnv = Environment.GetEnvironmentVariable("GAMEWATCHER_SECRETS_DIR");
                if (!string.IsNullOrWhiteSpace(fromEnv))
                {
                    var cand = Path.Combine(fromEnv, "openai-api-key.txt");
                    if (File.Exists(cand))
                    {
                        var key = File.ReadAllText(cand).Trim();
                        if (!string.IsNullOrWhiteSpace(key)) return key;
                    }
                }

                // Search upwards for Secrets/openai-api-key.txt relative to executable
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    var cand = Path.Combine(dir.FullName, "Secrets", "openai-api-key.txt");
                    if (File.Exists(cand))
                    {
                        var key = File.ReadAllText(cand).Trim();
                        if (!string.IsNullOrWhiteSpace(key)) return key;
                    }
                    dir = dir.Parent;
                }

                // Last resort: hard-coded workspace path if running from source
                var defaultPath = Path.Combine("C:\\Code Projects\\GameWatcher\\Secrets", "openai-api-key.txt");
                if (File.Exists(defaultPath))
                {
                    var key = File.ReadAllText(defaultPath).Trim();
                    if (!string.IsNullOrWhiteSpace(key)) return key;
                }
            }
            catch { }
            return null;
        }
    }
}
