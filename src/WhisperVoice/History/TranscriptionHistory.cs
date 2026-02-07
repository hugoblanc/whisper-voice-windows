using System.Text.Json;
using WhisperVoice.Logging;

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
        var entries = LoadHistory();
        entries.Add(new TranscriptionEntry
        {
            Timestamp = DateTime.Now,
            Text = text,
            Provider = provider,
            Mode = mode
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

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string Preview => Text.Length > 100 ? Text.Substring(0, 100) + "..." : Text;
}
