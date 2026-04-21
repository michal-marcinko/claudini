namespace CcLauncher.Core.Config;

public sealed record ProjectSettings(
    string ProjectId,
    string? DisplayName,
    bool Pinned,
    bool Hidden,
    string? DefaultArgs,
    string? SystemPrompt,
    DateTime? LastLaunchedAt)
{
    public static ProjectSettings Default(string projectId) =>
        new(projectId, null, false, false, null, null, null);
}
