using Avalonia;
using Avalonia.Styling;

namespace CcLauncher.App.Services;

/// <summary>
/// Maps the stored theme string ("System" | "Light" | "Dark") to Avalonia's
/// <see cref="ThemeVariant"/> and applies it to <see cref="Application"/>.
/// </summary>
public static class ThemeService
{
    public static void Apply(string theme)
    {
        var app = Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark"  => ThemeVariant.Dark,
            _       => ThemeVariant.Default, // System
        };
    }
}
