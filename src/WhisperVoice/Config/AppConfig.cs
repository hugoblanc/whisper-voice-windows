using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhisperVoice.Config;

public enum AudioCaptureMode
{
    Instant,
    Balanced,
    Privacy
}

public class AppConfig
{
    public string Provider { get; set; } = "openai";      // Default provider for backward compatibility
    public string ApiKey { get; set; } = string.Empty;    // Main API key (backward compatibility)
    public Dictionary<string, string>? ProviderApiKeys { get; set; }  // Per-provider API keys
    public uint ShortcutModifiers { get; set; } = 0x0006; // MOD_CONTROL | MOD_SHIFT (Ctrl+Shift)
    public uint ShortcutKeyCode { get; set; } = 0x20;     // VK_SPACE
    public uint PushToTalkKeyCode { get; set; } = 0x72;   // VK_F3
    public List<CustomModeConfig> CustomModes { get; set; } = new();
    public List<string> DisabledBuiltInModeIds { get; set; } = new();
    public AudioCaptureMode AudioCaptureMode { get; set; } = AudioCaptureMode.Instant;
    public bool AutoModeEnabled { get; set; } = true;
    public List<AutoModeRuleConfig> AutoModeRules { get; set; } = AutoModeRuleConfig.CreateDefaults();

    /// <summary>
    /// Get the API key for the specified provider, falling back to main ApiKey for backward compatibility
    /// </summary>
    public string GetApiKeyForProvider(string providerId)
    {
        if (ProviderApiKeys?.TryGetValue(providerId, out var key) == true && !string.IsNullOrEmpty(key))
        {
            return key;
        }
        return ApiKey;
    }

    /// <summary>
    /// Get the API key for the currently selected provider
    /// </summary>
    public string GetCurrentApiKey() => GetApiKeyForProvider(Provider);

    /// <summary>
    /// Get OpenAI API key for AI processing modes.
    /// Returns the key from providerApiKeys.openai, or the main apiKey if using OpenAI as provider.
    /// </summary>
    public string? GetOpenAIKeyForProcessing()
    {
        // First check providerApiKeys for explicit OpenAI key
        if (ProviderApiKeys?.TryGetValue("openai", out var key) == true &&
            !string.IsNullOrEmpty(key) &&
            key.StartsWith("sk-"))
        {
            return key;
        }

        // Fall back to main apiKey if using OpenAI as provider
        if (Provider == "openai" && !string.IsNullOrEmpty(ApiKey) && ApiKey.StartsWith("sk-"))
        {
            return ApiKey;
        }

        return null;
    }

    /// <summary>
    /// Check if AI processing modes are available (requires OpenAI key)
    /// </summary>
    public bool HasOpenAIKeyForProcessing => !string.IsNullOrEmpty(GetOpenAIKeyForProcessing());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
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
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            config?.EnsureDefaults();
            return config;
        }
        catch
        {
            return null;
        }
    }

    public void Save()
    {
        EnsureDefaults();
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private void EnsureDefaults()
    {
        ProviderApiKeys ??= new Dictionary<string, string>();
        CustomModes ??= new List<CustomModeConfig>();
        DisabledBuiltInModeIds ??= new List<string>();
        AutoModeRules ??= AutoModeRuleConfig.CreateDefaults();
        if (!Enum.IsDefined(AudioCaptureMode))
        {
            AudioCaptureMode = AudioCaptureMode.Instant;
        }

        var existingIds = new HashSet<string>(DisabledBuiltInModeIds, StringComparer.OrdinalIgnoreCase);
        foreach (var mode in CustomModes.Where(m => m.IsValid))
        {
            mode.EnsureId(existingIds);
            existingIds.Add(mode.Id);
        }

        var existingRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in AutoModeRules)
        {
            rule.EnsureId(existingRuleIds);
            existingRuleIds.Add(rule.Id);
        }
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

public class AutoModeRuleConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string ProcessName { get; set; } = "";
    public string WindowTitleContains { get; set; } = "";
    public string ModeId { get; set; } = "";

    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ModeId) &&
        (!string.IsNullOrWhiteSpace(ProcessName) || !string.IsNullOrWhiteSpace(WindowTitleContains));

    [JsonIgnore]
    public int Specificity =>
        (string.IsNullOrWhiteSpace(ProcessName) ? 0 : 1) +
        (string.IsNullOrWhiteSpace(WindowTitleContains) ? 0 : 1);

    public AutoModeRuleConfig Clone() => new()
    {
        Id = Id,
        Name = Name,
        Enabled = Enabled,
        ProcessName = ProcessName,
        WindowTitleContains = WindowTitleContains,
        ModeId = ModeId
    };

    public void EnsureId(ISet<string> existingIds)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = CreateUniqueId(Name, ProcessName, WindowTitleContains, existingIds);
        }
    }

    public static List<AutoModeRuleConfig> CreateDefaults() => new()
    {
        new AutoModeRuleConfig
        {
            Id = "slack_brut",
            Name = "Slack -> Brut",
            ProcessName = "Slack",
            ModeId = "voice-to-text"
        },
        new AutoModeRuleConfig
        {
            Id = "chrome_gmail_email",
            Name = "Gmail in Chrome -> Email",
            ProcessName = "chrome",
            WindowTitleContains = "Gmail",
            ModeId = "custom_email"
        },
        new AutoModeRuleConfig
        {
            Id = "vscode_brut",
            Name = "VS Code -> Brut",
            ProcessName = "Code",
            ModeId = "voice-to-text"
        }
    };

    private static string CreateUniqueId(string name, string processName, string title, ISet<string> existingIds)
    {
        var seed = string.IsNullOrWhiteSpace(name)
            ? $"{processName}_{title}_rule"
            : name;
        var baseId = "auto_" + Slugify(seed);
        if (baseId == "auto_")
        {
            baseId = "auto_rule";
        }

        var candidate = baseId;
        var suffix = 2;
        while (existingIds.Contains(candidate))
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch =>
            {
                if (char.IsLetterOrDigit(ch)) return ch;
                if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '>') return '_';
                return '\0';
            })
            .Where(ch => ch != '\0')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("__", StringComparison.Ordinal))
        {
            slug = slug.Replace("__", "_", StringComparison.Ordinal);
        }

        return slug.Trim('_');
    }
}
