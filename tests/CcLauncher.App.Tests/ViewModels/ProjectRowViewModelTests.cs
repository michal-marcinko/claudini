using CcLauncher.App.ViewModels;
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;
using FluentAssertions;
using Xunit;

namespace CcLauncher.App.Tests.ViewModels;

public class ProjectRowViewModelTests
{
    private static DiscoveredProject Project(string id, string cwd, params DiscoveredSession[] sessions) =>
        new(id, cwd, sessions);

    private static DiscoveredSession Session(string id, DateTime at, string? preview = "hi") =>
        new(id, "/x/" + id + ".jsonl", at, at, 1, preview);

    [Fact]
    public void DisplayName_PrefersRenameOverride()
    {
        var p = Project("-a", "/foo/bar");
        var vm = new ProjectRowViewModel(p, ProjectSettings.Default("-a") with { DisplayName = "My App" });
        vm.DisplayName.Should().Be("My App");
    }

    [Fact]
    public void DisplayName_FallsBackToCwdBasename()
    {
        var p = Project("-a", "/foo/bar");
        var vm = new ProjectRowViewModel(p, ProjectSettings.Default("-a"));
        vm.DisplayName.Should().Be("bar");
    }

    [Fact]
    public void LatestSessionId_IsMostRecent()
    {
        var older = Session("old", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = Session("new", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));
        var vm = new ProjectRowViewModel(Project("-a", "/foo", older, newer), ProjectSettings.Default("-a"));
        vm.LatestSessionId.Should().Be("new");
    }

    [Fact]
    public void LatestSessionId_NullIfNoSessions()
    {
        var vm = new ProjectRowViewModel(Project("-a", "/foo"), ProjectSettings.Default("-a"));
        vm.LatestSessionId.Should().BeNull();
    }

    [Fact]
    public void Pinned_MirrorsSettings()
    {
        var vm = new ProjectRowViewModel(Project("-a", "/foo"), ProjectSettings.Default("-a") with { Pinned = true });
        vm.Pinned.Should().BeTrue();
    }
}
