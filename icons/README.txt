Whisper Voice - Icon Files Required
====================================

Place the following .ico files in this directory:

1. app.ico           - Main application icon (used for EXE and taskbar)
2. mic_idle.ico      - System tray icon when idle (gray microphone)
3. mic_recording.ico - System tray icon when recording (red microphone)
4. mic_transcribing.ico - System tray icon when transcribing (orange/loading)

Icon Requirements:
- Format: ICO (Windows icon format)
- Recommended sizes: 16x16, 32x32, 48x48, 256x256 (multi-resolution ICO)
- Transparent background

You can convert PNG to ICO using:
- Online: https://convertio.co/png-ico/
- ImageMagick: magick convert icon.png -define icon:auto-resize=256,48,32,16 icon.ico

If icons are missing, the application will use the default Windows application icon.
