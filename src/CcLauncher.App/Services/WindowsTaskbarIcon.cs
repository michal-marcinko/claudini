using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace CcLauncher.App.Services;

/// <summary>
/// Workaround for <see href="https://github.com/AvaloniaUI/Avalonia/issues/11569"/>.
/// Avalonia's <c>Window.Icon</c> on Win32 only sets <c>ICON_BIG</c> via WM_SETICON —
/// it never populates <c>ICON_SMALL</c>, which is the slot Windows actually reads for
/// the 16×16 taskbar thumbnail and the title-bar icon. The visible symptom is a
/// default blue-square placeholder in the taskbar even when <c>Window.Icon</c> loaded
/// successfully. This helper loads the .ico at both sizes via Win32 <c>LoadImage</c>
/// and sends <c>WM_SETICON</c> for both slots.
/// </summary>
internal static class WindowsTaskbarIcon
{
    private const uint IMAGE_ICON      = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const int  WM_SETICON      = 0x0080;
    private const int  ICON_SMALL      = 0;
    private const int  ICON_BIG        = 1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType,
                                           int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Tracked only for diagnostics — see the long comment in Apply for why we
    // don't DestroyIcon. The OS releases all USER handles on process exit.
    private static IntPtr _lastSmall;
    private static IntPtr _lastBig;

    /// <summary>
    /// Loads the .ico file referenced by the avares URI, extracts 16×16 and 32×32
    /// frames, and pins them to the window's <c>ICON_SMALL</c> and <c>ICON_BIG</c>
    /// slots. Safe to call on non-Windows platforms — it no-ops there.
    /// </summary>
    public static void Apply(Window window, string avaresUri)
    {
        if (!OperatingSystem.IsWindows()) return;

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            CcLauncher.Core.Logging.FileLog.Warn(
                "WindowsTaskbarIcon.Apply: no platform handle yet — call after Opened.");
            return;
        }

        // LoadImage requires an on-disk path. Materialize the embedded .ico once
        // into %TEMP% so Win32 can open it. Overwriting is fine — it's the same bytes.
        string tempPath;
        try
        {
            tempPath = Path.Combine(Path.GetTempPath(), "claudini-icon.ico");
            using var src = AssetLoader.Open(new Uri(avaresUri));
            using var dst = File.Create(tempPath);
            src.CopyTo(dst);
        }
        catch (Exception ex)
        {
            CcLauncher.Core.Logging.FileLog.Error(
                $"WindowsTaskbarIcon.Apply: failed to extract ico: {ex.Message}");
            return;
        }

        // LoadImage picks the closest-matching frame from a multi-resolution ICO.
        // Our claudini.ico carries 16/24/32/48/64/128/256 — so 16 and 32 are direct hits.
        var hSmall = LoadImage(IntPtr.Zero, tempPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        var hBig   = LoadImage(IntPtr.Zero, tempPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

        if (hSmall == IntPtr.Zero && hBig == IntPtr.Zero)
        {
            CcLauncher.Core.Logging.FileLog.Error(
                $"WindowsTaskbarIcon.Apply: LoadImage returned null for both sizes ({tempPath})");
            return;
        }

        if (hSmall != IntPtr.Zero) SendMessage(handle, WM_SETICON, (IntPtr)ICON_SMALL, hSmall);
        if (hBig   != IntPtr.Zero) SendMessage(handle, WM_SETICON, (IntPtr)ICON_BIG,   hBig);

        // DO NOT call DestroyIcon on the previous handles here.
        //
        // Earlier I tried freeing _lastSmall / _lastBig at this point. That caused
        // a CLR fatal 0x80131506 (COR_E_EXECUTIONENGINE) crash some seconds-to-
        // minutes later. Two reasons:
        //
        // 1. The OS keeps references to icon handles in caches OUTSIDE the
        //    window's HWND — taskbar thumbnails, alt-tab thumbnails, jump-lists.
        //    DestroyIcon on a handle they still hold causes use-after-free.
        // 2. The very first time Apply runs, _lastSmall / _lastBig are zero so
        //    nothing happens. On the second run they're our previous LoadImage
        //    handles — fine to destroy. But Avalonia's Window.Icon=... in XAML
        //    has already populated the slots once; SendMessage will replace
        //    those Avalonia-owned handles, and if we ever tracked them too we'd
        //    free Avalonia's state.
        //
        // The leak is bounded: 2 USER handles per dashboard open. Far below the
        // 10,000-per-process USER limit. The handles are released by the OS on
        // process exit anyway.
        _lastSmall = hSmall;
        _lastBig   = hBig;

        CcLauncher.Core.Logging.FileLog.Info(
            $"WindowsTaskbarIcon.Apply: small=0x{hSmall.ToInt64():X} big=0x{hBig.ToInt64():X}");
    }
}
