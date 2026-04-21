using CcLauncher.Core.Config;
using CcLauncher.Core.Launch;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcLauncher.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigStore _config;

    public SettingsViewModel(IConfigStore config)
    {
        _config = config;
        var g = config.GetGlobalSettings();
        _terminalCommand = g.TerminalCommand;
        _globalDefaultArgs = g.GlobalDefaultArgs ?? "";
        _globalSystemPrompt = g.GlobalSystemPrompt ?? "";
        _launchOnStartup = g.LaunchOnStartup;
        _closeOnLaunch = g.CloseOnLaunch;
        _theme = g.Theme;
    }

    [ObservableProperty] private string _terminalCommand = "";
    [ObservableProperty] private string _globalDefaultArgs = "";
    [ObservableProperty] private string _globalSystemPrompt = "";
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _closeOnLaunch;
    [ObservableProperty] private string _theme = "System";

    public System.Collections.Generic.IReadOnlyList<string> ThemeOptions { get; } =
        new[] { "System", "Light", "Dark" };

    public void Save()
    {
        _config.SaveGlobalSettings(new GlobalSettings(
            TerminalCommand:    string.IsNullOrWhiteSpace(TerminalCommand) ? LauncherFactory.DefaultTerminalCommand() : TerminalCommand,
            GlobalDefaultArgs:  string.IsNullOrWhiteSpace(GlobalDefaultArgs) ? null : GlobalDefaultArgs,
            GlobalSystemPrompt: string.IsNullOrWhiteSpace(GlobalSystemPrompt) ? null : GlobalSystemPrompt,
            LaunchOnStartup:    LaunchOnStartup,
            ResumeAllOnOpen:    false,
            CloseOnLaunch:      CloseOnLaunch,
            Theme:              Theme));
    }
}
