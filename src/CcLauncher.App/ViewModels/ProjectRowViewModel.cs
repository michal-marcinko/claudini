using System;
using System.Collections.Generic;
using System.Linq;
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;

namespace CcLauncher.App.ViewModels;

public sealed class ProjectRowViewModel
{
    private bool _isExpanded;
    private readonly Action<string, bool>? _onExpandedChanged;

    public ProjectRowViewModel(
        DiscoveredProject project,
        ProjectSettings settings,
        bool initiallyExpanded = false,
        Action<string, bool>? onExpandedChanged = null)
    {
        Project = project;
        Settings = settings;
        _isExpanded = initiallyExpanded;
        _onExpandedChanged = onExpandedChanged;
        Sessions = project.Sessions
            .OrderByDescending(s => s.LastActivity)
            .Select(s => new SessionRowViewModel(s))
            .ToList();
    }

    public DiscoveredProject Project { get; }
    public ProjectSettings Settings { get; private set; }
    public IReadOnlyList<SessionRowViewModel> Sessions { get; }

    // Used by DashboardViewModel for surgical updates — lets us swap the underlying
    // settings without recreating the row (which would destroy Avalonia's visual state
    // like the ToggleButton the user just clicked).
    internal void UpdateSettings(ProjectSettings updated) => Settings = updated;

    // Toggle state survives Refresh() rebuilds by flowing through the onExpandedChanged
    // callback up to DashboardViewModel, which reseeds the value on the next construction.
    // Visual-tree rendering uses element-name binding off the ToggleButton, so no INPC needed.
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            _onExpandedChanged?.Invoke(Project.Id, value);
        }
    }

    public string Id => Project.Id;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Settings.DisplayName)
            ? Settings.DisplayName!
            : DeriveBasename(Project.Cwd);

    public string? LatestSessionId =>
        Project.Sessions.Count == 0
            ? null
            : Project.Sessions.OrderByDescending(s => s.LastActivity).First().Id;

    public DateTime LastActivity => Project.LastActivity;
    public string RelativeWhen   => RelativeTime.Format(LastActivity);
    public bool Pinned           => Settings.Pinned;

    private static string DeriveBasename(string cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return "(unknown)";
        var trimmed = cwd.TrimEnd('/', '\\');
        var sep = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        return sep < 0 ? trimmed : trimmed[(sep + 1)..];
    }
}
