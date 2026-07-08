namespace WhisperVoice.Processing;

public record ProcessingModelInfo(string Id, string DisplayName);

public static class ProcessingModelCatalog
{
    public const string DefaultModel = "gpt-5.4-nano";

    private static readonly ProcessingModelInfo[] Models =
    {
        new("gpt-5.4-nano", "GPT-5.4 Nano (fastest / lowest cost)"),
        new("gpt-5.4-mini", "GPT-5.4 Mini (balanced)"),
        new("gpt-5.4", "GPT-5.4 (premium)"),
        new("gpt-5.5", "GPT-5.5 (frontier)"),
        new("gpt-4.1-mini", "GPT-4.1 Mini (legacy)"),
        new("gpt-4.1", "GPT-4.1 (legacy)"),
        new("gpt-4o-mini", "GPT-4o Mini (legacy)"),
        new("gpt-4o", "GPT-4o (legacy)")
    };

    public static IReadOnlyList<ProcessingModelInfo> GetAvailableModels() => Models;

    public static string Normalize(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return DefaultModel;
        }

        var trimmed = modelId.Trim();
        return Models.Any(model => string.Equals(model.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            ? Models.First(model => string.Equals(model.Id, trimmed, StringComparison.OrdinalIgnoreCase)).Id
            : trimmed;
    }

    public static bool UsesMaxCompletionTokens(string modelId) =>
        modelId.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
}
