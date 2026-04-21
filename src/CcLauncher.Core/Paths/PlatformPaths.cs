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
