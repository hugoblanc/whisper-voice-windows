# Whisper Voice for Windows

> Voice-to-text for Windows with AI processing modes. Press a shortcut, speak, text appears at your cursor.

Native Windows app supporting **OpenAI Whisper** and **Mistral Voxtral**. Lightweight single EXE (~70 MB).

**macOS user?** Check out [whisper-voice](https://github.com/hugoblanc/whisper-voice).

---

## Download

### Easy Install (Recommended)

1. **[Download WhisperVoice.exe](https://github.com/hugoblanc/whisper-voice-windows/releases/latest/download/WhisperVoice.exe)**
2. Double-click to run
3. A setup wizard will guide you through configuration
4. Done! The app runs in your system tray

### Build from Source

```powershell
git clone https://github.com/hugoblanc/whisper-voice-windows.git
cd whisper-voice-windows\src\WhisperVoice
dotnet publish -c Release
.\bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe
```

---

## How It Works

### Toggle Mode (default)
1. Press **Ctrl+Shift+Space** (configurable)
2. Speak - a recording window with waveform appears
3. Press **Shift** to cycle through AI modes (optional)
4. Press again to stop → Text is processed and pasted

### Push-to-Talk Mode
1. Hold **F3** (configurable)
2. Speak while holding
3. Release to transcribe and paste

---

## Features

### Recording Window
- **Live waveform visualization** with animated audio levels
- **Recording timer** showing elapsed time
- **Status indicators**: red (recording), blue (processing)
- **Cancel button** or press Escape to abort

### AI Processing Modes
Switch modes by pressing **Shift** during recording:

| Mode | Description |
|------|-------------|
| **Brut** | Raw transcription, no processing |
| **Clean** | Removes filler words (um, uh), fixes punctuation |
| **Formel** | Professional tone, proper structure |
| **Casual** | Natural, friendly tone |
| **Markdown** | Converts to headers, lists, code blocks |

> AI modes require an OpenAI API key (uses GPT-4o-mini for processing)

---

## Supported Providers

| Provider | Model | Cost | Setup |
|----------|-------|------|-------|
| **OpenAI** | gpt-4o-mini-transcribe | ~$0.006/min | [Get API key](https://platform.openai.com/api-keys) |
| **Mistral** | voxtral-mini | ~$0.001/min | [Get API key](https://console.mistral.ai/api-keys) |

> **Note**: You provide your own API key. For AI processing modes, you need an OpenAI key (even if using Mistral for transcription).

---

## System Tray

The app lives in your system tray with status indicators:
- **Gray mic** - Idle, ready to record
- **Red mic** - Recording in progress
- **Orange mic** - Transcribing/Processing

Right-click the tray icon for options:
- Current mode (and how to switch)
- Preferences
- Logs
- Quit

---

## Configuration

Config file: `%LOCALAPPDATA%\WhisperVoice\config.json`

```json
{
  "provider": "openai",
  "apiKey": "sk-...",
  "providerApiKeys": {
    "openai": "sk-your-openai-key",
    "mistral": "your-mistral-key"
  },
  "shortcutModifiers": 6,
  "shortcutKeyCode": 32,
  "pushToTalkKeyCode": 114
}
```

### API Keys for AI Modes

To use AI processing modes with Mistral transcription, add your OpenAI key:

```json
{
  "provider": "mistral",
  "apiKey": "your-mistral-key",
  "providerApiKeys": {
    "openai": "sk-your-openai-key"
  }
}
```

### Shortcut Modifier Values
- `1` = Alt
- `2` = Ctrl
- `4` = Shift
- `8` = Win
- Combine: `6` = Ctrl+Shift, `3` = Ctrl+Alt

### Key Codes
- Space: `32` (0x20)
- F1-F12: `112-123` (0x70-0x7B)

---

## Requirements

- Windows 10 (1809+) or Windows 11
- Microphone
- Internet connection
- API key from OpenAI or Mistral

---

## Troubleshooting

### Hotkey doesn't work
- Another app may be using the same shortcut
- Try a different shortcut in Preferences
- Restart the app

### Text not pasting
- Some apps block simulated keyboard input
- Try pasting manually (Ctrl+V) after transcription

### AI modes are grayed out
Add an OpenAI API key to `providerApiKeys.openai` in your config.

### Check logs
Right-click tray icon → Open Logs

---

## What's New in v3.0

- **Recording window** with live waveform visualization
- **AI processing modes** (Clean, Formal, Casual, Markdown)
- **Mode switching** with Shift key during recording
- **Mistral Voxtral** support

---

## Uninstall

Run `uninstall.ps1` or manually:
1. Close the app from system tray
2. Delete the EXE file
3. Delete `%LOCALAPPDATA%\WhisperVoice\`

---

## License

MIT
