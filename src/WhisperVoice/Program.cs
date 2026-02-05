using WhisperVoice;
using WhisperVoice.Audio;
using WhisperVoice.Config;

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

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Load configuration or show setup wizard
        var config = AppConfig.Load();

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            using var wizard = new SetupWizard();
            if (wizard.ShowDialog() != DialogResult.OK || wizard.Result == null)
            {
                return; // User cancelled setup
            }
            config = wizard.Result;
        }

        // Check microphone availability at startup
        var micError = AudioRecorder.GetMicrophoneError();
        if (micError != null)
        {
            var result = MessageBox.Show(
                $"{micError}\n\nDo you want to continue anyway?",
                "Whisper Voice - Microphone Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.No)
            {
                return;
            }
        }

        try
        {
            using var app = new WhisperVoiceApp(config);
            Application.Run(app);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An error occurred:\n\n{ex.Message}",
                "Whisper Voice - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
