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

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api);

        req.Content = new StringContent(
            JsonSerializer.Serialize(new { inputs = text }),
            Encoding.UTF8,
            "application/json"
        );

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        // Nếu trả về HTML hoặc rỗng → KHÔNG PHẢI JSON → tránh crash
        if (string.IsNullOrWhiteSpace(json) || json.TrimStart().StartsWith("<"))
        {
            Console.WriteLine("⚠ HF trả về HTML / lỗi:");
            Console.WriteLine(json);
            return false; // không block tin nhắn, nhưng không crash
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch
        {
            Console.WriteLine("⚠ JSON parse error:");
            Console.WriteLine(json);
            return false;
        }

        var arr = doc.RootElement;

        // trường hợp model đang load: trả về JSON nhưng không có dữ liệu dự đoán
        if (!arr.ValueKind.Equals(JsonValueKind.Array))
        {
            Console.WriteLine("⚠ Unexpected JSON format:");
            Console.WriteLine(json);
            return false;
        }

        foreach (var item in arr[0].EnumerateArray())
        {
            string label = item.GetProperty("label").GetString();
            double score = item.GetProperty("score").GetDouble();

            if (label == "toxic" && score > 0.55)
                return true;
        }

        return false;
    }

}
