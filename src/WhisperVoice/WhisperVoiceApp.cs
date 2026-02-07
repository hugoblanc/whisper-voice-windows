using WhisperVoice.Api;
using WhisperVoice.Audio;
using WhisperVoice.Clipboard;
using WhisperVoice.Config;
using WhisperVoice.History;
using WhisperVoice.Hotkeys;
using WhisperVoice.Logging;
using WhisperVoice.Processing;
using WhisperVoice.Tray;
using WhisperVoice.UI;

namespace WhisperVoice;

public class WhisperVoiceApp : Form
{
    private AppConfig _config;
    private readonly TrayIcon _trayIcon;
    private readonly AudioRecorder _recorder;
    private ITranscriptionProvider _transcriptionProvider;
    private readonly GlobalHotkey _globalHotkey;
    private readonly KeyboardHook _keyboardHook;
    private readonly KeyboardHook _shiftHook;
    private readonly ModeManager _modeManager;
    private readonly TextProcessor _textProcessor;

    private AppState _state = AppState.Idle;
    private int _toggleHotkeyId;
    private int _historyHotkeyId;
    private System.Windows.Forms.Timer? _timeoutTimer;
    private PreferencesWindow? _preferencesWindow;
    private RecordingWindow? _recordingWindow;
    private HistoryWindow? _historyWindow;

    private const int TimeoutSeconds = 45;
    private const uint VK_SHIFT = 0x10;

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

        // Initialize AI processing
        _modeManager = new ModeManager(() => _config.HasOpenAIKeyForProcessing);
        _modeManager.ModeChanged += OnModeChanged;
        _textProcessor = new TextProcessor();

        _trayIcon = new TrayIcon(
            config.GetToggleShortcutDescription(),
            config.GetPushToTalkKeyDescription(),
            _modeManager.CurrentMode.Name,
            _modeManager.HasAIModesAvailable
        );
        _trayIcon.QuitRequested += () => Application.Exit();
        _trayIcon.PreferencesRequested += ShowPreferences;

        _globalHotkey = new GlobalHotkey(Handle);
        _keyboardHook = new KeyboardHook();
        _shiftHook = new KeyboardHook();

        SetupHotkeys();
    }

    private void OnModeChanged(AIMode mode)
    {
        _trayIcon.UpdateModeLabel(mode.Name);
        _recordingWindow?.SetMode(mode.Name);
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

        // Register history hotkey (Ctrl+H)
        Logger.Info("Registering history hotkey: Ctrl+H");
        try
        {
            const int MOD_CONTROL = 0x0002;
            const int VK_H = 0x48;
            _historyHotkeyId = _globalHotkey.Register(MOD_CONTROL, VK_H);
            Logger.Info($"History hotkey registered successfully (ID: {_historyHotkeyId})");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to register history hotkey (Ctrl+H): {ex.Message}");
            // Non-critical, don't add to errors
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

        // Setup Shift key hook for mode switching during recording
        _shiftHook.KeyDown += OnShiftKeyDown;
        try
        {
            _shiftHook.Start(VK_SHIFT);
            Logger.Info("Shift key hook installed for mode switching");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to install Shift key hook: {ex.Message}");
            // Non-critical, don't show error to user
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
            else if (id == _historyHotkeyId)
            {
                ShowHistoryWindow();
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

    private void OnShiftKeyDown()
    {
        // Only switch modes while recording
        if (_state == AppState.Recording)
        {
            var newMode = _modeManager.NextMode();
            Logger.Info($"Mode switched to: {newMode.Name}");
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

            // Show recording window
            ShowRecordingWindow();

            // Connect audio level to recording window
            _recorder.AudioLevelChanged += OnAudioLevelChanged;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start recording", ex);
            _trayIcon.ShowNotification("Recording Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ShowRecordingWindow()
    {
        // Close existing window if any
        _recordingWindow?.Close();

        _recordingWindow = new RecordingWindow();
        _recordingWindow.SetMode(_modeManager.CurrentMode.Name);
        _recordingWindow.CancelRequested += OnRecordingCancelled;
        _recordingWindow.FormClosed += (_, _) => _recordingWindow = null;
        _recordingWindow.Show();
    }

    private void OnRecordingCancelled()
    {
        Logger.Info("Recording cancelled by user");
        if (_state == AppState.Recording)
        {
            // Disconnect audio level event
            _recorder.AudioLevelChanged -= OnAudioLevelChanged;

            _recorder.StopRecording();
            CloseRecordingWindow();
            SetState(AppState.Idle);
        }
    }

    private void OnAudioLevelChanged(float level)
    {
        _recordingWindow?.UpdateAudioLevel(level);
    }

    private void CloseRecordingWindow()
    {
        if (_recordingWindow != null && !_recordingWindow.IsDisposed)
        {
            if (_recordingWindow.InvokeRequired)
                _recordingWindow.Invoke(() => _recordingWindow.Close());
            else
                _recordingWindow.Close();
        }
        _recordingWindow = null;
    }

    private async void StopRecordingAndTranscribe()
    {
        if (_state != AppState.Recording) return;

        // Capture current mode before stopping (it might change)
        var currentMode = _modeManager.CurrentMode;

        Logger.Info("Stopping recording...");

        // Disconnect audio level event
        _recorder.AudioLevelChanged -= OnAudioLevelChanged;

        var audioPath = _recorder.StopRecording();
        SetState(AppState.Transcribing);
        _recordingWindow?.SetState(AppState.Transcribing);
        Logger.Info($"Recording stopped. Audio file: {audioPath}, Mode: {currentMode.Name}");

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

            // Step 1: Transcribe audio
            Logger.Info($"Sending audio to {_transcriptionProvider.DisplayName}...");
            var text = await _transcriptionProvider.TranscribeAsync(audioPath);

            if (string.IsNullOrWhiteSpace(text))
            {
                Logger.Warn("Transcription returned empty text");
                return;
            }

            Logger.Info($"Transcription received: {text.Length} chars");
            Logger.Debug($"Transcription text: {text}");

            // Step 2: Apply AI processing if mode requires it
            if (currentMode.RequiresProcessing)
            {
                var apiKey = _config.GetOpenAIKeyForProcessing();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        text = await _textProcessor.ProcessAsync(text, currentMode, apiKey);
                        Logger.Info($"AI processing complete: {text.Length} chars");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"AI processing failed, using raw transcription: {ex.Message}");
                        // Continue with raw transcription
                    }
                }
                else
                {
                    Logger.Warn("AI mode selected but no OpenAI key available");
                }
            }

            // Step 3: Paste result
            ClipboardPaste.Paste(text);
            // No notification - text is already pasted at cursor

            // Step 4: Save to history
            TranscriptionHistory.AddEntry(text, _config.Provider, currentMode.Name);
            Logger.Debug("Transcription saved to history");
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
            CloseRecordingWindow();
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

    private void ShowHistoryWindow()
    {
        // If already open, bring to front
        if (_historyWindow != null && !_historyWindow.IsDisposed)
        {
            _historyWindow.BringToFront();
            _historyWindow.Activate();
            return;
        }

        _historyWindow = new HistoryWindow();
        _historyWindow.FormClosed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
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
        _shiftHook.Dispose();
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
