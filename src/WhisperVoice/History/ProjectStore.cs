using System.Text.Json;
using WhisperVoice.Config;
using WhisperVoice.Logging;
using WhisperVoice.Processing;

namespace WhisperVoice.History;

public class ProjectConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#7BC0D6";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool Archived { get; set; }
}

public static class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ProjectsFilePath => Path.Combine(AppConfig.ConfigDirectory, "projects.json");

    public static List<ProjectConfig> LoadProjects()
    {
        try
        {
            if (!File.Exists(ProjectsFilePath))
            {
                return new List<ProjectConfig>();
            }

            var json = File.ReadAllText(ProjectsFilePath);
            return JsonSerializer.Deserialize<List<ProjectConfig>>(json, JsonOptions) ?? new List<ProjectConfig>();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load projects", ex);
            return new List<ProjectConfig>();
        }
    }

    public static void SaveProjects(List<ProjectConfig> projects)
    {
        try
        {
            Directory.CreateDirectory(AppConfig.ConfigDirectory);
            File.WriteAllText(ProjectsFilePath, JsonSerializer.Serialize(projects, JsonOptions));
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save projects", ex);
        }
    }

    public static ProjectConfig? GetProject(string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return null;
        return LoadProjects().FirstOrDefault(project =>
            string.Equals(project.Id, projectId, StringComparison.OrdinalIgnoreCase));
    }

    public static ProjectConfig GetOrCreateProject(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        var projects = LoadProjects();
        var existing = projects.FirstOrDefault(project =>
            string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Archived = false;
            SaveProjects(projects);
            return existing;
        }

        var project = new ProjectConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Color = PickColor(projects.Count),
            CreatedAt = DateTime.Now
        };
        projects.Add(project);
        SaveProjects(projects);
        return project;
    }

    public static ProjectConfig? PredictProject(DictationContext context, AppConfig config, IReadOnlyList<TranscriptionEntry>? history = null)
    {
        if (!config.ProjectTaggingEnabled)
        {
            return null;
        }

        var projects = LoadProjects()
            .Where(project => !project.Archived)
            .ToList();
        if (projects.Count == 0)
        {
            return null;
        }

        history ??= TranscriptionHistory.LoadHistory();

        var predictedId =
            PredictFromExactSignal(history, entry => entry.WorkspaceName, context.WorkspaceName) ??
            PredictFromExactSignal(history, entry => entry.BrowserHost, context.BrowserHost) ??
            PredictFromProcess(history, context.ActiveProcessName) ??
            config.LastUsedProjectId;

        return projects.FirstOrDefault(project =>
            string.Equals(project.Id, predictedId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? PredictFromExactSignal(
        IReadOnlyList<TranscriptionEntry> history,
        Func<TranscriptionEntry, string?> readSignal,
        string? currentSignal)
    {
        if (string.IsNullOrWhiteSpace(currentSignal))
        {
            return null;
        }

        return history
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ProjectId) &&
                            string.Equals(readSignal(entry), currentSignal, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.ProjectId!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string? PredictFromProcess(IReadOnlyList<TranscriptionEntry> history, string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var matches = history
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ProjectId) &&
                            string.Equals(entry.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count < 5)
        {
            return null;
        }

        var top = matches
            .GroupBy(entry => entry.ProjectId!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .First();

        return top.Count() >= matches.Count * 0.8 ? top.Key : null;
    }

    private static string PickColor(int index)
    {
        string[] colors =
        {
            "#7BC0D6",
            "#FF7A4A",
            "#68D391",
            "#F6C85F",
            "#9F7AEA",
            "#63B3ED",
            "#F687B3"
        };
        return colors[index % colors.Length];
    }
}
