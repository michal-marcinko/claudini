// tests/CcLauncher.Core.Tests/Discovery/DiscoveredModelsTests.cs
using CcLauncher.Core.Discovery;
using FluentAssertions;
using Xunit;

namespace CcLauncher.Core.Tests.Discovery;

public class DiscoveredModelsTests
{
    [Fact]
    public void DiscoveredSession_Equality_ByValue()
    {
        var a = new DiscoveredSession(
            Id: "abc",
            FilePath: "/tmp/abc.jsonl",
            StartedAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc),
            LastActivity: new DateTime(2026, 4, 21, 11, 0, 0, DateTimeKind.Utc),
            MessageCount: 5,
            FirstUserMsg: "hello");
        var b = a with { };
        Assert.Equal(a, b);
    }

    [Fact]
    public void DiscoveredProject_LastActivity_IsMaxOfSessions()
    {
        var earlier = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var later   = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var p = new DiscoveredProject(
            Id: "-foo-bar",
            Cwd: "/foo/bar",
            Sessions: new[]
            {
                new DiscoveredSession("s1", "/x", earlier, earlier, 1, null),
                new DiscoveredSession("s2", "/x", later,   later,   1, null),
            });
        p.LastActivity.Should().Be(later);
    }
}
