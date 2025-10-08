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
        private readonly string? _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<bool> GenerateWavAsync(string text, string voice, string outputPath)
        {
            if (!IsConfigured) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Basic API call to OpenAI TTS (model name adaptable)
            var url = "https://api.openai.com/v1/audio/speech";

            var payload = new
            {
                model = "gpt-4o-mini-tts",
                voice = voice,
                input = text,
                format = "wav"
            };
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
    }
}

