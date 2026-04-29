using System.Runtime.InteropServices;
using WhisperVoice.Logging;

namespace WhisperVoice.Clipboard;

public static class ClipboardPaste
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

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
    private const ushort VK_V = 0x56;

    public static IntPtr CaptureTargetWindow()
    {
        return GetForegroundWindow();
    }

    public static bool Paste(string text, IntPtr targetWindow = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            Logger.Warn("ClipboardPaste called with empty text");
            return false;
        }

        Logger.Debug($"Starting paste operation ({text.Length} chars)");

        if (targetWindow == IntPtr.Zero || !IsWindow(targetWindow))
        {
            targetWindow = GetForegroundWindow();
            Logger.Debug("Paste target was unavailable; using current foreground window");
        }

        if (targetWindow != IntPtr.Zero)
        {
            Logger.Debug($"Restoring paste target window: 0x{targetWindow.ToInt64():X}");

            // Attach to target window's thread to allow SetForegroundWindow
            var targetThread = GetWindowThreadProcessId(targetWindow, out _);
            var currentThread = GetCurrentThreadId();
            bool attached = false;

            if (targetThread != currentThread)
            {
                attached = AttachThreadInput(currentThread, targetThread, true);
                Logger.Debug($"Attached to target thread (current: {currentThread}, target: {targetThread})");
            }

            // Activate window
            var foregroundChanged = SetForegroundWindow(targetWindow);
            Logger.Debug($"SetForegroundWindow result: {foregroundChanged}");
            Thread.Sleep(120);

            if (attached)
            {
                AttachThreadInput(currentThread, targetThread, false);
            }
        }
        else
        {
            Logger.Warn("No target window available for paste");
        }

        // Copy text to clipboard (must be on STA thread)
        try
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                System.Windows.Forms.Clipboard.SetText(text);
                Logger.Debug("Text copied to clipboard (STA thread)");
            }
            else
            {
                var thread = new Thread(() => System.Windows.Forms.Clipboard.SetText(text));
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(1000);
                Logger.Debug("Text copied to clipboard (MTA thread)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to copy text to clipboard", ex);
            return false;
        }

        // Longer delay to ensure clipboard is ready
        Thread.Sleep(150); // Increased from 100ms

        // Simulate Ctrl+V
        Logger.Debug("Sending Ctrl+V keystroke");
        var kbInputs = new INPUT[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL } }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_V } }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_V, dwFlags = KEYEVENTF_KEYUP } }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } }
            }
        };

        var inputSize = Marshal.SizeOf<INPUT>();
        var result = SendInput((uint)kbInputs.Length, kbInputs, inputSize);
        var lastError = result == 0 ? Marshal.GetLastWin32Error() : 0;
        Logger.Debug($"SendInput result: {result} events sent (INPUT size: {inputSize}, last error: {lastError})");
        var success = result == (uint)kbInputs.Length;
        if (success)
        {
            Logger.Info("Paste operation completed");
        }
        else
        {
            Logger.Warn($"Paste operation may be incomplete: {result}/{kbInputs.Length} input events sent");
        }

        return success;
    }
}
