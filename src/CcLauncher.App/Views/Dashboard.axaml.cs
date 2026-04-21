using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CcLauncher.App.Services;
using CcLauncher.App.ViewModels;

namespace CcLauncher.App.Views;

public partial class Dashboard : Window
{
    private readonly DashboardViewModel _vm;

    public Dashboard()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new DashboardViewModel(AppServices.Discovery, AppServices.Config, AppServices.Launcher);
        DataContext = _vm;
        Opened += (_, _) => _vm.Refresh();
    }

    private void ProjectRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.Tag is ProjectRowViewModel row)
        {
            _vm.LaunchProject(row, sessionId: null);
            if (AppServices.Config.GetGlobalSettings().CloseOnLaunch) Hide();
        }
    }

    private void NewSession_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is ProjectRowViewModel row)
        {
            _vm.LaunchNewSession(row);
            if (AppServices.Config.GetGlobalSettings().CloseOnLaunch) Hide();
            e.Handled = true;
        }
    }
}
