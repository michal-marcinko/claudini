using System;
using Avalonia;

namespace CcLauncher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CcLauncher.Core.Logging.FileLog.Initialize(CcLauncher.Core.Paths.PlatformPaths.LogsDir());

        // Last-resort logging before death. CLR fatals (e.g. 0x80131506
        // ExecutionEngine errors from corrupt P/Invoke state) usually bypass these,
        // but managed unhandled exceptions and unobserved task faults do hit here
        // and at least give us a stack trace to chase the next time something dies.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CcLauncher.Core.Logging.FileLog.Error(
                "AppDomain.UnhandledException (terminating=" + e.IsTerminating + ")",
                e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            CcLauncher.Core.Logging.FileLog.Error("TaskScheduler.UnobservedTaskException", e.Exception);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
