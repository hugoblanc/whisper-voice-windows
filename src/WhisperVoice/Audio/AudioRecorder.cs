using NAudio.Wave;

namespace WhisperVoice.Audio;

public class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempFilePath;
    private bool _isRecording;

    public bool IsRecording => _isRecording;

    public void StartRecording()
    {
        if (_isRecording) return;

        var tempDir = Path.Combine(Path.GetTempPath(), "WhisperVoice");
        Directory.CreateDirectory(tempDir);
        _tempFilePath = Path.Combine(tempDir, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, mono
        };

        _writer = new WaveFileWriter(_tempFilePath, _waveIn.WaveFormat);

        _waveIn.DataAvailable += (sender, e) =>
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        };

        _waveIn.RecordingStopped += (sender, e) =>
        {
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;
        };

        _waveIn.StartRecording();
        _isRecording = true;
    }

    public string? StopRecording()
    {
        if (!_isRecording) return null;

        _isRecording = false;
        _waveIn?.StopRecording();

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
