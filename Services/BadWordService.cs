using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class BadWordService
{
    private readonly HttpClient _http;
    private readonly string _api;

    public BadWordService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _api = config["HuggingFace:ApiKey"];
    }

    public async Task<bool> IsBadAsync(string text)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api-inference.huggingface.co/models/unitary/toxic-bert"
        );

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _api);

        req.Content = new StringContent(
            JsonSerializer.Serialize(new { inputs = text }),
            Encoding.UTF8,
            "application/json"
        );

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        var arr = JsonDocument.Parse(json).RootElement;

        foreach (var item in arr.EnumerateArray())
        {
            var label = item.GetProperty("label").GetString();
            var score = item.GetProperty("score").GetDouble();

            if (label == "toxic" && score > 0.55)
                return true;
        }

        return false;
    }
}
