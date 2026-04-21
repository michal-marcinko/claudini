namespace CcLauncher.Core.Config;

public sealed record GlobalSettings(
    string TerminalCommand,
    string? GlobalDefaultArgs,
    string? GlobalSystemPrompt,
    bool LaunchOnStartup,
    bool ResumeAllOnOpen,
    bool CloseOnLaunch)
{
    public static GlobalSettings Defaults(string platformTerminalCommand) =>
        new(platformTerminalCommand, null, null, false, false, true);
}
