using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class BadWordService
{
    private readonly HttpClient _http;
    private readonly string _api;   // HuggingFace API key

    public BadWordService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _api = config["HuggingFace:ApiKey"];
    }

    // ===========================
    // Kiểm tra song song cả 2 model
    // ===========================
    public async Task<bool> IsBadAsync(string text)
    {
        var tasks = new[]
        {
            CheckHuggingFaceAsync("https://api-inference.huggingface.co/models/unitary/toxic-bert", text),
            CheckHuggingFaceAsync("https://api-inference.huggingface.co/models/vijjj1/toxic-comment-phobert", text)
        };

        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);
    }

    // ===========================
    // Kiểm tra 1 model Hugging Face
    // ===========================
    private async Task<bool> CheckHuggingFaceAsync(string modelUrl, string text)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, modelUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api);
            req.Content = new StringContent(
                JsonSerializer.Serialize(new { inputs = text }),
                Encoding.UTF8,
                "application/json"
            );

            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"--- HF Model: {modelUrl} ---");
            Console.WriteLine($"Tin nhắn: {text}");
            Console.WriteLine($"Raw JSON: {json}");

            if (string.IsNullOrWhiteSpace(json) || json.TrimStart().StartsWith("<"))
            {
                Console.WriteLine("⚠ HF trả về HTML hoặc lỗi, bỏ qua.");
                return false;
            }

            JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return false;

            JsonElement first = root[0];

            // Nếu first là object → model trả kiểu đơn giản
            if (first.ValueKind == JsonValueKind.Object)
            {
                return ParseLabelScore(first);
            }
            // Nếu first là array → model trả nested array
            else if (first.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in first.EnumerateArray())
                {
                    if (ParseLabelScore(item))
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ HF exception: {ex.Message}");
            return false;
        }
    }

    // ===========================
    // Kiểm tra label & score
    // ===========================
    private bool ParseLabelScore(JsonElement item)
    {
        if (!item.TryGetProperty("label", out var labelProp) ||
            !item.TryGetProperty("score", out var scoreProp))
            return false;

        string label = labelProp.GetString()?.ToLower() ?? "";
        double score = scoreProp.GetDouble();

        Console.WriteLine($"Label: {label}, Score: {score}");

        if ((label.Contains("toxic") || label.Contains("hate") || label == "label_1") && score > 0.55)
            return true;

        return false;
    }
}
