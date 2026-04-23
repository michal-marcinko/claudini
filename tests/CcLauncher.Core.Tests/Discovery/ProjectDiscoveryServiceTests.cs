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

    [Fact]
    public void Scan_PoisonedCwdWithNewline_IsRejected()
    {
        // Security: a jsonl dropped by another user (or corrupt session file)
        // could carry a cwd string with embedded CR/LF, which would break out
        // of shell-quoting in the PowerShell launcher. Discovery must reject
        // any cwd containing control characters and fall through to the next
        // candidate (or the encoded-id fallback).
        var dir = Path.Combine(_root, "-poison");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "s.jsonl"), new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","cwd":"C:\\bad\r\nStart-Process calc\r\n#","message":{"role":"user","content":"x"}}""",
        });

        var projects = new ProjectDiscoveryService(_root).Scan();

        projects.Should().ContainSingle();
        projects[0].Cwd.Should().NotContain("\r");
        projects[0].Cwd.Should().NotContain("\n");
    }

    [Fact]
    public void Scan_JsonlWithLeadingMetadataLines_StillRecoversCwd()
    {
        // Regression: modern Claude Code prepends metadata records (permission-mode,
        // hook output, SessionStart reminders) that lack a `cwd` field. Discovery must
        // scan forward until it finds a record that carries the working directory,
        // otherwise it falls back to the encoded folder name and the launcher tries
        // to cd into a non-existent relative path.
        var dir = Path.Combine(_root, "-home-x-foo");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "s1.jsonl"), new[]
        {
            """{"type":"permission-mode","permissionMode":"bypassPermissions","sessionId":"s1"}""",
            """{"type":"attachment","attachment":{"type":"hook_success","hookName":"SessionStart:startup"}}""",
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","cwd":"/home/x/foo","message":{"role":"user","content":"hi"}}""",
        });

        var projects = new ProjectDiscoveryService(_root).Scan();

        projects.Should().ContainSingle();
        projects[0].Cwd.Should().Be("/home/x/foo");
    }
}
