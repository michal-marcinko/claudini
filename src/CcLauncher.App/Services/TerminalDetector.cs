using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CcLauncher.App.Services;

public sealed record TerminalProfile(string DisplayName, string Command, bool IsAvailable)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Discovers which Windows terminals are installed and which one is the user's
/// system default. Used to populate the Settings dropdown and to seed a sensible
/// terminal command on first run instead of always defaulting to "wt".
/// </summary>
public static class TerminalDetector
{
    // Windows 11's "Default terminal application" setting persists at
    // HKCU\Console\%%Startup. The DelegationTerminal value is a GUID identifying
    // the chosen terminal; the canonical mapping is:
    //   {2EACA947-7F5F-4CFA-BA87-8F7FBEEFBE69} = Windows Terminal
    //   {B23D10C0-E52E-411E-9D5B-C09FDF709C7D} = Windows Console Host (legacy)
    //   {00000000-0000-0000-0000-000000000000} = "Let Windows decide"
    private const string WindowsTerminalGuid = "{2EACA947-7F5F-4CFA-BA87-8F7FBEEFBE69}";

    public static IReadOnlyList<TerminalProfile> KnownProfiles() => new[]
    {
        new TerminalProfile("Windows Terminal",  "wt",         IsOnPath("wt.exe")),
        new TerminalProfile("PowerShell 7",      "pwsh",       IsOnPath("pwsh.exe")),
        new TerminalProfile("Windows PowerShell","powershell", true), // shipped with every Windows since Vista
        new TerminalProfile("Command Prompt",    "cmd",        true),
    };

    /// <summary>
    /// Best guess for what the user expects on first launch. Reads the Windows 11
    /// default-terminal registry setting, falling back to whatever is installed.
    /// Always returns one of the four well-known commands so it round-trips
    /// through <see cref="KnownProfiles"/>.
    /// </summary>
    public static string DetectDefaultCommand()
    {
        if (!OperatingSystem.IsWindows()) return "wt";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Console\%%Startup");
            var delegated = key?.GetValue("DelegationTerminal") as string;

            // User explicitly chose Windows Terminal as their system default.
            if (string.Equals(delegated, WindowsTerminalGuid, StringComparison.OrdinalIgnoreCase)
                && IsOnPath("wt.exe"))
                return "wt";
        }
        catch { /* registry blocked, fall through */ }

        // No explicit preference. Prefer wt if installed (it's the best UX),
        // then PowerShell 7, then Windows PowerShell which is always present.
        if (IsOnPath("wt.exe"))   return "wt";
        if (IsOnPath("pwsh.exe")) return "pwsh";
        return "powershell";
    }

    private static bool IsOnPath(string exe)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            return path.Split(Path.PathSeparator)
                       .Where(d => !string.IsNullOrWhiteSpace(d))
                       .Any(d => File.Exists(Path.Combine(d, exe)));
        }
        catch { return false; }
    }
}
