using WhisperVoice.Api;
using WhisperVoice.Audio;
using WhisperVoice.Clipboard;
using WhisperVoice.Config;
using WhisperVoice.Hotkeys;
using WhisperVoice.Tray;

namespace WhisperVoice;

public class WhisperVoiceApp : Form
{
    private readonly AppConfig _config;
    private readonly TrayIcon _trayIcon;
    private readonly AudioRecorder _recorder;
    private readonly WhisperApi _whisperApi;
    private readonly GlobalHotkey _globalHotkey;
    private readonly KeyboardHook _keyboardHook;

    private AppState _state = AppState.Idle;
    private int _toggleHotkeyId;
    private System.Windows.Forms.Timer? _timeoutTimer;

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
        _whisperApi = new WhisperApi(config.ApiKey);

        _trayIcon = new TrayIcon(
            config.GetToggleShortcutDescription(),
            config.GetPushToTalkKeyDescription()
        );
        _trayIcon.QuitRequested += () => Application.Exit();

        _globalHotkey = new GlobalHotkey(Handle);
        _keyboardHook = new KeyboardHook();

        SetupHotkeys();
    }

    private void SetupHotkeys()
    {
        var errors = new List<string>();

        // Register toggle hotkey (Ctrl+Shift+Space by default)
        try
        {
            _toggleHotkeyId = _globalHotkey.Register(_config.ShortcutModifiers, _config.ShortcutKeyCode);
        }
        catch (Exception ex)
        {
            var shortcut = _config.GetToggleShortcutDescription();
            errors.Add($"Toggle shortcut ({shortcut}): Another app may be using this shortcut. Try a different one in config.");
        }

        // Setup push-to-talk keyboard hook
        _keyboardHook.KeyDown += OnPttKeyDown;
        _keyboardHook.KeyUp += OnPttKeyUp;

        try
        {
            _keyboardHook.Start(_config.PushToTalkKeyCode);
        }
        catch (Exception ex)
        {
            var pttKey = _config.GetPushToTalkKeyDescription();
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

        try
        {
            _recorder.StartRecording();
            SetState(AppState.Recording);
            _trayIcon.ShowNotification("Recording", "Recording started...");
        }
        catch (Exception ex)
        {
            _trayIcon.ShowNotification("Recording Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private async void StopRecordingAndTranscribe()
    {
        if (_state != AppState.Recording) return;

        var audioPath = _recorder.StopRecording();
        SetState(AppState.Transcribing);

        // Start timeout timer
        StartTimeoutTimer();

        try
        {
            if (string.IsNullOrEmpty(audioPath))
            {
                throw new InvalidOperationException("No audio recorded");
            }

            var text = await _whisperApi.TranscribeAsync(audioPath);

            if (!string.IsNullOrWhiteSpace(text))
            {
                ClipboardPaste.Paste(text);

                var preview = text.Length > 50 ? text[..50] + "..." : text;
                _trayIcon.ShowNotification("Transcription Complete", preview);
            }
            else
            {
                _trayIcon.ShowNotification("No Speech Detected", "The recording was empty or contained no speech.");
            }
        }
        catch (Exception ex)
        {
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
