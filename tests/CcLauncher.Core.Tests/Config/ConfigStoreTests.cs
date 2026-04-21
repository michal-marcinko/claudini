using CcLauncher.Core.Config;
using FluentAssertions;
using Xunit;

namespace CcLauncher.Core.Tests.Config;

public class ConfigStoreTests
{
    private static IConfigStore CreateStore() =>
        ConfigStore.OpenInMemory("platform-term");

    [Fact]
    public void GlobalSettings_Defaults_WhenUnset()
    {
        var store = CreateStore();
        var g = store.GetGlobalSettings();
        g.TerminalCommand.Should().Be("platform-term");
        g.LaunchOnStartup.Should().BeFalse();
    }

    [Fact]
    public void GlobalSettings_Roundtrip()
    {
        var store = CreateStore();
        var updated = new GlobalSettings(
            TerminalCommand: "custom-term",
            GlobalDefaultArgs: "--model opus",
            GlobalSystemPrompt: "be brief",
            LaunchOnStartup: true,
            ResumeAllOnOpen: true,
            CloseOnLaunch: false);

        store.SaveGlobalSettings(updated);
        store.GetGlobalSettings().Equals(updated).Should().BeTrue("GlobalSettings roundtrip should be equal");
    }

    [Fact]
    public void GlobalSettings_Theme_DefaultsToSystem()
    {
        var store = CreateStore();
        store.GetGlobalSettings().Theme.Should().Be("System");
    }

    [Fact]
    public void GlobalSettings_Theme_Roundtrips()
    {
        var store = CreateStore();
        var settings = GlobalSettings.Defaults("term") with { Theme = "Dark" };
        store.SaveGlobalSettings(settings);
        store.GetGlobalSettings().Theme.Should().Be("Dark");
    }

    [Fact]
    public void ProjectSettings_MissingProject_ReturnsDefault()
    {
        var store = CreateStore();
        var p = store.GetProjectSettings("-unknown");
        p.Equals(ProjectSettings.Default("-unknown")).Should().BeTrue("missing project should return Default");
    }

    [Fact]
    public void ProjectSettings_Upsert_Roundtrip()
    {
        var store = CreateStore();
        var p = new ProjectSettings(
            ProjectId: "-foo",
            DisplayName: "Foo",
            Pinned: true,
            Hidden: false,
            DefaultArgs: "--verbose",
            SystemPrompt: null,
            LastLaunchedAt: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));

        store.SaveProjectSettings(p);
        store.GetProjectSettings("-foo").Equals(p).Should().BeTrue("saved ProjectSettings should roundtrip correctly");
    }

    [Fact]
    public void ProjectSettings_GetAll_ReturnsDictByProjectId()
    {
        var store = CreateStore();
        store.SaveProjectSettings(ProjectSettings.Default("-a") with { Pinned = true });
        store.SaveProjectSettings(ProjectSettings.Default("-b") with { Hidden = true });

        var all = store.GetAllProjectSettings();

        all.Should().HaveCount(2);
        all["-a"].Pinned.Should().BeTrue();
        all["-b"].Hidden.Should().BeTrue();
    }

    [Fact]
    public void LaunchHistory_Insert_AssignsId()
    {
        var store = CreateStore();
        var id = store.InsertLaunch("-proj", "session-1", 1234);
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetRecentLaunches_OrdersByLaunchedAtDescending_AndRespectsLimit()
    {
        var store = CreateStore();

        // Three inserts with distinct launched_at timestamps.
        // InsertLaunch stamps launched_at = DateTime.UtcNow; sleep briefly between calls
        // to guarantee strict monotonic ordering at the column's serialized precision.
        store.InsertLaunch("-proj", "session-1", 1);
        Thread.Sleep(50);
        store.InsertLaunch("-proj", "session-2", 2);
        Thread.Sleep(50);
        store.InsertLaunch("-proj", "session-3", 3);

        var recent = store.GetRecentLaunches(take: 2);

        recent.Should().HaveCount(2);
        recent[0].SessionId.Should().Be("session-3");
        recent[1].SessionId.Should().Be("session-2");
        (recent[0].LaunchedAt >= recent[1].LaunchedAt).Should().BeTrue("results should be ordered newest first");
    }

    [Fact]
    public void SaveGlobalSettings_OverwritesExistingValues_OnConflict()
    {
        var store = CreateStore();

        var settings1 = new GlobalSettings(
            TerminalCommand: "term-1",
            GlobalDefaultArgs: "--first",
            GlobalSystemPrompt: "prompt-1",
            LaunchOnStartup: false,
            ResumeAllOnOpen: false,
            CloseOnLaunch: true);

        var settings2 = new GlobalSettings(
            TerminalCommand: "term-2",
            GlobalDefaultArgs: "--second",
            GlobalSystemPrompt: "prompt-2",
            LaunchOnStartup: true,
            ResumeAllOnOpen: true,
            CloseOnLaunch: false);

        store.SaveGlobalSettings(settings1);
        store.SaveGlobalSettings(settings2);

        store.GetGlobalSettings().Equals(settings2).Should().BeTrue("second save should overwrite the first via ON CONFLICT DO UPDATE");
    }
}
