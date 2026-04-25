using System.Diagnostics;

namespace CcLauncher.Core.Launch;

public sealed class WindowsLauncher : ILauncher
{
    public LaunchResult Launch(LaunchRequest request)
    {
        // Resolve the user's TerminalCommand setting to the right launch strategy.
        // Direct .exe path -> invoke verbatim with ArgumentList (safe).
        // wt / wt.exe       -> Windows Terminal with -d <cwd>.
        // cmd               -> cmd.exe /K "cd /d <cwd> & claude ..." so the user's
        //                      autorun / cmd profile loads.
        // powershell        -> powershell.exe -NoExit -Command. Loads $PROFILE
        //                      (PowerShell 5) so user aliases / functions are available.
        // pwsh              -> pwsh.exe (PowerShell 7) variant, same syntax.
        // Anything else     -> fall back to powershell.exe so a typo doesn't break things.
        var cmd = (request.TerminalCommand ?? string.Empty).Trim();

        ProcessStartInfo psi;
        if (File.Exists(cmd))                      psi = BuildDirectExe(request);
        else if (IsWindowsTerminal(cmd))           psi = BuildWindowsTerminal(request);
        else if (IsCmd(cmd))                       psi = BuildCmd(request);
        else if (IsPwsh(cmd))                      psi = BuildPowerShell(request, "pwsh.exe");
        else                                       psi = BuildPowerShell(request, "powershell.exe");

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

    private static ProcessStartInfo BuildDirectExe(LaunchRequest req)
    {
        var psi = new ProcessStartInfo(req.TerminalCommand)
        {
            WorkingDirectory = req.Cwd,
            UseShellExecute = false,
        };
        foreach (var a in req.ClaudeArgs) psi.ArgumentList.Add(a);
        return psi;
    }

    private static ProcessStartInfo BuildWindowsTerminal(LaunchRequest req)
    {
        var psi = new ProcessStartInfo("wt.exe") { UseShellExecute = true };
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(req.Cwd);
        psi.ArgumentList.Add("claude");
        foreach (var a in req.ClaudeArgs) psi.ArgumentList.Add(a);
        return psi;
    }

    // PowerShell -NoExit -Command "Set-Location ...; claude ...". Single-quoted
    // PowerShell strings escape ' as ''; we already reject CR/LF/NUL in cwd at
    // the discovery layer, so a poisoned jsonl can't break out of the quoting.
    // ClaudeArgs come from settings the user typed plus session UUIDs from
    // jsonl filenames — both treated as trusted.
    private static ProcessStartInfo BuildPowerShell(LaunchRequest req, string binary)
    {
        var args = string.Join(' ', req.ClaudeArgs.Select(PsQuote));
        var line = $"Set-Location -LiteralPath '{PsEscape(req.Cwd)}'; claude {args}";

        var psi = new ProcessStartInfo(binary) { UseShellExecute = true };
        psi.ArgumentList.Add("-NoExit");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(line);
        return psi;
    }

    private static ProcessStartInfo BuildCmd(LaunchRequest req)
    {
        var args = string.Join(' ', req.ClaudeArgs.Select(CmdQuote));
        var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
        psi.ArgumentList.Add("/K");
        // /K runs the command then keeps the shell open. cd /d for cross-drive.
        psi.ArgumentList.Add($"cd /d \"{req.Cwd}\" & claude {args}");
        return psi;
    }

    private static bool IsWindowsTerminal(string cmd) =>
        cmd.Equals("wt", StringComparison.OrdinalIgnoreCase) ||
        cmd.Equals("wt.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsCmd(string cmd) =>
        cmd.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
        cmd.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsPwsh(string cmd) =>
        cmd.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
        cmd.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase);

    // Resolved-command-line preview. Only quotes args with spaces — sufficient
    // for human-readable display in the launch-failure log.
    private static string Quote(string s) =>
        s.Contains(' ') ? $"\"{s}\"" : s;

    // PowerShell single-quote escape: the only thing single-quoted strings
    // interpret is '' (literal apostrophe). Wrap every value to neutralise
    // metacharacters like $ ; & " .
    private static string PsEscape(string s) => s.Replace("'", "''");
    private static string PsQuote(string s) => "'" + PsEscape(s) + "'";

    // cmd.exe quoting: wrap any arg containing whitespace or " in double quotes,
    // and double up internal " to "". cmd doesn't do backslash escaping inside
    // double quotes the way PowerShell does.
    private static string CmdQuote(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        if (!s.Any(c => c == ' ' || c == '\t' || c == '"')) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
