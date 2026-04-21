namespace CcLauncher.Core.Launch;

public sealed record LaunchRequest(
    string Cwd,
    string TerminalCommand,
    IReadOnlyList<string> ClaudeArgs);
