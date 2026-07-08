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
    private readonly KeyboardHook _modeSwitchHook;
    private readonly KeyboardHook _escapeHook;
    private readonly ModeManager _modeManager;
    private readonly TextProcessor _textProcessor;

    private AppState _state = AppState.Idle;
    private int _toggleHotkeyId;
    private int _historyHotkeyId;
    private System.Windows.Forms.Timer? _timeoutTimer;
    private PreferencesWindow? _preferencesWindow;
    private RecordingWindow? _recordingWindow;
    private HistoryWindow? _historyWindow;
    private IntPtr _pasteTargetWindow = IntPtr.Zero;
    private DictationContext? _recordingContext;
    private RecordingJournalSession? _recordingJournal;
    private ProjectConfig? _recordingProject;
    private bool _recordingProjectPredicted;
    private string? _pendingAutoModeReason;
    private DateTime? _recordingStartedAt;
    private bool _isStartingRecorder;
    private bool _stopRequestedAfterRecorderStart;
    private bool _cancelRequestedAfterRecorderStart;

    private const int TimeoutSeconds = 45;
    private const uint VK_TAB = 0x09;
    private const uint VK_ESCAPE = 0x1B;

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
        _recorder.SetCaptureMode(config.AudioCaptureMode);
        _transcriptionProvider = TranscriptionProviderFactory.Create(config);
        Logger.Info($"Using transcription provider: {_transcriptionProvider.DisplayName}");

        // Initialize AI processing
        _modeManager = new ModeManager(() => _config);
        _modeManager.ModeChanged += OnModeChanged;
        _textProcessor = new TextProcessor();

        _trayIcon = new TrayIcon(
            config.GetToggleShortcutDescription(),
            config.GetPushToTalkKeyDescription(),
            _modeManager.CurrentMode.Name,
            _modeManager.HasAIModesAvailable,
            config.PostActions,
            config.ActivePostActionId
        );
        _trayIcon.QuitRequested += () => Application.Exit();
        _trayIcon.PreferencesRequested += ShowPreferences;
        _trayIcon.PostActionSelected += OnPostActionSelected;

        _globalHotkey = new GlobalHotkey(Handle);
        _keyboardHook = new KeyboardHook();
        _modeSwitchHook = new KeyboardHook();
        _escapeHook = new KeyboardHook();

        SetupHotkeys();
        ApplyAudioCaptureMode(warmImmediately: true);
    }

    private void WarmUpRecorder()
    {
        if (_config.AudioCaptureMode != AudioCaptureMode.Instant) return;

        _ = _recorder.WarmUpAsync().ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                Logger.Warn($"Audio recorder warm-up failed: {task.Exception.GetBaseException().Message}");
            }
        }, TaskScheduler.Default);
    }

    private void ApplyAudioCaptureMode(bool warmImmediately)
    {
        _recorder.SetCaptureMode(_config.AudioCaptureMode);
        Logger.Info($"Audio capture mode active: {_config.AudioCaptureMode}");

        if (warmImmediately && _config.AudioCaptureMode == AudioCaptureMode.Instant)
        {
            WarmUpRecorder();
        }
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
        _keyboardHook.KeyDown -= OnPttKeyDown;
        _keyboardHook.KeyUp -= OnPttKeyUp;
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

        // Setup Tab key hook for mode switching during recording
        _modeSwitchHook.KeyDown -= OnModeSwitchKeyDown;
        _modeSwitchHook.KeyDown += OnModeSwitchKeyDown;
        _modeSwitchHook.ShouldSuppressKey = () => _state == AppState.Recording;
        try
        {
            _modeSwitchHook.Start(VK_TAB);
            Logger.Info("Tab key hook installed for mode switching");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to install Tab key hook: {ex.Message}");
            // Non-critical, don't show error to user
        }

        // Setup Escape key hook for cancelling recording while the overlay is not focused
        _escapeHook.KeyDown -= OnEscapeKeyDown;
        _escapeHook.KeyDown += OnEscapeKeyDown;
        try
        {
            _escapeHook.Start(VK_ESCAPE);
            Logger.Info("Escape key hook installed for recording cancellation");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to install Escape key hook: {ex.Message}");
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
        RunOnUiThread(() =>
        {
            if (_state == AppState.Idle)
            {
                StartRecording();
            }
        });
    }

    private void OnPttKeyUp()
    {
        RunOnUiThread(() =>
        {
            if (_state == AppState.Recording)
            {
                StopRecordingAndTranscribe();
            }
        });
    }

    private void OnModeSwitchKeyDown()
    {
        RunOnUiThread(() =>
        {
            // Only switch modes while recording
            if (_state == AppState.Recording)
            {
                var newMode = _modeManager.NextMode();
                _pendingAutoModeReason = null;
                _recordingWindow?.SetAutoModeReason(null);
                Logger.Info($"Mode switched to: {newMode.Name}");
            }
        });
    }

    private void OnEscapeKeyDown()
    {
        RunOnUiThread(() =>
        {
            if (_state == AppState.Recording)
            {
                OnRecordingCancelled();
            }
        });
    }

    private async void StartRecording()
    {
        if (_state != AppState.Idle) return;

        Logger.Info("Starting recording...");
        var journal = RecordingJournal.Start(_config.Provider, _transcriptionProvider.DisplayName, _modeManager.CurrentMode.Name);
        _recordingJournal = journal;
        _isStartingRecorder = false;
        _stopRequestedAfterRecorderStart = false;
        _cancelRequestedAfterRecorderStart = false;
        _pendingAutoModeReason = null;
        _recordingProject = null;
        _recordingProjectPredicted = false;

        try
        {
            var prepareStep = journal.StartStep("prepare_recording");
            try
            {
                _pasteTargetWindow = ClipboardPaste.CaptureTargetWindow();
                Logger.Debug($"Captured paste target window: 0x{_pasteTargetWindow.ToInt64():X}");
                _recordingContext = WindowsContextCapturer.Capture(
                    _pasteTargetWindow,
                    includeSelectedText: false);
                prepareStep.Complete("ok", $"target=0x{_pasteTargetWindow.ToInt64():X}");
            }
            catch (Exception ex)
            {
                prepareStep.Fail(ex);
                throw;
            }

            ApplyProjectPrediction(_recordingContext, journal);
            ApplyAutoMode(_recordingContext, journal);

            _recorder.AudioLevelChanged -= OnAudioLevelChanged;
            _recorder.AudioLevelChanged += OnAudioLevelChanged;

            _isStartingRecorder = true;
            _recordingStartedAt = DateTime.Now;
            var recorderWasWarm = _recorder.IsWarmedUp;
            var startRecorderStep = journal.StartStep("start_recorder");
            var startRecorderTask = Task.Run(() => _recorder.StartRecording());

            journal.Track("show_recording_window", () =>
            {
                SetState(AppState.Recording);
                ShowRecordingWindow();
                if (!recorderWasWarm)
                {
                    _recordingWindow?.SetStarting();
                }
            });

            try
            {
                await startRecorderTask;
                var recorderStartDetail = $"{_config.AudioCaptureMode.ToString().ToLowerInvariant()}; {(recorderWasWarm ? "warm" : "cold fallback")}";
                startRecorderStep.Complete("ok", recorderStartDetail);
            }
            catch (Exception ex)
            {
                startRecorderStep.Fail(ex);
                throw;
            }

            _isStartingRecorder = false;

            if (_cancelRequestedAfterRecorderStart || _state != AppState.Recording || _recordingJournal != journal)
            {
                Logger.Info("Recording start completed after cancellation; stopping recorder");
                _cancelRequestedAfterRecorderStart = false;
                _stopRequestedAfterRecorderStart = false;

                if (_state == AppState.Recording && _recordingJournal == journal)
                {
                    OnRecordingCancelled();
                }
                else
                {
                    var audioPath = await Task.Run(() => _recorder.StopRecording());
                    CleanupAfterRecording(audioPath, journal);
                }

                return;
            }

            _recordingWindow?.SetState(AppState.Recording);

            Logger.Info("Recording started successfully");

            if (_stopRequestedAfterRecorderStart)
            {
                _stopRequestedAfterRecorderStart = false;
                StopRecordingAndTranscribe();
            }
        }
        catch (Exception ex)
        {
            _isStartingRecorder = false;
            _stopRequestedAfterRecorderStart = false;
            _cancelRequestedAfterRecorderStart = false;
            _recorder.AudioLevelChanged -= OnAudioLevelChanged;
            _pasteTargetWindow = IntPtr.Zero;
            _recordingContext = null;
            _recordingProject = null;
            _recordingProjectPredicted = false;
            _recordingStartedAt = null;
            CloseRecordingWindow();
            SetState(AppState.Idle);
            journal.Finish("failed", ex.Message);
            _recordingJournal = null;
            Logger.Error("Failed to start recording", ex);
            _trayIcon.ShowNotification("Recording Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ApplyProjectPrediction(DictationContext? context, RecordingJournalSession journal)
    {
        if (context == null || !_config.ProjectTaggingEnabled) return;

        var project = ProjectStore.PredictProject(context, _config);
        _recordingProject = project;
        _recordingProjectPredicted = project != null;
        ApplyProjectToContext(context, project);

        if (project != null)
        {
            var detail = $"project={project.Name}; {context.ContextSummary}; title={context.ActiveWindowTitle ?? ""}";
            journal.AddEvent("project_prediction", "ok", detail);
            Logger.Info($"Project predicted: {detail}");
        }
        else
        {
            journal.AddEvent("project_prediction", "none", context.ContextSummary);
        }
    }

    private static void ApplyProjectToContext(DictationContext context, ProjectConfig? project)
    {
        context.ProjectId = project?.Id ?? "";
        context.ProjectName = project?.Name ?? "";
    }

    private void ApplyAutoMode(DictationContext? context, RecordingJournalSession journal)
    {
        if (context == null) return;

        var mode = _modeManager.ResolveAutoMode(context, out var matchedRule);
        if (mode == null || matchedRule == null)
        {
            if (_config.AutoModeEnabled && !_config.AutoModeFallbackToLastUsed)
            {
                _modeManager.SetMode(AIMode.Brut.Id);
                journal.SetMode(_modeManager.CurrentMode.Name);
                _pendingAutoModeReason = "auto: Brut (default)";
                journal.AddEvent("auto_mode", "reset", "no matching rule; reset to Brut");
                Logger.Info("Auto mode: no matching rule; reset to Brut");
            }

            return;
        }

        _modeManager.SetMode(mode.Id);
        journal.SetMode(mode.Name);

        var detail = $"rule={matchedRule.Name}; mode={mode.Name}; app={context.ActiveProcessName ?? "unknown"}; title={context.ActiveWindowTitle ?? ""}";
        _pendingAutoModeReason = $"auto: {mode.Name} ({matchedRule.Name})";
        journal.AddEvent("auto_mode", "ok", detail);
        Logger.Info($"Auto mode matched: {detail}");
    }

    private void ShowRecordingWindow()
    {
        // Close existing window if any
        _recordingWindow?.Close();

        _recordingWindow = new RecordingWindow(_pasteTargetWindow);
        _recordingWindow.SetMode(_modeManager.CurrentMode.Name);
        _recordingWindow.SetAutoModeReason(_pendingAutoModeReason);
        _recordingWindow.SetProject(
            _recordingProject?.Name,
            _recordingProject == null ? null : ColorFromProject(_recordingProject),
            _recordingProjectPredicted);
        _recordingWindow.CancelRequested += OnRecordingCancelled;
        _recordingWindow.StopRequested += StopRecordingAndTranscribe;
        _recordingWindow.ModeCycleRequested += CycleModeDuringRecording;
        _recordingWindow.ProjectPickRequested += PickProjectForCurrentRecording;
        _recordingWindow.FormClosed += (_, _) => _recordingWindow = null;
        _recordingWindow.Show();
    }

    private void PickProjectForCurrentRecording()
    {
        if (_state != AppState.Recording || _recordingWindow == null) return;

        using var dialog = new ProjectPickerDialog(_recordingProject);
        dialog.TopMost = true;

        var wasTopMost = _recordingWindow.TopMost;
        _recordingWindow.TopMost = false;

        DialogResult result;
        try
        {
            result = dialog.ShowDialog(_recordingWindow);
        }
        finally
        {
            if (_recordingWindow != null && !_recordingWindow.IsDisposed)
            {
                _recordingWindow.TopMost = wasTopMost;
                _recordingWindow.BringToFront();
            }
        }

        if (result != DialogResult.OK) return;

        _recordingProject = dialog.SelectedProject;
        _recordingProjectPredicted = false;

        if (_recordingContext != null)
        {
            ApplyProjectToContext(_recordingContext, _recordingProject);
        }

        if (_recordingProject != null)
        {
            _config.LastUsedProjectId = _recordingProject.Id;
            _config.Save();
            _recordingJournal?.AddEvent("project_selected", "ok", $"project={_recordingProject.Name}");
        }
        else
        {
            _recordingJournal?.AddEvent("project_selected", "none", "untagged");
        }

        _recordingWindow.SetProject(
            _recordingProject?.Name,
            _recordingProject == null ? null : ColorFromProject(_recordingProject),
            predicted: false);
    }

    private void CycleModeDuringRecording()
    {
        if (_state != AppState.Recording) return;

        var newMode = _modeManager.NextMode();
        _pendingAutoModeReason = null;
        _recordingWindow?.SetAutoModeReason(null);
        Logger.Info($"Mode switched from recording window to: {newMode.Name}");
    }

    private async void OnRecordingCancelled()
    {
        Logger.Info("Recording cancelled by user");
        if (_state == AppState.Recording)
        {
            if (_isStartingRecorder)
            {
                _cancelRequestedAfterRecorderStart = true;
                Logger.Info("Recording cancellation queued while recorder is starting");
                _recordingWindow?.SetStarting();
                return;
            }

            var journal = _recordingJournal;
            var recordingStartedAt = _recordingStartedAt;

            // Disconnect audio level event
            _recorder.AudioLevelChanged -= OnAudioLevelChanged;

            SetState(AppState.Transcribing);
            _recordingWindow?.SetState(AppState.Transcribing);

            string? audioPath = null;
            try
            {
                if (recordingStartedAt.HasValue)
                {
                    journal?.AddDurationStep("record_audio", recordingStartedAt.Value, DateTime.Now, "cancelled");
                }

                audioPath = journal != null
                    ? await journal.TrackAsync("stop_recorder", () => Task.Run(() => _recorder.StopRecording()),
                        path => string.IsNullOrEmpty(path) ? "no audio path" : Path.GetFileName(path))
                    : await Task.Run(() => _recorder.StopRecording());
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to cancel recording cleanly", ex);
                journal?.AddEvent("cancel_cleanup_error", "failed", ex.Message);
            }
            finally
            {
                CleanupAfterRecording(audioPath, journal);

                _pasteTargetWindow = IntPtr.Zero;
                _recordingContext = null;
                _recordingStartedAt = null;
                SetState(AppState.Idle);
                journal?.Finish("cancelled");
                _recordingJournal = null;
            }
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

        if (_isStartingRecorder)
        {
            _stopRequestedAfterRecorderStart = true;
            Logger.Info("Recording stop queued while recorder is starting");
            return;
        }

        // Capture current mode before stopping (it might change)
        var currentMode = _modeManager.CurrentMode;
        var pasteTargetWindow = _pasteTargetWindow;
        var recordingContext = _recordingContext;
        var journal = _recordingJournal;
        var recordingStartedAt = _recordingStartedAt;
        var finalStatus = "completed";
        string? finalError = null;
        string? audioPath = null;

        Logger.Info("Stopping recording...");
        journal?.SetMode(currentMode.Name);

        // Disconnect audio level event
        _recorder.AudioLevelChanged -= OnAudioLevelChanged;

        SetState(AppState.Transcribing);
        _recordingWindow?.SetState(AppState.Transcribing);

        // Start timeout timer
        StartTimeoutTimer();

        try
        {
            if (recordingStartedAt.HasValue)
            {
                journal?.AddDurationStep("record_audio", recordingStartedAt.Value, DateTime.Now, "ok", $"mode={currentMode.Name}");
            }

            audioPath = journal != null
                ? await journal.TrackAsync("stop_recorder", () => Task.Run(() => _recorder.StopRecording()),
                    path => string.IsNullOrEmpty(path) ? "no audio path" : Path.GetFileName(path))
                : await Task.Run(() => _recorder.StopRecording());
            Logger.Info($"Recording stopped. Audio file: {audioPath}, Mode: {currentMode.Name}");

            if (string.IsNullOrEmpty(audioPath))
            {
                throw new InvalidOperationException("No audio recorded");
            }

            var fileInfo = new FileInfo(audioPath);
            Logger.Info($"Audio file size: {fileInfo.Length} bytes");
            journal?.SetAudioBytes(fileInfo.Length);

            if (currentMode.IsSuper && recordingContext?.HasSelectedText != true)
            {
                var capturedContext = journal != null
                    ? await journal.TrackAsync("capture_super_context", async () =>
                    {
                        await Task.Delay(120);
                        return WindowsContextCapturer.Capture(pasteTargetWindow, includeSelectedText: true);
                    }, ctx => ctx.HasSelectedText ? $"selected={ctx.SelectedText!.Length} chars" : "no selected text")
                    : WindowsContextCapturer.Capture(pasteTargetWindow, includeSelectedText: true);

                if (capturedContext.HasSelectedText || capturedContext.HasAmbientContext)
                {
                    recordingContext = capturedContext;
                }
            }

            // Step 1: Transcribe audio
            Logger.Info($"Sending audio to {_transcriptionProvider.DisplayName}...");
            var vocabularyPrompt = BuildVocabularyPrompt(_config.CustomVocabulary);
            if (!string.IsNullOrWhiteSpace(vocabularyPrompt))
            {
                journal?.AddEvent("custom_vocabulary", "ok", $"{_config.CustomVocabulary.Count} terms");
            }

            var text = journal != null
                ? await journal.TrackAsync("transcribe_audio", () => _transcriptionProvider.TranscribeAsync(audioPath, vocabularyPrompt),
                    result => $"{result.Length} chars")
                : await _transcriptionProvider.TranscribeAsync(audioPath, vocabularyPrompt);

            if (string.IsNullOrWhiteSpace(text))
            {
                Logger.Warn("Transcription returned empty text");
                finalStatus = "empty";
                finalError = "Transcription returned empty text";
                return;
            }

            Logger.Info($"Transcription received: {text.Length} chars");
            Logger.Debug($"Transcription text: {text}");
            journal?.SetRawTextChars(text.Length);
            var rawText = text;

            // Step 2: Apply AI processing if mode requires it
            if (currentMode.RequiresProcessing)
            {
                var apiKey = _config.GetOpenAIKeyForProcessing();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    JournalStepScope? processingStep = null;
                    try
                    {
                        processingStep = journal?.StartStep("ai_processing");
                        text = await _textProcessor.ProcessAsync(
                            text,
                            currentMode,
                            apiKey,
                            recordingContext,
                            _config.ProcessingModel,
                            _config.CustomVocabulary);
                        processingStep?.Complete("ok", $"{text.Length} chars; model={_config.ProcessingModel}");
                        Logger.Info($"AI processing complete: {text.Length} chars");
                    }
                    catch (Exception ex)
                    {
                        processingStep?.Warn(ex.Message);
                        Logger.Warn($"AI processing failed, using raw transcription: {ex.Message}");
                        // Continue with raw transcription
                    }
                }
                else
                {
                    journal?.AddEvent("ai_processing", "skipped", "no OpenAI key available");
                    Logger.Warn("AI mode selected but no OpenAI key available");
                }
            }
            else
            {
                journal?.AddEvent("ai_processing", "skipped", "mode does not require processing");
            }

            journal?.SetFinalTextChars(text.Length);

            // Step 3: Run post-transcription action
            if (journal != null)
            {
                await journal.TrackAsync("close_recording_window", async () =>
                {
                    CloseRecordingWindow();
                    await Task.Delay(75);
                });
            }
            else
            {
                CloseRecordingWindow();
                await Task.Delay(75);
            }

            var action = PostActionRuleResolver.Resolve(_config, recordingContext, out var matchedActionRule);
            if (matchedActionRule != null)
            {
                var detail = $"rule={matchedActionRule.Name}; action={action.Label}; app={recordingContext?.ActiveProcessName ?? "unknown"}; title={recordingContext?.ActiveWindowTitle ?? ""}";
                journal?.AddEvent("auto_post_action", "ok", detail);
                Logger.Info($"Auto post-action matched: {detail}");
            }

            var actionResult = journal != null
                ? await journal.TrackAsync("post_action",
                    () => PostActionExecutor.ExecuteAsync(
                        action,
                        text,
                        rawText,
                        recordingContext,
                        _config.Provider,
                        currentMode.Name,
                        pasteTargetWindow),
                    result => $"{action.Label}: {result.Detail}")
                : await PostActionExecutor.ExecuteAsync(
                    action,
                    text,
                    rawText,
                    recordingContext,
                    _config.Provider,
                    currentMode.Name,
                    pasteTargetWindow);

            journal?.SetPasted(actionResult.Pasted);
            if (!actionResult.Success)
            {
                journal?.AddEvent("post_action_warning", "warning", actionResult.Detail);
                _trayIcon.ShowNotification("Post-action Warning", $"'{action.Label}' may have failed: {actionResult.Detail}", ToolTipIcon.Warning);
            }

            // Step 4: Save to history
            if (journal != null)
            {
                journal.Track("save_history", () => TranscriptionHistory.AddEntry(text, _config.Provider, currentMode.Name, recordingContext, action.Label));
            }
            else
            {
                TranscriptionHistory.AddEntry(text, _config.Provider, currentMode.Name, recordingContext, action.Label);
            }
            Logger.Debug("Transcription saved to history");
        }
        catch (Exception ex)
        {
            finalStatus = "failed";
            finalError = ex.Message;
            Logger.Error("Transcription failed", ex);
            _trayIcon.ShowNotification("Transcription Error", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            StopTimeoutTimer();
            CleanupAfterRecording(audioPath, journal);

            _pasteTargetWindow = IntPtr.Zero;
            _recordingContext = null;
            _recordingProject = null;
            _recordingProjectPredicted = false;
            _recordingStartedAt = null;
            SetState(AppState.Idle);
            journal?.Finish(finalStatus, finalError);
            _recordingJournal = null;
        }
    }

    private void CleanupAfterRecording(string? audioPath, RecordingJournalSession? journal)
    {
        var step = journal?.StartStep("cleanup");

        try
        {
            AudioRecorder.CleanupTempFile(audioPath);
            CloseRecordingWindow();
            step?.Complete();
        }
        catch (Exception ex)
        {
            step?.Fail(ex);
            Logger.Warn($"Recording cleanup failed: {ex.Message}");
        }
    }

    private static string? BuildVocabularyPrompt(IReadOnlyList<string>? vocabulary)
    {
        if (vocabulary == null || vocabulary.Count == 0)
        {
            return null;
        }

        return string.Join(", ", vocabulary
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(action);
            return;
        }

        action();
    }

    private void SetState(AppState state)
    {
        _state = state;
        _trayIcon.SetState(state);
    }

    private void ShowPreferences()
    {
        RunOnUiThread(() =>
        {
            try
            {
                Logger.Info("Opening Preferences window...");

                // If already open, bring to front
                if (_preferencesWindow != null && !_preferencesWindow.IsDisposed)
                {
                    BringWindowToFront(_preferencesWindow);
                    return;
                }

                _preferencesWindow = new PreferencesWindow(_config);
                _preferencesWindow.SettingsSaved += OnSettingsSaved;
                _preferencesWindow.FormClosed += (_, _) => _preferencesWindow = null;
                _preferencesWindow.Show();
                BringWindowToFront(_preferencesWindow);

                Logger.Info("Preferences window opened");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open Preferences window", ex);
                _preferencesWindow = null;
                _trayIcon.ShowNotification("Preferences Error", ex.Message, ToolTipIcon.Error);
            }
        });
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

    private static void BringWindowToFront(Form window)
    {
        if (window.WindowState == FormWindowState.Minimized)
        {
            window.WindowState = FormWindowState.Normal;
        }

        window.ShowInTaskbar = true;
        window.Show();
        window.Activate();
        window.BringToFront();
        window.TopMost = true;
        window.TopMost = false;
    }

    private static Color ColorFromProject(ProjectConfig project)
    {
        try
        {
            return ColorTranslator.FromHtml(project.Color);
        }
        catch
        {
            return Color.FromArgb(123, 192, 214);
        }
    }

    private void OnSettingsSaved(AppConfig newConfig)
    {
        var providerChanged = newConfig.Provider != _config.Provider ||
                              newConfig.GetCurrentApiKey() != _config.GetCurrentApiKey();
        var shortcutsChanged = newConfig.ShortcutModifiers != _config.ShortcutModifiers ||
                               newConfig.ShortcutKeyCode != _config.ShortcutKeyCode ||
                               newConfig.PushToTalkKeyCode != _config.PushToTalkKeyCode;
        var audioCaptureModeChanged = newConfig.AudioCaptureMode != _config.AudioCaptureMode;

        _config = newConfig;
        _modeManager.ReloadModes();

        if (providerChanged)
        {
            ReloadProvider();
        }

        if (shortcutsChanged)
        {
            ReloadHotkeys();
        }

        if (audioCaptureModeChanged)
        {
            ApplyAudioCaptureMode(warmImmediately: true);
        }

        // Update tray menu labels
        _trayIcon.UpdateShortcutLabels(
            _config.GetToggleShortcutDescription(),
            _config.GetPushToTalkKeyDescription()
        );
        _trayIcon.UpdatePostActions(_config.PostActions, _config.ActivePostActionId);

        _trayIcon.ShowNotification("Settings Saved", "Your preferences have been updated.");
    }

    private void OnPostActionSelected(string actionId)
    {
        _config.ActivePostActionId = PostActionConfig.NormalizeActiveId(_config.PostActions, actionId);
        _config.Save();
        _trayIcon.UpdatePostActions(_config.PostActions, _config.ActivePostActionId);
        Logger.Info($"Post-action selected from tray: {_config.ActivePostActionId}");
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

        if (_historyHotkeyId != 0)
        {
            _globalHotkey.Unregister(_historyHotkeyId);
            _historyHotkeyId = 0;
        }

        // Stop hooks before registering them again
        _keyboardHook.Stop();
        _modeSwitchHook.Stop();
        _escapeHook.Stop();

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
        _modeSwitchHook.Dispose();
        _escapeHook.Dispose();
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
