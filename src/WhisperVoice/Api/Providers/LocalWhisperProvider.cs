using WhisperVoice.Logging;

namespace WhisperVoice.Api.Providers;

/// <summary>
/// Local Whisper provider using whisper.cpp or Whisper.net for offline transcription
/// </summary>
public class LocalWhisperProvider : ITranscriptionProvider
{
    private readonly string _modelPath;
    private const int MinFileSizeBytes = 1000;

    public string ProviderId => "local";
    public string DisplayName => "Local (Offline)";
    public string ApiKeyHelpUrl => ""; // No API key needed for local

    public LocalWhisperProvider(string modelPath = "")
    {
        _modelPath = modelPath;
    }

    public bool ValidateApiKeyFormat(string apiKey, out string? errorMessage)
    {
        // Local provider doesn't need an API key
        errorMessage = null;
        return true;
    }

    public async Task<string> TranscribeAsync(string audioFilePath)
    {
        ValidateAudioFile(audioFilePath);

        // Check if model exists
        if (string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath))
        {
            throw new InvalidOperationException(
                "Whisper model not found. Please download a model in Preferences > Local Mode.");
        }

        try
        {
            Logger.Info($"Starting local transcription with model: {Path.GetFileName(_modelPath)}");

            // TODO: Integrate with Whisper.net NuGet package
            // For now, this is a placeholder that needs Whisper.net implementation
            //
            // Example integration:
            // using var whisperFactory = WhisperFactory.FromPath(_modelPath);
            // using var processor = whisperFactory.CreateBuilder()
            //     .WithLanguage("fr")
            //     .Build();
            //
            // using var fileStream = File.OpenRead(audioFilePath);
            // var segments = await processor.ProcessAsync(fileStream);
            // return string.Join(" ", segments.Select(s => s.Text));

            throw new NotImplementedException(
                "Local Whisper mode requires Whisper.net NuGet package integration. " +
                "This feature will be available in a future update. " +
                "For now, please use OpenAI or Mistral providers."
            );
        }
        catch (Exception ex)
        {
            Logger.Error("Local transcription failed", ex);
            throw new InvalidOperationException($"Local transcription failed: {ex.Message}", ex);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync()
    {
        try
        {
            // Check if model file exists
            if (string.IsNullOrEmpty(_modelPath))
            {
                return (false, "No model selected. Please download a Whisper model first.");
            }

            if (!File.Exists(_modelPath))
            {
                return (false, $"Model file not found: {_modelPath}");
            }

            // TODO: Validate model file format
            // For now, just check file exists
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void ValidateAudioFile(string audioFilePath)
    {
        var fileInfo = new FileInfo(audioFilePath);
        if (!fileInfo.Exists)
        {
            Logger.Error($"Audio file not found: {audioFilePath}");
            throw new FileNotFoundException("Audio file not found", audioFilePath);
        }

        Logger.Debug($"Audio file size: {fileInfo.Length} bytes");

        if (fileInfo.Length < MinFileSizeBytes)
        {
            Logger.Warn($"Audio file too small: {fileInfo.Length} bytes (min: {MinFileSizeBytes})");
            throw new InvalidOperationException("Recording too short or empty");
        }
    }
}

/// <summary>
/// Model information for local Whisper models
/// </summary>
public record WhisperModel(
    string Name,
    string Size,
    string Url,
    string FileName
)
{
    /// <summary>
    /// Available Whisper models from Hugging Face
    /// Based on ggml models compatible with whisper.cpp
    /// </summary>
    public static readonly WhisperModel[] AvailableModels =
    {
        new("Tiny (75 MB)", "75 MB",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            "ggml-tiny.bin"),

        new("Base (142 MB)", "142 MB",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            "ggml-base.bin"),

        new("Small (466 MB)", "466 MB",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            "ggml-small.bin"),

        new("Medium (1.5 GB)", "1.5 GB",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
            "ggml-medium.bin"),

        new("Large (2.9 GB)", "2.9 GB",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
            "ggml-large-v3.bin")
    };

    /// <summary>
    /// Get the local path where models are stored
    /// </summary>
    public static string ModelsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhisperVoice",
        "models"
    );

    /// <summary>
    /// Get the full path for this model file
    /// </summary>
    public string LocalPath => Path.Combine(ModelsDirectory, FileName);

    /// <summary>
    /// Check if this model is downloaded
    /// </summary>
    public bool IsDownloaded => File.Exists(LocalPath);
}
