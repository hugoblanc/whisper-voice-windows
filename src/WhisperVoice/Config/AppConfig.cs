using System.Text.Json;

namespace WhisperVoice.Config;

public class AppConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public uint ShortcutModifiers { get; set; } = 0x0006; // MOD_CONTROL | MOD_SHIFT (Ctrl+Shift)
    public uint ShortcutKeyCode { get; set; } = 0x20;     // VK_SPACE
    public uint PushToTalkKeyCode { get; set; } = 0x72;   // VK_F3

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhisperVoice");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig? Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return null;

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    public string GetToggleShortcutDescription()
    {
        var parts = new List<string>();

        if ((ShortcutModifiers & 0x0001) != 0) parts.Add("Alt");
        if ((ShortcutModifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((ShortcutModifiers & 0x0004) != 0) parts.Add("Shift");
        if ((ShortcutModifiers & 0x0008) != 0) parts.Add("Win");

        parts.Add(GetKeyName(ShortcutKeyCode));

        return string.Join("+", parts);
    }

    public string GetPushToTalkKeyDescription()
    {
        return GetKeyName(PushToTalkKeyCode);
    }

    private static string GetKeyName(uint keyCode) => keyCode switch
    {
        0x20 => "Space",
        0x70 => "F1",
        0x71 => "F2",
        0x72 => "F3",
        0x73 => "F4",
        0x74 => "F5",
        0x75 => "F6",
        0x76 => "F7",
        0x77 => "F8",
        0x78 => "F9",
        0x79 => "F10",
        0x7A => "F11",
        0x7B => "F12",
        _ => $"Key{keyCode:X2}"
    };
}
