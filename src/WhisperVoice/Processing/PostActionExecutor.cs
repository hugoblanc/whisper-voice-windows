using System.Diagnostics;
using WhisperVoice.Clipboard;
using WhisperVoice.Config;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

public record PostActionResult(bool Success, bool PasteAttempted, bool Pasted, string Detail);

public static class PostActionExecutor
{
    private const int CommandTimeoutMs = 30_000;

    public static async Task<PostActionResult> ExecuteAsync(
        PostActionConfig action,
        string text,
        string rawText,
        DictationContext? context,
        string provider,
        string mode,
        IntPtr targetWindow)
    {
        Logger.Info($"[PostAction] Executing '{action.Label}' ({action.Type})");

        return action.Type switch
        {
            "paste" => Paste(text, targetWindow),
            "pasteEnter" => await PasteThenKeyAsync(text, targetWindow, ClipboardPaste.VK_RETURN, "Enter"),
            "copy" => Copy(text),
            "pasteTab" => await PasteThenKeyAsync(text, targetWindow, ClipboardPaste.VK_TAB, "Tab"),
            "pasteSend" => await PasteThenKeyAsync(text, targetWindow, ClipboardPaste.VK_RETURN, "Ctrl+Enter", modifier: 0x11),
            "pasteEscape" => await PasteThenKeyAsync(text, targetWindow, ClipboardPaste.VK_ESCAPE, "Escape"),
            "command" => await RunCommandAsync(action, text, rawText, context, provider, mode),
            _ => Paste(text, targetWindow)
        };
    }

    private static PostActionResult Paste(string text, IntPtr targetWindow)
    {
        var pasted = ClipboardPaste.Paste(text, targetWindow);
        return new PostActionResult(pasted, PasteAttempted: true, Pasted: pasted, pasted ? "paste sent" : "clipboard only");
    }

    private static PostActionResult Copy(string text)
    {
        var copied = ClipboardPaste.Copy(text);
        return new PostActionResult(copied, PasteAttempted: false, Pasted: false, copied ? "copied to clipboard" : "copy failed");
    }

    private static async Task<PostActionResult> PasteThenKeyAsync(
        string text,
        IntPtr targetWindow,
        ushort keyCode,
        string keyLabel,
        ushort? modifier = null)
    {
        var pasted = ClipboardPaste.Paste(text, targetWindow);
        if (!pasted)
        {
            return new PostActionResult(false, PasteAttempted: true, Pasted: false, "paste failed before key");
        }

        await Task.Delay(180);
        var keySent = ClipboardPaste.SendKey(keyCode, modifier);
        return new PostActionResult(
            keySent,
            PasteAttempted: true,
            Pasted: true,
            keySent ? $"paste sent; {keyLabel} sent" : $"paste sent; {keyLabel} failed");
    }

    private static async Task<PostActionResult> RunCommandAsync(
        PostActionConfig action,
        string text,
        string rawText,
        DictationContext? context,
        string provider,
        string mode)
    {
        if (string.IsNullOrWhiteSpace(action.Command))
        {
            Logger.Warn("[PostAction] Empty command action; falling back to clipboard copy");
            return Copy(text);
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + action.Command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            process.StartInfo.Environment["WV_TRANSCRIPTION"] = text;
            process.StartInfo.Environment["WV_RAW_TRANSCRIPTION"] = rawText;
            process.StartInfo.Environment["WV_APP_PROCESS"] = context?.ActiveProcessName ?? "";
            process.StartInfo.Environment["WV_APP_NAME"] = context?.ActiveProcessName ?? "";
            process.StartInfo.Environment["WV_APP_PATH"] = context?.ActiveProcessPath ?? "";
            process.StartInfo.Environment["WV_APP_WINDOW_TITLE"] = context?.ActiveWindowTitle ?? "";
            process.StartInfo.Environment["WV_BROWSER_URL"] = context?.BrowserUrl ?? "";
            process.StartInfo.Environment["WV_BROWSER_HOST"] = context?.BrowserHost ?? "";
            process.StartInfo.Environment["WV_WORKSPACE"] = context?.WorkspaceName ?? "";
            process.StartInfo.Environment["WV_PROJECT_ID"] = context?.ProjectId ?? "";
            process.StartInfo.Environment["WV_PROJECT"] = context?.ProjectName ?? "";
            process.StartInfo.Environment["WV_MODE"] = mode;
            process.StartInfo.Environment["WV_PROVIDER"] = provider;

            Logger.Info($"[PostAction] Running command: {Truncate(action.Command, 120)}");
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(CommandTimeoutMs));
            if (completed != waitTask)
            {
                TryKill(process);
                return new PostActionResult(false, PasteAttempted: false, Pasted: false, "command timed out");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode == 0)
            {
                Logger.Info("[PostAction] Command completed successfully");
                return new PostActionResult(true, PasteAttempted: false, Pasted: false,
                    string.IsNullOrWhiteSpace(stdout) ? "command exit 0" : $"command exit 0; {Truncate(stdout.Trim(), 120)}");
            }

            Logger.Warn($"[PostAction] Command exited with {process.ExitCode}: {Truncate(stderr, 200)}");
            return new PostActionResult(false, PasteAttempted: false, Pasted: false,
                $"command exit {process.ExitCode}: {Truncate(stderr.Trim(), 120)}");
        }
        catch (Exception ex)
        {
            Logger.Error("[PostAction] Command failed", ex);
            return new PostActionResult(false, PasteAttempted: false, Pasted: false, ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only.
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
