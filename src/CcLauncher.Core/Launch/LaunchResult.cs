namespace CcLauncher.Core.Launch;

public sealed record LaunchResult(bool Success, int? Pid, string? Error, string ResolvedCommandLine);
