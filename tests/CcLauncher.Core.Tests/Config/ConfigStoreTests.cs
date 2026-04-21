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
}
