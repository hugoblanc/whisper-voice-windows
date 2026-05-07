using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
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
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_TAB = 0x09;
    public const ushort VK_ESCAPE = 0x1B;

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

        if (!Copy(text))
        {
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

    public static bool Copy(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Logger.Warn("Clipboard copy called with empty text");
            return false;
        }

        try
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                SetClipboardContent(text);
                Logger.Debug("Text copied to clipboard (STA thread)");
            }
            else
            {
                Exception? threadException = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        SetClipboardContent(text);
                    }
                    catch (Exception ex)
                    {
                        threadException = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                if (!thread.Join(1000))
                {
                    throw new TimeoutException("Clipboard copy timed out");
                }

                if (threadException != null)
                {
                    throw threadException;
                }

                Logger.Debug("Text copied to clipboard (MTA thread)");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to copy text to clipboard", ex);
            return false;
        }
    }

    public static bool SendKey(ushort keyCode, ushort? modifier = null)
    {
        var inputs = new List<INPUT>();

        if (modifier.HasValue)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = modifier.Value } }
            });
        }

        inputs.Add(new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = keyCode } }
        });
        inputs.Add(new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = keyCode, dwFlags = KEYEVENTF_KEYUP } }
        });

        if (modifier.HasValue)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = modifier.Value, dwFlags = KEYEVENTF_KEYUP } }
            });
        }

        var result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        var lastError = result == 0 ? Marshal.GetLastWin32Error() : 0;
        Logger.Debug($"SendKey key=0x{keyCode:X2} modifier={(modifier.HasValue ? $"0x{modifier.Value:X2}" : "none")} result={result}/{inputs.Count} lastError={lastError}");
        return result == (uint)inputs.Count;
    }

    private static void SetClipboardContent(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, true, text);
        data.SetData(DataFormats.Text, true, text);

        var html = TryBuildHtmlClipboardPayload(text);
        if (!string.IsNullOrWhiteSpace(html))
        {
            data.SetData(DataFormats.Html, true, html);
            Logger.Info($"[Paste] wrote plain + HTML ({html.Length} chars)");
        }

        System.Windows.Forms.Clipboard.SetDataObject(data, true);
    }

    private static string? TryBuildHtmlClipboardPayload(string text)
    {
        if (!MarkupDetected(text))
        {
            return null;
        }

        var fragment = MarkupToHtmlFragment(text);
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        return BuildCfHtml(fragment);
    }

    private static bool MarkupDetected(string text)
    {
        if (text.Contains("```") || text.Contains('`')) return true;
        if (Regex.IsMatch(text, @"\*\*[^\s*].+?\*\*")) return true;
        if (Regex.IsMatch(text, @"\*[^\s*][^*\r\n]*\*")) return true;
        if (Regex.IsMatch(text, @"(^|\s)_[^\s_][^_\r\n]*_(\s|$|[.,!?;:])")) return true;
        if (Regex.IsMatch(text, @"(^|\n)\s*(>|[-*]\s|[0-9]+\.\s|•\s)")) return true;
        if (Regex.IsMatch(text, @"(^|\n)#{1,6}\s+\S")) return true;
        return false;
    }

    private static string MarkupToHtmlFragment(string text)
    {
        var work = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var blocks = new List<string>();
        var inlines = new List<string>();

        work = Regex.Replace(work, "```([\\s\\S]*?)```", match =>
        {
            var inner = match.Groups[1].Value.Trim('\n');
            blocks.Add($"<pre><code>{WebUtility.HtmlEncode(inner)}</code></pre>");
            return $"\u0001CB{blocks.Count - 1}\u0001";
        });

        work = Regex.Replace(work, "`([^`\n]+)`", match =>
        {
            inlines.Add($"<code>{WebUtility.HtmlEncode(match.Groups[1].Value)}</code>");
            return $"\u0001IC{inlines.Count - 1}\u0001";
        });

        work = WebUtility.HtmlEncode(work);
        work = Regex.Replace(work, @"\*\*([^\s*][\s\S]*?[^\s*])\*\*", "<strong>$1</strong>");
        work = Regex.Replace(work, @"\*([^\s*][^*\n]*?[^\s*])\*", "<strong>$1</strong>");
        work = Regex.Replace(work, @"(^|\s)_([^\s_][^_\n]*?[^\s_])_(\s|$|[.,!?;:])", "$1<em>$2</em>$3");

        foreach (var (html, i) in blocks.Select((html, i) => (html, i)))
        {
            work = work.Replace($"\u0001CB{i}\u0001", html);
        }

        foreach (var (html, i) in inlines.Select((html, i) => (html, i)))
        {
            work = work.Replace($"\u0001IC{i}\u0001", html);
        }

        var lines = work.Split('\n');
        var htmlBuilder = new StringBuilder();
        var inList = false;
        var inQuote = false;

        void CloseBlocks()
        {
            if (inList)
            {
                htmlBuilder.Append("</ul>");
                inList = false;
            }

            if (inQuote)
            {
                htmlBuilder.Append("</blockquote>");
                inQuote = false;
            }
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                CloseBlocks();
                htmlBuilder.Append("<p></p>");
                continue;
            }

            var heading = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
            if (heading.Success)
            {
                CloseBlocks();
                var level = Math.Min(heading.Groups[1].Value.Length, 6);
                htmlBuilder.Append($"<h{level}>{heading.Groups[2].Value}</h{level}>");
                continue;
            }

            if (trimmed.StartsWith("&gt;"))
            {
                if (inList)
                {
                    htmlBuilder.Append("</ul>");
                    inList = false;
                }

                if (!inQuote)
                {
                    htmlBuilder.Append("<blockquote>");
                    inQuote = true;
                }

                htmlBuilder.Append(trimmed[4..].TrimStart()).Append("<br>");
                continue;
            }

            var bullet = Regex.Match(trimmed, @"^([-*]|•|\d+\.)\s+(.+)$");
            if (bullet.Success)
            {
                if (inQuote)
                {
                    htmlBuilder.Append("</blockquote>");
                    inQuote = false;
                }

                if (!inList)
                {
                    htmlBuilder.Append("<ul>");
                    inList = true;
                }

                htmlBuilder.Append("<li>").Append(bullet.Groups[2].Value).Append("</li>");
                continue;
            }

            CloseBlocks();
            htmlBuilder.Append("<p>").Append(line).Append("</p>");
        }

        CloseBlocks();
        return htmlBuilder.ToString();
    }

    private static string BuildCfHtml(string fragment)
    {
        const string startFragmentMarker = "<!--StartFragment-->";
        const string endFragmentMarker = "<!--EndFragment-->";
        var htmlPrefix = $"<html><body>{startFragmentMarker}";
        var htmlSuffix = $"{endFragmentMarker}</body></html>";
        const string headerTemplate =
            "Version:0.9\r\n" +
            "StartHTML:{0:0000000000}\r\n" +
            "EndHTML:{1:0000000000}\r\n" +
            "StartFragment:{2:0000000000}\r\n" +
            "EndFragment:{3:0000000000}\r\n";

        var emptyHeader = string.Format(headerTemplate, 0, 0, 0, 0);
        var startHtml = Encoding.UTF8.GetByteCount(emptyHeader);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(htmlPrefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(htmlSuffix);
        var header = string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment);
        return header + htmlPrefix + fragment + htmlSuffix;
    }
}
