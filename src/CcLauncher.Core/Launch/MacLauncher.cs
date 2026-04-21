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
