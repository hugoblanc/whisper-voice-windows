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

    public void Reset()
    {
        _currentModeIndex = 0;
        ModeChanged?.Invoke(CurrentMode);
    }
}
