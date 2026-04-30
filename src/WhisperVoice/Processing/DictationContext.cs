namespace WhisperVoice.Processing;

public class DictationContext
{
    public string? SelectedText { get; set; }
    public string? ActiveWindowTitle { get; set; }
    public string? ActiveProcessName { get; set; }
    public string? ActiveProcessPath { get; set; }

    public bool HasSelectedText => !string.IsNullOrWhiteSpace(SelectedText);

    public bool HasAmbientContext =>
        !string.IsNullOrWhiteSpace(ActiveWindowTitle) ||
        !string.IsNullOrWhiteSpace(ActiveProcessName);
}
