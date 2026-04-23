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
        string? slug = null;
        string? lastPrompt = null;
        var count = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            firstLine ??= line;
            lastLine = line;
            count++;

            // Side-channel parses — best-effort, never fatal to the main scan.
            TryExtractSideChannels(line, ref slug, ref lastPrompt);
        }

        var (startedAt, firstUserMsg) = ParseFirst(firstLine, fi);
        var lastActivity = ParseTimestamp(lastLine) ?? startedAt;

        return new DiscoveredSession(
            Id: id,
            FilePath: filePath,
            StartedAt: startedAt,
            LastActivity: lastActivity,
            MessageCount: count,
            FirstUserMsg: firstUserMsg,
            LastPrompt: lastPrompt,
            Slug: slug);
    }

    // Claude Code attaches a `slug` (three-word codename) to many records, and emits
    // `{"type":"last-prompt","lastPrompt":"..."}` whenever the user submits a prompt
    // — the resume picker displays the most recent one. We capture both on a single pass.
    private static void TryExtractSideChannels(string line, ref string? slug, ref string? lastPrompt)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (slug is null &&
                root.TryGetProperty("slug", out var slugEl) &&
                slugEl.ValueKind == JsonValueKind.String)
            {
                slug = slugEl.GetString();
            }

            if (root.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String &&
                typeEl.GetString() == "last-prompt" &&
                root.TryGetProperty("lastPrompt", out var lpEl) &&
                lpEl.ValueKind == JsonValueKind.String)
            {
                var lp = lpEl.GetString();
                if (!string.IsNullOrEmpty(lp))
                    lastPrompt = lp!.Length > PreviewMaxChars ? lp[..PreviewMaxChars] : lp;
            }
        }
        catch (JsonException) { /* not our concern on side-channel scan */ }
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
