// src/CcLauncher.Core/Discovery/DiscoveredProject.cs
namespace CcLauncher.Core.Discovery;

public sealed record DiscoveredProject(
    string Id,
    string Cwd,
    IReadOnlyList<DiscoveredSession> Sessions)
{
    public DateTime LastActivity =>
        Sessions.Count == 0
            ? DateTime.MinValue
            : Sessions.Max(s => s.LastActivity);
}
