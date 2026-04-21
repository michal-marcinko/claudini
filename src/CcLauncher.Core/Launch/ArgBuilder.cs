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
