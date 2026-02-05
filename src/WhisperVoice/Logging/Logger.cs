using System.Text;

namespace WhisperVoice.Logging;

public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _logDirectory;
    private static readonly string _logFilePath;
    private static bool _initialized;

    public const string Version = "2.2.0";

    static Logger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhisperVoice",
            "logs"
        );
        _logFilePath = Path.Combine(_logDirectory, $"whispervoice_{DateTime.Now:yyyy-MM-dd}.log");
    }

    public static string LogFilePath => _logFilePath;
    public static string LogDirectory => _logDirectory;

    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            Directory.CreateDirectory(_logDirectory);
            _initialized = true;

            // Clean old logs (keep last 7 days)
            CleanOldLogs(7);

            Info("=== Whisper Voice Started ===");
            Info($"Version: {Version}");
            Info($"OS: {Environment.OSVersion}");
            Info($".NET: {Environment.Version}");
            Info($"Machine: {Environment.MachineName}");
            Info($"Log file: {_logFilePath}");
        }
        catch
        {
            // Logging should never crash the app
        }
    }

    public static void Info(string message)
    {
        Log("INFO", message);
    }

    public static void Debug(string message)
    {
        Log("DEBUG", message);
    }

    public static void Warn(string message)
    {
        Log("WARN", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            Log("ERROR", $"{message}: {ex.Message}");
            Log("ERROR", $"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Log("ERROR", $"Inner exception: {ex.InnerException.Message}");
            }
        }
        else
        {
            Log("ERROR", message);
        }
    }

    private static void Log(string level, string message)
    {
        if (!_initialized) return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] {message}";

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Never crash due to logging
        }
    }

    private static void CleanOldLogs(int keepDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(_logDirectory, "whispervoice_*.log"))
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoff)
                {
                    fileInfo.Delete();
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public static void LogConfig(Config.AppConfig config)
    {
        Info($"Config loaded from: {Config.AppConfig.ConfigPath}");
        Info($"Provider: {config.Provider}");
        Info($"Toggle shortcut: {config.GetToggleShortcutDescription()}");
        Info($"PTT key: {config.GetPushToTalkKeyDescription()}");

        var apiKey = config.GetCurrentApiKey();
        var maskedKey = string.IsNullOrEmpty(apiKey) ? "NOT SET" :
            apiKey.Length > 4 ? apiKey[..4] + "***" + apiKey[^4..] : "***";
        Info($"API key: {maskedKey}");
    }

    public static void LogAudioDevices()
    {
        try
        {
            var deviceCount = NAudio.Wave.WaveInEvent.DeviceCount;
            Info($"Audio input devices found: {deviceCount}");

            for (int i = 0; i < deviceCount; i++)
            {
                var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                Info($"  Device {i}: {caps.ProductName} (channels: {caps.Channels})");
            }
        }
        catch (Exception ex)
        {
            Warn($"Could not enumerate audio devices: {ex.Message}");
        }
    }

    public static string GetRecentLogs(int lines = 50)
    {
        try
        {
            if (!File.Exists(_logFilePath))
                return "No logs available.";

            var allLines = File.ReadAllLines(_logFilePath);
            var recentLines = allLines.TakeLast(lines);
            return string.Join(Environment.NewLine, recentLines);
        }
        catch (Exception ex)
        {
            return $"Error reading logs: {ex.Message}";
        }
    }

    public static void OpenLogFolder()
    {
        try
        {
            if (Directory.Exists(_logDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", _logDirectory);
            }
        }
        catch
        {
            // Ignore
        }
    }

    public static void OpenLogFile()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _logFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore
        }
    }
}
