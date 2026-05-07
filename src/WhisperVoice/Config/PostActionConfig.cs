using System.Text.Json.Serialization;

namespace WhisperVoice.Config;

public class PostActionConfig
{
    public const string BuiltInPasteId = "builtin-paste";

    private static readonly PostActionConfig[] BuiltIns =
    {
        new() { Id = BuiltInPasteId, Label = "Paste", Type = "paste" },
        new() { Id = "builtin-paste-enter", Label = "Paste + Enter", Type = "pasteEnter" },
        new() { Id = "builtin-copy", Label = "Copy only", Type = "copy" },
        new() { Id = "builtin-paste-tab", Label = "Paste + Tab", Type = "pasteTab" },
        new() { Id = "builtin-paste-send", Label = "Paste + Ctrl+Enter", Type = "pasteSend" },
        new() { Id = "builtin-paste-escape", Label = "Paste + Esc", Type = "pasteEscape" }
    };

    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "paste";
    public string Command { get; set; } = "";

    [JsonIgnore]
    public bool IsBuiltIn => Id.StartsWith("builtin-", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(Label) &&
        !string.IsNullOrWhiteSpace(Type) &&
        (!string.Equals(Type, "command", StringComparison.OrdinalIgnoreCase) ||
         !string.IsNullOrWhiteSpace(Command));

    public PostActionConfig Clone() => new()
    {
        Id = Id,
        Label = Label,
        Type = Type,
        Command = Command
    };

    public void EnsureId(IEnumerable<string> existingIds)
    {
        if (!string.IsNullOrWhiteSpace(Id)) return;
        Id = CreateUniqueId(Label, existingIds);
    }

    public static List<PostActionConfig> CreateDefaults() =>
        BuiltIns.Select(action => action.Clone()).ToList();

    public static List<PostActionConfig> MergeWithBuiltIns(IEnumerable<PostActionConfig>? configured)
    {
        var result = CreateDefaults();
        var existingIds = new HashSet<string>(
            result.Select(action => action.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var action in configured ?? Enumerable.Empty<PostActionConfig>())
        {
            if (action.IsBuiltIn)
            {
                continue;
            }

            var clone = action.Clone();
            clone.EnsureId(existingIds);
            if (existingIds.Contains(clone.Id))
            {
                clone.Id = CreateUniqueId(clone.Label, existingIds);
            }

            if (!clone.IsValid)
            {
                continue;
            }

            existingIds.Add(clone.Id);
            result.Add(clone);
        }

        return result;
    }

    public static PostActionConfig Resolve(IEnumerable<PostActionConfig>? actions, string? activeId)
    {
        var merged = MergeWithBuiltIns(actions);
        return merged.FirstOrDefault(action =>
                   string.Equals(action.Id, activeId, StringComparison.OrdinalIgnoreCase))
               ?? merged.First(action => action.Id == BuiltInPasteId);
    }

    public static string NormalizeActiveId(IEnumerable<PostActionConfig>? actions, string? activeId)
    {
        var resolved = Resolve(actions, activeId);
        return resolved.Id;
    }

    public static string Describe(PostActionConfig action) =>
        action.Type switch
        {
            "paste" => "Paste text at cursor",
            "pasteEnter" => "Paste text, then press Enter",
            "copy" => "Copy text to clipboard without pasting",
            "pasteTab" => "Paste text, then press Tab",
            "pasteSend" => "Paste text, then press Ctrl+Enter",
            "pasteEscape" => "Paste text, then press Escape",
            "command" => string.IsNullOrWhiteSpace(action.Command) ? "Run command" : action.Command,
            _ => action.Type
        };

    public static string CreateUniqueId(string label, IEnumerable<string> existingIds)
    {
        var baseId = "action_" + Slugify(label);
        if (baseId == "action_")
        {
            baseId = "action_custom";
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
