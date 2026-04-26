using System.Collections.Generic;
using System.Linq;
using CcLauncher.App.Services;
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
        _terminalCommand    = g.TerminalCommand;
        _globalDefaultArgs  = g.GlobalDefaultArgs ?? "";
        _globalSystemPrompt = g.GlobalSystemPrompt ?? "";
        _launchOnStartup    = g.LaunchOnStartup;
        _closeOnLaunch      = g.CloseOnLaunch;
        _theme              = g.Theme;

        TerminalProfiles = TerminalDetector.KnownProfiles();
        // If the saved command (or first-run detected default) maps to one of our
        // known profiles, select that. Otherwise fall back to Windows Terminal so
        // the dropdown always has a sensible selection.
        _selectedTerminalProfile =
            TerminalProfiles.FirstOrDefault(p => p.Command.Equals(_terminalCommand, System.StringComparison.OrdinalIgnoreCase))
            ?? TerminalProfiles[0];
    }

    [ObservableProperty] private string _terminalCommand = "";
    [ObservableProperty] private string _globalDefaultArgs = "";
    [ObservableProperty] private string _globalSystemPrompt = "";
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _closeOnLaunch;
    [ObservableProperty] private string _theme = "System";

    public IReadOnlyList<string> ThemeOptions { get; } =
        new[] { "System", "Light", "Dark" };

    public IReadOnlyList<TerminalProfile> TerminalProfiles { get; }

    // Bound to the Settings ComboBox. Setting it pushes the underlying command
    // string so Save() persists the right value without the UI needing to know
    // anything about the string-form of the command.
    private TerminalProfile? _selectedTerminalProfile;
    public TerminalProfile? SelectedTerminalProfile
    {
        get => _selectedTerminalProfile;
        set
        {
            if (SetProperty(ref _selectedTerminalProfile, value) && value is not null)
                TerminalCommand = value.Command;
        }
    }

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
