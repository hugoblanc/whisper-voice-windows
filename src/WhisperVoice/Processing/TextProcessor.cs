using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

/// <summary>
/// Processes transcribed text using GPT-4o-mini for AI modes
/// </summary>
public class TextProcessor
{
    private const string Model = "gpt-4o-mini";
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";
    private const int TimeoutSeconds = 30;

    private readonly HttpClient _httpClient;

    public TextProcessor()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
        };
    }

    /// <summary>
    /// Process text with the specified AI mode
    /// </summary>
    /// <param name="text">Raw transcription text</param>
    /// <param name="mode">AI processing mode</param>
    /// <param name="apiKey">OpenAI API key</param>
    /// <returns>Processed text, or original text if mode doesn't require processing</returns>
    public async Task<string> ProcessAsync(string text, AIMode mode, string apiKey)
    {
        // Brut mode - no processing
        if (!mode.RequiresProcessing || string.IsNullOrEmpty(mode.SystemPrompt))
        {
            Logger.Debug($"[TextProcessor] Mode '{mode.Name}' requires no processing");
            return text;
        }

        Logger.Info($"[TextProcessor] Processing with mode: {mode.Name}");

        var request = new ChatCompletionRequest
        {
            Model = Model,
            Messages = new[]
            {
                new ChatMessage { Role = "system", Content = mode.SystemPrompt },
                new ChatMessage { Role = "user", Content = text }
            },
            Temperature = 0.3,
            MaxTokens = 2048
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = content;

        try
        {
            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"[TextProcessor] API error: {response.StatusCode} - {responseBody}");
                throw new HttpRequestException($"API error {(int)response.StatusCode}: {ExtractErrorMessage(responseBody)}");
            }

            var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, JsonOptions);
            var processedText = result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(processedText))
            {
                Logger.Warn("[TextProcessor] Empty response from API, returning original text");
                return text;
            }

            Logger.Info($"[TextProcessor] Success - processed {text.Length} -> {processedText.Length} chars");
            return processedText;
        }
        catch (TaskCanceledException)
        {
            Logger.Error("[TextProcessor] Request timed out");
            throw new InvalidOperationException("AI processing timed out. Returning raw transcription.");
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"[TextProcessor] HTTP error: {ex.Message}");
            throw;
        }
    }

    private static string ExtractErrorMessage(string responseBody)
    {
        try
        {
            var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? responseBody;
            }
        }
        catch { }
        return responseBody;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Request/Response DTOs

    private class ChatCompletionRequest
    {
        public string Model { get; set; } = "";
        public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
    }

    private class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private class ChatCompletionResponse
    {
        public ChatChoice[]? Choices { get; set; }
    }

    private class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    #endregion
}
