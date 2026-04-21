# Claude Code Launcher — Design Spec

**Status:** Draft
**Date:** 2026-04-21
**Working name:** `cc-launcher` (placeholder — may be renamed before first release)

## 1. Problem

Claude Code users accumulate projects across many directories. After a PC restart, terminal windows are gone and it's easy to forget which projects were active, let alone which specific conversation was in progress. Navigating back to the right directory and finding the right session is manual, repetitive, and breaks flow.

## 2. Goals

- Solve the "where was I working?" problem after restart or context loss.
- Let users resume the right Claude Code conversation (not just `cd` to a folder) with one click.
- Stay out of the way: a tray app, not another window to manage.
- Cross-platform from day one (Windows, macOS, Linux).
- Lightweight: native UI, small binary, low RAM.
- Open-source-friendly: healthy contributor ecosystem.

## 3. Non-goals

- Replacing or wrapping Claude Code's CLI behavior. The app launches `claude`; it does not embed, proxy, or modify it.
- Multi-user / team features. Single-user, local-only for v1.
- Cloud sync of app config across machines.
- Embedded terminal emulator (possible future; not v1).

## 4. User experience

### 4.1 Tray icon
- Lives in the system tray / menu bar on all three platforms.
- Left-click → toggles the dashboard window.
- Right-click → native context menu: Open, Resume last session, Settings, Quit.
- Optional: launch on OS startup (off by default).

### 4.2 Dashboard window
Small, fixed-size panel (roughly 480×640), opens near the tray icon.

Contents:
- Search box (MVP: filter by project name; stretch: quick-launcher-style fuzzy search with global hotkey).
- Vertical list of projects, pinned items first, then sorted by last activity descending.
- Each row:
  - Display name (renamed override, else decoded cwd basename).
  - Last-used relative timestamp ("2h ago").
  - Primary action (row click): resume the latest session.
  - Secondary action ("New session" button visible on hover, and in the row overflow menu): start a fresh `claude` session in this cwd with no `--resume`.
  - Expand caret → reveals the session list for that project.
- Expanded session row:
  - Timestamp ("yesterday 18:42").
  - First user message preview (truncated ~60 chars).
  - Message count.
  - Click → resumes that specific session.

Curation (right-click row or row overflow menu):
- Pin / unpin.
- Hide (excluded from display; can be restored from Settings).
- Rename (display-only; does not touch the underlying folder).

### 4.3 Settings window
- Terminal command (with platform-appropriate default, see §6.3).
- Global default CLI args (applied to every launch).
- Global system prompt snippet (applied via `--append-system-prompt`).
- Launch on OS startup (toggle).
- "Resume all on open" toggle (stretch).
- Hidden projects list (to un-hide).
- Per-project settings sub-panel (accessed from dashboard row): display name, default args, system prompt.

## 5. Architecture

### 5.0 Technology

- **Language:** C# (.NET 8 LTS or newer).
- **UI framework:** Avalonia 12. Native cross-platform UI (no embedded browser engine), first-class `TrayIcon` support on Windows/macOS/Linux, XAML-based with strong tooling.
- **Storage:** SQLite via `Microsoft.Data.Sqlite`.
- **Why Avalonia over alternatives:** Electron/Tauri were ruled out (user prefers non-web frontend). Native Windows-only (WinUI 3 / WPF) ruled out for the cross-platform goal. Flutter Desktop's tray story is community-plugin territory and Dart is a less common desktop language. Rust + Slint/iced is lightest but steepest curve. Avalonia hits the sweet spot: lightweight, native, cross-platform, healthy ecosystem.

One process. Avalonia application owning a hidden main window and a tray icon. No separate service, daemon, or CLI binary.

Four internal modules, each testable in isolation:

### 5.1 `ProjectDiscovery`
- Pure-ish: only filesystem reads, no UI, no config writes.
- Inputs: path to `~/.claude/projects/`.
- Outputs: `DiscoveredProject[]`.
- Behavior: enumerate subfolders; for each JSONL file, read first + last line to pull `cwd`, `startedAt`, `lastActivity`, and first user message preview. Do not parse whole files unless explicitly requested for session detail.
- File watcher: surfaces new sessions and new projects while dashboard is open.

### 5.2 `ConfigStore`
- Wraps SQLite file at platform-appropriate config path (see §7).
- Handles migrations via a simple numbered-script system.
- Exposes CRUD for `ProjectSettings`, `GlobalSettings`, `LaunchHistory`.

### 5.3 `Launcher`
- Single interface: `Launch(cwd, args)`.
- Three platform implementations: Windows, macOS, Linux (see §6.3).
- Returns success / failure. Never throws to callers for expected failures (missing terminal, bad cwd).

### 5.4 `DashboardViewModel`
- Only layer that knows about UI.
- Binds discovery + config + launcher to the XAML view.
- Owns merge logic (overlay config on discovered state; apply sort / filter / hide rules).

## 6. Data flow

### 6.1 Startup
1. App process launches, reads `ConfigStore`, initializes tray icon.
2. Main window is created hidden.
3. If `launch_on_startup` and `resume_all_on_open` are both enabled and this was launched by OS login → perform resume-all flow (§6.4).
4. Otherwise idle until user interacts.

### 6.2 Dashboard open
1. User left-clicks tray → window shows.
2. `DashboardViewModel.Refresh()`:
   - `ProjectDiscovery.Scan()` off the UI thread.
   - `ConfigStore.GetAllProjectSettings()`.
   - Merge, filter hidden, apply rename overrides, sort pinned-first then by `lastActivity desc`.
3. Render list. Session previews are loaded lazily on row expand.
4. File watcher keeps the list fresh while the window is open.

### 6.3 Launch
Inputs: `project`, `session?` (null = new session).

1. Resolve session id:
   - If `session` provided, use its id.
   - Else use project's latest session id (resume).
   - "New session" action passes null explicitly, yielding plain `claude` with no `--resume`.
2. Build args:
   - Start with `global_default_args`.
   - Append `project.default_args`.
   - If `session.id` present, append `--resume <id>`.
   - If any system prompt configured, append `--append-system-prompt "<merged>"` where `merged` = global + project, joined with a blank line.
3. Resolve terminal command:
   - Windows: `wt.exe -d "<cwd>" claude <args>`. Fallback if `wt.exe` is missing: `powershell -NoExit -Command "cd '<cwd>'; claude <args>"`.
   - macOS: `osascript -e 'tell application "Terminal" to do script "cd <cwd>; claude <args>"'`. Fallback: user-configured command.
   - Linux: `x-terminal-emulator -e bash -c "cd '<cwd>'; claude <args>; exec bash"`. Fallback: user-configured command.
4. Spawn detached process. Record PID only if active-tracking is enabled.
5. `ConfigStore.UpdateLastLaunched(project_id)`. Optionally insert a `LaunchHistory` row.
6. Close dashboard window (configurable: close on launch or stay open).

Failures return to the VM, which shows a non-modal toast with the attempted command and a link to settings.

### 6.4 Resume-all (stretch)
Two tiers, ship in this order:

**Lightweight (v1.x):** on trigger, iterate the N most-recently-active sessions (N configurable, default 3) and launch each as if the user clicked it. No process tracking, no shutdown detection required.

**Accurate (v2.x if demanded):** record spawned terminal PIDs; on next resume-all, prefer sessions whose PIDs are no longer alive. Cross-platform PID liveness checks are doable but fiddly — defer until user feedback confirms it's worth the complexity.

## 7. Data model

### 7.1 Discovered state (ephemeral, read-only)

```
DiscoveredProject
  id           string   (= encoded folder name under ~/.claude/projects/, stable key)
  cwd          string   (from first JSONL entry, authoritative)
  sessions     DiscoveredSession[]
  lastActivity DateTime (max of session.lastActivity)

DiscoveredSession
  id            string  (session UUID = JSONL filename stem)
  filePath      string
  startedAt     DateTime (first entry timestamp)
  lastActivity  DateTime (last entry timestamp)
  messageCount  int
  firstUserMsg  string? (truncated preview)
```

### 7.2 App state (SQLite)

Database location:
- Windows: `%APPDATA%\cc-launcher\app.db`
- macOS: `~/Library/Application Support/cc-launcher/app.db`
- Linux: `$XDG_CONFIG_HOME/cc-launcher/app.db` (default `~/.config/cc-launcher/app.db`)

Schema:

```sql
CREATE TABLE ProjectSettings (
  project_id           TEXT PRIMARY KEY,
  display_name         TEXT,
  pinned               INTEGER NOT NULL DEFAULT 0,
  hidden               INTEGER NOT NULL DEFAULT 0,
  default_args         TEXT,
  system_prompt        TEXT,
  last_launched_at     TEXT
);

CREATE TABLE GlobalSettings (
  key                  TEXT PRIMARY KEY,
  value                TEXT
);
-- Expected keys: terminal_command, global_default_args, global_system_prompt,
-- launch_on_startup, resume_all_on_open, close_on_launch.

CREATE TABLE LaunchHistory (
  id                   INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id           TEXT NOT NULL,
  project_id           TEXT NOT NULL,
  launched_at          TEXT NOT NULL,
  closed_at            TEXT,
  pid                  INTEGER
);
```

`default_args` and `system_prompt` columns are present from v1 even though the UI for editing them ships later. Avoids a near-term migration.

## 8. Error handling

Principle: never crash on unexpected input. Degrade, log, continue.

- **Discovery failures:** missing `~/.claude/projects/` → empty-state. Unreadable folder → skip + log. Corrupt JSONL → keep the session with unknown metadata, show filename + mtime. Undecodable folder name → use folder name as-is.
- **Launch failures:** terminal missing → toast with the failed command + settings link. `claude` not on PATH → same treatment. Missing cwd → row shows warning; offer remove / relocate.
- **Config failures:** SQLite locked / corrupt → back up as `app.db.corrupt-<timestamp>`, create fresh, log loudly. Migration failure → refuse to start, show diagnostic window with reset option. Never silently destroy state.

All non-fatal errors go to `<config_dir>/logs/app.log` (rolling, capped at ~5MB).

## 9. Testing

- **Unit tests (majority):**
  - `ProjectDiscovery` with temp directory fixtures. Include corrupt JSONL, missing files, unusual encodings.
  - `ConfigStore` with in-memory SQLite. Cover CRUD and migrations.
  - Arg builder (pure function): empty, overrides, system-prompt merging.
- **Integration tests:**
  - `Launcher` with a stub "terminal" exe that records its arguments + cwd to a file.
  - Run per-platform in CI to catch spawning bugs specific to each OS.
- **UI tests:** out of scope for MVP. View-model tests via `Avalonia.Headless` are acceptable if needed; views stay thin so the VM is the right test level.

No running `claude` process required in tests.

## 10. Scope

### 10.1 MVP (v0.1)
1. Tray icon with context menu (Open, Resume last, Settings, Quit).
2. Dashboard window with auto-discovery of projects from `~/.claude/projects/`.
3. Expandable project rows with session previews.
4. Click project → resume latest session. Click session → resume that session. New-session action → plain `claude`.
5. Curation: pin, hide, rename.
6. Settings: terminal command, global default args, launch-on-startup.
7. Cross-platform terminal hand-off (Windows / macOS / Linux) with per-platform defaults.
8. SQLite-backed config with migrations.
9. Rolling log file.

### 10.2 Stretch (post-MVP)
- **A. Default CLI args** — per-project and global. Schema already present; UI ships later.
- **B. Custom system prompts** — per-project and global, merged and injected via `--append-system-prompt`. Schema already present.
- **C. Resume-all (lightweight)** — reopen N most-recent sessions on demand or at startup.
- **D. Workspaces** — named groups of projects launched as a set.
- **E. Active session tracking** — PID tracking for accurate resume-all and a "running" indicator.
- **F. Global hotkey quick-launcher** — Alfred-style fuzzy launcher.
- **G. Embedded terminal** — revisit only if hand-off proves painful in practice.

## 11. Open questions

- **Name.** `cc-launcher` is a placeholder. Alternatives: `kodo`, or something else. Decide before first public release so the repo and binary names don't churn.
- **Terminal defaults on Linux.** `x-terminal-emulator` is the Debian convention but not universal. Fallback chain worth validating: `$TERMINAL` env var → `x-terminal-emulator` → `gnome-terminal` → `konsole` → `xterm`.
- **Packaging.** Single-file self-contained publish per platform vs. requiring .NET runtime. Single-file is friendlier but larger (~60–80 MB). Acceptable for a desktop app.
