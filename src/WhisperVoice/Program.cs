using WhisperVoice;
using WhisperVoice.Audio;
using WhisperVoice.Config;
using WhisperVoice.Logging;

namespace WhisperVoice;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Prevent multiple instances - check FIRST before anything else
        using var mutex = new Mutex(true, "WhisperVoice-SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Whisper Voice is already running.\n\nCheck the system tray.",
                "Whisper Voice",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        // Initialize logging first
        Logger.Initialize();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Log audio devices for debugging
        Logger.LogAudioDevices();

        // Load configuration or show setup wizard
        Logger.Info("Loading configuration...");
        var config = AppConfig.Load();

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            Logger.Info("No config found, showing setup wizard");
            using var wizard = new SetupWizard();
            if (wizard.ShowDialog() != DialogResult.OK || wizard.Result == null)
            {
                Logger.Info("Setup wizard cancelled by user");
                return;
            }
            config = wizard.Result;
            Logger.Info("Setup wizard completed successfully");
        }

        Logger.LogConfig(config);

        // Check microphone availability at startup
        var micError = AudioRecorder.GetMicrophoneError();
        if (micError != null)
        {
            Logger.Warn($"Microphone issue detected: {micError}");
            var result = MessageBox.Show(
                $"{micError}\n\nDo you want to continue anyway?",
                "Whisper Voice - Microphone Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.No)
            {
                Logger.Info("User chose to exit due to microphone issue");
                return;
            }
            Logger.Info("User chose to continue despite microphone issue");
        }

        try
        {
            Logger.Info("Starting main application...");
            using var app = new WhisperVoiceApp(config);
            Application.Run(app);
            Logger.Info("Application exited normally");
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal application error", ex);
            MessageBox.Show(
                $"An error occurred:\n\n{ex.Message}\n\nCheck logs at:\n{Logger.LogDirectory}",
                "Whisper Voice - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
