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
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty("cwd", out var c) ? c.GetString() : null;
        }
        catch { return null; }
    }
}
