using CcLauncher.Core.Paths;
using FluentAssertions;
using Xunit;

namespace CcLauncher.Core.Tests.Paths;

public class PlatformPathsTests
{
    [Fact]
    public void ConfigDir_IsAbsoluteAndContainsAppName()
    {
        var p = PlatformPaths.ConfigDir();
        Path.IsPathRooted(p).Should().BeTrue();
        p.Should().Contain("cc-launcher");
    }

    [Fact]
    public void ClaudeProjectsDir_EndsInProjectsUnderHome()
    {
        var p = PlatformPaths.ClaudeProjectsDir();
        p.Should().EndWith(Path.Combine(".claude", "projects"));
    }

    [Fact]
    public void DatabaseFile_IsInsideConfigDir()
    {
        PlatformPaths.DatabaseFile().Should().StartWith(PlatformPaths.ConfigDir());
    }
}
