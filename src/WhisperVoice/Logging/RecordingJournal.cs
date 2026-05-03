using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WhisperVoice.Logging;

public static class RecordingJournal
{
    private static readonly object FileLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string JournalDirectory => Logger.LogDirectory;
    public static string JournalFilePath => Path.Combine(JournalDirectory, $"recording_journal_{DateTime.Now:yyyy-MM-dd}.jsonl");

    public static RecordingJournalSession Start(string providerId, string providerName, string modeName)
    {
        var entry = new RecordingJournalEntry
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            StartedAt = DateTime.Now,
            ProviderId = providerId,
            ProviderName = providerName,
            Mode = modeName,
            Status = "running"
        };

        return new RecordingJournalSession(entry);
    }

    internal static void Save(RecordingJournalEntry entry)
    {
        try
        {
            Directory.CreateDirectory(JournalDirectory);
            var json = JsonSerializer.Serialize(entry, JsonOptions);

            lock (FileLock)
            {
                File.AppendAllText(JournalFilePath, json + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to write recording journal: {ex.Message}");
        }
    }

    public static List<RecordingJournalEntry> GetRecentEntries(int maxEntries = 100)
    {
        var entries = new List<RecordingJournalEntry>();

        try
        {
            if (!Directory.Exists(JournalDirectory)) return entries;

            foreach (var file in Directory.GetFiles(JournalDirectory, "recording_journal_*.jsonl")
                         .OrderByDescending(File.GetLastWriteTime))
            {
                var lines = File.ReadAllLines(file);
                for (var i = lines.Length - 1; i >= 0 && entries.Count < maxEntries; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    try
                    {
                        var entry = JsonSerializer.Deserialize<RecordingJournalEntry>(lines[i], JsonOptions);
                        if (entry != null) entries.Add(entry);
                    }
                    catch
                    {
                        // Skip malformed lines so one bad entry does not hide the journal.
                    }
                }

                if (entries.Count >= maxEntries) break;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to read recording journal: {ex.Message}");
        }

        return entries;
    }

    public static void Clear()
    {
        try
        {
            if (!Directory.Exists(JournalDirectory)) return;

            foreach (var file in Directory.GetFiles(JournalDirectory, "recording_journal_*.jsonl"))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to clear recording journal: {ex.Message}");
            throw;
        }
    }

    public static void OpenFolder() => Logger.OpenLogFolder();
}

public sealed class RecordingJournalSession
{
    private readonly RecordingJournalEntry _entry;
    private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
    private bool _saved;
    private int _stepIndex;

    internal RecordingJournalSession(RecordingJournalEntry entry)
    {
        _entry = entry;
    }

    public void SetMode(string modeName) => _entry.Mode = modeName;
    public void SetAudioBytes(long bytes) => _entry.AudioBytes = bytes;
    public void SetRawTextChars(int chars) => _entry.RawTextChars = chars;
    public void SetFinalTextChars(int chars) => _entry.FinalTextChars = chars;
    public void SetPasted(bool pasted) => _entry.Pasted = pasted;

    public void AddDurationStep(string name, DateTime startedAt, DateTime endedAt, string status = "ok", string? detail = null)
    {
        _entry.Steps.Add(new RecordingJournalStep
        {
            Order = ++_stepIndex,
            Name = name,
            StartedAt = startedAt,
            DurationMs = Math.Max(0, (int)(endedAt - startedAt).TotalMilliseconds),
            Status = status,
            Detail = detail
        });
    }

    public void AddEvent(string name, string status = "ok", string? detail = null)
    {
        _entry.Steps.Add(new RecordingJournalStep
        {
            Order = ++_stepIndex,
            Name = name,
            StartedAt = DateTime.Now,
            DurationMs = 0,
            Status = status,
            Detail = detail
        });
    }

    public T Track<T>(string name, Func<T> action, Func<T, string?>? detail = null)
    {
        var step = StartStep(name);
        try
        {
            var result = action();
            step.Complete("ok", detail?.Invoke(result));
            return result;
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public void Track(string name, Action action, string? detail = null)
    {
        var step = StartStep(name);
        try
        {
            action();
            step.Complete("ok", detail);
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public async Task<T> TrackAsync<T>(string name, Func<Task<T>> action, Func<T, string?>? detail = null)
    {
        var step = StartStep(name);
        try
        {
            var result = await action();
            step.Complete("ok", detail?.Invoke(result));
            return result;
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public async Task TrackAsync(string name, Func<Task> action, string? detail = null)
    {
        var step = StartStep(name);
        try
        {
            await action();
            step.Complete("ok", detail);
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public JournalStepScope StartStep(string name) =>
        new(this, ++_stepIndex, name);

    public void Finish(string status, string? error = null)
    {
        if (_saved) return;

        _totalStopwatch.Stop();
        _entry.Status = status;
        _entry.Error = error;
        _entry.EndedAt = DateTime.Now;
        _entry.TotalMs = (int)_totalStopwatch.ElapsedMilliseconds;

        RecordingJournal.Save(_entry);
        Logger.Info($"[RecordingJournal] {status} id={_entry.Id} total={_entry.TotalMs}ms mode={_entry.Mode} provider={_entry.ProviderName}");
        _saved = true;
    }

    internal void AddCompletedStep(RecordingJournalStep step)
    {
        _entry.Steps.Add(step);
    }
}

public sealed class JournalStepScope
{
    private readonly RecordingJournalSession _session;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly RecordingJournalStep _step;
    private bool _completed;

    internal JournalStepScope(RecordingJournalSession session, int order, string name)
    {
        _session = session;
        _step = new RecordingJournalStep
        {
            Order = order,
            Name = name,
            StartedAt = DateTime.Now,
            Status = "running"
        };
    }

    public void Complete(string status = "ok", string? detail = null)
    {
        if (_completed) return;

        _stopwatch.Stop();
        _step.DurationMs = (int)_stopwatch.ElapsedMilliseconds;
        _step.Status = status;
        _step.Detail = detail;
        _session.AddCompletedStep(_step);
        _completed = true;
    }

    public void Fail(Exception ex)
    {
        if (_completed) return;

        _stopwatch.Stop();
        _step.DurationMs = (int)_stopwatch.ElapsedMilliseconds;
        _step.Status = "failed";
        _step.Error = ex.Message;
        _session.AddCompletedStep(_step);
        _completed = true;
    }

    public void Warn(string detail)
    {
        Complete("warning", detail);
    }
}

public class RecordingJournalEntry
{
    public string Id { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Status { get; set; } = "";
    public int TotalMs { get; set; }
    public long? AudioBytes { get; set; }
    public int? RawTextChars { get; set; }
    public int? FinalTextChars { get; set; }
    public bool? Pasted { get; set; }
    public string? Error { get; set; }
    public List<RecordingJournalStep> Steps { get; set; } = new();
}

public class RecordingJournalStep
{
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public int DurationMs { get; set; }
    public string Status { get; set; } = "";
    public string? Detail { get; set; }
    public string? Error { get; set; }
}
