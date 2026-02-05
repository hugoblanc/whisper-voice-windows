using WhisperVoice.Api;
using WhisperVoice.Audio;
using WhisperVoice.Clipboard;
using WhisperVoice.Config;
using WhisperVoice.Hotkeys;
using WhisperVoice.Logging;
using WhisperVoice.Tray;

namespace WhisperVoice;

public class WhisperVoiceApp : Form
{
    private AppConfig _config;
    private readonly TrayIcon _trayIcon;
    private readonly AudioRecorder _recorder;
    private ITranscriptionProvider _transcriptionProvider;
    private readonly GlobalHotkey _globalHotkey;
    private readonly KeyboardHook _keyboardHook;

    private AppState _state = AppState.Idle;
    private int _toggleHotkeyId;
    private System.Windows.Forms.Timer? _timeoutTimer;
    private PreferencesWindow? _preferencesWindow;

    private const int TimeoutSeconds = 45;

    public WhisperVoiceApp(AppConfig config)
    {
        _config = config;

        // Create invisible window for hotkey registration
        Text = "WhisperVoice";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.None;
        Opacity = 0;

        _recorder = new AudioRecorder();
        _transcriptionProvider = TranscriptionProviderFactory.Create(config);
        Logger.Info($"Using transcription provider: {_transcriptionProvider.DisplayName}");

        _trayIcon = new TrayIcon(
            config.GetToggleShortcutDescription(),
            config.GetPushToTalkKeyDescription()
        );
        _trayIcon.QuitRequested += () => Application.Exit();
        _trayIcon.PreferencesRequested += ShowPreferences;

        _globalHotkey = new GlobalHotkey(Handle);
        _keyboardHook = new KeyboardHook();

        SetupHotkeys();
    }

    private void SetupHotkeys()
    {
        var errors = new List<string>();

        // Register toggle hotkey (Ctrl+Shift+Space by default)
        var shortcut = _config.GetToggleShortcutDescription();
        Logger.Info($"Registering toggle hotkey: {shortcut}");
        try
        {
            _toggleHotkeyId = _globalHotkey.Register(_config.ShortcutModifiers, _config.ShortcutKeyCode);
            Logger.Info($"Toggle hotkey registered successfully (ID: {_toggleHotkeyId})");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to register toggle hotkey: {shortcut}", ex);
            errors.Add($"Toggle shortcut ({shortcut}): Another app may be using this shortcut. Try a different one in config.");
        }

        // Setup push-to-talk keyboard hook
        var pttKey = _config.GetPushToTalkKeyDescription();
        Logger.Info($"Setting up PTT keyboard hook for: {pttKey}");
        _keyboardHook.KeyDown += OnPttKeyDown;
        _keyboardHook.KeyUp += OnPttKeyUp;

        try
        {
            _keyboardHook.Start(_config.PushToTalkKeyCode);
            Logger.Info("PTT keyboard hook installed successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to install PTT keyboard hook for: {pttKey}", ex);
            errors.Add($"Push-to-Talk ({pttKey}): Failed to install keyboard hook. Your antivirus may be blocking it.");
        }

        // Show consolidated error notification
        if (errors.Count > 0)
        {
            _trayIcon.ShowNotification(
                "Hotkey Setup Warning",
                string.Join("\n", errors),
                ToolTipIcon.Warning
            );
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GlobalHotkey.WM_HOTKEY)
        {
            var id = m.WParam.ToInt32();
            if (id == _toggleHotkeyId)
            {
                ToggleRecording();
            }
        }

        base.WndProc(ref m);
    }

    private void ToggleRecording()
    {
        if (_state == AppState.Idle)
        {
            StartRecording();
        }
        else if (_state == AppState.Recording)
        {
            StopRecordingAndTranscribe();
        }
    }

    private void OnPttKeyDown()
    {
        if (_state == AppState.Idle)
        {
            StartRecording();
        }
    }

    private void OnPttKeyUp()
    {
        if (_state == AppState.Recording)
        {
            StopRecordingAndTranscribe();
        }
    }

    private void StartRecording()
    {
        if (_state != AppState.Idle) return;

        Logger.Info("Starting recording...");
        try
        {
            _recorder.StartRecording();
            SetState(AppState.Recording);
            Logger.Info("Recording started successfully");
            _trayIcon.ShowNotification("Recording", "Recording started...");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start recording", ex);
            _trayIcon.ShowNotification("Recording Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private async void StopRecordingAndTranscribe()
    {
        if (_state != AppState.Recording) return;

        Logger.Info("Stopping recording...");
        var audioPath = _recorder.StopRecording();
        SetState(AppState.Transcribing);
        Logger.Info($"Recording stopped. Audio file: {audioPath}");

        // Start timeout timer
        StartTimeoutTimer();

        try
        {
            if (string.IsNullOrEmpty(audioPath))
            {
                throw new InvalidOperationException("No audio recorded");
            }

            var fileInfo = new FileInfo(audioPath);
            Logger.Info($"Audio file size: {fileInfo.Length} bytes");

            Logger.Info($"Sending audio to {_transcriptionProvider.DisplayName}...");
            var text = await _transcriptionProvider.TranscribeAsync(audioPath);

            if (!string.IsNullOrWhiteSpace(text))
            {
                Logger.Info($"Transcription received: {text.Length} chars");
                Logger.Debug($"Transcription text: {text}");
                ClipboardPaste.Paste(text);

                var preview = text.Length > 50 ? text[..50] + "..." : text;
                _trayIcon.ShowNotification("Transcription Complete", preview);
            }
            else
            {
                Logger.Warn("Transcription returned empty text");
                _trayIcon.ShowNotification("No Speech Detected", "The recording was empty or contained no speech.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Transcription failed", ex);
            _trayIcon.ShowNotification("Transcription Error", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            StopTimeoutTimer();
            AudioRecorder.CleanupTempFile(audioPath);
            SetState(AppState.Idle);
        }
    }

    private void SetState(AppState state)
    {
        _state = state;
        _trayIcon.SetState(state);
    }

    private void ShowPreferences()
    {
        // If already open, bring to front
        if (_preferencesWindow != null && !_preferencesWindow.IsDisposed)
        {
            _preferencesWindow.BringToFront();
            _preferencesWindow.Activate();
            return;
        }

        _preferencesWindow = new PreferencesWindow(_config);
        _preferencesWindow.SettingsSaved += OnSettingsSaved;
        _preferencesWindow.FormClosed += (_, _) => _preferencesWindow = null;
        _preferencesWindow.Show();
    }

    private void OnSettingsSaved(AppConfig newConfig)
    {
        var providerChanged = newConfig.Provider != _config.Provider ||
                              newConfig.GetCurrentApiKey() != _config.GetCurrentApiKey();
        var shortcutsChanged = newConfig.ShortcutModifiers != _config.ShortcutModifiers ||
                               newConfig.ShortcutKeyCode != _config.ShortcutKeyCode ||
                               newConfig.PushToTalkKeyCode != _config.PushToTalkKeyCode;

        _config = newConfig;

        if (providerChanged)
        {
            ReloadProvider();
        }

        if (shortcutsChanged)
        {
            ReloadHotkeys();
        }

        // Update tray menu labels
        _trayIcon.UpdateShortcutLabels(
            _config.GetToggleShortcutDescription(),
            _config.GetPushToTalkKeyDescription()
        );

        _trayIcon.ShowNotification("Settings Saved", "Your preferences have been updated.");
    }

    private void ReloadProvider()
    {
        Logger.Info("Reloading transcription provider...");
        _transcriptionProvider = TranscriptionProviderFactory.Create(_config);
        Logger.Info($"Now using: {_transcriptionProvider.DisplayName}");
    }

    private void ReloadHotkeys()
    {
        Logger.Info("Reloading hotkeys...");

        // Unregister existing toggle hotkey
        if (_toggleHotkeyId != 0)
        {
            _globalHotkey.Unregister(_toggleHotkeyId);
            _toggleHotkeyId = 0;
        }

        // Stop PTT hook
        _keyboardHook.Stop();

        // Re-register with new config
        SetupHotkeys();

        Logger.Info("Hotkeys reloaded successfully");
    }

    private void StartTimeoutTimer()
    {
        _timeoutTimer = new System.Windows.Forms.Timer
        {
            Interval = TimeoutSeconds * 1000
        };
        _timeoutTimer.Tick += (_, _) =>
        {
            StopTimeoutTimer();
            if (_state == AppState.Transcribing)
            {
                _trayIcon.ShowNotification("Timeout", "Transcription timed out", ToolTipIcon.Warning);
                SetState(AppState.Idle);
            }
        };
        _timeoutTimer.Start();
    }

    private void StopTimeoutTimer()
    {
        _timeoutTimer?.Stop();
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _keyboardHook.Dispose();
        _globalHotkey.Dispose();
        _recorder.Dispose();
        _trayIcon.Dispose();
        AudioRecorder.CleanupAllTempFiles();

        base.OnFormClosing(e);
    }

    protected override void SetVisibleCore(bool value)
    {
        // Keep window invisible
        base.SetVisibleCore(false);
    }
}
