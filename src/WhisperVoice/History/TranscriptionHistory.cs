using System.Text.Json;
using WhisperVoice.Logging;
using WhisperVoice.Processing;

namespace WhisperVoice.History;

/// <summary>
/// Manages transcription history storage and retrieval
/// </summary>
public class TranscriptionHistory
{
    private const int MaxEntries = 100;
    private static readonly string HistoryFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhisperVoice",
        "history.json"
    );

    public static List<TranscriptionEntry> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFilePath))
                return new List<TranscriptionEntry>();

            var json = File.ReadAllText(HistoryFilePath);
            var entries = JsonSerializer.Deserialize<List<TranscriptionEntry>>(json);
            return entries ?? new List<TranscriptionEntry>();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load transcription history", ex);
            return new List<TranscriptionEntry>();
        }
    }

    public static void SaveHistory(List<TranscriptionEntry> entries)
    {
        try
        {
            // Limit to MaxEntries (keep most recent)
            if (entries.Count > MaxEntries)
            {
                entries = entries.OrderByDescending(e => e.Timestamp)
                                .Take(MaxEntries)
                                .ToList();
            }

            var directory = Path.GetDirectoryName(HistoryFilePath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(entries, options);
            File.WriteAllText(HistoryFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save transcription history", ex);
        }
    }

    public static void AddEntry(string text, string provider, string mode)
    {
        AddEntry(text, provider, mode, null, null);
    }

    public static void AddEntry(string text, string provider, string mode, DictationContext? context, string? actionLabel)
    {
        var entries = LoadHistory();
        entries.Add(new TranscriptionEntry
        {
            Timestamp = DateTime.Now,
            Text = text,
            Provider = provider,
            Mode = mode,
            Action = actionLabel ?? "",
            ProcessName = context?.ActiveProcessName ?? "",
            ProcessPath = context?.ActiveProcessPath ?? "",
            WindowTitle = context?.ActiveWindowTitle ?? "",
            BrowserUrl = context?.BrowserUrl ?? "",
            BrowserHost = context?.BrowserHost ?? "",
            WorkspaceName = context?.WorkspaceName ?? "",
            ProjectId = context?.ProjectId ?? "",
            ProjectName = context?.ProjectName ?? ""
        });
        SaveHistory(entries);
    }

    public static void DeleteEntry(TranscriptionEntry entry)
    {
        var entries = LoadHistory();
        entries.RemoveAll(e => e.Timestamp == entry.Timestamp && e.Text == entry.Text);
        SaveHistory(entries);
    }

    public static void ClearHistory()
    {
        SaveHistory(new List<TranscriptionEntry>());
    }
}

/// <summary>
/// Represents a single transcription history entry
/// </summary>
public class TranscriptionEntry
{
    public DateTime Timestamp { get; set; }
    public string Text { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Action { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string BrowserUrl { get; set; } = "";
    public string BrowserHost { get; set; } = "";
    public string WorkspaceName { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string Preview => Text.Length > 100 ? Text.Substring(0, 100) + "..." : Text;
    public string ProjectDisplay => string.IsNullOrWhiteSpace(ProjectName) ? "(untagged)" : ProjectName;
    public string ContextDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WorkspaceName)) return WorkspaceName;
            if (!string.IsNullOrWhiteSpace(BrowserHost)) return BrowserHost;
            if (!string.IsNullOrWhiteSpace(ProcessName)) return ProcessName;
            return "";
        }
    }
}
