using NAudio.Wave;
using WhisperVoice.Logging;

namespace WhisperVoice.Audio;

public class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempFilePath;
    private bool _isRecording;
    private readonly ManualResetEvent _recordingStoppedEvent = new(true);
    private readonly object _lock = new();

    public bool IsRecording => _isRecording;

    public static bool IsMicrophoneAvailable()
    {
        try
        {
            return WaveInEvent.DeviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetMicrophoneError()
    {
        Logger.Debug($"Checking microphone availability. Device count: {WaveInEvent.DeviceCount}");

        if (WaveInEvent.DeviceCount == 0)
        {
            Logger.Warn("No microphone detected");
            return "No microphone detected. Please connect a microphone and restart the app.";
        }

        // Check Windows microphone permission
        try
        {
            using var testWaveIn = new WaveInEvent();
            testWaveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            Logger.Debug("Microphone access test passed");
        }
        catch (Exception ex)
        {
            Logger.Error("Microphone access test failed", ex);
            if (ex.Message.Contains("denied") || ex.Message.Contains("access"))
                return "Microphone access denied. Please allow microphone access in Windows Settings > Privacy > Microphone.";
            return $"Microphone error: {ex.Message}";
        }

        return null;
    }

    public void StartRecording()
    {
        if (_isRecording) return;

        // Wait for any previous recording to fully stop
        _recordingStoppedEvent.WaitOne(2000);

        lock (_lock)
        {
            if (_isRecording) return;

            if (!IsMicrophoneAvailable())
            {
                throw new InvalidOperationException(GetMicrophoneError() ?? "No microphone available");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "WhisperVoice");
            Directory.CreateDirectory(tempDir);
            _tempFilePath = Path.Combine(tempDir, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, mono
            };

            _writer = new WaveFileWriter(_tempFilePath, _waveIn.WaveFormat);
            _recordingStoppedEvent.Reset();

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isRecording = true;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;
        }
        _recordingStoppedEvent.Set();
    }

    public string? StopRecording()
    {
        if (!_isRecording) return null;

        _isRecording = false;
        _waveIn?.StopRecording();

        // Wait for file to be fully written (max 2 seconds)
        _recordingStoppedEvent.WaitOne(2000);

        return _tempFilePath;
    }

    public static void CleanupTempFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public static void CleanupAllTempFiles()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "WhisperVoice");
            if (Directory.Exists(tempDir))
            {
                foreach (var file in Directory.GetFiles(tempDir, "*.wav"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        _waveIn?.StopRecording();
        _writer?.Dispose();
        _waveIn?.Dispose();
    }
}
