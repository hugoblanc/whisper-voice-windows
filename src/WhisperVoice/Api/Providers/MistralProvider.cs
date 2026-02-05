namespace WhisperVoice.Api.Providers;

/// <summary>
/// Mistral Voxtral transcription provider
/// </summary>
public class MistralProvider : BaseTranscriptionProvider
{
    public override string ProviderId => "mistral";
    public override string DisplayName => "Mistral Voxtral";
    public override string ApiKeyHelpUrl => "https://console.mistral.ai/api-keys";

    protected override string ApiEndpoint => "https://api.mistral.ai/v1/audio/transcriptions";
    protected override string ModelName => "voxtral-mini-latest";

    public MistralProvider(string apiKey) : base(apiKey) { }

    protected override void ConfigureHttpClient(HttpClient client)
    {
        // Mistral uses x-api-key header (NOT Bearer token!)
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    public override bool ValidateApiKeyFormat(string apiKey, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            errorMessage = "API key is required.";
            return false;
        }

        // Mistral keys don't have a specific prefix, just check minimum length
        if (apiKey.Length < 10)
        {
            errorMessage = "Mistral API key appears too short.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
