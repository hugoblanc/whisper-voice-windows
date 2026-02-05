using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.NetworkInformation;

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
            throw new FileNotFoundException("Audio file not found", audioFilePath);

        if (fileInfo.Length < MinFileSizeBytes)
            throw new InvalidOperationException("Recording too short or empty");

        // Check network connectivity
        if (!IsNetworkAvailable())
            throw new InvalidOperationException("No internet connection. Please check your network.");

        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await SendTranscriptionRequestAsync(audioFilePath);
                return result;
            }
            catch (HttpRequestException ex) when (IsRetryableError(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                await Task.Delay(RetryDelayMs * attempt);
            }
            catch (TaskCanceledException ex) when (attempt < MaxRetries)
            {
                lastException = ex;
                await Task.Delay(RetryDelayMs * attempt);
            }
        }

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

        var audioBytes = await File.ReadAllBytesAsync(audioFilePath);
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        content.Add(audioContent, "file", Path.GetFileName(audioFilePath));
        content.Add(new StringContent(Model), "model");

        var response = await _httpClient.PostAsync(ApiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API error {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
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
