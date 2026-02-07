using WhisperVoice.Config;
using WhisperVoice.Logging;

namespace WhisperVoice.Processing;

/// <summary>
/// Manages AI processing mode selection and availability
/// </summary>
public class ModeManager
{
    private int _currentModeIndex;
    private readonly Func<bool> _hasOpenAIKey;

    public event Action<AIMode>? ModeChanged;

    public AIMode CurrentMode => AIMode.All[_currentModeIndex];

    /// <summary>
    /// Check if AI modes are available (requires OpenAI API key)
    /// </summary>
    public bool HasAIModesAvailable => _hasOpenAIKey();

    public ModeManager(Func<bool> hasOpenAIKeyFunc)
    {
        _hasOpenAIKey = hasOpenAIKeyFunc;
        _currentModeIndex = 0;
    }

    /// <summary>
    /// Check if a specific mode is available
    /// </summary>
    public bool IsModeAvailable(AIMode mode)
    {
        // Brut mode is always available
        if (!mode.RequiresProcessing) return true;

        // AI modes require OpenAI key
        return _hasOpenAIKey();
    }

    /// <summary>
    /// Switch to the next available mode (cycles through)
    /// </summary>
    /// <returns>The new current mode</returns>
    public AIMode NextMode()
    {
        var modes = AIMode.All;
        var startIndex = _currentModeIndex;
        var nextIndex = (_currentModeIndex + 1) % modes.Length;
        var attempts = 0;

        // Find next available mode
        while (!IsModeAvailable(modes[nextIndex]) && attempts < modes.Length)
        {
            nextIndex = (nextIndex + 1) % modes.Length;
            attempts++;
        }

        // If no available mode found, stay on Brut
        if (attempts >= modes.Length)
        {
            _currentModeIndex = 0;
        }
        else
        {
            _currentModeIndex = nextIndex;
        }

        Logger.Info($"[ModeManager] Switched to mode: {CurrentMode.Name}");
        ModeChanged?.Invoke(CurrentMode);
        return CurrentMode;
    }

    /// <summary>
    /// Set mode by ID
    /// </summary>
    public void SetMode(string modeId)
    {
        var mode = AIMode.GetById(modeId);
        if (mode == null || !IsModeAvailable(mode)) return;

        _currentModeIndex = AIMode.IndexOf(mode);
        Logger.Info($"[ModeManager] Set mode to: {CurrentMode.Name}");
        ModeChanged?.Invoke(CurrentMode);
    }

    /// <summary>
    /// Reset to default mode (Brut)
    /// </summary>
    public void Reset()
    {
        _currentModeIndex = 0;
        ModeChanged?.Invoke(CurrentMode);
    }
}
