using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhisperVoice.Logging;

namespace WhisperVoice.Api;

/// <summary>
/// Base class for transcription providers with shared logic (retry, validation, network check)
/// </summary>
public abstract class BaseTranscriptionProvider : ITranscriptionProvider
{
    protected const int MaxRetries = 3;
    protected const int RetryDelayMs = 1000;
    protected const int MinFileSizeBytes = 1000;

    protected readonly string _apiKey;
    protected readonly HttpClient _httpClient;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public abstract string ProviderId { get; }
    public abstract string DisplayName { get; }
    public abstract string ApiKeyHelpUrl { get; }

    /// <summary>API endpoint URL for transcription</summary>
    protected abstract string ApiEndpoint { get; }

    /// <summary>Model name to use for transcription</summary>
    protected abstract string ModelName { get; }

    protected BaseTranscriptionProvider(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        ConfigureHttpClient(_httpClient);
    }

    /// <summary>
    /// Configure HTTP client with provider-specific authentication headers
    /// </summary>
    protected abstract void ConfigureHttpClient(HttpClient client);

    public abstract bool ValidateApiKeyFormat(string apiKey, out string? errorMessage);

    public async Task<string> TranscribeAsync(string audioFilePath)
    {
        ValidateAudioFile(audioFilePath);
        CheckNetworkAvailability();
        return await TranscribeWithRetryAsync(audioFilePath);
    }

    protected void ValidateAudioFile(string audioFilePath)
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

    protected void CheckNetworkAvailability()
    {
        if (!IsNetworkAvailable())
        {
            Logger.Error("No network connectivity detected");
            throw new InvalidOperationException("No internet connection. Please check your network.");
        }
    }

    protected static bool IsNetworkAvailable()
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

    public virtual async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync()
    {
        try
        {
            if (!IsNetworkAvailable())
                return (false, "No internet connection");

            await TestApiCredentialsAsync();
            return (true, null);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            return (false, "Invalid API key");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("insufficient_quota"))
        {
            return (false, "No API credits remaining");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("403"))
        {
            return (false, "API key does not have access to this model");
        }
        catch (TaskCanceledException)
        {
            return (false, "Connection timed out");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Test API credentials by making a lightweight API call.
    /// Override in providers to use provider-specific endpoints.
    /// </summary>
    protected abstract Task TestApiCredentialsAsync();

    protected async Task<string> TranscribeWithRetryAsync(string audioFilePath)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Logger.Info($"API request attempt {attempt}/{MaxRetries} to {ProviderId}");
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

    protected virtual async Task<string> SendTranscriptionRequestAsync(string audioFilePath)
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
        Logger.Debug($"Sending {audioBytes.Length} bytes to {ProviderId} API");

        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        content.Add(audioContent, "file", Path.GetFileName(audioFilePath));
        content.Add(new StringContent(ModelName), "model");

        Logger.Debug($"POST {ApiEndpoint} with model={ModelName}");
        var response = await _httpClient.PostAsync(ApiEndpoint, content);
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

    protected virtual bool IsRetryableError(HttpRequestException ex)
    {
        // Retry on 5xx server errors and network errors
        if (ex.Message.Contains("5") || ex.Message.Contains("network"))
            return true;

        return false;
    }

    /// <summary>
    /// Format exception with user-friendly message. Override for provider-specific error handling.
    /// </summary>
    protected virtual Exception FormatException(Exception? ex)
    {
        if (ex == null)
            return new InvalidOperationException("Transcription failed after retries");

        var message = ex.Message;

        if (message.Contains("401"))
            return new InvalidOperationException($"Invalid API key. Please check your {DisplayName} API key in settings.");

        if (message.Contains("429"))
            return new InvalidOperationException("Rate limit exceeded. Please wait a moment and try again.");

        if (message.Contains("insufficient_quota") || message.Contains("402"))
            return new InvalidOperationException($"No API credits remaining. Please add credits to your {DisplayName} account.");

        if (ex is TaskCanceledException)
            return new InvalidOperationException("Request timed out. Please check your internet connection.");

        return ex;
    }

    protected class TranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
