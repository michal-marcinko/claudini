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

    // In-memory only: expansion is ephemeral UI state, not a user preference.
    // Refresh() consults this to reseed each new ProjectRowViewModel.
    private readonly HashSet<string> _expandedProjectIds = new();

    public DashboardViewModel(IProjectDiscoveryService discovery, IConfigStore config, ILauncher launcher)
    {
        _discovery = discovery;
        _config = config;
        _launcher = launcher;
    }

    private void OnRowExpandedChanged(string projectId, bool expanded)
    {
        if (expanded) _expandedProjectIds.Add(projectId);
        else          _expandedProjectIds.Remove(projectId);
    }

    public ObservableCollection<ProjectRowViewModel> Rows { get; } = new();
    public ObservableCollection<ProjectRowViewModel> PinnedRows { get; } = new();
    public ObservableCollection<ProjectRowViewModel> RecentRows { get; } = new();

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private bool _hasPinned;

    public void Refresh()
    {
        Rows.Clear();
        PinnedRows.Clear();
        RecentRows.Clear();
        var settings = _config.GetAllProjectSettings();

        var projects = _discovery.Scan()
            .Where(p => !(settings.TryGetValue(p.Id, out var s) && s.Hidden))
            .Select(p => new ProjectRowViewModel(
                p,
                settings.TryGetValue(p.Id, out var s) ? s : ProjectSettings.Default(p.Id),
                initiallyExpanded: _expandedProjectIds.Contains(p.Id),
                onExpandedChanged: OnRowExpandedChanged))
            .OrderByDescending(r => r.Pinned)
            .ThenByDescending(r => r.LastActivity)
            .ToList();

        foreach (var r in projects)
        {
            Rows.Add(r);
            if (r.Pinned) PinnedRows.Add(r);
            else          RecentRows.Add(r);
        }
        HasPinned = PinnedRows.Count > 0;
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
        row.UpdateSettings(updated);

        // Surgical move: don't re-scan discovery or rebuild the UI — just shift
        // this one row between FAVOURITES and RECENT. The same instance stays
        // alive, so the user's hover/click/expand state is preserved and there's
        // no visible pop when rearranging.
        PinnedRows.Remove(row);
        RecentRows.Remove(row);
        var target = updated.Pinned ? PinnedRows : RecentRows;
        target.Insert(InsertionIndexByRecency(target, row), row);

        // Rows is the flat ordered view — keep it consistent with the sections.
        Rows.Remove(row);
        Rows.Insert(InsertionIndexInFlat(row), row);

        HasPinned = PinnedRows.Count > 0;
    }

    public void Hide(ProjectRowViewModel row)
    {
        _config.SaveProjectSettings(row.Settings with { Hidden = true });
        // Surgical remove — matches Hide's intent: the row disappears.
        PinnedRows.Remove(row);
        RecentRows.Remove(row);
        Rows.Remove(row);
        HasPinned = PinnedRows.Count > 0;
    }

    // Insert within a section in descending LastActivity order (ties resolved by first match).
    private static int InsertionIndexByRecency(ObservableCollection<ProjectRowViewModel> target, ProjectRowViewModel row)
    {
        for (int i = 0; i < target.Count; i++)
            if (target[i].LastActivity < row.LastActivity) return i;
        return target.Count;
    }

    // Flat Rows is ordered: all pinned first (by recency), then all unpinned (by recency).
    private int InsertionIndexInFlat(ProjectRowViewModel row)
    {
        for (int i = 0; i < Rows.Count; i++)
        {
            var other = Rows[i];
            if (row.Pinned && !other.Pinned) return i;
            if (row.Pinned == other.Pinned && other.LastActivity < row.LastActivity) return i;
        }
        return Rows.Count;
    }

    public void Rename(ProjectRowViewModel row, string newName)
    {
        _config.SaveProjectSettings(row.Settings with { DisplayName = string.IsNullOrWhiteSpace(newName) ? null : newName });
        Refresh();
    }
}
