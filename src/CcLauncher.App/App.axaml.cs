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
    private Views.Settings? _settingsWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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

    private void OnResumeLast(object? sender, EventArgs e)
    {
        if (_dashboard is null) return;
        _dashboard.ResumeMostRecent();
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        if (_settingsWindow is null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new Views.Settings();
            _settingsWindow.Show();
        }
        else _settingsWindow.Activate();
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
