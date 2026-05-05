using System.Text.RegularExpressions;
using Microsoft.Win32;
using WhisperVoice.Logging;

namespace WhisperVoice.Config;

public static class StartupManager
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WhisperVoice";

    public static void SetAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key == null) return;

        if (enable)
        {
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    public static void RepairAutoStartPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            var command = key.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(command)) return;

            var currentPath = Path.GetFullPath(Application.ExecutablePath);
            var configuredPath = ParseExecutablePath(command);
            if (string.IsNullOrWhiteSpace(configuredPath)) return;

            configuredPath = Path.GetFullPath(configuredPath);
            if (string.Equals(configuredPath, currentPath, StringComparison.OrdinalIgnoreCase)) return;

            key.SetValue(ValueName, $"\"{currentPath}\"");
            Logger.Info($"Updated Windows startup entry from '{configuredPath}' to '{currentPath}'");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to repair Windows startup entry: {ex.Message}");
        }
    }

    private static string? ParseExecutablePath(string command)
    {
        command = command.Trim();
        if (command.Length == 0) return null;

        if (command[0] == '"')
        {
            var closingQuote = command.IndexOf('"', 1);
            return closingQuote > 1 ? command[1..closingQuote] : null;
        }

        var match = Regex.Match(command, @"^[^\s]+\.exe", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : command;
    }
}
