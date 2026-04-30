using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

public static class WindowsContextCapturer
{
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_C = 0x43;
    private static readonly int[] ModifierKeys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };

    public static DictationContext Capture(IntPtr targetWindow, bool includeSelectedText)
    {
        var context = new DictationContext();

        if (targetWindow == IntPtr.Zero || !IsWindow(targetWindow))
        {
            Logger.Debug("[ContextCapturer] No valid target window available");
            return context;
        }

        context.ActiveWindowTitle = GetTitle(targetWindow);
        PopulateProcessInfo(targetWindow, context);

        if (includeSelectedText)
        {
            context.SelectedText = CaptureSelectedText(targetWindow);
        }

        Logger.Debug($"[ContextCapturer] app={context.ActiveProcessName ?? "unknown"} title={context.ActiveWindowTitle ?? ""} selected={(context.HasSelectedText ? context.SelectedText!.Length : 0)} chars");
        return context;
    }

    private static string? GetTitle(IntPtr window)
    {
        var length = GetWindowTextLength(window);
        if (length <= 0) return null;

        var builder = new StringBuilder(length + 1);
        return GetWindowText(window, builder, builder.Capacity) > 0
            ? builder.ToString()
            : null;
    }

    private static void PopulateProcessInfo(IntPtr window, DictationContext context)
    {
        try
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0) return;

            using var process = Process.GetProcessById((int)processId);
            context.ActiveProcessName = process.ProcessName;

            try
            {
                context.ActiveProcessPath = process.MainModule?.FileName;
            }
            catch
            {
                // Some elevated/system processes do not expose their module path.
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[ContextCapturer] Failed to read process info: {ex.Message}");
        }
    }

    private static string? CaptureSelectedText(IntPtr targetWindow)
    {
        try
        {
            var previousClipboard = System.Windows.Forms.Clipboard.GetDataObject();
            var sentinel = $"__WHISPER_VOICE_COPY_SENTINEL_{Guid.NewGuid():N}__";

            try
            {
                System.Windows.Forms.Clipboard.SetText(sentinel);
                SetForegroundWindow(targetWindow);
                WaitForModifierRelease(TimeSpan.FromMilliseconds(700));
                Thread.Sleep(80);
                SendCtrlC();

                var copiedText = WaitForClipboardText(sentinel, TimeSpan.FromMilliseconds(500));
                if (!string.IsNullOrWhiteSpace(copiedText))
                {
                    return copiedText.Trim();
                }

                return null;
            }
            finally
            {
                RestoreClipboard(previousClipboard);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[ContextCapturer] Selected text capture skipped: {ex.Message}");
            return null;
        }
    }

    private static string? WaitForClipboardText(string sentinel, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (System.Windows.Forms.Clipboard.ContainsText())
                {
                    var text = System.Windows.Forms.Clipboard.GetText();
                    if (!string.Equals(text, sentinel, StringComparison.Ordinal))
                    {
                        return text;
                    }
                }
            }
            catch
            {
                // Clipboard can be temporarily locked by the target app.
            }

            Thread.Sleep(40);
        }

        return null;
    }

    private static void RestoreClipboard(IDataObject? previousClipboard)
    {
        try
        {
            if (previousClipboard != null)
            {
                System.Windows.Forms.Clipboard.SetDataObject(previousClipboard, true);
            }
            else
            {
                System.Windows.Forms.Clipboard.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[ContextCapturer] Failed to restore clipboard: {ex.Message}");
        }
    }

    private static void SendCtrlC()
    {
        var inputs = new[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL } }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_C } }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_C, dwFlags = KEYEVENTF_KEYUP } }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } }
            }
        };

        var result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (result != inputs.Length)
        {
            Logger.Debug($"[ContextCapturer] Ctrl+C sent {result}/{inputs.Length} events");
        }
    }

    private static void WaitForModifierRelease(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (!ModifierKeys.Any(IsKeyDown))
            {
                return;
            }

            Thread.Sleep(25);
        }
    }

    private static bool IsKeyDown(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
