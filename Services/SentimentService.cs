using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DoAnChuyenNganh.Services
{
    public class SentimentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public SentimentService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["HuggingFace:ApiKey"];
        }

        public async Task<(string label, double score)> AnalyzeAsync(string text)
        {
            var url = "https://api-inference.huggingface.co/models/distilbert-base-uncased-finetuned-sst-2-english";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var body = JsonSerializer.Serialize(new { inputs = text });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // gọi API
            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            // ❗ 1) Nếu HTML → model đang warm
            if (responseString.StartsWith("<"))
            {
                await Task.Delay(1500);
                response = await _httpClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }

            // ❗ 2) Nếu JSON chứa lỗi
            if (responseString.Contains("\"error\""))
            {
                // Model loading → retry
                if (responseString.Contains("loading"))
                {
                    await Task.Delay(1500);
                    response = await _httpClient.SendAsync(request);
                    responseString = await response.Content.ReadAsStringAsync();

                    // Nếu vẫn lỗi → trả neutral
                    if (responseString.Contains("\"error\""))
                        return ("neutral", 0.5);
                }
                else
                {
                    // Model lỗi khác → trả neutral
                    return ("neutral", 0.5);
                }
            }

            // ❗ 3) Nếu đầu vào không phải JSON array → trả neutral
            if (!responseString.TrimStart().StartsWith("["))
            {
                return ("neutral", 0.5);
            }

            // ❗ 4) Parse an toàn
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(responseString);
            }
            catch
            {
                return ("neutral", 0.5);
            }

            // Nếu rỗng
            if (!doc.RootElement.EnumerateArray().Any())
                return ("neutral", 0.5);

            var first = doc.RootElement[0][0];

            string label = first.GetProperty("label").GetString()!;
            double score = first.GetProperty("score").GetDouble();

            return (label, score);
        }
    }
}
