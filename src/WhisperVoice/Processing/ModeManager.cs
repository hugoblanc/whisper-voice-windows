using WhisperVoice.Config;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

/// <summary>
/// Manages AI processing mode selection and availability.
/// The available list is built from built-in modes plus enabled custom modes.
/// </summary>
public class ModeManager
{
    private readonly Func<AppConfig> _configProvider;
    private readonly List<AIMode> _modes = new();
    private int _currentModeIndex;

    public event Action<AIMode>? ModeChanged;

    public IReadOnlyList<AIMode> Modes => _modes;
    public AIMode CurrentMode => _modes.Count > 0 ? _modes[_currentModeIndex] : AIMode.Brut;
    public bool HasAIModesAvailable => _configProvider().HasOpenAIKeyForProcessing;

    public ModeManager(Func<AppConfig> configProvider)
    {
        _configProvider = configProvider;
        ReloadModes(raiseChanged: false);
    }

    /// <summary>
    /// Reload modes from config while preserving the selected mode when possible.
    /// </summary>
    public void ReloadModes(bool raiseChanged = true)
    {
        var previousModeId = CurrentMode.Id;
        var config = _configProvider();
        var disabledBuiltIns = new HashSet<string>(
            config.DisabledBuiltInModeIds ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        var nextModes = new List<AIMode>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mode in AIMode.BuiltInModes)
        {
            if (disabledBuiltIns.Contains(mode.Id)) continue;
            nextModes.Add(mode);
            ids.Add(mode.Id);
        }

        foreach (var customMode in config.CustomModes.Where(mode => mode.Enabled && mode.IsValid))
        {
            customMode.EnsureId(ids);
            if (!ids.Add(customMode.Id))
            {
                Logger.Warn($"[ModeManager] Skipping duplicate custom mode id: {customMode.Id}");
                continue;
            }

            nextModes.Add(AIMode.FromCustom(customMode));
        }

        if (nextModes.Count == 0)
        {
            nextModes.Add(AIMode.Brut);
        }

        _modes.Clear();
        _modes.AddRange(nextModes);

        var previousIndex = _modes.FindIndex(mode =>
            string.Equals(mode.Id, previousModeId, StringComparison.OrdinalIgnoreCase));
        _currentModeIndex = previousIndex >= 0 ? previousIndex : 0;

        Logger.Info($"[ModeManager] Loaded {_modes.Count} modes ({_modes.Count(mode => mode.IsCustom)} custom)");

        if (raiseChanged)
        {
            ModeChanged?.Invoke(CurrentMode);
        }
    }

    /// <summary>
    /// Check if a specific mode is available.
    /// </summary>
    public bool IsModeAvailable(AIMode mode)
    {
        if (!mode.RequiresProcessing) return true;
        return HasAIModesAvailable;
    }

    /// <summary>
    /// Switch to the next available mode.
    /// </summary>
    public AIMode NextMode()
    {
        if (_modes.Count == 0)
        {
            ReloadModes();
        }

        var nextIndex = (_currentModeIndex + 1) % _modes.Count;
        var attempts = 0;

        while (!IsModeAvailable(_modes[nextIndex]) && attempts < _modes.Count)
        {
            nextIndex = (nextIndex + 1) % _modes.Count;
            attempts++;
        }

        _currentModeIndex = attempts >= _modes.Count ? 0 : nextIndex;

        Logger.Info($"[ModeManager] Switched to mode: {CurrentMode.Name}");
        ModeChanged?.Invoke(CurrentMode);
        return CurrentMode;
    }

    public void SetMode(string modeId)
    {
        var index = _modes.FindIndex(mode =>
            string.Equals(mode.Id, modeId, StringComparison.OrdinalIgnoreCase));

        if (index < 0 || !IsModeAvailable(_modes[index])) return;

        _currentModeIndex = index;
        Logger.Info($"[ModeManager] Set mode to: {CurrentMode.Name}");
        ModeChanged?.Invoke(CurrentMode);
    }

    public AIMode? ResolveAutoMode(DictationContext context, out AutoModeRuleConfig? matchedRule)
    {
        matchedRule = null;
        var config = _configProvider();
        if (!config.AutoModeEnabled || config.AutoModeRules.Count == 0)
        {
            return null;
        }

        var candidates = config.AutoModeRules
            .Select((rule, index) => new { Rule = rule, Index = index })
            .Where(item => item.Rule.Enabled && item.Rule.IsValid && Matches(item.Rule, context))
            .OrderByDescending(item => item.Rule.Specificity)
            .ThenBy(item => item.Index);

        foreach (var candidate in candidates)
        {
            var mode = FindMode(candidate.Rule.ModeId);
            if (mode == null)
            {
                Logger.Warn($"[ModeManager] Auto mode rule '{candidate.Rule.Name}' targets missing mode id: {candidate.Rule.ModeId}");
                continue;
            }

            if (!IsModeAvailable(mode))
            {
                Logger.Warn($"[ModeManager] Auto mode rule '{candidate.Rule.Name}' targets unavailable mode: {mode.Name}");
                continue;
            }

            matchedRule = candidate.Rule;
            return mode;
        }

        return null;
    }

    private AIMode? FindMode(string modeId) =>
        _modes.FirstOrDefault(mode => string.Equals(mode.Id, modeId, StringComparison.OrdinalIgnoreCase));

    private static bool Matches(AutoModeRuleConfig rule, DictationContext context)
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

    public void Reset()
    {
        _currentModeIndex = 0;
        ModeChanged?.Invoke(CurrentMode);
    }
}
