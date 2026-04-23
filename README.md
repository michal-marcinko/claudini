<p align="center">
  <img src="src/CcLauncher.App/Assets/claudini-light.png" width="120" alt="Claudini — top hat + spark logo" />
</p>

<h1 align="center">Claudini</h1>

<p align="center">
  <em>A Windows tray launcher for Claude Code sessions.</em><br/>
  Favourite your projects, one-click resume, no <code>cd</code>-ing, no typing <code>--resume &lt;uuid&gt;</code>.
</p>

<p align="center">
  <a href="https://github.com/michal-marcinko/claudini/releases/latest">
    <img src="https://img.shields.io/github/v/release/michal-marcinko/claudini?label=latest&color=0a84ff" alt="Latest release" />
  </a>
  <img src="https://img.shields.io/badge/windows-x64-lightgrey" alt="Windows x64" />
  <img src="https://img.shields.io/badge/.NET-9.0-512bd4" alt=".NET 9" />
</p>

---

## The problem

Claude Code keeps a per-project history at `~/.claude/projects/<encoded-cwd>/<session-uuid>.jsonl`. Picking up where you left off looks like this:

```powershell
cd C:\Users\you\Desktop\some-project
claude --resume 5fadcc1e-6787-4af7-8846-839637ec0d37
```

You have to remember the project, find the session UUID, open a terminal, `cd`, paste. Multiply that by the number of projects you're bouncing between and you stop resuming sessions altogether — you just start new ones.

## The solution

Claudini is a tiny system-tray app that does all of that for you. Left-click the tray icon → dashboard slides up showing every Claude Code project you've touched, grouped by favourites and recency. Click a project → a new terminal opens with the right `cd` and `claude --resume`. That's it.

## Features

- **Tray-first.** Left-click the tray icon to toggle the dashboard; right-click for Open / Resume last / Settings / Quit.
- **Favourites + Recent.** Click the dot on any row to favourite — favourites pin to the top, the rest sort by recency.
- **One-click resume.** Click a project row to resume its most recent session. Expand any row to see previous sessions and pick a specific one. Click **New** for a fresh session in that project.
- **Prompt-based session labels.** Rows show the most recent prompt — the same label `claude --resume` displays — not a UUID or filename.
- **Inline slide-out settings.** Terminal command, global `claude` args, system-prompt prefix, launch-on-startup, theme (Light / Dark / System) — all in a panel that slides in, no separate window.
- **Extended title bar.** Windows chrome merges with the app top strip. Drag anywhere up top to move the window.
- **Stays in the tray.** Closing the dashboard hides it; `Quit` from the tray menu actually exits.
- **Poisoned-jsonl hardening.** A malicious session file can't smuggle shell-breaking strings into the launcher — any `cwd` containing control characters is rejected.

## Install

Download the latest `Claudini.exe` from the [releases page](https://github.com/michal-marcinko/claudini/releases/latest).

- Self-contained single-file build, ~100 MB. No .NET install required on the target machine.
- No installer, no admin. Drop it anywhere and run.

On first launch the Claudini icon appears in your system tray. If you don't see it, expand the `^` hidden-icons flyout in the bottom-right of your taskbar.

## Requirements

- Windows 10 or 11, x64
- [`claude`](https://docs.anthropic.com/claude-code) CLI installed and on your `PATH`
- [Windows Terminal](https://apps.microsoft.com/detail/9n0dx20hk701) recommended (anything PowerShell-capable works; configure in Settings)

## How it works

1. On launch, Claudini scans `~/.claude/projects/*/*.jsonl` and infers each project's real working directory from the jsonl contents. Modern Claude Code jsonl files open with metadata records that don't carry `cwd`, so Claudini scans forward until it finds a record that does, validates the string for safety, and falls back to the encoded folder name if nothing clean is found.
2. Projects are grouped by favourite status and sorted by last activity. The file watcher on `~/.claude/projects/` refreshes the dashboard as sessions are created or updated.
3. Clicking a row spawns your configured terminal (Windows Terminal by default) with `cd <project-cwd>; claude --resume <session-id>`.
4. Favourites, hidden projects, display-name overrides, and launch history live in a local SQLite database at `%APPDATA%\cc-launcher\app.db`.

## Build from source

```powershell
git clone https://github.com/michal-marcinko/claudini.git
cd claudini
dotnet build
dotnet test            # 57 tests across Core + App
.\publish.ps1          # produces publish\win-x64\Claudini.exe
```

Requires the .NET 9 SDK.

## Tech

- [Avalonia 11](https://avaloniaui.net) with FluentTheme — cross-platform XAML, Mica on Windows 11
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for observable view-models
- `Microsoft.Data.Sqlite` for local settings + launch history
- xUnit + FluentAssertions for tests
- P/Invoke to `user32.dll` for a taskbar-icon fix ([Avalonia #11569](https://github.com/AvaloniaUI/Avalonia/issues/11569)) — Avalonia on Win32 only sets `ICON_BIG`, so the taskbar thumbnail falls back to a default; Claudini sends a second `WM_SETICON` for `ICON_SMALL`.

## Known limitations

- **Windows-first.** The macOS and Linux launcher paths exist in the codebase but aren't exercised in 0.1.0.
- **System tray only.** The dashboard is a tray-attached window by design, not a taskbar app.
- **Local, single-user.** No remote or multi-user Claude Code session handling.

## Why "Claudini"?

Because it makes Claude Code sessions reappear from thin air. Top hat. Spark. You get it.
