using System.Runtime.InteropServices;

namespace WhisperVoice.Clipboard;

public static class ClipboardPaste
{
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
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

    public static void Paste(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Copy text to clipboard (must be on STA thread)
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            System.Windows.Forms.Clipboard.SetText(text);
        }
        else
        {
            // If not on STA thread, invoke on one
            var thread = new Thread(() => System.Windows.Forms.Clipboard.SetText(text));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(1000); // Wait max 1 second
        }

        // Small delay before pasting (like macOS version)
        Thread.Sleep(100);

        // Simulate Ctrl+V
        var inputs = new INPUT[]
        {
            // Ctrl down
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = VK_CONTROL }
                }
            },
            // V down
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = VK_V }
                }
            },
            // V up
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = VK_V, dwFlags = KEYEVENTF_KEYUP }
                }
            },
            // Ctrl up
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP }
                }
            }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
}
