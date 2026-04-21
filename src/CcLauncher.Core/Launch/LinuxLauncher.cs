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
