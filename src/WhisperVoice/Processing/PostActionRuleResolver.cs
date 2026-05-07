using WhisperVoice.Config;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

public static class PostActionRuleResolver
{
    public static PostActionConfig Resolve(
        AppConfig config,
        DictationContext? context,
        out AutoPostActionRuleConfig? matchedRule)
    {
        matchedRule = null;
        var fallback = PostActionConfig.Resolve(config.PostActions, config.ActivePostActionId);

        if (context == null || !config.AutoPostActionEnabled || config.AutoPostActionRules.Count == 0)
        {
            return fallback;
        }

        var candidates = config.AutoPostActionRules
            .Select((rule, index) => new { Rule = rule, Index = index })
            .Where(item => item.Rule.Enabled && item.Rule.IsValid && Matches(item.Rule, context))
            .OrderByDescending(item => item.Rule.Specificity)
            .ThenBy(item => item.Index);

        foreach (var candidate in candidates)
        {
            var action = PostActionConfig.Resolve(config.PostActions, candidate.Rule.ActionId);
            if (!string.Equals(action.Id, candidate.Rule.ActionId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"[AutoPostAction] Rule '{candidate.Rule.Name}' targets missing action id: {candidate.Rule.ActionId}");
                continue;
            }

            matchedRule = candidate.Rule;
            return action;
        }

        return fallback;
    }

    private static bool Matches(AutoPostActionRuleConfig rule, DictationContext context)
    {
        if (!string.IsNullOrWhiteSpace(rule.ProcessName) &&
            !ProcessMatches(context.ActiveProcessName, rule.ProcessName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.WindowTitleContains) &&
            !TitleMatches(context.ActiveWindowTitle, rule.WindowTitleContains))
        {
            return false;
        }

        return true;
    }

    private static bool ProcessMatches(string? actualProcessName, string expectedProcessName)
    {
        if (string.IsNullOrWhiteSpace(actualProcessName)) return false;

        var actual = Path.GetFileNameWithoutExtension(actualProcessName.Trim());
        var expected = Path.GetFileNameWithoutExtension(expectedProcessName.Trim());
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleMatches(string? title, string expectedSubstring) =>
        !string.IsNullOrWhiteSpace(title) &&
        title.Contains(expectedSubstring.Trim(), StringComparison.OrdinalIgnoreCase);
}
