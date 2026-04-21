using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;
using CcLauncher.Core.Launch;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcLauncher.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IProjectDiscoveryService _discovery;
    private readonly IConfigStore _config;
    private readonly ILauncher _launcher;

    public DashboardViewModel(IProjectDiscoveryService discovery, IConfigStore config, ILauncher launcher)
    {
        _discovery = discovery;
        _config = config;
        _launcher = launcher;
    }

    public ObservableCollection<ProjectRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string? _lastError;

    public void Refresh()
    {
        Rows.Clear();
        var settings = _config.GetAllProjectSettings();

        var projects = _discovery.Scan()
            .Where(p => !(settings.TryGetValue(p.Id, out var s) && s.Hidden))
            .Select(p => new ProjectRowViewModel(
                p,
                settings.TryGetValue(p.Id, out var s) ? s : ProjectSettings.Default(p.Id)))
            .OrderByDescending(r => r.Pinned)
            .ThenByDescending(r => r.LastActivity);

        foreach (var r in projects) Rows.Add(r);
    }

    public LaunchResult LaunchProject(ProjectRowViewModel row, string? sessionId)
    {
        var sid = sessionId ?? row.LatestSessionId;
        if (sid is null) return LaunchNewSession(row);

        var global = _config.GetGlobalSettings();
        var args = ArgBuilder.Build(global, row.Settings, sid);
        var req = new LaunchRequest(row.Project.Cwd, global.TerminalCommand, args);
        var result = _launcher.Launch(req);
        if (result.Success)
        {
            _config.InsertLaunch(row.Id, sid, result.Pid);
            _config.SaveProjectSettings(row.Settings with { LastLaunchedAt = DateTime.UtcNow });
        }
        else
        {
            LastError = $"Launch failed: {result.Error}";
            CcLauncher.Core.Logging.FileLog.Error($"Launch failed for {row.Id}: {result.Error} (cmd: {result.ResolvedCommandLine})");
        }
        return result;
    }

    public LaunchResult LaunchNewSession(ProjectRowViewModel row)
    {
        var global = _config.GetGlobalSettings();
        var args = ArgBuilder.Build(global, row.Settings, sessionId: null);
        var req = new LaunchRequest(row.Project.Cwd, global.TerminalCommand, args);
        var result = _launcher.Launch(req);
        if (result.Success)
        {
            _config.SaveProjectSettings(row.Settings with { LastLaunchedAt = DateTime.UtcNow });
        }
        else
        {
            LastError = $"Launch failed: {result.Error}";
            CcLauncher.Core.Logging.FileLog.Error($"Launch failed for {row.Id}: {result.Error} (cmd: {result.ResolvedCommandLine})");
        }
        return result;
    }

    public void TogglePinned(ProjectRowViewModel row)
    {
        var updated = row.Settings with { Pinned = !row.Settings.Pinned };
        _config.SaveProjectSettings(updated);
        Refresh();
    }

    public void Hide(ProjectRowViewModel row)
    {
        _config.SaveProjectSettings(row.Settings with { Hidden = true });
        Refresh();
    }

    public void Rename(ProjectRowViewModel row, string newName)
    {
        _config.SaveProjectSettings(row.Settings with { DisplayName = string.IsNullOrWhiteSpace(newName) ? null : newName });
        Refresh();
    }
}
