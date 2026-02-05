# Whisper Voice for Windows

Voice transcription app using OpenAI's Whisper API. Press a shortcut to record, and the transcribed text is automatically pasted at your cursor location.

## Quick Start

### Option 1: Download the EXE (Recommended)

1. Download `WhisperVoice.exe` from the releases
2. Double-click to run
3. On first launch, a setup wizard will appear:
   - Enter your OpenAI API key
   - Choose your toggle shortcut (default: Alt+Space)
   - Choose your push-to-talk key (default: F3)
4. Done! The app runs in your system tray

### Option 2: Build from Source

```powershell
# Clone the repo
git clone https://github.com/your-repo/whisper-voice-windows.git
cd whisper-voice-windows

# Build
cd src\WhisperVoice
dotnet publish -c Release

# Run
.\bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe
```

## Usage

### Toggle Mode (Default: Ctrl+Shift+Space)
- Press **Ctrl+Shift+Space** to start recording
- Press **Ctrl+Shift+Space** again to stop and transcribe
- Text is automatically pasted at cursor

### Push-to-Talk Mode (Default: F3)
- **Hold F3** to record
- **Release F3** to stop and transcribe
- Text is automatically pasted at cursor

## System Tray

The app lives in your system tray with status indicators:
- **Gray mic** - Idle, ready to record
- **Red mic** - Recording in progress
- **Orange mic** - Transcribing...

Right-click the tray icon for options.

## Configuration

Config file location: `%LOCALAPPDATA%\WhisperVoice\config.json`

```json
{
  "apiKey": "sk-...",
  "shortcutModifiers": 1,
  "shortcutKeyCode": 32,
  "pushToTalkKeyCode": 114
}
```

### Shortcut Modifier Values
- `1` = Alt
- `2` = Ctrl
- `4` = Shift
- `8` = Win
- Combine with addition (e.g., `12` = Win+Shift)

### Key Codes
- Space: `32` (0x20)
- F1-F12: `112-123` (0x70-0x7B)

## Requirements

- Windows 10 (version 1809+) or Windows 11
- Microphone
- Internet connection
- OpenAI API key with credits

## Uninstall

Run `uninstall.ps1` or manually:
1. Close the app from system tray
2. Delete the EXE file
3. Delete `%LOCALAPPDATA%\WhisperVoice\`
4. Remove from startup (if enabled): `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WhisperVoice`

## Troubleshooting

### Hotkey doesn't work
- Another app may be using the same shortcut
- Try a different shortcut in the config file
- Restart the app

### No transcription
- Check your API key is valid
- Check you have credits on your OpenAI account
- Check your internet connection

### Text not pasting
- Some apps block simulated keyboard input
- Try pasting manually (Ctrl+V) after transcription

## License

MIT
