using CcLauncher.Core.Config;
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
    }

    [ObservableProperty] private string _terminalCommand = "";
    [ObservableProperty] private string _globalDefaultArgs = "";
    [ObservableProperty] private string _globalSystemPrompt = "";
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _closeOnLaunch;

    public void Save()
    {
        _config.SaveGlobalSettings(new GlobalSettings(
            TerminalCommand:    string.IsNullOrWhiteSpace(TerminalCommand) ? "wt.exe" : TerminalCommand,
            GlobalDefaultArgs:  string.IsNullOrWhiteSpace(GlobalDefaultArgs) ? null : GlobalDefaultArgs,
            GlobalSystemPrompt: string.IsNullOrWhiteSpace(GlobalSystemPrompt) ? null : GlobalSystemPrompt,
            LaunchOnStartup:    LaunchOnStartup,
            ResumeAllOnOpen:    false,
            CloseOnLaunch:      CloseOnLaunch));
    }
}
