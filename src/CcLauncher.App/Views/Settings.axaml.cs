using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CcLauncher.App.Services;
using CcLauncher.App.ViewModels;

namespace CcLauncher.App.Views;

public partial class Settings : Window
{
    private readonly SettingsViewModel _vm;

    public Settings()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new SettingsViewModel(AppServices.Config);
        DataContext = _vm;
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.Save();
        StartupIntegration.Apply(_vm.LaunchOnStartup);
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
