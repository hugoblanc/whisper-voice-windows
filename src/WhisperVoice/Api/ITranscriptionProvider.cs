namespace WhisperVoice.Api;

/// <summary>
/// Interface for transcription providers (OpenAI, Mistral, etc.)
/// </summary>
public interface ITranscriptionProvider
{
    /// <summary>Provider identifier (e.g., "openai", "mistral")</summary>
    string ProviderId { get; }

    /// <summary>Human-readable provider name (e.g., "OpenAI Whisper")</summary>
    string DisplayName { get; }

    /// <summary>URL where users can get their API key</summary>
    string ApiKeyHelpUrl { get; }

    /// <summary>
    /// Validates the API key format (not actual validity with the API)
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if format is valid</returns>
    bool ValidateApiKeyFormat(string apiKey, out string? errorMessage);

    /// <summary>
    /// Transcribe an audio file to text
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file (WAV format)</param>
    /// <returns>Transcribed text</returns>
    Task<string> TranscribeAsync(string audioFilePath);
}

/// <summary>
/// Provider metadata for UI display
/// </summary>
public record ProviderInfo(string Id, string DisplayName, string ApiKeyHelpUrl);
