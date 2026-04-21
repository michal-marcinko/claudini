using System.Linq;
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

    private void SessionRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.Tag is SessionRowViewModel srow)
        {
            var parent = _vm.Rows.FirstOrDefault(r => r.Sessions.Any(s => s.Id == srow.Id));
            if (parent is null) return;
            _vm.LaunchProject(parent, sessionId: srow.Id);
            if (AppServices.Config.GetGlobalSettings().CloseOnLaunch) Hide();
            e.Handled = true;
        }
    }

    private void TogglePin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem m && m.Tag is ProjectRowViewModel row) _vm.TogglePinned(row);
    }

    private void Hide_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem m && m.Tag is ProjectRowViewModel row) _vm.Hide(row);
    }

    private async void Rename_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MenuItem m || m.Tag is not ProjectRowViewModel row) return;
        var dialog = new Window
        {
            Width = 320, Height = 120, Title = "Rename",
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var tb = new TextBox { Text = row.DisplayName, Margin = new Avalonia.Thickness(12) };
        var ok = new Button { Content = "OK", Margin = new Avalonia.Thickness(12), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        ok.Click += (_, _) => dialog.Close(tb.Text);
        dialog.Content = new StackPanel { Children = { tb, ok } };
        var result = await dialog.ShowDialog<string?>(this);
        if (result is not null) _vm.Rename(row, result);
    }
}
