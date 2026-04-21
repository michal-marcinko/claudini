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
            // TODO: the claude-args portion is composed into a single -Command string. Quote()
            // only wraps space-containing args; embedded '"', ';' or '$' in args would not be
            // safely escaped. Claude session ids are UUIDs and Cwd flows from Claude Code's own
            // JSONL (not user-supplied), so this is currently safe in practice.
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
