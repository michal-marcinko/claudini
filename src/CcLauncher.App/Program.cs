using System;
using Avalonia;

namespace CcLauncher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CcLauncher.Core.Logging.FileLog.Initialize(CcLauncher.Core.Paths.PlatformPaths.LogsDir());
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
