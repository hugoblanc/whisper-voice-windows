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
    private const int TreeScopeDescendants = 4;
    private const int UiaControlTypePropertyId = 30003;
    private const int UiaNamePropertyId = 30005;
    private const int UiaValueValuePropertyId = 30045;
    private const int UiaEditControlTypeId = 50004;

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
        PopulateEnrichedContext(targetWindow, context);

        if (includeSelectedText)
        {
            context.SelectedText = CaptureSelectedText(targetWindow);
        }

        Logger.Debug($"[ContextCapturer] app={context.ActiveProcessName ?? "unknown"} title={context.ActiveWindowTitle ?? ""} url={context.BrowserUrl ?? ""} workspace={context.WorkspaceName ?? ""} selected={(context.HasSelectedText ? context.SelectedText!.Length : 0)} chars");
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

    private static void PopulateEnrichedContext(IntPtr window, DictationContext context)
    {
        var processName = Path.GetFileNameWithoutExtension(context.ActiveProcessName ?? "");

        if (IsChromiumBrowser(processName))
        {
            var url = TryReadBrowserUrl(window);
            if (!string.IsNullOrWhiteSpace(url))
            {
                context.BrowserUrl = url;
                context.BrowserHost = TryGetHost(url);
            }
        }

        if (IsVsCode(processName))
        {
            context.WorkspaceName = TryExtractVsCodeWorkspace(context.ActiveWindowTitle);
        }
    }

    private static bool IsChromiumBrowser(string processName) =>
        processName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("msedge", StringComparison.OrdinalIgnoreCase);

    private static bool IsVsCode(string processName) =>
        processName.Equals("Code", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("Code - Insiders", StringComparison.OrdinalIgnoreCase);

    private static string? TryReadBrowserUrl(IntPtr window)
    {
        try
        {
            var automationType = Type.GetTypeFromProgID("UIAutomationClient.CUIAutomation");
            if (automationType == null) return null;

            dynamic? automation = Activator.CreateInstance(automationType);
            if (automation == null) return null;

            dynamic root = automation.ElementFromHandle(window);
            if (root == null) return null;

            dynamic condition = automation.CreatePropertyCondition(UiaControlTypePropertyId, UiaEditControlTypeId);
            dynamic edits = root.FindAll(TreeScopeDescendants, condition);
            int count = edits.Length;

            for (var i = 0; i < count; i++)
            {
                dynamic edit = edits.GetElement(i);
                var name = SafeAutomationString(() => Convert.ToString(edit.GetCurrentPropertyValue(UiaNamePropertyId)) ?? "");
                var value = SafeAutomationString(() => Convert.ToString(edit.GetCurrentPropertyValue(UiaValueValuePropertyId)) ?? "");
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = name;
                }

                if (LooksLikeBrowserAddressBar(name, value) && TryNormalizeUrl(value, out var normalized))
                {
                    return normalized;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[ContextCapturer] Browser URL capture skipped: {ex.Message}");
        }

        return null;
    }

    private static string SafeAutomationString(Func<string> read)
    {
        try
        {
            return read() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool LooksLikeBrowserAddressBar(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var lowerName = name.ToLowerInvariant();
        var namedLikeAddressBar =
            lowerName.Contains("address", StringComparison.Ordinal) ||
            lowerName.Contains("search", StringComparison.Ordinal) ||
            lowerName.Contains("adresse", StringComparison.Ordinal) ||
            lowerName.Contains("rechercher", StringComparison.Ordinal) ||
            lowerName.Contains("web", StringComparison.Ordinal);

        return namedLikeAddressBar || TryNormalizeUrl(value, out _);
    }

    private static bool TryNormalizeUrl(string value, out string normalized)
    {
        normalized = "";
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Contains(' ') && !value.Contains("://", StringComparison.Ordinal)) return false;

        var candidate = value;
        if (!candidate.Contains("://", StringComparison.Ordinal) &&
            (candidate.Contains('.') || candidate.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        if (string.IsNullOrWhiteSpace(uri.Host)) return false;

        normalized = uri.ToString();
        return true;
    }

    private static string? TryGetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
    }

    private static string? TryExtractVsCodeWorkspace(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var cleaned = title.Trim();
        foreach (var suffix in new[] { " - Visual Studio Code", " - Visual Studio Code - Insiders" })
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^suffix.Length];
                break;
            }
        }

        var parts = cleaned
            .Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length >= 2)
        {
            return parts[^1];
        }

        return parts.Length == 1 ? parts[0] : null;
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
