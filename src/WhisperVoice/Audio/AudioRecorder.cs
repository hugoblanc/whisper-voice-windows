using System.Diagnostics;
using NAudio.Wave;
using WhisperVoice.Config;
using WhisperVoice.Logging;

namespace WhisperVoice.Audio;

public class AudioRecorder : IDisposable
{
    private const int SampleRate = 16000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int BufferMilliseconds = 50;
    private const int NumberOfBuffers = 3;
    private const int PreRollMilliseconds = 350;
    private const int BalancedIdleReleaseMilliseconds = 180_000;

    private readonly WaveFormat _waveFormat = new(SampleRate, BitsPerSample, Channels);
    private readonly ManualResetEvent _recordingStoppedEvent = new(true);
    private readonly Queue<byte[]> _preRollBuffers = new();
    private readonly object _lock = new();
    private readonly int _maxPreRollBytes = SampleRate * Channels * (BitsPerSample / 8) * PreRollMilliseconds / 1000;

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempFilePath;
    private bool _isCaptureRunning;
    private bool _isRecording;
    private bool _disposed;
    private int _preRollBytes;
    private Task? _warmUpTask;
    private System.Threading.Timer? _idleReleaseTimer;
    private AudioCaptureMode _captureMode = AudioCaptureMode.Instant;

    public bool IsRecording
    {
        get
        {
            lock (_lock) return _isRecording;
        }
    }

    public bool IsWarmedUp
    {
        get
        {
            lock (_lock) return _isCaptureRunning;
        }
    }

    public event Action<float>? AudioLevelChanged;

    public void SetCaptureMode(AudioCaptureMode captureMode)
    {
        if (!Enum.IsDefined(captureMode))
        {
            captureMode = AudioCaptureMode.Instant;
        }

        var shouldStopIdleCapture = false;
        var shouldScheduleIdleRelease = false;

        lock (_lock)
        {
            if (_disposed) return;

            _captureMode = captureMode;
            shouldStopIdleCapture = captureMode == AudioCaptureMode.Privacy && _isCaptureRunning && !_isRecording;
            shouldScheduleIdleRelease = captureMode == AudioCaptureMode.Balanced && _isCaptureRunning && !_isRecording;
        }

        Logger.Info($"Audio capture mode set to {captureMode}");

        if (shouldStopIdleCapture)
        {
            StopCaptureIfIdle("privacy mode selected");
        }
        else if (shouldScheduleIdleRelease)
        {
            ScheduleIdleRelease();
        }
    }

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

        try
        {
            using var testWaveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels)
            };
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

    /// <summary>
    /// Opens the microphone in the background so StartRecording only has to
    /// create a WAV writer. This removes the slow device initialization from
    /// the hotkey path and keeps a short pre-roll to preserve first syllables.
    /// </summary>
    public Task WarmUpAsync()
    {
        lock (_lock)
        {
            if (_disposed || _isCaptureRunning || _captureMode == AudioCaptureMode.Privacy)
            {
                return Task.CompletedTask;
            }

            if (_warmUpTask is { IsCompleted: false })
            {
                return _warmUpTask;
            }

            _warmUpTask = Task.Run(WarmUpCapture);
            return _warmUpTask;
        }
    }

    public void StartRecording()
    {
        CancelIdleReleaseTimer();
        EnsureCaptureRunning();

        lock (_lock)
        {
            ThrowIfDisposed();
            if (_isRecording) return;

            var tempDir = Path.Combine(Path.GetTempPath(), "WhisperVoice");
            Directory.CreateDirectory(tempDir);
            _tempFilePath = Path.Combine(tempDir, $"recording_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.wav");

            _writer = new WaveFileWriter(_tempFilePath, _waveFormat);
            foreach (var buffer in _preRollBuffers)
            {
                _writer.Write(buffer, 0, buffer.Length);
            }

            Logger.Debug($"Audio recording started with {_preRollBytes} pre-roll bytes");
            _preRollBuffers.Clear();
            _preRollBytes = 0;
            _isRecording = true;
            _recordingStoppedEvent.Reset();
        }
    }

    public string? StopRecording()
    {
        WaveFileWriter? writerToDispose;
        string? tempFilePath;
        AudioCaptureMode captureMode;

        lock (_lock)
        {
            if (!_isRecording) return null;

            _isRecording = false;
            writerToDispose = _writer;
            _writer = null;
            tempFilePath = _tempFilePath;
            _tempFilePath = null;
            captureMode = _captureMode;
            _recordingStoppedEvent.Set();
        }

        try
        {
            writerToDispose?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to finalize audio file", ex);
        }

        ApplyPostRecordingCapturePolicy(captureMode);
        return tempFilePath;
    }

    private void EnsureCaptureRunning()
    {
        Task? warmUpTask;
        lock (_lock)
        {
            ThrowIfDisposed();
            if (_isCaptureRunning) return;
            warmUpTask = _warmUpTask;
        }

        if (warmUpTask is { IsCompleted: false })
        {
            warmUpTask.GetAwaiter().GetResult();
        }

        lock (_lock)
        {
            if (_isCaptureRunning) return;
        }

        WarmUpCapture();
    }

    private void WarmUpCapture()
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            if (_isCaptureRunning) return;
        }

        if (!IsMicrophoneAvailable())
        {
            throw new InvalidOperationException(GetMicrophoneError() ?? "No microphone available");
        }

        var stopwatch = Stopwatch.StartNew();
        WaveInEvent? waveIn = null;

        try
        {
            waveIn = new WaveInEvent
            {
                WaveFormat = _waveFormat,
                BufferMilliseconds = BufferMilliseconds,
                NumberOfBuffers = NumberOfBuffers
            };
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnCaptureStopped;

            lock (_lock)
            {
                ThrowIfDisposed();
                _waveIn = waveIn;
            }

            waveIn.StartRecording();

            lock (_lock)
            {
                ThrowIfDisposed();
                _isCaptureRunning = true;
            }

            stopwatch.Stop();
            Logger.Info($"Audio recorder warmed up in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch
        {
            stopwatch.Stop();

            try
            {
                waveIn?.StopRecording();
            }
            catch
            {
                // Ignore cleanup errors after a failed warm-up.
            }

            waveIn?.Dispose();

            lock (_lock)
            {
                if (ReferenceEquals(_waveIn, waveIn))
                {
                    _waveIn = null;
                }

                _isCaptureRunning = false;
                _warmUpTask = null;
            }

            Logger.Warn($"Audio recorder warm-up failed after {stopwatch.ElapsedMilliseconds}ms");
            throw;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            var level = GetPeakLevel(e.Buffer, e.BytesRecorded);
            Action<float>? audioLevelHandler = null;

            lock (_lock)
            {
                if (_isRecording && _writer != null)
                {
                    _writer.Write(e.Buffer, 0, e.BytesRecorded);
                    audioLevelHandler = AudioLevelChanged;
                }
                else
                {
                    AddPreRollBuffer(e.Buffer, e.BytesRecorded);
                }
            }

            if (audioLevelHandler != null)
            {
                try
                {
                    audioLevelHandler(level);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Audio level update failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to handle audio data", ex);
        }
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            Logger.Error("Audio capture stopped with an error", e.Exception);
        }

        var shouldRestart = false;
        var stoppedWaveIn = sender as WaveInEvent;

        lock (_lock)
        {
            if (ReferenceEquals(_waveIn, stoppedWaveIn))
            {
                _waveIn = null;
            }

            _isCaptureRunning = false;
            _isRecording = false;
            _writer?.Dispose();
            _writer = null;
            _tempFilePath = null;
            _preRollBuffers.Clear();
            _preRollBytes = 0;
            _warmUpTask = null;
            _recordingStoppedEvent.Set();
            shouldRestart = !_disposed &&
                            (_captureMode == AudioCaptureMode.Instant ||
                             (_captureMode == AudioCaptureMode.Balanced && _idleReleaseTimer != null));
        }

        if (stoppedWaveIn != null)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    stoppedWaveIn.DataAvailable -= OnDataAvailable;
                    stoppedWaveIn.RecordingStopped -= OnCaptureStopped;
                    stoppedWaveIn.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Audio capture cleanup skipped: {ex.Message}");
                }
            });
        }

        if (shouldRestart)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(750);
                try
                {
                    await WarmUpAsync();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Audio recorder restart failed: {ex.Message}");
                }
            });
        }
    }

    private static float GetPeakLevel(byte[] buffer, int bytesRecorded)
    {
        float max = 0;
        for (var i = 0; i + 1 < bytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(buffer, i);
            var sample32 = Math.Abs(sample / 32768f);
            if (sample32 > max) max = sample32;
        }

        return max;
    }

    private void AddPreRollBuffer(byte[] source, int bytesRecorded)
    {
        if (bytesRecorded <= 0 || _maxPreRollBytes <= 0) return;

        var copy = new byte[bytesRecorded];
        Buffer.BlockCopy(source, 0, copy, 0, bytesRecorded);
        _preRollBuffers.Enqueue(copy);
        _preRollBytes += copy.Length;

        while (_preRollBytes > _maxPreRollBytes && _preRollBuffers.Count > 0)
        {
            _preRollBytes -= _preRollBuffers.Dequeue().Length;
        }
    }

    private void ApplyPostRecordingCapturePolicy(AudioCaptureMode captureMode)
    {
        switch (captureMode)
        {
            case AudioCaptureMode.Instant:
                break;
            case AudioCaptureMode.Balanced:
                ScheduleIdleRelease();
                break;
            case AudioCaptureMode.Privacy:
                StopCaptureIfIdle("privacy mode");
                break;
        }
    }

    private void ScheduleIdleRelease()
    {
        System.Threading.Timer? previousTimer;

        lock (_lock)
        {
            if (_disposed || _captureMode != AudioCaptureMode.Balanced || !_isCaptureRunning || _isRecording)
            {
                return;
            }

            previousTimer = _idleReleaseTimer;
            _idleReleaseTimer = new System.Threading.Timer(
                _ => StopCaptureIfIdle("balanced idle timeout"),
                null,
                BalancedIdleReleaseMilliseconds,
                Timeout.Infinite);
        }

        previousTimer?.Dispose();
        Logger.Debug($"Audio capture will be released after {BalancedIdleReleaseMilliseconds / 1000}s of inactivity");
    }

    private void CancelIdleReleaseTimer()
    {
        System.Threading.Timer? timerToDispose;

        lock (_lock)
        {
            timerToDispose = _idleReleaseTimer;
            _idleReleaseTimer = null;
        }

        timerToDispose?.Dispose();
    }

    private void StopCaptureIfIdle(string reason)
    {
        var shouldStop = false;
        System.Threading.Timer? timerToDispose;

        lock (_lock)
        {
            timerToDispose = _idleReleaseTimer;
            _idleReleaseTimer = null;

            if (!_disposed && !_isRecording && _isCaptureRunning)
            {
                shouldStop = true;
            }
        }

        timerToDispose?.Dispose();
        if (!shouldStop) return;

        Logger.Info($"Audio capture released: {reason}");
        StopCapture();
    }

    private void StopCapture()
    {
        WaveInEvent? waveInToStop;
        System.Threading.Timer? timerToDispose;

        lock (_lock)
        {
            timerToDispose = _idleReleaseTimer;
            _idleReleaseTimer = null;
            waveInToStop = _waveIn;
            _waveIn = null;
            _isCaptureRunning = false;
            _warmUpTask = null;
            _preRollBuffers.Clear();
            _preRollBytes = 0;
        }

        timerToDispose?.Dispose();
        if (waveInToStop == null) return;

        try
        {
            waveInToStop.DataAvailable -= OnDataAvailable;
            waveInToStop.RecordingStopped -= OnCaptureStopped;
            waveInToStop.StopRecording();
        }
        catch (Exception ex)
        {
            Logger.Debug($"Audio capture stop skipped: {ex.Message}");
        }
        finally
        {
            waveInToStop.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioRecorder));
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
            // Ignore cleanup errors.
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
            // Ignore cleanup errors.
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            StopRecording();
        }
        catch
        {
            // Ignore disposal errors.
        }

        StopCapture();

        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
            _idleReleaseTimer?.Dispose();
            _idleReleaseTimer = null;
        }

        _recordingStoppedEvent.Dispose();
    }
}
