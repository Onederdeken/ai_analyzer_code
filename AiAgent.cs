using System.Net.Http.Json;
using System.Text;
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
            _httpClient.Timeout = TimeSpan.FromSeconds(1000);
            _apiKey = aiSettings["OpenAiApiKey"];
        }

        public async Task<string> SendPromptAsync(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = "gpt-4.1",
                    messages = new[]
                    {
                    new { role = "user", content = prompt }
                },
                    temperature = 0.7,
                    max_tokens = 2000
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.openai.com/v1/chat/completions"
                );

                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);

                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception(json);

                return JsonDocument.Parse(json)
                    .RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                    ?.Trim() ?? "[empty]";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI ERROR: {ex}");
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
