using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

/// <summary>
/// Processes transcribed text using the configured OpenAI model for AI modes.
/// </summary>
public class TextProcessor
{
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
    public async Task<string> ProcessAsync(
        string text,
        AIMode mode,
        string apiKey,
        DictationContext? context = null,
        string? modelId = null,
        IReadOnlyList<string>? vocabulary = null)
    {
        // Brut mode - no processing
        if (!mode.RequiresProcessing || string.IsNullOrEmpty(mode.SystemPrompt))
        {
            Logger.Debug($"[TextProcessor] Mode '{mode.Name}' requires no processing");
            return text;
        }

        var model = ProcessingModelCatalog.Normalize(modelId);
        Logger.Info($"[TextProcessor] Processing with mode: {mode.Name}, model={model}");
        var systemPrompt = BuildSystemPrompt(mode, context, vocabulary);
        var usesCompletionTokens = ProcessingModelCatalog.UsesMaxCompletionTokens(model);

        var request = new ChatCompletionRequest
        {
            Model = model,
            Messages = new[]
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = text }
            },
            Temperature = 0.3,
            MaxTokens = usesCompletionTokens ? null : 2048,
            MaxCompletionTokens = usesCompletionTokens ? 2048 : null
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

    private static string BuildSystemPrompt(AIMode mode, DictationContext? context, IReadOnlyList<string>? vocabulary)
    {
        string systemPrompt;
        if (!mode.IsSuper)
        {
            systemPrompt = mode.SystemPrompt ?? "";
        }
        else
        {
            var builder = new StringBuilder();

            if (context?.HasSelectedText == true)
            {
                builder.AppendLine("Tu es un assistant intelligent. L'utilisateur a selectionne le texte suivant :");
                builder.AppendLine("---");
                builder.AppendLine(context.SelectedText);
                builder.AppendLine("---");
                builder.AppendLine("Il te donne une instruction vocale a appliquer sur ce texte.");
            }
            else
            {
                builder.AppendLine("Tu es un assistant intelligent. L'utilisateur te donne une instruction vocale.");
            }

            builder.AppendLine("Reponds UNIQUEMENT avec le resultat demande, rien d'autre.");

            if (context?.HasAmbientContext == true)
            {
                builder.AppendLine();
                builder.AppendLine("Contexte au moment de la dictee, a utiliser seulement si pertinent :");

                if (!string.IsNullOrWhiteSpace(context.ActiveProcessName))
                {
                    builder.AppendLine($"- Application active: {context.ActiveProcessName}");
                }

                if (!string.IsNullOrWhiteSpace(context.ActiveWindowTitle))
                {
                    builder.AppendLine($"- Titre de fenetre: {context.ActiveWindowTitle}");
                }

                if (!string.IsNullOrWhiteSpace(context.BrowserUrl))
                {
                    builder.AppendLine($"- URL navigateur: {context.BrowserUrl}");
                }

                if (!string.IsNullOrWhiteSpace(context.WorkspaceName))
                {
                    builder.AppendLine($"- Workspace: {context.WorkspaceName}");
                }

                if (!string.IsNullOrWhiteSpace(context.ProjectName))
                {
                    builder.AppendLine($"- Projet: {context.ProjectName}");
                }
            }

            systemPrompt = builder.ToString();
        }

        if (vocabulary?.Count > 0)
        {
            systemPrompt += Environment.NewLine + Environment.NewLine +
                "Termes a conserver exactement, en restaurant cette orthographe si la transcription les a deformes : " +
                string.Join(", ", vocabulary) + ".";
        }

        return systemPrompt;
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
        public int? MaxTokens { get; set; }
        public int? MaxCompletionTokens { get; set; }
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
