// tests/CcLauncher.Core.Tests/Discovery/JsonlSessionReaderTests.cs
using CcLauncher.Core.Discovery;
using FluentAssertions;
using Xunit;

namespace CcLauncher.Core.Tests.Discovery;

public class JsonlSessionReaderTests : IDisposable
{
    private readonly string _dir;

    public JsonlSessionReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cc-launcher-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteJsonl(string name, string[] lines)
    {
        var path = Path.Combine(_dir, name + ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Read_HealthyFile_ExtractsAllFields()
    {
        var path = WriteJsonl("abc", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","cwd":"/foo/bar","message":{"role":"user","content":"hello world"}}""",
            """{"type":"assistant","timestamp":"2026-04-21T10:00:05Z","message":{"role":"assistant","content":"hi"}}""",
            """{"type":"user","timestamp":"2026-04-21T10:05:00Z","message":{"role":"user","content":"again"}}""",
        });

        var session = new JsonlSessionReader().Read(path);

        session.Id.Should().Be("abc");
        session.FilePath.Should().Be(path);
        session.StartedAt.Should().Be(new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
        session.LastActivity.Should().Be(new DateTime(2026, 4, 21, 10, 5, 0, DateTimeKind.Utc));
        session.MessageCount.Should().Be(3);
        session.FirstUserMsg.Should().Be("hello world");
    }

    [Fact]
    public void Read_SingleLine_StartEqualsEnd()
    {
        var path = WriteJsonl("single", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","cwd":"/x","message":{"role":"user","content":"only"}}"""
        });

        var s = new JsonlSessionReader().Read(path);

        s.StartedAt.Should().Be(s.LastActivity);
        s.MessageCount.Should().Be(1);
    }

    [Fact]
    public void Read_EmptyFile_ReturnsSessionWithMtimeFallback()
    {
        var path = WriteJsonl("empty", Array.Empty<string>());
        var mtime = File.GetLastWriteTimeUtc(path);

        var s = new JsonlSessionReader().Read(path);

        s.MessageCount.Should().Be(0);
        s.FirstUserMsg.Should().BeNull();
        s.StartedAt.Should().BeCloseTo(mtime, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Read_CorruptFirstLine_FallsBackGracefully()
    {
        var path = WriteJsonl("corrupt", new[]
        {
            "this is not json",
            """{"type":"user","timestamp":"2026-04-21T10:05:00Z","message":{"role":"user","content":"recovered"}}""",
        });

        var s = new JsonlSessionReader().Read(path);

        s.MessageCount.Should().Be(2);
        s.FirstUserMsg.Should().BeNull(); // couldn't parse first
        s.LastActivity.Should().Be(new DateTime(2026, 4, 21, 10, 5, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_TruncatesPreviewTo60Chars()
    {
        var longMsg = new string('x', 200);
        var path = WriteJsonl("long", new[]
        {
            $$$"""{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"{{{longMsg}}}"}}"""
        });

        var s = new JsonlSessionReader().Read(path);

        s.FirstUserMsg.Should().HaveLength(60);
    }

    [Fact]
    public void Read_CapturesSlug_FromAnyRecord()
    {
        var path = WriteJsonl("slug", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"hi"},"slug":"velvet-kindling-key"}""",
            """{"type":"assistant","timestamp":"2026-04-21T10:00:05Z","message":{"role":"assistant","content":"hello"}}""",
        });

        var s = new JsonlSessionReader().Read(path);

        s.Slug.Should().Be("velvet-kindling-key");
    }

    [Fact]
    public void Read_CapturesLastPrompt_FromTerminalRecord()
    {
        var path = WriteJsonl("lp", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"first"}}""",
            """{"type":"user","timestamp":"2026-04-21T10:05:00Z","message":{"role":"user","content":"second"}}""",
            """{"type":"last-prompt","lastPrompt":"why are we getting temporary api errors again?"}""",
        });

        var s = new JsonlSessionReader().Read(path);

        s.LastPrompt.Should().Be("why are we getting temporary api errors again?");
    }

    [Fact]
    public void Read_LastPrompt_TakesMostRecentWhenMultiple()
    {
        var path = WriteJsonl("lp-multi", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"hi"}}""",
            """{"type":"last-prompt","lastPrompt":"old prompt"}""",
            """{"type":"user","timestamp":"2026-04-21T10:05:00Z","message":{"role":"user","content":"more"}}""",
            """{"type":"last-prompt","lastPrompt":"newest prompt"}""",
        });

        var s = new JsonlSessionReader().Read(path);

        s.LastPrompt.Should().Be("newest prompt");
    }

    [Fact]
    public void Read_LastPrompt_TruncatesLikeFirstUserMsg()
    {
        var longPrompt = new string('y', 200);
        var path = WriteJsonl("lp-long", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"hi"}}""",
            $$$"""{"type":"last-prompt","lastPrompt":"{{{longPrompt}}}"}""",
        });

        var s = new JsonlSessionReader().Read(path);

        s.LastPrompt.Should().HaveLength(60);
    }

    [Fact]
    public void Read_NoSlugOrLastPrompt_ReturnsNull()
    {
        var path = WriteJsonl("plain", new[]
        {
            """{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"hi"}}""",
        });

        var s = new JsonlSessionReader().Read(path);

        s.Slug.Should().BeNull();
        s.LastPrompt.Should().BeNull();
    }
}
