using System.Runtime.InteropServices;

namespace WhisperVoice.Hotkeys;

public class GlobalHotkey : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    // Modifier constants
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _windowHandle;
    private readonly List<int> _registeredIds = new();
    private int _nextId = 1;

    public GlobalHotkey(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public int Register(uint modifiers, uint keyCode)
    {
        var id = _nextId++;

        // Add MOD_NOREPEAT to prevent repeated hotkey messages while held
        if (!RegisterHotKey(_windowHandle, id, modifiers | MOD_NOREPEAT, keyCode))
        {
            throw new InvalidOperationException($"Failed to register hotkey. Error: {Marshal.GetLastWin32Error()}");
        }

        _registeredIds.Add(id);
        return id;
    }

    public void Unregister(int id)
    {
        if (_registeredIds.Contains(id))
        {
            UnregisterHotKey(_windowHandle, id);
            _registeredIds.Remove(id);
        }
    }

    public void Dispose()
    {
        foreach (var id in _registeredIds.ToList())
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _registeredIds.Clear();
    }
}
