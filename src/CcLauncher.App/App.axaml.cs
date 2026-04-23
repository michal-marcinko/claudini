using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CcLauncher.App.Services;
using CcLauncher.App.Views;

namespace CcLauncher.App;

public partial class App : Application
{
    private Dashboard? _dashboard;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Stay alive as a tray-only process when the dashboard closes. Default is
            // OnLastWindowClose, which kills the app (and the tray icon with it) the
            // moment the user hits X on the dashboard. OnExplicitShutdown means the
            // only exit path is the tray Quit menu, which calls desktop.Shutdown().
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            ThemeService.Apply(AppServices.Config.GetGlobalSettings().Theme);
            _dashboard = new Dashboard();
            desktop.MainWindow = _dashboard;
            _dashboard.Hide();
            desktop.ShutdownRequested += (_, _) => AppServices.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        if (_dashboard is null) return;
        _dashboard.Show();
        _dashboard.Activate();
    }

    // Left-click on the tray icon toggles the dashboard — open if hidden, hide if
    // already visible. Matches the typical tray-app pattern (Things, Raycast).
    private void OnTrayClicked(object? sender, EventArgs e)
    {
        if (_dashboard is null) return;
        if (_dashboard.IsVisible) _dashboard.Hide();
        else { _dashboard.Show(); _dashboard.Activate(); }
    }

    private void OnResumeLast(object? sender, EventArgs e)
    {
        if (_dashboard is null) return;
        _dashboard.ResumeMostRecent();
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        // Show the dashboard and slide the settings panel in — no separate window.
        if (_dashboard is null) return;
        _dashboard.Show();
        _dashboard.Activate();
        _dashboard.ShowSettingsPanel();
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        // Tell the dashboard this is a real shutdown — otherwise its Closing
        // handler cancels the close (to keep the tray alive on X) and the app
        // never actually exits.
        _dashboard?.PrepareShutdown();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
