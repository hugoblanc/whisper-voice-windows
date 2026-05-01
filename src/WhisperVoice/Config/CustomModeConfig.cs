namespace WhisperVoice.Config;

public class CustomModeConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Icon { get; set; } = "star";

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Prompt);

    public CustomModeConfig Clone() => new()
    {
        Id = Id,
        Name = Name,
        Prompt = Prompt,
        Enabled = Enabled,
        Icon = Icon
    };

    public void EnsureId(IEnumerable<string> existingIds)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = CreateUniqueId(Name, existingIds);
        }
    }

    public static string CreateUniqueId(string name, IEnumerable<string> existingIds)
    {
        var baseId = "custom_" + Slugify(name);
        if (baseId == "custom_")
        {
            baseId = "custom_mode";
        }

        var existing = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var candidate = baseId;
        var suffix = 2;

        while (existing.Contains(candidate))
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
                if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_') return '_';
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
