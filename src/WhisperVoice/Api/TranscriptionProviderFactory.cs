using WhisperVoice.Api.Providers;
using WhisperVoice.Config;

namespace WhisperVoice.Api;

/// <summary>
/// Factory for creating transcription providers
/// </summary>
public static class TranscriptionProviderFactory
{
    private static readonly Dictionary<string, Func<string, ITranscriptionProvider>> _providers = new()
    {
        ["openai"] = apiKey => new OpenAIProvider(apiKey),
        ["mistral"] = apiKey => new MistralProvider(apiKey)
    };

    /// <summary>
    /// Get metadata for all available providers (for UI display)
    /// </summary>
    public static IReadOnlyList<ProviderInfo> GetAvailableProviders()
    {
        return new List<ProviderInfo>
        {
            new("openai", "OpenAI Whisper", "https://platform.openai.com/api-keys"),
            new("mistral", "Mistral Voxtral", "https://console.mistral.ai/api-keys")
        };
    }

    /// <summary>
    /// Create a transcription provider from application configuration
    /// </summary>
    public static ITranscriptionProvider Create(AppConfig config)
    {
        var providerId = config.Provider ?? "openai";
        var apiKey = config.GetApiKeyForProvider(providerId);
        return Create(providerId, apiKey);
    }

    /// <summary>
    /// Create a transcription provider by ID
    /// </summary>
    public static ITranscriptionProvider Create(string providerId, string apiKey)
    {
        if (!_providers.TryGetValue(providerId.ToLowerInvariant(), out var factory))
        {
            throw new ArgumentException(
                $"Unknown provider: {providerId}. Available providers: {string.Join(", ", _providers.Keys)}");
        }
        return factory(apiKey);
    }

    /// <summary>
    /// Validate API key format for a specific provider (without creating a full instance)
    /// </summary>
    public static bool ValidateApiKey(string providerId, string apiKey, out string? errorMessage)
    {
        var provider = Create(providerId, apiKey);
        return provider.ValidateApiKeyFormat(apiKey, out errorMessage);
    }

    /// <summary>
    /// Check if a provider ID is valid
    /// </summary>
    public static bool IsValidProvider(string providerId)
    {
        return _providers.ContainsKey(providerId.ToLowerInvariant());
    }
}
