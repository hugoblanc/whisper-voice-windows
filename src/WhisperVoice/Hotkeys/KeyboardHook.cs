using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WhisperVoice.Hotkeys;

public class KeyboardHook : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private uint _watchedKeyCode;
    private bool _keyIsDown;

    public event Action? KeyDown;
    public event Action? KeyUp;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start(uint keyCode)
    {
        _watchedKeyCode = keyCode;
        _keyIsDown = false;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        if (curModule != null)
        {
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _keyIsDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = (uint)Marshal.ReadInt32(lParam);

            if (vkCode == _watchedKeyCode)
            {
                var msg = (int)wParam;

                if ((msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) && !_keyIsDown)
                {
                    _keyIsDown = true;
                    KeyDown?.Invoke();
                }
                else if ((msg == WM_KEYUP || msg == WM_SYSKEYUP) && _keyIsDown)
                {
                    _keyIsDown = false;
                    KeyUp?.Invoke();
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }
}
