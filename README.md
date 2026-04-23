<p align="center">
  <img src="src/CcLauncher.App/Assets/claudini-light.png" width="120" alt="Claudini" />
</p>

<h1 align="center">Claudini</h1>

<p align="center">System-tray app for resuming Claude Code sessions on Windows.</p>

<p align="center">
  <a href="https://github.com/michal-marcinko/claudini/releases/latest"><img src="https://img.shields.io/github/v/release/michal-marcinko/claudini?label=release" alt="latest release" /></a>
  <img src="https://img.shields.io/badge/windows-x64-lightgrey" alt="windows x64" />
  <img src="https://img.shields.io/badge/.NET-9.0-512bd4" alt="dotnet 9" />
</p>

Claude Code stores per-project transcripts under `~/.claude/projects/`. To resume one, you have to remember the project path, dig up the session UUID, and run `cd <project>; claude --resume <uuid>`. After a few projects it's easier to just start fresh. Claudini puts every project you've touched in a tray dropdown and resumes them in one click.

## Install

Grab `Claudini.exe` from the [latest release](https://github.com/michal-marcinko/claudini/releases/latest). Single-file self-contained build. No installer, no admin, no .NET runtime needed on the target machine. Drop it anywhere and run.

The icon lives in the system tray once it's running. Windows 11 hides tray icons by default, so you'll find it behind the `^` overflow at the bottom-right of your taskbar. Drag it out of the overflow if you want it one-click away.

## Using it

Left-click the tray icon. The dashboard drops down with two sections, Favourites and Recent. Click a project to resume its most recent session. Expand a row to see older sessions and pick one. Click the dot beside a project to toggle favourite.

The **New** button starts a fresh session in that project's cwd instead of resuming.

Right-click the tray icon for Open / Resume last / Settings / Quit.

## Settings

Gear button in the top-left of the dashboard, or the tray menu. From there:

- Terminal command (default `wt`)
- Global extra args appended to every `claude` invocation
- A system-prompt prefix injected via `--append-system-prompt`
- Launch on Windows startup
- Close dashboard after a launch
- Theme: Light / Dark / System

Saves are written to SQLite at `%APPDATA%\cc-launcher\app.db` and applied immediately.

## Requirements

- Windows 10 or 11, x64
- [`claude`](https://docs.anthropic.com/claude-code) on your `PATH`
- A PowerShell-capable terminal. [Windows Terminal](https://apps.microsoft.com/detail/9n0dx20hk701) is the default.

## Build from source

```powershell
git clone https://github.com/michal-marcinko/claudini.git
cd claudini
dotnet build
dotnet test            # 57 tests
.\publish.ps1          # produces publish\win-x64\Claudini.exe
```

Needs the .NET 9 SDK.

## How it works

Claudini watches `~/.claude/projects/` and reads each session jsonl to pull out:

- the real working directory. Modern Claude Code jsonl files lead with metadata records that don't carry `cwd`, so discovery scans forward until it finds a record that does. The result is validated for control characters (CR/LF/NUL) before it flows to the launcher, so a malformed or hostile session file can't inject into the shell.
- the most recent prompt, shown as the session label. This matches the label `claude --resume` uses so the same entry you'd pick in the CLI picker is the same entry you'll pick here.
- the slug and timestamps.

Launching spawns your terminal with the equivalent of `cd <cwd>; claude --resume <session-id>`. Favourites, hidden projects, display-name overrides, and launch history live in the SQLite file above.

Two details worth calling out for anyone reading the code:

1. Avalonia 11 on Windows doesn't set `ICON_SMALL` via `WM_SETICON` ([upstream issue](https://github.com/AvaloniaUI/Avalonia/issues/11569)), so the taskbar thumbnail falls back to a default. Claudini P/Invokes a second `WM_SETICON` to populate that slot.
2. Pinning a favourite used to call `Refresh()` which re-scanned the filesystem and rebuilt every row, producing a visible stutter on every click. The dashboard now does a surgical move between the Favourites/Recent collections with the same row instance, so the ToggleButton you clicked stays alive and just slides.

## Not in 0.1

- The macOS and Linux launcher paths exist in code but aren't exercised yet.
- No multi-user or remote Claude Code session handling.
- The dashboard is tray-attached. It doesn't stand alone as a normal taskbar app.

## Stack

.NET 9, Avalonia 11 (FluentTheme, Mica on Win11), CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, xUnit, FluentAssertions.
