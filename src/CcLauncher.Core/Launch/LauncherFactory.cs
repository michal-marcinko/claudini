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
