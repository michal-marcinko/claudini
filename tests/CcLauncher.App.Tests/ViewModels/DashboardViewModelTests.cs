using CcLauncher.App.ViewModels;
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;
using CcLauncher.Core.Launch;
using FluentAssertions;
using Xunit;

namespace CcLauncher.App.Tests.ViewModels;

public class DashboardViewModelTests
{
    private sealed class FakeDiscovery : IProjectDiscoveryService
    {
        public IReadOnlyList<DiscoveredProject> Projects { get; set; } = Array.Empty<DiscoveredProject>();
        public IReadOnlyList<DiscoveredProject> Scan() => Projects;
    }

    private sealed class FakeConfig : IConfigStore
    {
        public Dictionary<string, ProjectSettings> Settings { get; } = new();
        public GlobalSettings Global { get; set; } = GlobalSettings.Defaults("wt");
        public GlobalSettings GetGlobalSettings() => Global;
        public void SaveGlobalSettings(GlobalSettings s) => Global = s;
        public ProjectSettings GetProjectSettings(string id) => Settings.GetValueOrDefault(id) ?? ProjectSettings.Default(id);
        public IReadOnlyDictionary<string, ProjectSettings> GetAllProjectSettings() => Settings;
        public void SaveProjectSettings(ProjectSettings p) => Settings[p.ProjectId] = p;
        public long InsertLaunch(string p, string s, int? pid) => 1;
        public IReadOnlyList<LaunchHistoryEntry> GetRecentLaunches(int take) => Array.Empty<LaunchHistoryEntry>();
    }

    private sealed class FakeLauncher : ILauncher
    {
        public LaunchRequest? Captured { get; private set; }
        public LaunchResult Launch(LaunchRequest r) { Captured = r; return new LaunchResult(true, 1, null, "<fake>"); }
    }

    private static DiscoveredProject P(string id, string cwd, DateTime at) =>
        new(id, cwd, new[] { new DiscoveredSession(id + "-s", "/x", at, at, 1, "hi") });

    [Fact]
    public void Refresh_HidesHiddenProjects()
    {
        var disc = new FakeDiscovery { Projects = new[] { P("-a", "/a", DateTime.UtcNow) } };
        var cfg  = new FakeConfig();
        cfg.Settings["-a"] = ProjectSettings.Default("-a") with { Hidden = true };
        var vm = new DashboardViewModel(disc, cfg, new FakeLauncher());

        vm.Refresh();

        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Refresh_SortsPinnedFirst_ThenByRecency()
    {
        var disc = new FakeDiscovery
        {
            Projects = new[]
            {
                P("-old", "/old", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
                P("-new", "/new", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
                P("-pin", "/pin", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)),
            },
        };
        var cfg = new FakeConfig();
        cfg.Settings["-pin"] = ProjectSettings.Default("-pin") with { Pinned = true };

        var vm = new DashboardViewModel(disc, cfg, new FakeLauncher());
        vm.Refresh();

        vm.Rows.Select(r => r.Id).Should().ContainInOrder("-pin", "-new", "-old");
    }

    [Fact]
    public void Launch_ResumeLatest_BuildsRequestWithResumeArg()
    {
        var disc = new FakeDiscovery { Projects = new[] { P("-a", "/a", DateTime.UtcNow) } };
        var launcher = new FakeLauncher();
        var vm = new DashboardViewModel(disc, new FakeConfig(), launcher);
        vm.Refresh();

        vm.LaunchProject(vm.Rows[0], sessionId: null);

        Assert.NotNull(launcher.Captured);
        launcher.Captured!.ClaudeArgs.Should().Contain("--resume");
    }

    [Fact]
    public void Launch_NewSession_OmitsResume()
    {
        var disc = new FakeDiscovery { Projects = new[] { P("-a", "/a", DateTime.UtcNow) } };
        var launcher = new FakeLauncher();
        var vm = new DashboardViewModel(disc, new FakeConfig(), launcher);
        vm.Refresh();

        vm.LaunchNewSession(vm.Rows[0]);

        launcher.Captured!.ClaudeArgs.Should().NotContain("--resume");
    }

    [Fact]
    public void TogglePinned_PersistsAndResorts()
    {
        var disc = new FakeDiscovery
        {
            Projects = new[]
            {
                P("-old", "/old", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
                P("-new", "/new", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            },
        };
        var cfg = new FakeConfig();
        var vm = new DashboardViewModel(disc, cfg, new FakeLauncher());
        vm.Refresh();

        vm.TogglePinned(vm.Rows.First(r => r.Id == "-old"));

        vm.Rows.Select(r => r.Id).Should().ContainInOrder("-old", "-new");
        cfg.Settings["-old"].Pinned.Should().BeTrue();
    }

    [Fact]
    public void Hide_RemovesRow_AndPersists()
    {
        var disc = new FakeDiscovery
        {
            Projects = new[] { P("-a", "/a", DateTime.UtcNow) },
        };
        var cfg = new FakeConfig();
        var vm = new DashboardViewModel(disc, cfg, new FakeLauncher());
        vm.Refresh();

        vm.Hide(vm.Rows[0]);

        vm.Rows.Should().BeEmpty();
        cfg.Settings["-a"].Hidden.Should().BeTrue();
    }

    [Fact]
    public void Refresh_GroupsPinnedAndRecentIntoSeparateCollections()
    {
        var disc = new FakeDiscovery
        {
            Projects = new[]
            {
                P("-a", "/a", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)),
                P("-b", "/b", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
                P("-c", "/c", new DateTime(2026, 4, 1,  0, 0, 0, DateTimeKind.Utc)),
            },
        };
        var cfg = new FakeConfig();
        cfg.Settings["-a"] = ProjectSettings.Default("-a") with { Pinned = true };
        var vm = new DashboardViewModel(disc, cfg, new FakeLauncher());

        vm.Refresh();

        vm.PinnedRows.Select(r => r.Id).Should().ContainInOrder("-a");
        vm.RecentRows.Select(r => r.Id).Should().ContainInOrder("-b", "-c");
        vm.HasPinned.Should().BeTrue();
    }

    [Fact]
    public void Refresh_NoPinned_LeavesHasPinnedFalse()
    {
        var disc = new FakeDiscovery { Projects = new[] { P("-a", "/a", DateTime.UtcNow) } };
        var vm = new DashboardViewModel(disc, new FakeConfig(), new FakeLauncher());

        vm.Refresh();

        vm.HasPinned.Should().BeFalse();
        vm.PinnedRows.Should().BeEmpty();
        vm.RecentRows.Should().HaveCount(1);
    }

    [Fact]
    public void Rename_UpdatesDisplayName_AndPersists()
    {
        var disc = new FakeDiscovery
        {
            Projects = new[] { P("-a", "/foo/bar", DateTime.UtcNow) },
        };
        var cfg = new FakeConfig();
        var vm = new DashboardViewModel(disc, cfg, new FakeLauncher());
        vm.Refresh();

        vm.Rename(vm.Rows[0], "My Thing");

        vm.Rows[0].DisplayName.Should().Be("My Thing");
        cfg.Settings["-a"].DisplayName.Should().Be("My Thing");
    }
}
