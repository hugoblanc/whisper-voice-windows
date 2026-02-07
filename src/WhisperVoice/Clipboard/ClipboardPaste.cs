using System.Runtime.InteropServices;
using WhisperVoice.Logging;

namespace WhisperVoice.Clipboard;

public static class ClipboardPaste
{
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

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

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    public static void Paste(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Logger.Warn("ClipboardPaste called with empty text");
            return;
        }

        Logger.Debug($"Starting paste operation ({text.Length} chars)");

        // Get window under mouse cursor and activate it
        GetCursorPos(out var cursorPos);
        var targetWindow = WindowFromPoint(cursorPos);

        if (targetWindow != IntPtr.Zero)
        {
            Logger.Debug($"Target window found at cursor position ({cursorPos.X}, {cursorPos.Y})");

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
            SetForegroundWindow(targetWindow);
            Thread.Sleep(100); // Increased from 50ms

            // Click at cursor position to place the text caret there
            SendInput(2, new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_MOUSE,
                    U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } }
                },
                new INPUT
                {
                    type = INPUT_MOUSE,
                    U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } }
                }
            }, Marshal.SizeOf<INPUT>());

            Thread.Sleep(100); // Increased from 50ms

            if (attached)
            {
                AttachThreadInput(currentThread, targetThread, false);
            }
        }
        else
        {
            Logger.Warn("No target window found under cursor");
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
            return;
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

        var result = SendInput((uint)kbInputs.Length, kbInputs, Marshal.SizeOf<INPUT>());
        Logger.Debug($"SendInput result: {result} events sent");
        Logger.Info("Paste operation completed");
    }
}
