# Whisper Voice Windows - Project Context

## Overview

Native Windows voice transcription app supporting **OpenAI Whisper** and **Mistral Voxtral** APIs. Two recording modes: Toggle (Ctrl+Shift+Space) and Push-to-Talk (F3). Text is automatically pasted at cursor location.

**macOS counterpart**: [whisper-voice](https://github.com/hugoblanc/whisper-voice) - Keep features in sync!

## Tech Stack

- **.NET 8.0** (Windows Forms)
- **NAudio**: Audio recording (16kHz WAV)
- **HttpClient**: API calls
- **NotifyIcon**: System tray app
- **SendInput**: Keyboard simulation for paste

## Project Structure

```
whisper-voice-windows/
├── CLAUDE.md               # This file - project context
├── FEATURES.md             # Feature parity checklist with macOS
├── README.md               # User documentation
├── docs/
│   └── CODE_SIGNING.md     # Windows code signing guide
├── icons/                  # Tray and app icons
│   ├── app.ico
│   ├── mic_idle.ico
│   ├── mic_recording.ico
│   └── mic_transcribing.ico
├── install.ps1             # Installation script
├── uninstall.ps1           # Uninstallation script
└── src/WhisperVoice/
    ├── WhisperVoice.csproj # Project file
    ├── WhisperVoice.sln    # Solution file
    ├── Program.cs          # Entry point
    ├── WhisperVoiceApp.cs  # Main app logic
    ├── Api/
    │   ├── ITranscriptionProvider.cs
    │   ├── BaseTranscriptionProvider.cs
    │   ├── TranscriptionProviderFactory.cs
    │   └── Providers/
    │       ├── OpenAIProvider.cs
    │       └── MistralProvider.cs
    ├── Audio/
    │   └── AudioRecorder.cs
    ├── Clipboard/
    │   └── ClipboardPaste.cs
    ├── Config/
    │   ├── AppConfig.cs
    │   ├── PreferencesWindow.cs
    │   └── SetupWizard.cs
    ├── Hotkeys/
    │   ├── GlobalHotkey.cs
    │   └── KeyboardHook.cs
    ├── Logging/
    │   └── Logger.cs
    ├── Processing/
    │   ├── AIMode.cs           # AI processing modes
    │   ├── ModeManager.cs      # Mode switching logic
    │   └── TextProcessor.cs    # GPT-4o-mini API
    ├── Tray/
    │   └── TrayIcon.cs
    └── UI/
        └── RecordingWindow.cs  # Waveform window
```

## Key Classes

| Class | File | Role |
|-------|------|------|
| `WhisperVoiceApp` | WhisperVoiceApp.cs | Main app, hotkeys, recording flow |
| `AppConfig` | Config/AppConfig.cs | JSON config management |
| `PreferencesWindow` | Config/PreferencesWindow.cs | Settings UI |
| `SetupWizard` | Config/SetupWizard.cs | First-run setup |
| `AudioRecorder` | Audio/AudioRecorder.cs | NAudio recording |
| `OpenAIProvider` | Api/Providers/OpenAIProvider.cs | OpenAI Whisper API |
| `MistralProvider` | Api/Providers/MistralProvider.cs | Mistral Voxtral API |
| `AIMode` | Processing/AIMode.cs | Mode definitions |
| `ModeManager` | Processing/ModeManager.cs | Mode switching |
| `TextProcessor` | Processing/TextProcessor.cs | GPT-4o-mini processing |
| `RecordingWindow` | UI/RecordingWindow.cs | Waveform visualization |
| `TrayIcon` | Tray/TrayIcon.cs | System tray management |
| `GlobalHotkey` | Hotkeys/GlobalHotkey.cs | RegisterHotKey API |
| `KeyboardHook` | Hotkeys/KeyboardHook.cs | Low-level keyboard hook |
| `ClipboardPaste` | Clipboard/ClipboardPaste.cs | SendInput for Ctrl+V |
| `Logger` | Logging/Logger.cs | File logging |

## Configuration

Config file: `%LOCALAPPDATA%\WhisperVoice\config.json`

```json
{
    "provider": "openai",
    "apiKey": "sk-...",
    "providerApiKeys": {
        "openai": "sk-...",
        "mistral": "..."
    },
    "shortcutModifiers": 6,
    "shortcutKeyCode": 32,
    "pushToTalkKeyCode": 114
}
```

**Modifier values**: 1=Alt, 2=Ctrl, 4=Shift, 8=Win (combine with addition)
**Key codes**: Space=32, F1-F12=112-123

Logs: `%LOCALAPPDATA%\WhisperVoice\logs\whispervoice_YYYY-MM-DD.log`

## Building

```powershell
cd src\WhisperVoice
dotnet build              # Debug build
dotnet publish -c Release # Release build (single EXE)
```

Output: `bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe`

## Key Implementation Details

### System Tray App
Using `NotifyIcon` with embedded .ico resources. Icons switch based on state (idle/recording/transcribing).

### Global Hotkeys
- Toggle mode: `RegisterHotKey` Windows API via `GlobalHotkey` class
- Push-to-Talk: Low-level keyboard hook via `KeyboardHook` class
- Mode switching: Shift key hook during recording

### Audio Recording
Using NAudio `WaveInEvent` with 16kHz, 16-bit, mono WAV format for API compatibility.

### Paste via SendInput
Using `SendInput` Win32 API to simulate Ctrl+V keystroke (more reliable than clipboard-only approach).

### Multi-Provider Architecture
`TranscriptionProviderFactory` creates provider based on config. Both providers inherit from `BaseTranscriptionProvider` for shared retry logic.

### AI Processing Modes
After transcription, text can be processed with GPT-4o-mini:
- **Brut**: No processing (raw transcription)
- **Clean**: Remove filler words, fix punctuation
- **Formel**: Professional tone
- **Casual**: Friendly tone
- **Markdown**: Convert to structured markdown

Mode switching with Shift key during recording. Requires OpenAI API key in `providerApiKeys.openai`.

### Recording Window
WinForms window with:
- Animated waveform visualization (double-buffered panel)
- Recording timer
- Status indicator (color changes: red→blue→green)
- Cancel button + Escape key support

## Common Issues

### Hotkey doesn't work
- Another app may be using the same shortcut
- Try different modifiers in Preferences
- Check Windows focus (some apps block global hotkeys)

### Text not pasting
- Some apps block simulated keyboard input
- Check clipboard contains the text (Ctrl+V manually)

### Antivirus blocking
- Some AV software flags keyboard hooks
- Add exception for WhisperVoice.exe

### AI modes not working
- Requires OpenAI API key in `providerApiKeys.openai`
- Even when using Mistral for transcription

## Feature Parity with macOS

See [FEATURES.md](FEATURES.md) for detailed checklist.

**Implemented**: OpenAI, Mistral, AI modes, recording window, preferences
**Not yet implemented**: Local mode (whisper.cpp), transcription history

## Version History

- **v3.0.0**: AI modes, recording window, mode switching
- **v2.3.0**: Mistral support, improved hotkeys
- **v2.0.0**: Multi-provider architecture
- **v1.0.0**: Initial release (OpenAI only)
