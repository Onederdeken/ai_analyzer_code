using System.Net.Http.Json;
using System.Text.Json;
namespace frontAIagent
{

    public class AiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AiClient(HttpClient httpClient, IConfiguration configuration)
        {
            var aiSettings = configuration.GetSection("AiSettings");
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(300);
            _apiKey = aiSettings["OpenAiApiKey"];
        }

        public async Task<string> GetAiResponseAsync(string prompt, string model = "gpt-3.5-turbo", double temperature = 0.7, int maxTokens = 2000)
        {
            try
            {
                Console.WriteLine($"Sending AI request to {model}");

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            },
                    temperature = temperature,
                    max_tokens = maxTokens
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(request);
                var jsonString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Детальный вывод ошибки
                    var errorMessage = $"""
            ❌ OPENAI API ERROR:
            Status Code: {(int)response.StatusCode} {response.StatusCode}
            Request URL: {request.RequestUri}
            API Key: {_apiKey?.Substring(0, Math.Min(10, _apiKey?.Length ?? 0))}... (first 10 chars)
            Model: {model}
            
            Response Body:
            {jsonString}

            Possible Reasons:
            • Invalid API key
            • API key expired
            • Insufficient credits
            • Model not available
            • Account suspended
            • Regional restrictions
            """;

                    Console.WriteLine(errorMessage);
                    throw new Exception(errorMessage);
                }

                var content = JsonDocument.Parse(jsonString)
                    .RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                    throw new Exception("AI returned empty response");

                Console.WriteLine($" AI response received ({content.Length} chars)");
                return content.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error in AI client: {ex.Message}");
                throw;
            }
        }
    }

    public class AiRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Provider { get; set; } = "openrouter"; // openrouter, openai, mistral
        public double? Temperature { get; set; } = 0.7; // Средняя температура
        public int? MaxTokens { get; set; } = 2000;
        public double? TopP { get; set; } = 1.0;
        public string? ReferenceUrl { get; set; }
        public string? AppName { get; set; }
    }

    public class ProviderConfig
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
    }
}
