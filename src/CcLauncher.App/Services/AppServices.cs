using CcLauncher.Core.Config;
using CcLauncher.Core.Discovery;
using CcLauncher.Core.Launch;
using CcLauncher.Core.Paths;

namespace CcLauncher.App.Services;

public static class AppServices
{
    private static readonly object _lock = new();
    private static IConfigStore? _config;
    private static IProjectDiscoveryService? _discovery;
    private static ILauncher? _launcher;

    public static IConfigStore Config
    {
        get
        {
            lock (_lock)
            {
                _config ??= ConfigStore.OpenFile(
                    PlatformPaths.DatabaseFile(),
                    System.OperatingSystem.IsWindows()
                        ? TerminalDetector.DetectDefaultCommand()
                        : LauncherFactory.DefaultTerminalCommand());
                return _config;
            }
        }
    }

    public static IProjectDiscoveryService Discovery
    {
        get
        {
            lock (_lock)
            {
                _discovery ??= new ProjectDiscoveryService(PlatformPaths.ClaudeProjectsDir());
                return _discovery;
            }
        }
    }

    public static ILauncher Launcher
    {
        get
        {
            lock (_lock)
            {
                _launcher ??= LauncherFactory.ForCurrentOs();
                return _launcher;
            }
        }
    }

    public static void Dispose()
    {
        lock (_lock)
        {
            (_config as System.IDisposable)?.Dispose();
            _config = null;
        }
    }
}
