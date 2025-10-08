using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameWatcher.AuthorStudio.Services
{
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
            => GenerateAsync(text, voice, 1.0, "wav", outputPath);

        public async Task<bool> GenerateAsync(string text, string voice, double speed, string format, string outputPath)
        {
            if (!IsConfigured) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Basic API call to OpenAI TTS (model name adaptable)
            var url = "https://api.openai.com/v1/audio/speech";

            async Task<bool> CallAsync(bool includeSpeed)
            {
                var fmt = string.Equals(format, "mp3", StringComparison.OrdinalIgnoreCase) ? "mp3" : "wav";
                object payload = includeSpeed
                    ? new { model = "gpt-4o-mini-tts", voice = voice, input = text, format = fmt, speed = speed }
                    : new { model = "gpt-4o-mini-tts", voice = voice, input = text, format = fmt };
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
