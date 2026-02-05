using System.Net.Http.Headers;

namespace WhisperVoice.Api.Providers;

/// <summary>
/// OpenAI Whisper transcription provider
/// </summary>
public class OpenAIProvider : BaseTranscriptionProvider
{
    public override string ProviderId => "openai";
    public override string DisplayName => "OpenAI Whisper";
    public override string ApiKeyHelpUrl => "https://platform.openai.com/api-keys";

    protected override string ApiEndpoint => "https://api.openai.com/v1/audio/transcriptions";
    protected override string ModelName => "gpt-4o-mini-transcribe";

    public OpenAIProvider(string apiKey) : base(apiKey) { }

    protected override void ConfigureHttpClient(HttpClient client)
    {
        // OpenAI uses Bearer token authentication
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public override bool ValidateApiKeyFormat(string apiKey, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            errorMessage = "API key is required.";
            return false;
        }

        if (!apiKey.StartsWith("sk-") && !apiKey.StartsWith("sk-proj-"))
        {
            errorMessage = "OpenAI API key should start with 'sk-' or 'sk-proj-'.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    protected override async Task TestApiCredentialsAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        var response = await client.GetAsync("https://api.openai.com/v1/models");
        response.EnsureSuccessStatusCode();
    }
}
