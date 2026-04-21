using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            _dashboard = new Dashboard();
            desktop.MainWindow = _dashboard;
            _dashboard.Hide();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        if (_dashboard is null) return;
        _dashboard.Show();
        _dashboard.Activate();
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
