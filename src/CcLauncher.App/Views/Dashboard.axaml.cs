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
    private SettingsViewModel _settingsVm;

    // Must match ExtendClientAreaTitleBarHeightHint in Dashboard.axaml. Empty clicks
    // in the top strip start a native window drag via BeginMoveDrag.
    private const double TitleBarHeight = 40;

    // Set by App.OnQuit before calling desktop.Shutdown() so the Closing handler
    // below knows to actually let the window close instead of cancelling-to-tray.
    private bool _shuttingDown;

    /// <summary>Signals that a real shutdown is in progress — the next Close is allowed to go through.</summary>
    public void PrepareShutdown() => _shuttingDown = true;

    // The file watcher fires every time a jsonl is written to or closed by claude.
    // If a refresh races a flush-on-exit (sharing-violation, partial write, missing
    // dir), an unhandled exception on the UI thread takes the whole app down. Wrap
    // every refresh — startup, watcher tick, post-action — in a soft catch + log.
    private void SafeRefresh()
    {
        try { _vm.Refresh(); }
        catch (System.Exception ex)
        {
            CcLauncher.Core.Logging.FileLog.Error("Refresh failed (recovered)", ex);
        }
    }

    public Dashboard()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new DashboardViewModel(AppServices.Discovery, AppServices.Config, AppServices.Launcher);
        DataContext = _vm;

        // Extending the client area disables the OS's default title-bar drag. Restore
        // drag-to-move by calling BeginMoveDrag when a left-button press lands in the
        // top strip and no child control consumed the event first. PointerPressed
        // bubbles up: if the gear handled it, we never see it here.
        PointerPressed += (_, e) =>
        {
            if (e.Handled) return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (e.GetPosition(this).Y > TitleBarHeight) return;
            BeginMoveDrag(e);
        };

        // Clicking the X on the dashboard just hides the window — the app keeps
        // running in the tray. Actual exit is via the tray's Quit menu, which
        // calls PrepareShutdown() first so this handler lets the close through.
        Closing += (_, e) =>
        {
            if (_shuttingDown) return;
            e.Cancel = true;
            Hide();

            // Reset any in-flight settings edits so reopening shows a clean panel
            // bound to the currently-persisted global settings.
            var panel = this.FindControl<Avalonia.Controls.Border>("SettingsPanel");
            if (panel is not null)
            {
                panel.Classes.Remove("open");
                _settingsVm = new SettingsViewModel(AppServices.Config);
                panel.DataContext = _settingsVm;
            }
        };

        _settingsVm = new SettingsViewModel(AppServices.Config);
        var panel = this.FindControl<Avalonia.Controls.Border>("SettingsPanel");
        if (panel is not null) panel.DataContext = _settingsVm;

        Opened += (_, _) =>
        {
            // Avalonia's Window.Icon only fills ICON_BIG on Win32; the taskbar reads
            // ICON_SMALL, so we send WM_SETICON directly for both slots. See
            // WindowsTaskbarIcon for the full rationale.
            WindowsTaskbarIcon.Apply(this, "avares://Claudini/Assets/claudini.ico");

            // Refresh on every dashboard show. We used to also run a FileSystemWatcher
            // on ~/.claude/projects/ that pushed Refresh ticks while the dashboard was
            // hidden, but that was implicated in a CLR fatal (0x80131506) when the user
            // closed a launched terminal -- presumably some interaction between the
            // watcher's native I/O completion ports and the jsonl flush-on-claude-exit.
            // Keeping refresh tied to Opened is simpler and means the dashboard is
            // always fresh when you open it; trade-off is you have to close+reopen to
            // see new sessions if the dashboard is already up.
            SafeRefresh();
        };
    }

    private void OpenSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowSettingsPanel();

    /// <summary>
    /// Slides the in-dashboard settings panel open. Callable from outside (e.g. the
    /// tray menu) so "Settings..." never needs to pop a separate window.
    /// Always rebinds a fresh SettingsViewModel so stale edits from a prior open
    /// (abandoned via window-X instead of Cancel) don't reappear.
    /// </summary>
    public void ShowSettingsPanel()
    {
        var panel = this.FindControl<Avalonia.Controls.Border>("SettingsPanel");
        if (panel is null) return;
        _settingsVm = new SettingsViewModel(AppServices.Config);
        panel.DataContext = _settingsVm;
        if (!panel.Classes.Contains("open")) panel.Classes.Add("open");
    }

    private void CloseSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panel = this.FindControl<Avalonia.Controls.Border>("SettingsPanel");
        if (panel is null) return;
        panel.Classes.Remove("open");

        // Discard any unsaved edits — next open starts fresh from persisted state.
        _settingsVm = new SettingsViewModel(AppServices.Config);
        panel.DataContext = _settingsVm;
    }

    private void SaveSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settingsVm.Save();
        StartupIntegration.Apply(_settingsVm.LaunchOnStartup);
        ThemeService.Apply(_settingsVm.Theme);
        CloseSettings_Click(sender, e);
    }

    public void ResumeMostRecent()
    {
        _vm.Refresh();
        var row = _vm.Rows.FirstOrDefault();
        if (row is null) return;
        _vm.LaunchProject(row, sessionId: null);
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

    private void Favourite_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Click on the pin dot — stop it from also toggling the row's expansion
        // (the Button sits inside a ToggleButton, so clicks would otherwise bubble).
        if (sender is Button b && b.Tag is ProjectRowViewModel row)
        {
            _vm.TogglePinned(row);
            e.Handled = true;
        }
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
