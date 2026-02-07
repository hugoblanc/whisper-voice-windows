# Feature Parity Checklist: Windows vs macOS

This document tracks feature parity between the Windows and macOS versions of Whisper Voice.

**Reference**: [macOS repo](https://github.com/hugoblanc/whisper-voice)

---

## Core Features

| Feature | macOS | Windows | Notes |
|---------|:-----:|:-------:|-------|
| Toggle hotkey recording | ✅ | ✅ | Ctrl+Shift+Space (Win) / Option+Space (Mac) |
| Push-to-talk recording | ✅ | ✅ | F3 default |
| Auto-paste at cursor | ✅ | ✅ | SendInput (Win) / CGEvent (Mac) |
| System tray/menu bar | ✅ | ✅ | |
| Config file persistence | ✅ | ✅ | JSON config |
| Preferences window | ✅ | ✅ | |
| Setup wizard (first run) | ✅ | ✅ | |

---

## Transcription Providers

| Provider | macOS | Windows | Notes |
|----------|:-----:|:-------:|-------|
| OpenAI Whisper (gpt-4o-mini-transcribe) | ✅ | ✅ | |
| Mistral Voxtral (voxtral-mini) | ✅ | ✅ | |
| Local (whisper.cpp) | ✅ | ❌ | **TODO** - See implementation notes below |

### Local Mode Implementation Notes

macOS uses whisper-server (HTTP API on port 8178) bundled in the app:
- Server binary: `Contents/MacOS/whisper-server`
- Models downloaded from Hugging Face to `~/Library/Application Support/WhisperVoice/models/`
- Server starts on first local transcription, stays running for fast subsequent calls

For Windows implementation:
1. Need whisper.cpp Windows build (or use whisper.net NuGet package)
2. Model download UI in Preferences
3. Server lifecycle management
4. Consider using [Whisper.net](https://github.com/sandrohanea/whisper.net) for simpler integration

---

## AI Processing Modes

| Mode | macOS | Windows | Notes |
|------|:-----:|:-------:|-------|
| Brut (raw) | ✅ | ✅ | No processing |
| Clean | ✅ | ✅ | Remove filler words |
| Formel | ✅ | ✅ | Professional tone |
| Casual | ✅ | ✅ | Friendly tone |
| Markdown | ✅ | ✅ | Structure as MD |
| Mode switching (Shift key) | ✅ | ✅ | During recording |
| Mode indicator in UI | ✅ | ✅ | Tray menu + recording window |

---

## Recording UI

| Feature | macOS | Windows | Notes |
|---------|:-----:|:-------:|-------|
| Recording window | ✅ | ✅ | |
| Live waveform visualization | ✅ | ✅ | |
| Recording timer | ✅ | ✅ | |
| Status indicator (colors) | ✅ | ✅ | Red/Blue/Green |
| Current mode display | ✅ | ✅ | |
| Cancel button | ✅ | ✅ | |
| Escape to cancel | ✅ | ✅ | |
| Real audio level waveform | ✅ | ❌ | **TODO** - Currently simulated |

### Real Audio Level Implementation

macOS gets actual audio levels from AVAudioRecorder. For Windows:
```csharp
// In AudioRecorder.cs, expose audio level from NAudio:
public event Action<float>? AudioLevelChanged;

private void OnDataAvailable(object? sender, WaveInEventArgs e)
{
    // Calculate RMS level from buffer
    float max = 0;
    for (int i = 0; i < e.BytesRecorded; i += 2)
    {
        short sample = BitConverter.ToInt16(e.Buffer, i);
        float sample32 = sample / 32768f;
        if (sample32 > max) max = sample32;
    }
    AudioLevelChanged?.Invoke(max);

    _writer?.Write(e.Buffer, 0, e.BytesRecorded);
}
```

---

## Transcription History

| Feature | macOS | Windows | Notes |
|---------|:-----:|:-------:|-------|
| History window (Cmd/Ctrl+H) | ✅ | ❌ | **TODO** |
| Search past transcriptions | ✅ | ❌ | **TODO** |
| Copy from history | ✅ | ❌ | **TODO** |
| Delete entries | ✅ | ❌ | **TODO** |
| Provider/mode display | ✅ | ❌ | **TODO** |

### History Implementation Notes

macOS stores history in UserDefaults. For Windows:
1. Create `TranscriptionHistory.cs` with JSON storage
2. Create `HistoryWindow.cs` (WinForms DataGridView)
3. Store in `%LOCALAPPDATA%\WhisperVoice\history.json`
4. Add Ctrl+H global hotkey
5. Limit entries (e.g., last 100)

---

## Preferences Window

| Feature | macOS | Windows | Notes |
|---------|:-----:|:-------:|-------|
| Provider selection | ✅ | ✅ | |
| API key input | ✅ | ✅ | |
| Test connection button | ✅ | ✅ | |
| Toggle shortcut config | ✅ | ✅ | |
| PTT key config | ✅ | ✅ | |
| Logs tab | ✅ | ❌ | **TODO** - Currently opens log file |
| Local mode model download | ✅ | ❌ | Depends on local mode |

---

## Code Signing & Distribution

| Feature | macOS | Windows | Notes |
|---------|:-----:|:-------:|-------|
| Code signing | ✅ | ❌ | **TODO** - See docs/CODE_SIGNING.md |
| Notarization | ✅ | N/A | Apple-specific |
| No security warnings | ✅ | ❌ | Needs signed EXE |
| DMG installer | ✅ | N/A | |
| Single EXE distribution | N/A | ✅ | Self-contained |
| MSI/MSIX installer | N/A | ❌ | Optional enhancement |

---

## Logging & Debugging

| Feature | macOS | Windows | Notes |
|---------|:-----:|:-------:|-------|
| File logging | ✅ | ✅ | |
| Log rotation | ✅ | ✅ | 7 days |
| Logs in Preferences | ✅ | ❌ | Opens external file |
| Open log folder | ✅ | ✅ | |

---

## Priority TODO List

### High Priority (Feature Parity)
1. **Code signing** - Remove "Unknown Publisher" warning ($29/year Certum)
2. **Transcription history** - Ctrl+H to view past transcriptions
3. **Logs tab in Preferences** - Inline log viewer

### Medium Priority (Nice to Have)
4. **Local mode (whisper.cpp)** - Offline transcription
5. **Real audio levels** - Actual waveform from microphone
6. **Auto-update** - Check for new versions

### Low Priority (Future)
7. **MSI installer** - For enterprise deployment
8. **Multi-language prompts** - English mode prompts option
9. **Custom prompts** - User-defined AI modes

---

## Implementation Status by File

| File | Status | Notes |
|------|--------|-------|
| `WhisperVoiceApp.cs` | ✅ Complete | Main app logic |
| `AppConfig.cs` | ✅ Complete | Config with multi-provider keys |
| `OpenAIProvider.cs` | ✅ Complete | |
| `MistralProvider.cs` | ✅ Complete | |
| `LocalWhisperProvider.cs` | ❌ Missing | Needs whisper.cpp integration |
| `AIMode.cs` | ✅ Complete | 5 modes defined |
| `ModeManager.cs` | ✅ Complete | Mode switching logic |
| `TextProcessor.cs` | ✅ Complete | GPT-4o-mini API |
| `RecordingWindow.cs` | ✅ Complete | Waveform UI |
| `HistoryWindow.cs` | ❌ Missing | History viewer |
| `TranscriptionHistory.cs` | ❌ Missing | History storage |
| `PreferencesWindow.cs` | ⚠️ Partial | Missing logs tab |
| `TrayIcon.cs` | ✅ Complete | |
| `AudioRecorder.cs` | ⚠️ Partial | Missing audio level event |

---

## Testing Checklist

Before each release, verify:

- [ ] Toggle hotkey works (Ctrl+Shift+Space)
- [ ] PTT works (F3 hold/release)
- [ ] Mode switching (Shift during recording)
- [ ] All 5 AI modes process correctly
- [ ] Recording window appears and animates
- [ ] Cancel button works
- [ ] Escape key cancels
- [ ] Text pastes at cursor
- [ ] OpenAI transcription works
- [ ] Mistral transcription works
- [ ] Preferences save correctly
- [ ] App starts on Windows startup (if enabled)
- [ ] Tray icon shows correct states
- [ ] Logs are created
