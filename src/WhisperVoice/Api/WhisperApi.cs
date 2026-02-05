using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.NetworkInformation;
using WhisperVoice.Logging;

namespace WhisperVoice.Api;

public class WhisperApi
{
    private const string ApiUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string Model = "gpt-4o-mini-transcribe";
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;
    private const int MinFileSizeBytes = 1000;

    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true // Handle both "text" and "Text"
    };

    public WhisperApi(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> TranscribeAsync(string audioFilePath)
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

        // Check network connectivity
        if (!IsNetworkAvailable())
        {
            Logger.Error("No network connectivity detected");
            throw new InvalidOperationException("No internet connection. Please check your network.");
        }

        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Logger.Info($"API request attempt {attempt}/{MaxRetries}");
                var result = await SendTranscriptionRequestAsync(audioFilePath);
                Logger.Info($"API request successful on attempt {attempt}");
                return result;
            }
            catch (HttpRequestException ex) when (IsRetryableError(ex) && attempt < MaxRetries)
            {
                Logger.Warn($"API request failed (attempt {attempt}), retrying: {ex.Message}");
                lastException = ex;
                await Task.Delay(RetryDelayMs * attempt);
            }
            catch (TaskCanceledException ex) when (attempt < MaxRetries)
            {
                Logger.Warn($"API request timed out (attempt {attempt}), retrying");
                lastException = ex;
                await Task.Delay(RetryDelayMs * attempt);
            }
        }

        Logger.Error("All API retry attempts failed", lastException);
        throw FormatException(lastException);
    }

    private static bool IsNetworkAvailable()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            return true; // Assume available if check fails
        }
    }

    private static Exception FormatException(Exception? ex)
    {
        if (ex == null)
            return new InvalidOperationException("Transcription failed after retries");

        // Make error messages more user-friendly
        var message = ex.Message;

        if (message.Contains("401"))
            return new InvalidOperationException("Invalid API key. Please check your OpenAI API key in settings.");

        if (message.Contains("429"))
            return new InvalidOperationException("Rate limit exceeded. Please wait a moment and try again.");

        if (message.Contains("insufficient_quota") || message.Contains("402"))
            return new InvalidOperationException("No API credits remaining. Please add credits to your OpenAI account.");

        if (ex is TaskCanceledException)
            return new InvalidOperationException("Request timed out. Please check your internet connection.");

        return ex;
    }

    private async Task<string> SendTranscriptionRequestAsync(string audioFilePath)
    {
        using var content = new MultipartFormDataContent();

        // Use FileShare.ReadWrite to avoid conflicts if the file handle is still being released
        byte[] audioBytes;
        using (var fs = new FileStream(audioFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var ms = new MemoryStream())
        {
            await fs.CopyToAsync(ms);
            audioBytes = ms.ToArray();
        }
        Logger.Debug($"Sending {audioBytes.Length} bytes to API");

        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        content.Add(audioContent, "file", Path.GetFileName(audioFilePath));
        content.Add(new StringContent(Model), "model");

        Logger.Debug($"POST {ApiUrl} with model={Model}");
        var response = await _httpClient.PostAsync(ApiUrl, content);
        Logger.Debug($"Response status: {(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Logger.Error($"API error response: {errorBody}");
            throw new HttpRequestException($"API error {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        Logger.Debug($"API response: {responseJson}");

        var result = JsonSerializer.Deserialize<TranscriptionResponse>(responseJson, JsonOptions);

        return result?.Text ?? throw new InvalidOperationException("Empty transcription response");
    }

    private static bool IsRetryableError(HttpRequestException ex)
    {
        // Retry on 5xx server errors and network errors
        if (ex.Message.Contains("5") || ex.Message.Contains("network"))
            return true;

        return false;
    }

    private class TranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
