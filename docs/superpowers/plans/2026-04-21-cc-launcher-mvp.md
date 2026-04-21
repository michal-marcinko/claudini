# cc-launcher MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship an Avalonia-based cross-platform tray app that auto-discovers Claude Code projects from `~/.claude/projects/`, presents them in a lightweight dashboard, and lets the user resume any session in their terminal with one click.

**Architecture:** Two-project solution. `CcLauncher.Core` is a pure-logic class library (discovery, config, launcher, arg building) with no UI dependency — fully unit-testable. `CcLauncher.App` is the Avalonia executable (tray, views, view-models) that consumes `Core` via constructor injection.

**Tech Stack:** .NET 9, Avalonia 11, CommunityToolkit.Mvvm (for `ObservableObject` / `RelayCommand`), Microsoft.Data.Sqlite, xUnit, FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-04-21-claude-code-launcher-design.md`

---

## Parallelization Map

After Phase 0 (scaffold) completes, **Phases 1, 2, and 3 can run in parallel** — no cross-file dependencies. Phase 4 depends on 1 + 2 + 3. Within Phase 4 onward, tasks are mostly sequential because they share App-level files (`Program.cs`, `App.axaml`, `Dashboard.axaml`).

```
Phase 0 (scaffold)
   │
   ├── Phase 1 (Discovery)   ┐
   ├── Phase 2 (Config)      ├── Phase 4 (App scaffold + tray)
   └── Phase 3 (Launch)      ┘        │
                                       └── Phase 5 (VMs) ─ Phase 6 (Views) ─ Phase 7 (Settings/watcher/startup)
```

**Testing discipline:** every Core task is test-first. `dotnet test` after each task must go green before commit. UI views are not unit tested in MVP; view-model behavior is.

---

## Phase 0: Solution scaffold

### Task 0.1: Initialize solution and projects

**Files:**
- Create: `cc-launcher.sln`
- Create: `src/CcLauncher.Core/CcLauncher.Core.csproj`
- Create: `src/CcLauncher.App/CcLauncher.App.csproj`
- Create: `tests/CcLauncher.Core.Tests/CcLauncher.Core.Tests.csproj`

- [ ] **Step 1: Run scaffolding commands from project root.**

```powershell
dotnet new sln -n cc-launcher
dotnet new classlib -n CcLauncher.Core -o src/CcLauncher.Core -f net8.0
dotnet new console  -n CcLauncher.App  -o src/CcLauncher.App  -f net8.0
dotnet new xunit    -n CcLauncher.Core.Tests -o tests/CcLauncher.Core.Tests -f net8.0
dotnet sln add src/CcLauncher.Core/CcLauncher.Core.csproj src/CcLauncher.App/CcLauncher.App.csproj tests/CcLauncher.Core.Tests/CcLauncher.Core.Tests.csproj
dotnet add tests/CcLauncher.Core.Tests/CcLauncher.Core.Tests.csproj reference src/CcLauncher.Core/CcLauncher.Core.csproj
dotnet add src/CcLauncher.App/CcLauncher.App.csproj reference src/CcLauncher.Core/CcLauncher.Core.csproj
dotnet add tests/CcLauncher.Core.Tests/CcLauncher.Core.Tests.csproj package FluentAssertions
```

- [ ] **Step 2: Delete auto-generated `Class1.cs` / `Program.cs` stubs we don't need.**

```powershell
Remove-Item src/CcLauncher.Core/Class1.cs
```

Leave `src/CcLauncher.App/Program.cs` for now (replaced in Phase 4).

- [ ] **Step 3: Build.**

```powershell
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Commit.**

```powershell
git add -A
git commit -m "chore: scaffold solution with Core, App, and Core.Tests"
```

### Task 0.2: `.gitignore`

**Files:**
- Create: `.gitignore`

- [ ] **Step 1: Write `.gitignore`.**

```gitignore
# .NET
bin/
obj/
*.user
*.suo

# IDE
.idea/
.vs/
.vscode/

# Publish output
publish/
*.pdb

# SQLite dev databases
*.db
*.db-shm
*.db-wal

# OS
.DS_Store
Thumbs.db

# Logs
logs/
*.log
```

- [ ] **Step 2: Verify nothing unwanted is tracked.**

```powershell
git status
```

Expected: only `.gitignore` is new; no `bin/` or `obj/` entries.

- [ ] **Step 3: Commit.**

```powershell
git add .gitignore
git commit -m "chore: add .gitignore"
```

---

## Phase 1: Core / Discovery

*Runnable in parallel with Phases 2 and 3.*

### Task 1.1: `DiscoveredSession` and `DiscoveredProject` records

**Files:**
- Create: `src/CcLauncher.Core/Discovery/DiscoveredSession.cs`
- Create: `src/CcLauncher.Core/Discovery/DiscoveredProject.cs`
- Create: `tests/CcLauncher.Core.Tests/Discovery/DiscoveredModelsTests.cs`

- [ ] **Step 1: Write failing test.**

```csharp
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
        a.Should().Be(b);
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
```

- [ ] **Step 2: Run — expect failure ("type does not exist").**

```powershell
dotnet test
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Discovery/DiscoveredSession.cs
namespace CcLauncher.Core.Discovery;

public sealed record DiscoveredSession(
    string Id,
    string FilePath,
    DateTime StartedAt,
    DateTime LastActivity,
    int MessageCount,
    string? FirstUserMsg);
```

```csharp
// src/CcLauncher.Core/Discovery/DiscoveredProject.cs
namespace CcLauncher.Core.Discovery;

public sealed record DiscoveredProject(
    string Id,
    string Cwd,
    IReadOnlyList<DiscoveredSession> Sessions)
{
    public DateTime LastActivity =>
        Sessions.Count == 0
            ? DateTime.MinValue
            : Sessions.Max(s => s.LastActivity);
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add DiscoveredSession and DiscoveredProject records"
```

### Task 1.2: `JsonlSessionReader` — parse first/last line of a session JSONL

**Files:**
- Create: `src/CcLauncher.Core/Discovery/JsonlSessionReader.cs`
- Create: `tests/CcLauncher.Core.Tests/Discovery/JsonlSessionReaderTests.cs`

JSONL format per line (what we care about):

```json
{"type":"user","timestamp":"2026-04-21T10:00:00Z","cwd":"/home/x/proj","message":{"role":"user","content":"hello world"}}
```

Only first + last line are read. First line gives `cwd`, `startedAt`, and possibly first user message. Last line gives `lastActivity`. Message count comes from line count (cheap — stream and count).

- [ ] **Step 1: Write failing tests.**

```csharp
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
            $$"""{"type":"user","timestamp":"2026-04-21T10:00:00Z","message":{"role":"user","content":"{{longMsg}}"}}"""
        });

        var s = new JsonlSessionReader().Read(path);

        s.FirstUserMsg.Should().HaveLength(60);
    }
}
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter JsonlSessionReaderTests
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Discovery/JsonlSessionReader.cs
using System.Text.Json;

namespace CcLauncher.Core.Discovery;

public sealed class JsonlSessionReader
{
    private const int PreviewMaxChars = 60;

    public DiscoveredSession Read(string filePath)
    {
        var id = Path.GetFileNameWithoutExtension(filePath);
        var fi = new FileInfo(filePath);

        string? firstLine = null;
        string? lastLine = null;
        var count = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            firstLine ??= line;
            lastLine = line;
            count++;
        }

        var (startedAt, firstUserMsg) = ParseFirst(firstLine, fi);
        var lastActivity = ParseTimestamp(lastLine) ?? startedAt;

        return new DiscoveredSession(
            Id: id,
            FilePath: filePath,
            StartedAt: startedAt,
            LastActivity: lastActivity,
            MessageCount: count,
            FirstUserMsg: firstUserMsg);
    }

    private static (DateTime startedAt, string? firstUserMsg) ParseFirst(string? line, FileInfo fi)
    {
        if (line is null) return (fi.LastWriteTimeUtc, null);

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var ts = root.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetDateTime(out var t)
                ? t.ToUniversalTime()
                : fi.LastWriteTimeUtc;

            string? preview = null;
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "user"
                && root.TryGetProperty("message", out var msgEl)
                && msgEl.TryGetProperty("content", out var contentEl))
            {
                preview = contentEl.ValueKind switch
                {
                    JsonValueKind.String => contentEl.GetString(),
                    JsonValueKind.Array  => FirstTextBlock(contentEl),
                    _ => null,
                };
                if (preview is { Length: > PreviewMaxChars })
                    preview = preview[..PreviewMaxChars];
            }

            return (ts, preview);
        }
        catch (JsonException)
        {
            return (fi.LastWriteTimeUtc, null);
        }
    }

    private static string? FirstTextBlock(JsonElement arr)
    {
        foreach (var item in arr.EnumerateArray())
            if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString();
        return null;
    }

    private static DateTime? ParseTimestamp(string? line)
    {
        if (line is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty("timestamp", out var t) && t.TryGetDateTime(out var dt)
                ? dt.ToUniversalTime()
                : null;
        }
        catch (JsonException) { return null; }
    }
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter JsonlSessionReaderTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add JsonlSessionReader with fixture-based tests"
```

### Task 1.3: `ProjectDiscoveryService` — scan `~/.claude/projects/`

**Files:**
- Create: `src/CcLauncher.Core/Discovery/IProjectDiscoveryService.cs`
- Create: `src/CcLauncher.Core/Discovery/ProjectDiscoveryService.cs`
- Create: `tests/CcLauncher.Core.Tests/Discovery/ProjectDiscoveryServiceTests.cs`

- [ ] **Step 1: Write failing tests.**

```csharp
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
                $$"""{"type":"user","timestamp":"{{s.startedAt}}","cwd":"{{cwd}}","message":{"role":"user","content":"{{s.firstMsg}}"}}""",
                $$"""{"type":"assistant","timestamp":"{{s.lastAt}}","message":{"role":"assistant","content":"reply"}}""",
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
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter ProjectDiscoveryServiceTests
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Discovery/IProjectDiscoveryService.cs
namespace CcLauncher.Core.Discovery;

public interface IProjectDiscoveryService
{
    IReadOnlyList<DiscoveredProject> Scan();
}
```

```csharp
// src/CcLauncher.Core/Discovery/ProjectDiscoveryService.cs
namespace CcLauncher.Core.Discovery;

public sealed class ProjectDiscoveryService : IProjectDiscoveryService
{
    private readonly string _projectsRoot;
    private readonly JsonlSessionReader _reader;

    public ProjectDiscoveryService(string projectsRoot, JsonlSessionReader? reader = null)
    {
        _projectsRoot = projectsRoot;
        _reader = reader ?? new JsonlSessionReader();
    }

    public IReadOnlyList<DiscoveredProject> Scan()
    {
        if (!Directory.Exists(_projectsRoot))
            return Array.Empty<DiscoveredProject>();

        var results = new List<DiscoveredProject>();
        foreach (var projDir in Directory.EnumerateDirectories(_projectsRoot))
        {
            DiscoveredProject? p;
            try { p = ReadProject(projDir); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            if (p is not null) results.Add(p);
        }

        return results
            .OrderByDescending(p => p.LastActivity)
            .ToList();
    }

    private DiscoveredProject? ReadProject(string projDir)
    {
        var id = Path.GetFileName(projDir);
        var sessions = new List<DiscoveredSession>();

        foreach (var jsonl in Directory.EnumerateFiles(projDir, "*.jsonl"))
        {
            try { sessions.Add(_reader.Read(jsonl)); }
            catch (IOException)   { /* skip corrupt */ }
            catch (JsonException) { /* skip corrupt */ }
        }

        var cwd = sessions.Count > 0
            ? InferCwd(sessions[0].FilePath) ?? DecodeId(id)
            : DecodeId(id);

        return new DiscoveredProject(id, cwd, sessions);
    }

    // Best-effort decode: Claude Code replaces path separators + colon with '-'.
    // We can't perfectly recover the original path, so this is a display fallback.
    private static string DecodeId(string id) => id;

    private static string? InferCwd(string firstJsonlPath)
    {
        try
        {
            var line = File.ReadLines(firstJsonlPath).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            if (line is null) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty("cwd", out var c) ? c.GetString() : null;
        }
        catch { return null; }
    }
}
```

(Note: the `using System.Text.Json` for `JsonException` needs to be added at the top of `ProjectDiscoveryService.cs`:)

```csharp
using System.Text.Json;
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter ProjectDiscoveryServiceTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add ProjectDiscoveryService with filesystem tests"
```

---

## Phase 2: Core / Config

*Runnable in parallel with Phases 1 and 3.*

### Task 2.1: Add SQLite dependency + config record types

**Files:**
- Modify: `src/CcLauncher.Core/CcLauncher.Core.csproj`
- Create: `src/CcLauncher.Core/Config/ProjectSettings.cs`
- Create: `src/CcLauncher.Core/Config/GlobalSettings.cs`
- Create: `src/CcLauncher.Core/Config/LaunchHistoryEntry.cs`

- [ ] **Step 1: Add Microsoft.Data.Sqlite.**

```powershell
dotnet add src/CcLauncher.Core/CcLauncher.Core.csproj package Microsoft.Data.Sqlite
```

- [ ] **Step 2: Write record types.**

```csharp
// src/CcLauncher.Core/Config/ProjectSettings.cs
namespace CcLauncher.Core.Config;

public sealed record ProjectSettings(
    string ProjectId,
    string? DisplayName,
    bool Pinned,
    bool Hidden,
    string? DefaultArgs,
    string? SystemPrompt,
    DateTime? LastLaunchedAt)
{
    public static ProjectSettings Default(string projectId) =>
        new(projectId, null, false, false, null, null, null);
}
```

```csharp
// src/CcLauncher.Core/Config/GlobalSettings.cs
namespace CcLauncher.Core.Config;

public sealed record GlobalSettings(
    string TerminalCommand,
    string? GlobalDefaultArgs,
    string? GlobalSystemPrompt,
    bool LaunchOnStartup,
    bool ResumeAllOnOpen,
    bool CloseOnLaunch)
{
    public static GlobalSettings Defaults(string platformTerminalCommand) =>
        new(platformTerminalCommand, null, null, false, false, true);
}
```

```csharp
// src/CcLauncher.Core/Config/LaunchHistoryEntry.cs
namespace CcLauncher.Core.Config;

public sealed record LaunchHistoryEntry(
    long Id,
    string SessionId,
    string ProjectId,
    DateTime LaunchedAt,
    DateTime? ClosedAt,
    int? Pid);
```

- [ ] **Step 3: Build.**

```powershell
dotnet build
```

Expected: succeeds.

- [ ] **Step 4: Commit.**

```powershell
git add -A
git commit -m "feat(core): add Config record types and Sqlite dependency"
```

### Task 2.2: `Migrations` — versioned schema scripts

**Files:**
- Create: `src/CcLauncher.Core/Config/Migrations.cs`
- Create: `tests/CcLauncher.Core.Tests/Config/MigrationsTests.cs`

- [ ] **Step 1: Write failing test.**

```csharp
// tests/CcLauncher.Core.Tests/Config/MigrationsTests.cs
using CcLauncher.Core.Config;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CcLauncher.Core.Tests.Config;

public class MigrationsTests
{
    [Fact]
    public void Apply_OnEmptyDb_CreatesAllTablesAndRecordsVersion()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        Migrations.Apply(conn);

        AssertTableExists(conn, "ProjectSettings");
        AssertTableExists(conn, "GlobalSettings");
        AssertTableExists(conn, "LaunchHistory");
        AssertTableExists(conn, "SchemaVersion");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(version) FROM SchemaVersion;";
        var v = Convert.ToInt64(cmd.ExecuteScalar());
        v.Should().Be(Migrations.CurrentVersion);
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        Migrations.Apply(conn);
        Migrations.Apply(conn); // second time should be a no-op

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SchemaVersion;";
        var rows = Convert.ToInt64(cmd.ExecuteScalar());
        rows.Should().Be(Migrations.CurrentVersion);
    }

    private static void AssertTableExists(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", table);
        cmd.ExecuteScalar().Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter MigrationsTests
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Config/Migrations.cs
using Microsoft.Data.Sqlite;

namespace CcLauncher.Core.Config;

public static class Migrations
{
    public const int CurrentVersion = 1;

    private static readonly string[] Scripts =
    {
        // version 1 — initial schema
        """
        CREATE TABLE ProjectSettings (
            project_id       TEXT PRIMARY KEY,
            display_name     TEXT,
            pinned           INTEGER NOT NULL DEFAULT 0,
            hidden           INTEGER NOT NULL DEFAULT 0,
            default_args     TEXT,
            system_prompt    TEXT,
            last_launched_at TEXT
        );

        CREATE TABLE GlobalSettings (
            key   TEXT PRIMARY KEY,
            value TEXT
        );

        CREATE TABLE LaunchHistory (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            session_id  TEXT NOT NULL,
            project_id  TEXT NOT NULL,
            launched_at TEXT NOT NULL,
            closed_at   TEXT,
            pid         INTEGER
        );

        CREATE INDEX idx_launch_history_project ON LaunchHistory(project_id, launched_at DESC);
        """,
    };

    public static void Apply(SqliteConnection connection)
    {
        EnsureVersionTable(connection);
        var current = GetCurrentVersion(connection);

        for (var i = current; i < CurrentVersion; i++)
        {
            using var tx = connection.BeginTransaction();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = Scripts[i];
                cmd.ExecuteNonQuery();
            }

            using (var vCmd = connection.CreateCommand())
            {
                vCmd.Transaction = tx;
                vCmd.CommandText = "INSERT INTO SchemaVersion(version, applied_at) VALUES ($v, $t);";
                vCmd.Parameters.AddWithValue("$v", i + 1);
                vCmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
                vCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    private static void EnsureVersionTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                version    INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static int GetCurrentVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM SchemaVersion;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter MigrationsTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add versioned SQLite migrations"
```

### Task 2.3: `ConfigStore` — CRUD over config tables

**Files:**
- Create: `src/CcLauncher.Core/Config/IConfigStore.cs`
- Create: `src/CcLauncher.Core/Config/ConfigStore.cs`
- Create: `tests/CcLauncher.Core.Tests/Config/ConfigStoreTests.cs`

- [ ] **Step 1: Write failing tests.**

```csharp
// tests/CcLauncher.Core.Tests/Config/ConfigStoreTests.cs
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
        store.GetGlobalSettings().Should().Be(updated);
    }

    [Fact]
    public void ProjectSettings_MissingProject_ReturnsDefault()
    {
        var store = CreateStore();
        var p = store.GetProjectSettings("-unknown");
        p.Should().Be(ProjectSettings.Default("-unknown"));
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
        store.GetProjectSettings("-foo").Should().Be(p);
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
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter ConfigStoreTests
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Config/IConfigStore.cs
namespace CcLauncher.Core.Config;

public interface IConfigStore
{
    GlobalSettings GetGlobalSettings();
    void SaveGlobalSettings(GlobalSettings settings);

    ProjectSettings GetProjectSettings(string projectId);
    IReadOnlyDictionary<string, ProjectSettings> GetAllProjectSettings();
    void SaveProjectSettings(ProjectSettings settings);

    long InsertLaunch(string projectId, string sessionId, int? pid);
    IReadOnlyList<LaunchHistoryEntry> GetRecentLaunches(int take);
}
```

```csharp
// src/CcLauncher.Core/Config/ConfigStore.cs
using Microsoft.Data.Sqlite;

namespace CcLauncher.Core.Config;

public sealed class ConfigStore : IConfigStore, IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly string _platformTerminal;

    private ConfigStore(SqliteConnection conn, string platformTerminal)
    {
        _conn = conn;
        _platformTerminal = platformTerminal;
        Migrations.Apply(_conn);
    }

    public static ConfigStore OpenFile(string path, string platformTerminal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        return new ConfigStore(conn, platformTerminal);
    }

    public static ConfigStore OpenInMemory(string platformTerminal)
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return new ConfigStore(conn, platformTerminal);
    }

    public GlobalSettings GetGlobalSettings()
    {
        var kv = ReadAllGlobalKv();
        return new GlobalSettings(
            TerminalCommand:     kv.GetValueOrDefault("terminal_command") ?? _platformTerminal,
            GlobalDefaultArgs:   kv.GetValueOrDefault("global_default_args"),
            GlobalSystemPrompt:  kv.GetValueOrDefault("global_system_prompt"),
            LaunchOnStartup:     kv.GetValueOrDefault("launch_on_startup") == "1",
            ResumeAllOnOpen:     kv.GetValueOrDefault("resume_all_on_open") == "1",
            CloseOnLaunch:       (kv.GetValueOrDefault("close_on_launch") ?? "1") == "1");
    }

    public void SaveGlobalSettings(GlobalSettings g)
    {
        WriteGlobalKv("terminal_command",     g.TerminalCommand);
        WriteGlobalKv("global_default_args",  g.GlobalDefaultArgs);
        WriteGlobalKv("global_system_prompt", g.GlobalSystemPrompt);
        WriteGlobalKv("launch_on_startup",    g.LaunchOnStartup ? "1" : "0");
        WriteGlobalKv("resume_all_on_open",   g.ResumeAllOnOpen ? "1" : "0");
        WriteGlobalKv("close_on_launch",      g.CloseOnLaunch ? "1" : "0");
    }

    public ProjectSettings GetProjectSettings(string projectId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT display_name,pinned,hidden,default_args,system_prompt,last_launched_at FROM ProjectSettings WHERE project_id=$id;";
        cmd.Parameters.AddWithValue("$id", projectId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return ProjectSettings.Default(projectId);
        return new ProjectSettings(
            ProjectId:       projectId,
            DisplayName:     r.IsDBNull(0) ? null : r.GetString(0),
            Pinned:          r.GetInt32(1) != 0,
            Hidden:          r.GetInt32(2) != 0,
            DefaultArgs:     r.IsDBNull(3) ? null : r.GetString(3),
            SystemPrompt:    r.IsDBNull(4) ? null : r.GetString(4),
            LastLaunchedAt:  r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)).ToUniversalTime());
    }

    public IReadOnlyDictionary<string, ProjectSettings> GetAllProjectSettings()
    {
        var dict = new Dictionary<string, ProjectSettings>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT project_id,display_name,pinned,hidden,default_args,system_prompt,last_launched_at FROM ProjectSettings;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.GetString(0);
            dict[id] = new ProjectSettings(
                id,
                r.IsDBNull(1) ? null : r.GetString(1),
                r.GetInt32(2) != 0,
                r.GetInt32(3) != 0,
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6)).ToUniversalTime());
        }
        return dict;
    }

    public void SaveProjectSettings(ProjectSettings p)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ProjectSettings(project_id,display_name,pinned,hidden,default_args,system_prompt,last_launched_at)
            VALUES($id,$dn,$pin,$hid,$da,$sp,$ll)
            ON CONFLICT(project_id) DO UPDATE SET
                display_name=excluded.display_name,
                pinned=excluded.pinned,
                hidden=excluded.hidden,
                default_args=excluded.default_args,
                system_prompt=excluded.system_prompt,
                last_launched_at=excluded.last_launched_at;
            """;
        cmd.Parameters.AddWithValue("$id",  p.ProjectId);
        cmd.Parameters.AddWithValue("$dn",  (object?)p.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pin", p.Pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$hid", p.Hidden ? 1 : 0);
        cmd.Parameters.AddWithValue("$da",  (object?)p.DefaultArgs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sp",  (object?)p.SystemPrompt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ll",  (object?)p.LastLaunchedAt?.ToString("O") ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public long InsertLaunch(string projectId, string sessionId, int? pid)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LaunchHistory(project_id, session_id, launched_at, pid)
            VALUES($pid, $sid, $at, $procPid);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$at",  DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$procPid", (object?)pid ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public IReadOnlyList<LaunchHistoryEntry> GetRecentLaunches(int take)
    {
        var list = new List<LaunchHistoryEntry>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id,session_id,project_id,launched_at,closed_at,pid FROM LaunchHistory ORDER BY launched_at DESC LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", take);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new LaunchHistoryEntry(
                Id:          r.GetInt64(0),
                SessionId:   r.GetString(1),
                ProjectId:   r.GetString(2),
                LaunchedAt:  DateTime.Parse(r.GetString(3)).ToUniversalTime(),
                ClosedAt:    r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)).ToUniversalTime(),
                Pid:         r.IsDBNull(5) ? null : r.GetInt32(5)));
        }
        return list;
    }

    private Dictionary<string, string?> ReadAllGlobalKv()
    {
        var dict = new Dictionary<string, string?>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM GlobalSettings;";
        using var r = cmd.ExecuteReader();
        while (r.Read()) dict[r.GetString(0)] = r.IsDBNull(1) ? null : r.GetString(1);
        return dict;
    }

    private void WriteGlobalKv(string key, string? value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO GlobalSettings(key,value) VALUES($k,$v)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter ConfigStoreTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add ConfigStore with SQLite persistence"
```

---

## Phase 3: Core / Launch

*Runnable in parallel with Phases 1 and 2.*

### Task 3.1: `ArgBuilder` — pure args composition

**Files:**
- Create: `src/CcLauncher.Core/Launch/ArgBuilder.cs`
- Create: `tests/CcLauncher.Core.Tests/Launch/ArgBuilderTests.cs`

- [ ] **Step 1: Write failing tests.**

```csharp
// tests/CcLauncher.Core.Tests/Launch/ArgBuilderTests.cs
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
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter ArgBuilderTests
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Launch/ArgBuilder.cs
using CcLauncher.Core.Config;

namespace CcLauncher.Core.Launch;

public static class ArgBuilder
{
    public static IReadOnlyList<string> Build(
        GlobalSettings global,
        ProjectSettings project,
        string? sessionId)
    {
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(global.GlobalDefaultArgs))
            args.AddRange(SplitArgs(global.GlobalDefaultArgs));

        if (!string.IsNullOrWhiteSpace(project.DefaultArgs))
            args.AddRange(SplitArgs(project.DefaultArgs));

        if (sessionId is not null)
        {
            args.Add("--resume");
            args.Add(sessionId);
        }

        var mergedPrompt = MergePrompts(global.GlobalSystemPrompt, project.SystemPrompt);
        if (!string.IsNullOrWhiteSpace(mergedPrompt))
        {
            args.Add("--append-system-prompt");
            args.Add(mergedPrompt);
        }

        return args;
    }

    private static string? MergePrompts(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b;
        if (string.IsNullOrWhiteSpace(b)) return a;
        return a + "\n\n" + b;
    }

    // Minimal arg splitter: splits on whitespace, honors double quotes.
    private static IEnumerable<string> SplitArgs(string input)
    {
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in input)
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter ArgBuilderTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add ArgBuilder for composing claude CLI args"
```

### Task 3.2: `ILauncher` interface + `LaunchRequest` + `LaunchResult`

**Files:**
- Create: `src/CcLauncher.Core/Launch/LaunchRequest.cs`
- Create: `src/CcLauncher.Core/Launch/LaunchResult.cs`
- Create: `src/CcLauncher.Core/Launch/ILauncher.cs`

- [ ] **Step 1: Write records + interface.**

```csharp
// src/CcLauncher.Core/Launch/LaunchRequest.cs
namespace CcLauncher.Core.Launch;

public sealed record LaunchRequest(
    string Cwd,
    string TerminalCommand,
    IReadOnlyList<string> ClaudeArgs);
```

```csharp
// src/CcLauncher.Core/Launch/LaunchResult.cs
namespace CcLauncher.Core.Launch;

public sealed record LaunchResult(bool Success, int? Pid, string? Error, string ResolvedCommandLine);
```

```csharp
// src/CcLauncher.Core/Launch/ILauncher.cs
namespace CcLauncher.Core.Launch;

public interface ILauncher
{
    LaunchResult Launch(LaunchRequest request);
}
```

- [ ] **Step 2: Build.**

```powershell
dotnet build
```

- [ ] **Step 3: Commit.**

```powershell
git add -A
git commit -m "feat(core): add ILauncher interface and request/result records"
```

### Task 3.3: Platform launcher implementations

**Files:**
- Create: `src/CcLauncher.Core/Launch/WindowsLauncher.cs`
- Create: `src/CcLauncher.Core/Launch/MacLauncher.cs`
- Create: `src/CcLauncher.Core/Launch/LinuxLauncher.cs`
- Create: `src/CcLauncher.Core/Launch/LauncherFactory.cs`
- Create: `tests/CcLauncher.Core.Tests/Launch/LauncherIntegrationTests.cs`

The launcher tests use a **stub terminal**: a small script (created at test setup) that writes its args and cwd to a file. We assert on that file's contents. This way we don't actually spawn Windows Terminal / Terminal.app during tests.

- [ ] **Step 1: Write failing test with stub-terminal fixture.**

```csharp
// tests/CcLauncher.Core.Tests/Launch/LauncherIntegrationTests.cs
using CcLauncher.Core.Launch;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit;

namespace CcLauncher.Core.Tests.Launch;

public class LauncherIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _stubPath;
    private readonly string _outputPath;

    public LauncherIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cc-launcher-launch-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _outputPath = Path.Combine(_tempDir, "out.txt");

        // Stub "terminal": writes its args + cwd to _outputPath, then exits.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _stubPath = Path.Combine(_tempDir, "stub.cmd");
            File.WriteAllText(_stubPath, $"""
                @echo off
                echo CWD=%CD% > "{_outputPath}"
                echo ARGS=%* >> "{_outputPath}"
                """);
        }
        else
        {
            _stubPath = Path.Combine(_tempDir, "stub.sh");
            File.WriteAllText(_stubPath, $"""
                #!/usr/bin/env bash
                echo "CWD=$PWD" > "{_outputPath}"
                echo "ARGS=$*" >> "{_outputPath}"
                """);
            File.SetUnixFileMode(_stubPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Launch_Synchronously_CapturesArgsAndCwd()
    {
        var launcher = LauncherFactory.ForTesting(_stubPath);
        var req = new LaunchRequest(
            Cwd: _tempDir,
            TerminalCommand: _stubPath, // overridden for test
            ClaudeArgs: new[] { "--resume", "abc" });

        var result = launcher.Launch(req);

        result.Success.Should().BeTrue();
        // Stub writes synchronously, but spawned process is detached.
        // Wait briefly for output.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(_outputPath) && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        File.Exists(_outputPath).Should().BeTrue();
        var contents = File.ReadAllText(_outputPath);
        contents.Should().Contain("--resume");
        contents.Should().Contain("abc");
    }
}
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter LauncherIntegrationTests
```

- [ ] **Step 3: Implement `WindowsLauncher`.**

```csharp
// src/CcLauncher.Core/Launch/WindowsLauncher.cs
using System.Diagnostics;

namespace CcLauncher.Core.Launch;

public sealed class WindowsLauncher : ILauncher
{
    public LaunchResult Launch(LaunchRequest request)
    {
        // If TerminalCommand is a .cmd/.exe we can invoke directly (test stub),
        // honor it verbatim. Otherwise assume wt.exe-style and fall back to powershell.
        var isDirectExe = File.Exists(request.TerminalCommand);

        ProcessStartInfo psi;
        if (isDirectExe)
        {
            psi = new ProcessStartInfo(request.TerminalCommand)
            {
                WorkingDirectory = request.Cwd,
                UseShellExecute = false,
            };
            foreach (var a in request.ClaudeArgs) psi.ArgumentList.Add(a);
        }
        else if (IsWindowsTerminal(request.TerminalCommand))
        {
            psi = new ProcessStartInfo("wt.exe")
            {
                UseShellExecute = true,
            };
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(request.Cwd);
            psi.ArgumentList.Add("claude");
            foreach (var a in request.ClaudeArgs) psi.ArgumentList.Add(a);
        }
        else
        {
            // Fallback: PowerShell -NoExit with a cd + claude command.
            var claudeLine = "claude " + string.Join(' ', request.ClaudeArgs.Select(Quote));
            psi = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = true,
            };
            psi.ArgumentList.Add("-NoExit");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add($"Set-Location -LiteralPath '{request.Cwd.Replace("'", "''")}'; {claudeLine}");
        }

        var resolved = $"{psi.FileName} {string.Join(' ', psi.ArgumentList.Select(Quote))}";
        try
        {
            var p = Process.Start(psi);
            return new LaunchResult(Success: true, Pid: p?.Id, Error: null, ResolvedCommandLine: resolved);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, null, ex.Message, resolved);
        }
    }

    private static bool IsWindowsTerminal(string cmd) =>
        cmd.Equals("wt", StringComparison.OrdinalIgnoreCase) ||
        cmd.Equals("wt.exe", StringComparison.OrdinalIgnoreCase);

    private static string Quote(string s) =>
        s.Contains(' ') ? $"\"{s}\"" : s;
}
```

- [ ] **Step 4: Implement `MacLauncher`.**

```csharp
// src/CcLauncher.Core/Launch/MacLauncher.cs
using System.Diagnostics;

namespace CcLauncher.Core.Launch;

public sealed class MacLauncher : ILauncher
{
    public LaunchResult Launch(LaunchRequest request)
    {
        // Direct-invoke path (test stub lands here).
        if (File.Exists(request.TerminalCommand))
        {
            var psi = new ProcessStartInfo(request.TerminalCommand)
            {
                WorkingDirectory = request.Cwd,
                UseShellExecute = false,
            };
            foreach (var a in request.ClaudeArgs) psi.ArgumentList.Add(a);
            try
            {
                var p = Process.Start(psi);
                return new LaunchResult(true, p?.Id, null, request.TerminalCommand);
            }
            catch (Exception ex)
            {
                return new LaunchResult(false, null, ex.Message, request.TerminalCommand);
            }
        }

        // Real path: osascript tell Terminal.app
        var claudeLine = "claude " + string.Join(' ', request.ClaudeArgs.Select(EscapeShell));
        var script = $"tell application \"Terminal\" to do script \"cd {EscapeShell(request.Cwd)}; {claudeLine}\"";
        var osa = new ProcessStartInfo("osascript")
        {
            UseShellExecute = false,
        };
        osa.ArgumentList.Add("-e");
        osa.ArgumentList.Add(script);

        var resolved = $"osascript -e \"{script}\"";
        try
        {
            var p = Process.Start(osa);
            return new LaunchResult(true, p?.Id, null, resolved);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, null, ex.Message, resolved);
        }
    }

    private static string EscapeShell(string s) =>
        "'" + s.Replace("'", "'\\''") + "'";
}
```

- [ ] **Step 5: Implement `LinuxLauncher`.**

```csharp
// src/CcLauncher.Core/Launch/LinuxLauncher.cs
using System.Diagnostics;

namespace CcLauncher.Core.Launch;

public sealed class LinuxLauncher : ILauncher
{
    public LaunchResult Launch(LaunchRequest request)
    {
        if (File.Exists(request.TerminalCommand))
        {
            var psi = new ProcessStartInfo(request.TerminalCommand)
            {
                WorkingDirectory = request.Cwd,
                UseShellExecute = false,
            };
            foreach (var a in request.ClaudeArgs) psi.ArgumentList.Add(a);
            try
            {
                var p = Process.Start(psi);
                return new LaunchResult(true, p?.Id, null, request.TerminalCommand);
            }
            catch (Exception ex)
            {
                return new LaunchResult(false, null, ex.Message, request.TerminalCommand);
            }
        }

        // Real path: x-terminal-emulator -e bash -c "cd <cwd>; claude <args>; exec bash"
        var claudeLine = "claude " + string.Join(' ', request.ClaudeArgs.Select(EscapeShell));
        var inner = $"cd {EscapeShell(request.Cwd)}; {claudeLine}; exec bash";
        var psi2 = new ProcessStartInfo(request.TerminalCommand)
        {
            UseShellExecute = false,
        };
        psi2.ArgumentList.Add("-e");
        psi2.ArgumentList.Add("bash");
        psi2.ArgumentList.Add("-c");
        psi2.ArgumentList.Add(inner);

        var resolved = $"{request.TerminalCommand} -e bash -c \"{inner}\"";
        try
        {
            var p = Process.Start(psi2);
            return new LaunchResult(true, p?.Id, null, resolved);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, null, ex.Message, resolved);
        }
    }

    private static string EscapeShell(string s) =>
        "'" + s.Replace("'", "'\\''") + "'";
}
```

- [ ] **Step 6: Implement `LauncherFactory`.**

```csharp
// src/CcLauncher.Core/Launch/LauncherFactory.cs
using System.Runtime.InteropServices;

namespace CcLauncher.Core.Launch;

public static class LauncherFactory
{
    public static ILauncher ForCurrentOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsLauncher();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return new MacLauncher();
        return new LinuxLauncher();
    }

    // Same mapping, but kept explicit so tests can be clear about intent.
    public static ILauncher ForTesting(string stubPath) => ForCurrentOs();

    public static string DefaultTerminalCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "wt.exe";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return "Terminal";
        return "x-terminal-emulator";
    }
}
```

- [ ] **Step 7: Run — expect pass.**

```powershell
dotnet test --filter LauncherIntegrationTests
```

- [ ] **Step 8: Commit.**

```powershell
git add -A
git commit -m "feat(core): add per-platform launchers with stub-terminal integration test"
```

### Task 3.4: `PlatformPaths` — per-OS config directory

**Files:**
- Create: `src/CcLauncher.Core/Paths/PlatformPaths.cs`
- Create: `tests/CcLauncher.Core.Tests/Paths/PlatformPathsTests.cs`

- [ ] **Step 1: Write failing tests.**

```csharp
// tests/CcLauncher.Core.Tests/Paths/PlatformPathsTests.cs
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
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter PlatformPathsTests
```

- [ ] **Step 3: Implement.**

```csharp
// src/CcLauncher.Core/Paths/PlatformPaths.cs
using System.Runtime.InteropServices;

namespace CcLauncher.Core.Paths;

public static class PlatformPaths
{
    private const string AppName = "cc-launcher";

    public static string ConfigDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppName);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", AppName);
        }
        // Linux
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdg)) return Path.Combine(xdg, AppName);
        var homeL = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeL, ".config", AppName);
    }

    public static string DatabaseFile() => Path.Combine(ConfigDir(), "app.db");
    public static string LogsDir()      => Path.Combine(ConfigDir(), "logs");

    public static string ClaudeProjectsDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude", "projects");
    }
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter PlatformPathsTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(core): add PlatformPaths for per-OS config and projects dirs"
```

---

## Phase 4: App scaffold + tray

### Task 4.1: Avalonia packages + Program.cs

**Files:**
- Modify: `src/CcLauncher.App/CcLauncher.App.csproj`
- Create: `src/CcLauncher.App/Program.cs` (replaces default)
- Create: `src/CcLauncher.App/App.axaml`
- Create: `src/CcLauncher.App/App.axaml.cs`

- [ ] **Step 1: Add Avalonia packages.**

```powershell
dotnet add src/CcLauncher.App/CcLauncher.App.csproj package Avalonia
dotnet add src/CcLauncher.App/CcLauncher.App.csproj package Avalonia.Desktop
dotnet add src/CcLauncher.App/CcLauncher.App.csproj package Avalonia.Themes.Fluent
dotnet add src/CcLauncher.App/CcLauncher.App.csproj package Avalonia.Fonts.Inter
dotnet add src/CcLauncher.App/CcLauncher.App.csproj package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Edit `.csproj` to set OutputType = WinExe (keeps console hidden on Windows) and add nullable.**

Open `src/CcLauncher.App/CcLauncher.App.csproj` and ensure it contains:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <AvaloniaResource Include="Assets/**" />
  </ItemGroup>
</Project>
```

(Note: `<ItemGroup>` for package refs will already exist from Step 1 — keep them.)

- [ ] **Step 3: Create `Assets/` dir + placeholder icon.**

```powershell
New-Item -ItemType Directory -Path src/CcLauncher.App/Assets -Force
```

Drop a 256×256 PNG named `tray.png` in `src/CcLauncher.App/Assets/`. For the MVP, a plain-color placeholder is fine; design later. Also add an `.ico` version named `tray.ico` for Windows tray compatibility (Avalonia requires `.ico` on Windows per docs).

- [ ] **Step 4: Create `Program.cs`.**

```csharp
// src/CcLauncher.App/Program.cs
using Avalonia;

namespace CcLauncher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 5: Create `App.axaml`.**

```xml
<!-- src/CcLauncher.App/App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="CcLauncher.App.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
  <TrayIcon.Icons>
    <TrayIcons>
      <TrayIcon Icon="/Assets/tray.ico"
                ToolTipText="cc-launcher"
                x:Name="AppTrayIcon">
        <TrayIcon.Menu>
          <NativeMenu>
            <NativeMenuItem Header="Open" Click="OnOpenDashboard" />
            <NativeMenuItem Header="Quit" Click="OnQuit" />
          </NativeMenu>
        </TrayIcon.Menu>
      </TrayIcon>
    </TrayIcons>
  </TrayIcon.Icons>
</Application>
```

- [ ] **Step 6: Create `App.axaml.cs` (code-behind).**

```csharp
// src/CcLauncher.App/App.axaml.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CcLauncher.App.Views;

namespace CcLauncher.App;

public partial class App : Application
{
    private Dashboard? _dashboard;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Construct (hidden) but don't show — window shown on tray click.
            _dashboard = new Dashboard();
            desktop.MainWindow = _dashboard;
            _dashboard.Hide();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        if (_dashboard is null) return;
        _dashboard.Show();
        _dashboard.Activate();
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
```

- [ ] **Step 7: Create empty `Dashboard` so the app builds.**

```xml
<!-- src/CcLauncher.App/Views/Dashboard.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CcLauncher.App.Views.Dashboard"
        Width="480" Height="640"
        ShowInTaskbar="False"
        WindowStartupLocation="Manual"
        Title="cc-launcher">
  <TextBlock Text="Dashboard placeholder" Margin="16" />
</Window>
```

```csharp
// src/CcLauncher.App/Views/Dashboard.axaml.cs
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CcLauncher.App.Views;

public partial class Dashboard : Window
{
    public Dashboard() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 8: Add `app.manifest` (Windows manifest — standard Avalonia boilerplate).**

```xml
<!-- src/CcLauncher.App/app.manifest -->
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="CcLauncher.App" />
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 9: Build + run.**

```powershell
dotnet build
dotnet run --project src/CcLauncher.App
```

Expected: app launches, tray icon appears, right-click → Open/Quit menu. Open shows the placeholder window. Quit exits cleanly.

- [ ] **Step 10: Commit.**

```powershell
git add -A
git commit -m "feat(app): bootstrap Avalonia tray app with placeholder dashboard"
```

---

## Phase 5: View-models

### Task 5.1: `ProjectRowViewModel` + `SessionRowViewModel`

**Files:**
- Create: `src/CcLauncher.App/ViewModels/SessionRowViewModel.cs`
- Create: `src/CcLauncher.App/ViewModels/ProjectRowViewModel.cs`
- Create: `tests/CcLauncher.Core.Tests/../` **(note: VM tests go in a new `CcLauncher.App.Tests` project)**
- Create: `tests/CcLauncher.App.Tests/CcLauncher.App.Tests.csproj`
- Create: `tests/CcLauncher.App.Tests/ViewModels/ProjectRowViewModelTests.cs`

- [ ] **Step 1: Create the App.Tests project.**

```powershell
dotnet new xunit -n CcLauncher.App.Tests -o tests/CcLauncher.App.Tests -f net8.0
dotnet sln add tests/CcLauncher.App.Tests/CcLauncher.App.Tests.csproj
dotnet add tests/CcLauncher.App.Tests/CcLauncher.App.Tests.csproj reference src/CcLauncher.App/CcLauncher.App.csproj src/CcLauncher.Core/CcLauncher.Core.csproj
dotnet add tests/CcLauncher.App.Tests/CcLauncher.App.Tests.csproj package FluentAssertions
```

- [ ] **Step 2: Write failing tests.**

```csharp
// tests/CcLauncher.App.Tests/ViewModels/ProjectRowViewModelTests.cs
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
```

- [ ] **Step 3: Implement `SessionRowViewModel`.**

```csharp
// src/CcLauncher.App/ViewModels/SessionRowViewModel.cs
using CcLauncher.Core.Discovery;

namespace CcLauncher.App.ViewModels;

public sealed class SessionRowViewModel
{
    public SessionRowViewModel(DiscoveredSession session) => Session = session;
    public DiscoveredSession Session { get; }

    public string Id           => Session.Id;
    public DateTime StartedAt  => Session.StartedAt;
    public DateTime LastActivity => Session.LastActivity;
    public int MessageCount    => Session.MessageCount;
    public string DisplayText  => Session.FirstUserMsg ?? "(no preview)";
    public string RelativeWhen => RelativeTime.Format(Session.LastActivity);
}

internal static class RelativeTime
{
    public static string Format(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalMinutes < 1)  return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours   < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays    < 30) return $"{(int)delta.TotalDays}d ago";
        return utc.ToLocalTime().ToString("yyyy-MM-dd");
    }
}
```

- [ ] **Step 4: Implement `ProjectRowViewModel`.**

```csharp
// src/CcLauncher.App/ViewModels/ProjectRowViewModel.cs
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;

namespace CcLauncher.App.ViewModels;

public sealed class ProjectRowViewModel
{
    public ProjectRowViewModel(DiscoveredProject project, ProjectSettings settings)
    {
        Project = project;
        Settings = settings;
        Sessions = project.Sessions
            .OrderByDescending(s => s.LastActivity)
            .Select(s => new SessionRowViewModel(s))
            .ToList();
    }

    public DiscoveredProject Project { get; }
    public ProjectSettings Settings { get; }
    public IReadOnlyList<SessionRowViewModel> Sessions { get; }

    public string Id => Project.Id;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Settings.DisplayName)
            ? Settings.DisplayName!
            : DeriveBasename(Project.Cwd);

    public string? LatestSessionId =>
        Project.Sessions.Count == 0
            ? null
            : Project.Sessions.OrderByDescending(s => s.LastActivity).First().Id;

    public DateTime LastActivity => Project.LastActivity;
    public string RelativeWhen   => RelativeTime.Format(LastActivity);
    public bool Pinned           => Settings.Pinned;

    private static string DeriveBasename(string cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return "(unknown)";
        var trimmed = cwd.TrimEnd('/', '\\');
        var sep = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        return sep < 0 ? trimmed : trimmed[(sep + 1)..];
    }
}
```

- [ ] **Step 5: Run — expect pass.**

```powershell
dotnet test --filter ProjectRowViewModelTests
```

- [ ] **Step 6: Commit.**

```powershell
git add -A
git commit -m "feat(app): add ProjectRowViewModel and SessionRowViewModel with unit tests"
```

### Task 5.2: `DashboardViewModel` — merge + sort + launch command

**Files:**
- Create: `src/CcLauncher.App/ViewModels/DashboardViewModel.cs`
- Create: `tests/CcLauncher.App.Tests/ViewModels/DashboardViewModelTests.cs`

- [ ] **Step 1: Write failing tests.**

```csharp
// tests/CcLauncher.App.Tests/ViewModels/DashboardViewModelTests.cs
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

        launcher.Captured.Should().NotBeNull();
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
}
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter DashboardViewModelTests
```

- [ ] **Step 3: Implement.**

Semantics:
- `LaunchProject(row, sessionId: null)` — resume the latest session in the project. If the project has no sessions, fall through to a fresh `claude` invocation.
- `LaunchProject(row, sessionId: "xyz")` — resume that specific session.
- `LaunchNewSession(row)` — always fresh, no `--resume`.

```csharp
// src/CcLauncher.App/ViewModels/DashboardViewModel.cs
using System.Collections.ObjectModel;
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;
using CcLauncher.Core.Launch;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcLauncher.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IProjectDiscoveryService _discovery;
    private readonly IConfigStore _config;
    private readonly ILauncher _launcher;

    public DashboardViewModel(IProjectDiscoveryService discovery, IConfigStore config, ILauncher launcher)
    {
        _discovery = discovery;
        _config = config;
        _launcher = launcher;
    }

    public ObservableCollection<ProjectRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string? _lastError;

    public void Refresh()
    {
        Rows.Clear();
        var settings = _config.GetAllProjectSettings();

        var projects = _discovery.Scan()
            .Where(p => !(settings.TryGetValue(p.Id, out var s) && s.Hidden))
            .Select(p => new ProjectRowViewModel(
                p,
                settings.TryGetValue(p.Id, out var s) ? s : ProjectSettings.Default(p.Id)))
            .OrderByDescending(r => r.Pinned)
            .ThenByDescending(r => r.LastActivity);

        foreach (var r in projects) Rows.Add(r);
    }

    public LaunchResult LaunchProject(ProjectRowViewModel row, string? sessionId)
    {
        var sid = sessionId ?? row.LatestSessionId;
        if (sid is null) return LaunchNewSession(row);

        var global = _config.GetGlobalSettings();
        var args = ArgBuilder.Build(global, row.Settings, sid);
        var req = new LaunchRequest(row.Project.Cwd, global.TerminalCommand, args);
        var result = _launcher.Launch(req);
        if (result.Success)
        {
            _config.InsertLaunch(row.Id, sid, result.Pid);
            _config.SaveProjectSettings(row.Settings with { LastLaunchedAt = DateTime.UtcNow });
        }
        else
        {
            LastError = $"Launch failed: {result.Error}";
        }
        return result;
    }

    public LaunchResult LaunchNewSession(ProjectRowViewModel row)
    {
        var global = _config.GetGlobalSettings();
        var args = ArgBuilder.Build(global, row.Settings, sessionId: null);
        var req = new LaunchRequest(row.Project.Cwd, global.TerminalCommand, args);
        var result = _launcher.Launch(req);
        if (result.Success)
            _config.SaveProjectSettings(row.Settings with { LastLaunchedAt = DateTime.UtcNow });
        else
            LastError = $"Launch failed: {result.Error}";
        return result;
    }
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter DashboardViewModelTests
```

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(app): add DashboardViewModel with refresh + launch logic"
```

---

## Phase 6: Views

### Task 6.1: Wire `DashboardViewModel` to `Dashboard.axaml`

**Files:**
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml`
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml.cs`
- Modify: `src/CcLauncher.App/App.axaml.cs`
- Create: `src/CcLauncher.App/Services/AppServices.cs` (simple service locator for MVP)

- [ ] **Step 1: Create a tiny service locator so the view can resolve its VM with correct dependencies.**

For MVP, skip a full DI container. One static class owns singletons.

```csharp
// src/CcLauncher.App/Services/AppServices.cs
using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;
using CcLauncher.Core.Launch;
using CcLauncher.Core.Paths;

namespace CcLauncher.App.Services;

public static class AppServices
{
    private static readonly object _lock = new();
    private static IConfigStore? _config;
    private static IProjectDiscoveryService? _discovery;
    private static ILauncher? _launcher;

    public static IConfigStore Config
    {
        get
        {
            lock (_lock)
            {
                _config ??= ConfigStore.OpenFile(
                    PlatformPaths.DatabaseFile(),
                    LauncherFactory.DefaultTerminalCommand());
                return _config;
            }
        }
    }

    public static IProjectDiscoveryService Discovery
    {
        get
        {
            lock (_lock)
            {
                _discovery ??= new ProjectDiscoveryService(PlatformPaths.ClaudeProjectsDir());
                return _discovery;
            }
        }
    }

    public static ILauncher Launcher
    {
        get
        {
            lock (_lock)
            {
                _launcher ??= LauncherFactory.ForCurrentOs();
                return _launcher;
            }
        }
    }
}
```

- [ ] **Step 2: Update `Dashboard.axaml` with a real list.**

```xml
<!-- src/CcLauncher.App/Views/Dashboard.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:CcLauncher.App.ViewModels"
        x:Class="CcLauncher.App.Views.Dashboard"
        x:DataType="vm:DashboardViewModel"
        Width="480" Height="640"
        ShowInTaskbar="False"
        WindowStartupLocation="Manual"
        Title="cc-launcher">
  <Grid RowDefinitions="Auto,*">
    <TextBox Grid.Row="0" Margin="12,12,12,6" Watermark="Filter projects..." x:Name="FilterBox" />
    <ScrollViewer Grid.Row="1" Margin="12,0,12,12">
      <ItemsControl ItemsSource="{Binding Rows}">
        <ItemsControl.ItemTemplate>
          <DataTemplate DataType="vm:ProjectRowViewModel">
            <Border Padding="8" Margin="0,0,0,4" CornerRadius="6" BorderThickness="1" BorderBrush="#22000000"
                    PointerPressed="ProjectRow_PointerPressed"
                    Tag="{Binding}">
              <Grid ColumnDefinitions="*,Auto">
                <StackPanel Grid.Column="0">
                  <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold" />
                  <TextBlock Text="{Binding RelativeWhen}" Opacity="0.6" FontSize="11" />
                </StackPanel>
                <Button Grid.Column="1" Content="New" Tag="{Binding}" Click="NewSession_Click" />
              </Grid>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </Grid>
</Window>
```

(Note: session expansion is deferred to a later task; this keeps the first wired version minimal.)

- [ ] **Step 3: Code-behind: bind VM + handle clicks.**

```csharp
// src/CcLauncher.App/Views/Dashboard.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CcLauncher.App.Services;
using CcLauncher.App.ViewModels;

namespace CcLauncher.App.Views;

public partial class Dashboard : Window
{
    private readonly DashboardViewModel _vm;

    public Dashboard()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new DashboardViewModel(AppServices.Discovery, AppServices.Config, AppServices.Launcher);
        DataContext = _vm;
        Opened += (_, _) => _vm.Refresh();
    }

    private void ProjectRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.Tag is ProjectRowViewModel row)
        {
            _vm.LaunchProject(row, sessionId: null);
            if (AppServices.Config.GetGlobalSettings().CloseOnLaunch) Hide();
        }
    }

    private void NewSession_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is ProjectRowViewModel row)
        {
            _vm.LaunchNewSession(row);
            if (AppServices.Config.GetGlobalSettings().CloseOnLaunch) Hide();
            e.Handled = true;
        }
    }
}
```

- [ ] **Step 4: Run.**

```powershell
dotnet run --project src/CcLauncher.App
```

Expected: click tray → window opens → list of projects (or empty if `~/.claude/projects/` empty). Clicking a project launches a terminal with `claude --resume <id>`. "New" button launches plain `claude`.

- [ ] **Step 5: Commit.**

```powershell
git add -A
git commit -m "feat(app): wire dashboard view to view-model with launch actions"
```

### Task 6.2: Expandable session rows

**Files:**
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml`
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml.cs`

- [ ] **Step 1: Update item template to include expandable sessions.**

Replace the `DataTemplate` in `Dashboard.axaml` with:

```xml
<DataTemplate DataType="vm:ProjectRowViewModel">
  <Expander Margin="0,0,0,4" Padding="8">
    <Expander.Header>
      <Grid ColumnDefinitions="*,Auto">
        <StackPanel Grid.Column="0" PointerPressed="ProjectRow_PointerPressed" Tag="{Binding}" Cursor="Hand">
          <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold" />
          <TextBlock Text="{Binding RelativeWhen}" Opacity="0.6" FontSize="11" />
        </StackPanel>
        <Button Grid.Column="1" Content="New" Tag="{Binding}" Click="NewSession_Click" />
      </Grid>
    </Expander.Header>
    <ItemsControl ItemsSource="{Binding Sessions}" Margin="12,4,0,0">
      <ItemsControl.ItemTemplate>
        <DataTemplate DataType="vm:SessionRowViewModel">
          <Border Padding="6" Margin="0,0,0,2" CornerRadius="4" Background="#0A000000"
                  PointerPressed="SessionRow_PointerPressed" Tag="{Binding}" Cursor="Hand">
            <StackPanel>
              <TextBlock Text="{Binding DisplayText}" FontSize="12" />
              <TextBlock Text="{Binding RelativeWhen}" Opacity="0.6" FontSize="10" />
            </StackPanel>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </Expander>
</DataTemplate>
```

- [ ] **Step 2: Handler for session click.**

Add to `Dashboard.axaml.cs`:

```csharp
private void SessionRow_PointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (sender is Control c && c.Tag is SessionRowViewModel srow)
    {
        // Find the parent project row (search the VM's Rows by session id).
        var parent = _vm.Rows.FirstOrDefault(r => r.Sessions.Any(s => s.Id == srow.Id));
        if (parent is null) return;
        _vm.LaunchProject(parent, sessionId: srow.Id);
        if (AppServices.Config.GetGlobalSettings().CloseOnLaunch) Hide();
        e.Handled = true;
    }
}
```

Also add `using System.Linq;` if not already present.

- [ ] **Step 3: Run + manually verify expansion + per-session click works.**

- [ ] **Step 4: Commit.**

```powershell
git add -A
git commit -m "feat(app): expandable session list with per-session resume"
```

### Task 6.3: Curation — right-click menu for pin / hide / rename

**Files:**
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml`
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml.cs`
- Modify: `src/CcLauncher.App/ViewModels/DashboardViewModel.cs`
- Modify: `tests/CcLauncher.App.Tests/ViewModels/DashboardViewModelTests.cs`

- [ ] **Step 1: Add failing tests for curation actions on the VM.**

Append to `DashboardViewModelTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run — expect failure.**

```powershell
dotnet test --filter DashboardViewModelTests
```

- [ ] **Step 3: Add methods to `DashboardViewModel`.**

```csharp
public void TogglePinned(ProjectRowViewModel row)
{
    var updated = row.Settings with { Pinned = !row.Settings.Pinned };
    _config.SaveProjectSettings(updated);
    Refresh();
}

public void Hide(ProjectRowViewModel row)
{
    _config.SaveProjectSettings(row.Settings with { Hidden = true });
    Refresh();
}

public void Rename(ProjectRowViewModel row, string newName)
{
    _config.SaveProjectSettings(row.Settings with { DisplayName = string.IsNullOrWhiteSpace(newName) ? null : newName });
    Refresh();
}
```

- [ ] **Step 4: Run — expect pass.**

```powershell
dotnet test --filter DashboardViewModelTests
```

- [ ] **Step 5: Wire to UI context menu.**

In the project `DataTemplate` inside `Dashboard.axaml`, add a `ContextMenu` to the `Expander.Header` Grid:

```xml
<Grid.ContextMenu>
  <ContextMenu>
    <MenuItem Header="Pin / unpin" Tag="{Binding}" Click="TogglePin_Click" />
    <MenuItem Header="Hide"        Tag="{Binding}" Click="Hide_Click" />
    <MenuItem Header="Rename..."   Tag="{Binding}" Click="Rename_Click" />
  </ContextMenu>
</Grid.ContextMenu>
```

In `Dashboard.axaml.cs` add handlers:

```csharp
private void TogglePin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    if (sender is MenuItem m && m.Tag is ProjectRowViewModel row) _vm.TogglePinned(row);
}

private void Hide_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    if (sender is MenuItem m && m.Tag is ProjectRowViewModel row) _vm.Hide(row);
}

private async void Rename_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    if (sender is not MenuItem m || m.Tag is not ProjectRowViewModel row) return;
    var dialog = new Window
    {
        Width = 320, Height = 120, Title = "Rename",
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };
    var tb = new TextBox { Text = row.DisplayName, Margin = new Avalonia.Thickness(12) };
    var ok = new Button { Content = "OK", Margin = new Avalonia.Thickness(12), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
    ok.Click += (_, _) => dialog.Close(tb.Text);
    dialog.Content = new StackPanel { Children = { tb, ok } };
    var result = await dialog.ShowDialog<string?>(this);
    if (result is not null) _vm.Rename(row, result);
}
```

- [ ] **Step 6: Run + manually verify.**

- [ ] **Step 7: Commit.**

```powershell
git add -A
git commit -m "feat(app): add pin/hide/rename context actions"
```

---

## Phase 7: Watcher, settings, startup

### Task 7.1: File watcher — live dashboard refresh

**Files:**
- Create: `src/CcLauncher.App/Services/ProjectsFileWatcher.cs`
- Modify: `src/CcLauncher.App/Views/Dashboard.axaml.cs`

- [ ] **Step 1: Implement watcher.**

```csharp
// src/CcLauncher.App/Services/ProjectsFileWatcher.cs
namespace CcLauncher.App.Services;

public sealed class ProjectsFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action _onChange;
    private readonly System.Timers.Timer _debounce;

    public ProjectsFileWatcher(string projectsRoot, Action onChange)
    {
        _onChange = onChange;
        _debounce = new System.Timers.Timer(400) { AutoReset = false };
        _debounce.Elapsed += (_, _) => _onChange();

        if (!Directory.Exists(projectsRoot))
            Directory.CreateDirectory(projectsRoot);

        _watcher = new FileSystemWatcher(projectsRoot)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
        };
        _watcher.Created += (_, _) => Bump();
        _watcher.Changed += (_, _) => Bump();
        _watcher.Deleted += (_, _) => Bump();
        _watcher.Renamed += (_, _) => Bump();
    }

    private void Bump()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
```

- [ ] **Step 2: Hook up in `Dashboard.axaml.cs`.**

```csharp
// Add fields:
private ProjectsFileWatcher? _watcher;

// In constructor, after DataContext = _vm:
Opened += (_, _) =>
{
    _vm.Refresh();
    _watcher ??= new ProjectsFileWatcher(
        CcLauncher.Core.Paths.PlatformPaths.ClaudeProjectsDir(),
        () => Avalonia.Threading.Dispatcher.UIThread.Post(_vm.Refresh));
};
Closed += (_, _) => { _watcher?.Dispose(); _watcher = null; };
```

- [ ] **Step 3: Manual verify: open dashboard, start `claude` in a new dir → dashboard row appears without reopening.**

- [ ] **Step 4: Commit.**

```powershell
git add -A
git commit -m "feat(app): auto-refresh dashboard on projects directory changes"
```

### Task 7.2: Settings window

**Files:**
- Create: `src/CcLauncher.App/ViewModels/SettingsViewModel.cs`
- Create: `src/CcLauncher.App/Views/Settings.axaml`
- Create: `src/CcLauncher.App/Views/Settings.axaml.cs`
- Modify: `src/CcLauncher.App/App.axaml` (add Settings menu item)
- Modify: `src/CcLauncher.App/App.axaml.cs` (handler)

- [ ] **Step 1: `SettingsViewModel`.**

```csharp
// src/CcLauncher.App/ViewModels/SettingsViewModel.cs
using CcLauncher.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcLauncher.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigStore _config;

    public SettingsViewModel(IConfigStore config)
    {
        _config = config;
        var g = config.GetGlobalSettings();
        _terminalCommand = g.TerminalCommand;
        _globalDefaultArgs = g.GlobalDefaultArgs ?? "";
        _globalSystemPrompt = g.GlobalSystemPrompt ?? "";
        _launchOnStartup = g.LaunchOnStartup;
        _closeOnLaunch = g.CloseOnLaunch;
    }

    [ObservableProperty] private string _terminalCommand = "";
    [ObservableProperty] private string _globalDefaultArgs = "";
    [ObservableProperty] private string _globalSystemPrompt = "";
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _closeOnLaunch;

    public void Save()
    {
        _config.SaveGlobalSettings(new GlobalSettings(
            TerminalCommand:    string.IsNullOrWhiteSpace(TerminalCommand) ? "wt.exe" : TerminalCommand,
            GlobalDefaultArgs:  string.IsNullOrWhiteSpace(GlobalDefaultArgs) ? null : GlobalDefaultArgs,
            GlobalSystemPrompt: string.IsNullOrWhiteSpace(GlobalSystemPrompt) ? null : GlobalSystemPrompt,
            LaunchOnStartup:    LaunchOnStartup,
            ResumeAllOnOpen:    false,
            CloseOnLaunch:      CloseOnLaunch));
    }
}
```

- [ ] **Step 2: `Settings.axaml`.**

```xml
<!-- src/CcLauncher.App/Views/Settings.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:CcLauncher.App.ViewModels"
        x:Class="CcLauncher.App.Views.Settings"
        x:DataType="vm:SettingsViewModel"
        Width="520" Height="420" Title="cc-launcher settings">
  <StackPanel Margin="16" Spacing="10">
    <TextBlock Text="Terminal command" FontWeight="SemiBold" />
    <TextBox Text="{Binding TerminalCommand}" />

    <TextBlock Text="Global default CLI args" FontWeight="SemiBold" Margin="0,8,0,0" />
    <TextBox Text="{Binding GlobalDefaultArgs}" Watermark="e.g. --model opus" />

    <TextBlock Text="Global system prompt (appended)" FontWeight="SemiBold" Margin="0,8,0,0" />
    <TextBox Text="{Binding GlobalSystemPrompt}" AcceptsReturn="True" Height="80" />

    <CheckBox Content="Launch on OS startup" IsChecked="{Binding LaunchOnStartup}" />
    <CheckBox Content="Close dashboard after launching" IsChecked="{Binding CloseOnLaunch}" />

    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8" Margin="0,16,0,0">
      <Button Content="Cancel" Click="Cancel_Click" />
      <Button Content="Save"   Click="Save_Click" Classes="accent" />
    </StackPanel>
  </StackPanel>
</Window>
```

- [ ] **Step 3: `Settings.axaml.cs`.**

```csharp
// src/CcLauncher.App/Views/Settings.axaml.cs
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CcLauncher.App.Services;
using CcLauncher.App.ViewModels;

namespace CcLauncher.App.Views;

public partial class Settings : Window
{
    private readonly SettingsViewModel _vm;

    public Settings()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new SettingsViewModel(AppServices.Config);
        DataContext = _vm;
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.Save();
        StartupIntegration.Apply(_vm.LaunchOnStartup); // defined in Task 7.3
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
```

- [ ] **Step 4: Add Settings item to the tray menu.**

In `App.axaml`, inside the `NativeMenu`:

```xml
<NativeMenuItem Header="Settings..." Click="OnOpenSettings" />
<NativeMenuItemSeparator />
```

In `App.axaml.cs`:

```csharp
private Views.Settings? _settingsWindow;

private void OnOpenSettings(object? sender, EventArgs e)
{
    if (_settingsWindow is null || !_settingsWindow.IsVisible)
    {
        _settingsWindow = new Views.Settings();
        _settingsWindow.Show();
    }
    else _settingsWindow.Activate();
}
```

- [ ] **Step 5: Run + manually verify settings persist across app restarts.**

- [ ] **Step 6: Commit.**

```powershell
git add -A
git commit -m "feat(app): add settings window for global options"
```

### Task 7.3: Launch-on-startup (OS integration)

**Files:**
- Create: `src/CcLauncher.App/Services/StartupIntegration.cs`

Platforms:
- Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- macOS: a LaunchAgent `.plist` at `~/Library/LaunchAgents/com.cclauncher.plist`.
- Linux: `.desktop` file at `~/.config/autostart/cc-launcher.desktop`.

- [ ] **Step 1: Implement.**

```csharp
// src/CcLauncher.App/Services/StartupIntegration.cs
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CcLauncher.App.Services;

public static class StartupIntegration
{
    private const string AppName = "cc-launcher";

    public static void Apply(bool enable)
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exe)) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))      ApplyWindows(exe!, enable);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     ApplyMac(exe!, enable);
        else                                                          ApplyLinux(exe!, enable);
    }

    private static void ApplyWindows(string exe, bool enable)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null) return;
        if (enable) key.SetValue(AppName, $"\"{exe}\"");
        else        key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static void ApplyMac(string exe, bool enable)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var plistDir = Path.Combine(home, "Library", "LaunchAgents");
        var plistPath = Path.Combine(plistDir, "com.cclauncher.plist");
        if (!enable)
        {
            if (File.Exists(plistPath)) File.Delete(plistPath);
            return;
        }
        Directory.CreateDirectory(plistDir);
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
              <dict>
                <key>Label</key><string>com.cclauncher</string>
                <key>ProgramArguments</key><array><string>{exe}</string></array>
                <key>RunAtLoad</key><true/>
              </dict>
            </plist>
            """;
        File.WriteAllText(plistPath, xml);
    }

    private static void ApplyLinux(string exe, bool enable)
    {
        var cfg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var autostartDir = Path.Combine(string.IsNullOrEmpty(cfg) ? Path.Combine(home, ".config") : cfg, "autostart");
        var path = Path.Combine(autostartDir, "cc-launcher.desktop");
        if (!enable)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(autostartDir);
        var content = $"""
            [Desktop Entry]
            Type=Application
            Name=cc-launcher
            Exec="{exe}"
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """;
        File.WriteAllText(path, content);
    }
}
```

- [ ] **Step 2: Verify manually per platform where available.**

On Windows only: flip the setting, restart PC, confirm app launches.

- [ ] **Step 3: Commit.**

```powershell
git add -A
git commit -m "feat(app): add cross-platform launch-on-startup integration"
```

### Task 7.4: Logging

**Files:**
- Create: `src/CcLauncher.Core/Logging/FileLog.cs`
- Modify: key `catch` sites in `Launcher` and `DashboardViewModel` (and elsewhere as needed)

- [ ] **Step 1: Implement a tiny rolling file logger.**

```csharp
// src/CcLauncher.Core/Logging/FileLog.cs
namespace CcLauncher.Core.Logging;

public static class FileLog
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly object _lock = new();
    private static string? _path;

    public static void Initialize(string logsDir)
    {
        Directory.CreateDirectory(logsDir);
        _path = Path.Combine(logsDir, "app.log");
    }

    public static void Write(string level, string message, Exception? ex = null)
    {
        if (_path is null) return;
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                    File.Move(_path, _path + ".1", overwrite: true);
                var line = $"{DateTime.UtcNow:O} [{level}] {message}" +
                           (ex is null ? "" : $"\n{ex}") + "\n";
                File.AppendAllText(_path, line);
            }
            catch { /* swallow — logging failures must never crash the app */ }
        }
    }

    public static void Info(string m)                    => Write("INFO", m);
    public static void Warn(string m, Exception? e = null) => Write("WARN", m, e);
    public static void Error(string m, Exception? e = null) => Write("ERROR", m, e);
}
```

- [ ] **Step 2: Initialize in `Program.cs`.**

Modify `Program.Main`:

```csharp
public static void Main(string[] args)
{
    CcLauncher.Core.Logging.FileLog.Initialize(CcLauncher.Core.Paths.PlatformPaths.LogsDir());
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```

- [ ] **Step 3: Use in `DashboardViewModel.LaunchProject` failure path.**

Replace `LastError = ...` with:

```csharp
LastError = $"Launch failed: {result.Error}";
CcLauncher.Core.Logging.FileLog.Error($"Launch failed for {row.Id}: {result.Error} (cmd: {result.ResolvedCommandLine})");
```

- [ ] **Step 4: Commit.**

```powershell
git add -A
git commit -m "feat(core): add rolling file logger wired to launch failures"
```

---

## Phase 8: Packaging

### Task 8.1: Publish configs per platform

**Files:**
- Modify: `src/CcLauncher.App/CcLauncher.App.csproj`
- Create: `publish.ps1` (convenience script)

- [ ] **Step 1: Add publish properties.**

Add to the `PropertyGroup` in `CcLauncher.App.csproj`:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
```

- [ ] **Step 2: Convenience script.**

```powershell
# publish.ps1
param([string]$Rid = "win-x64")
dotnet publish src/CcLauncher.App/CcLauncher.App.csproj `
  -c Release `
  -r $Rid `
  -o publish/$Rid
```

- [ ] **Step 3: Verify.**

```powershell
./publish.ps1 -Rid win-x64
./publish/win-x64/CcLauncher.App.exe
```

Expected: standalone exe runs, tray icon appears, full functionality.

- [ ] **Step 4: Commit.**

```powershell
git add -A
git commit -m "chore: add single-file self-contained publish config + script"
```

---

## Self-review

**Spec coverage:** MVP items 1–9 in spec §10.1 → covered by Phase 0–8.
1. Tray icon + context menu → Task 4.1.
2. Dashboard auto-discovery → Task 1.3 + 6.1.
3. Expandable session rows → Task 6.2.
4. Resume latest / resume specific / new session → Task 5.2 + 6.1 + 6.2.
5. Pin/hide/rename → Task 6.3.
6. Settings (terminal command, global args, launch-on-startup) → Task 7.2 + 7.3.
7. Cross-platform launcher → Task 3.3.
8. SQLite with migrations → Task 2.2 + 2.3.
9. Rolling log file → Task 7.4.

**Placeholder scan:** no "TBD", no "implement later", no "similar to Task N". Every step has concrete code or commands.

**Type consistency:** `IProjectDiscoveryService.Scan()`, `ILauncher.Launch()`, `IConfigStore.SaveProjectSettings()` are named consistently across Core and App layers. `DashboardViewModel.LaunchProject / LaunchNewSession` are referenced consistently.

**Scope:** single cohesive MVP. Stretch items (default args UI, system prompts UI, resume-all, workspaces, active tracking, global hotkey) deliberately excluded — data model accommodates them without migration.

---

## Execution options

**1. Subagent-Driven (recommended)** — dispatch fresh subagents per task with review checkpoints. Phases 1/2/3 can run concurrently (three parallel agents) after Phase 0 completes.

**2. Inline Execution** — execute tasks one-by-one in this session with periodic checkpoints.
