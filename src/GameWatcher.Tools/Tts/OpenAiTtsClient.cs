using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GameWatcher.Tools.Tts;

internal sealed class OpenAiTtsClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _voice;

    public OpenAiTtsClient(string apiKey, string model = "gpt-4o-mini-tts", string voice = "alloy")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini-tts" : model;
        _voice = string.IsNullOrWhiteSpace(voice) ? "alloy" : voice;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<byte[]> SynthesizeWavAsync(string text, CancellationToken ct = default)
    {
        var url = "https://api.openai.com/v1/audio/speech";
        var payload = new
        {
            model = _model,
            input = text,
            voice = _voice,
            format = "wav"
        };
        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }
}

