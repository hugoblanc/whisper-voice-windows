using WhisperVoice;
using WhisperVoice.Config;

namespace WhisperVoice;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
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

        // Prevent multiple instances
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
