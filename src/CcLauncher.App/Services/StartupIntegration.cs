using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CcLauncher.App.Services;

public static class StartupIntegration
{
    private const string AppName = "cc-launcher";

    public static void Apply(bool enable)
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exe)) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))      ApplyWindows(exe!, enable);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     ApplyMac(exe!, enable);
        else                                                          ApplyLinux(exe!, enable);
    }

    private static void ApplyWindows(string exe, bool enable)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null) return;
        if (enable) key.SetValue(AppName, $"\"{exe}\"");
        else        key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static void ApplyMac(string exe, bool enable)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var plistDir = Path.Combine(home, "Library", "LaunchAgents");
        var plistPath = Path.Combine(plistDir, "com.cclauncher.plist");
        if (!enable)
        {
            if (File.Exists(plistPath)) File.Delete(plistPath);
            return;
        }
        Directory.CreateDirectory(plistDir);
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
              <dict>
                <key>Label</key><string>com.cclauncher</string>
                <key>ProgramArguments</key><array><string>{exe}</string></array>
                <key>RunAtLoad</key><true/>
              </dict>
            </plist>
            """;
        File.WriteAllText(plistPath, xml);
    }

    private static void ApplyLinux(string exe, bool enable)
    {
        var cfg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var autostartDir = Path.Combine(string.IsNullOrEmpty(cfg) ? Path.Combine(home, ".config") : cfg, "autostart");
        var path = Path.Combine(autostartDir, "cc-launcher.desktop");
        if (!enable)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(autostartDir);
        var content = $"""
            [Desktop Entry]
            Type=Application
            Name=cc-launcher
            Exec="{exe}"
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """;
        File.WriteAllText(path, content);
    }
}
