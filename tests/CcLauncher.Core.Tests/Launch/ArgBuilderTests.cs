using CcLauncher.Core.Config;
using CcLauncher.Core.Launch;
using FluentAssertions;
using Xunit;

namespace CcLauncher.Core.Tests.Launch;

public class ArgBuilderTests
{
    private static GlobalSettings Global(string? args = null, string? prompt = null) =>
        new("wt", args, prompt, false, false, true);

    [Fact]
    public void Build_NoConfig_NoSession_Empty()
    {
        var args = ArgBuilder.Build(Global(), ProjectSettings.Default("-p"), sessionId: null);
        args.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithSession_AppendsResume()
    {
        var args = ArgBuilder.Build(Global(), ProjectSettings.Default("-p"), sessionId: "abc");
        args.Should().Equal("--resume", "abc");
    }

    [Fact]
    public void Build_GlobalArgs_ThenProjectArgs_ThenResume()
    {
        var global  = Global(args: "--model opus");
        var project = ProjectSettings.Default("-p") with { DefaultArgs = "--permission-mode acceptEdits" };
        var args = ArgBuilder.Build(global, project, sessionId: "abc");
        args.Should().Equal("--model", "opus", "--permission-mode", "acceptEdits", "--resume", "abc");
    }

    [Fact]
    public void Build_MergesSystemPrompts_GlobalThenProject()
    {
        var global  = Global(prompt: "be brief");
        var project = ProjectSettings.Default("-p") with { SystemPrompt = "use typescript" };
        var args = ArgBuilder.Build(global, project, sessionId: null);
        args.Should().ContainInOrder("--append-system-prompt", "be brief\n\nuse typescript");
    }

    [Fact]
    public void Build_OnlyGlobalPrompt_UsesGlobalAlone()
    {
        var global  = Global(prompt: "be brief");
        var args = ArgBuilder.Build(global, ProjectSettings.Default("-p"), null);
        args.Should().Equal("--append-system-prompt", "be brief");
    }
}
