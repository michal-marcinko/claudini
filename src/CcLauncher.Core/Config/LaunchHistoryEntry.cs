namespace CcLauncher.Core.Config;

public sealed record LaunchHistoryEntry(
    long Id,
    string SessionId,
    string ProjectId,
    DateTime LaunchedAt,
    DateTime? ClosedAt,
    int? Pid);
