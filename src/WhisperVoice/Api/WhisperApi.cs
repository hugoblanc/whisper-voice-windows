using System.Net.Http.Headers;
using System.Text.Json;

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

        throw lastException ?? new InvalidOperationException("Transcription failed after retries");
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
        var result = JsonSerializer.Deserialize<TranscriptionResponse>(responseJson);

        return result?.Text ?? throw new InvalidOperationException("Empty transcription response");
    }

    private static bool IsRetryableError(HttpRequestException ex)
    {
        // Retry on 5xx server errors
        if (ex.Message.Contains("5"))
            return true;

        return false;
    }

    private class TranscriptionResponse
    {
        public string? Text { get; set; }
    }
}
