// src/CcLauncher.Core/Discovery/ProjectDiscoveryService.cs
using System.Text.Json;

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
            try { results.Add(ReadProject(projDir)); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
        }

        return results
            .OrderByDescending(p => p.LastActivity)
            .ToList();
    }

    private DiscoveredProject ReadProject(string projDir)
    {
        var id = Path.GetFileName(projDir);
        var sessions = new List<DiscoveredSession>();

        foreach (var jsonl in Directory.EnumerateFiles(projDir, "*.jsonl"))
        {
            // Be permissive: any of these can fire when claude is actively writing to
            // a jsonl while we scan. We'd rather skip a single transient session than
            // crash the dashboard the moment a terminal window closes.
            try { sessions.Add(_reader.Read(jsonl)); }
            catch (IOException)                { /* sharing violation, partial write, missing */ }
            catch (UnauthorizedAccessException) { /* file briefly inaccessible during flush */ }
            catch (JsonException)              { /* corrupt or mid-write json */ }
            catch (NotSupportedException)      { /* malformed path */ }
        }

        var cwd = sessions.Count > 0
            ? InferCwd(sessions[0].FilePath) ?? DecodeId(id)
            : DecodeId(id);

        return new DiscoveredProject(id, cwd, sessions);
    }

    // Best-effort decode: Claude Code replaces path separators + colon with '-'.
    // We can't perfectly recover the original path, so this is a display fallback.
    private static string DecodeId(string id) => id;

    // Modern Claude Code jsonl files open with metadata records (permission-mode,
    // attachment/hook output, SessionStart reminders) that don't carry a cwd field.
    // Scan forward until we find a record that does, but cap the scan so we don't
    // read megabytes for a malformed file.
    private const int CwdScanLineLimit = 200;

    private static string? InferCwd(string firstJsonlPath)
    {
        try
        {
            var scanned = 0;
            foreach (var line in File.ReadLines(firstJsonlPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (++scanned > CwdScanLineLimit) break;

                string? cwd;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    cwd = doc.RootElement.TryGetProperty("cwd", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString()
                        : null;
                }
                catch (JsonException) { continue; }

                if (IsSafeCwd(cwd)) return cwd;
            }
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    // Reject cwd strings that could break out of shell quoting or smuggle path
    // sequences into downstream launchers. The jsonl can be attacker-influenced
    // (anyone who can drop a file in ~/.claude/projects/* can poison it), so any
    // cwd we return flows directly into a child process's WorkingDirectory and
    // the PowerShell fallback's Set-Location -LiteralPath argument.
    private static bool IsSafeCwd(string? cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return false;
        foreach (var ch in cwd)
            if (ch == '\r' || ch == '\n' || ch == '\0') return false;
        return true;
    }
}
