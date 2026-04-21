using System;
using System.Collections.Generic;
using System.Linq;
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;

namespace CcLauncher.App.ViewModels;

public sealed class ProjectRowViewModel
{
    public ProjectRowViewModel(DiscoveredProject project, ProjectSettings settings)
    {
        Project = project;
        Settings = settings;
        Sessions = project.Sessions
            .OrderByDescending(s => s.LastActivity)
            .Select(s => new SessionRowViewModel(s))
            .ToList();
    }

    public DiscoveredProject Project { get; }
    public ProjectSettings Settings { get; }
    public IReadOnlyList<SessionRowViewModel> Sessions { get; }

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
