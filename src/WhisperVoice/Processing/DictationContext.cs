namespace WhisperVoice.Processing;

public class DictationContext
{
    public string? SelectedText { get; set; }
    public string? ActiveWindowTitle { get; set; }
    public string? ActiveProcessName { get; set; }
    public string? ActiveProcessPath { get; set; }
    public string? BrowserUrl { get; set; }
    public string? BrowserHost { get; set; }
    public string? WorkspaceName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public bool HasSelectedText => !string.IsNullOrWhiteSpace(SelectedText);

    public bool HasAmbientContext =>
        !string.IsNullOrWhiteSpace(ActiveWindowTitle) ||
        !string.IsNullOrWhiteSpace(ActiveProcessName) ||
        !string.IsNullOrWhiteSpace(BrowserUrl) ||
        !string.IsNullOrWhiteSpace(WorkspaceName);

    public string ContextSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WorkspaceName))
            {
                return $"workspace={WorkspaceName}";
            }

            if (!string.IsNullOrWhiteSpace(BrowserHost))
            {
                return $"host={BrowserHost}";
            }

            if (!string.IsNullOrWhiteSpace(ActiveProcessName))
            {
                return $"app={ActiveProcessName}";
            }

            return "context=unknown";
        }
    }
}
