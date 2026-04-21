// tests/CcLauncher.Core.Tests/Discovery/ProjectDiscoveryServiceTests.cs
using CcLauncher.Core.Discovery;
using FluentAssertions;
using Xunit;

namespace CcLauncher.Core.Tests.Discovery;

public class ProjectDiscoveryServiceTests : IDisposable
{
    private readonly string _root;

    public ProjectDiscoveryServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "cc-launcher-disc-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void SeedProject(string folder, string cwd, params (string id, string startedAt, string lastAt, string firstMsg)[] sessions)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        foreach (var s in sessions)
        {
            var path = Path.Combine(dir, s.id + ".jsonl");
            File.WriteAllLines(path, new[]
            {
                $$$"""{"type":"user","timestamp":"{{{s.startedAt}}}","cwd":"{{{cwd}}}","message":{"role":"user","content":"{{{s.firstMsg}}}"}}""",
                $$$"""{"type":"assistant","timestamp":"{{{s.lastAt}}}","message":{"role":"assistant","content":"reply"}}""",
            });
        }
    }

    [Fact]
    public void Scan_EmptyRoot_ReturnsEmpty()
    {
        var svc = new ProjectDiscoveryService(_root);
        svc.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_MissingRoot_ReturnsEmpty()
    {
        var svc = new ProjectDiscoveryService(Path.Combine(_root, "does-not-exist"));
        svc.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_ProjectWithSessions_ReturnsProject()
    {
        SeedProject(
            "-home-x-foo",
            "/home/x/foo",
            ("s1", "2026-04-21T10:00:00Z", "2026-04-21T11:00:00Z", "do a thing"));

        var projects = new ProjectDiscoveryService(_root).Scan();

        projects.Should().ContainSingle();
        projects[0].Id.Should().Be("-home-x-foo");
        projects[0].Cwd.Should().Be("/home/x/foo");
        projects[0].Sessions.Should().ContainSingle();
        projects[0].Sessions[0].Id.Should().Be("s1");
        projects[0].Sessions[0].FirstUserMsg.Should().Be("do a thing");
    }

    [Fact]
    public void Scan_MultipleProjects_SortsByLastActivityDescending()
    {
        SeedProject("-a", "/a", ("s", "2026-04-01T00:00:00Z", "2026-04-01T00:00:00Z", "old"));
        SeedProject("-b", "/b", ("s", "2026-04-20T00:00:00Z", "2026-04-20T00:00:00Z", "new"));

        var projects = new ProjectDiscoveryService(_root).Scan();

        projects.Select(p => p.Id).Should().ContainInOrder("-b", "-a");
    }

    [Fact]
    public void Scan_ProjectWithNoSessions_StillIncluded()
    {
        Directory.CreateDirectory(Path.Combine(_root, "-empty-proj"));

        var projects = new ProjectDiscoveryService(_root).Scan();

        projects.Should().ContainSingle();
        projects[0].Sessions.Should().BeEmpty();
    }
}
