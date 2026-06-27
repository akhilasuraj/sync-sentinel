# SyncSentinel

[![CI](https://github.com/akhilasuraj/sync-sentinel/actions/workflows/ci.yml/badge.svg)](https://github.com/akhilasuraj/sync-sentinel/actions/workflows/ci.yml)

A Windows tray app that keeps your dev folders mirrored to OneDrive-synced
destinations on a per-job schedule — robocopy under the hood, an elegant UI on top.

> **Status: feature-complete (Phases 0–5).** Tray app, config CRUD UI, scheduler +
> run queue, run history + retention, and single-file packaging are all built and
> tested (test-first). See [`DESIGN.md`](DESIGN.md) for the design,
> [`CONTEXT.md`](CONTEXT.md) for the vocabulary, and
> [`docs/adr/0001-architecture.md`](docs/adr/0001-architecture.md) for the
> architecture decision and the alternatives weighed.

## What it does

- **Pick source → destination jobs** in a UI.
- **Named, reusable exclusion sets** — folder-sets and file-sets — composed per job.
- **robocopy underneath**, with editable behavior flags (global default + per-job override) and a live command preview.
- **Per-job interval** (default 15 min), one job at a time, no overlap.
- **Live status + streaming logs** per job, plus searchable run history.
- Lives in the **system tray**, autostarts on login.

## Install

Grab the latest [release](https://github.com/akhilasuraj/sync-sentinel/releases):

- **`SyncSentinel-Setup.exe`** *(recommended)* — a **per-user installer** (no admin).
  Installs to `%LOCALAPPDATA%\Programs\SyncSentinel`, adds a Start-menu shortcut, and
  launches the app on finish. Upgrades install in place.
- **`SyncSentinel.exe`** — the **portable** build: run it from anywhere, no install.

Both are **self-contained** (the .NET runtime is bundled — nothing else to install) and
need the **Edge WebView2 Runtime**, preinstalled on Windows 10/11; the installer warns
if it's missing ([download](https://developer.microsoft.com/microsoft-edge/webview2/)).

> The binaries are **unsigned**, so on first run Windows SmartScreen may show *"Windows
> protected your PC."* Click **More info → Run anyway** to proceed.

## Uninstall

- **Installed**: uninstall from **Settings → Apps** (or the Start-menu entry). This
  removes the program and its shortcuts, clears the login-autostart entry, and asks
  whether to also delete your settings + run history (`%APPDATA%\SyncSentinel`) — keep
  them if you plan to reinstall.
- **Portable**: run `SyncSentinel.exe --uninstall --purge-data` to clear the autostart
  entry and delete your data, then delete the exe. (Omit `--purge-data` to keep your
  settings + history.)

## Tech stack

| Layer | Choice | Version (target, Jun 2026) |
|---|---|---|
| Backend | C# / .NET — scheduler, robocopy process control, log streaming | **.NET 10 (LTS)** |
| API / realtime | ASP.NET Core (Kestrel, loopback-only) + SignalR | ships with .NET 10 |
| UI | React + TypeScript, rendered in the system **WebView2** (no bundled Chromium) | React 19, TS 6.0 |
| UI styling / build | Tailwind CSS + component kit (e.g. shadcn/ui); Vite bundler | Tailwind v4, Vite 8 |
| Shell | thin WinForms tray host (NotifyIcon) | .NET 10 |
| Storage | `config.json` + SQLite (Microsoft.Data.Sqlite) under `%APPDATA%\SyncSentinel` | 10.x |
| Build toolchain | Node.js (for the React build) | **Node 24 (Active LTS)** |
| Distribution | per-user installer + portable self-contained `.exe` | — |

Windows-only by design (robocopy). See the ADR for why this stack over Electron / Tauri / WPF.
Versions verified current as of June 2026; only **.NET** and **Node** have formal LTS tracks
(the rest follow latest-stable). TypeScript 7.0 (Go-native, ~10× faster) is at RC — adopt on GA.

## Build, test, run

Prerequisites: **.NET 10 SDK**, **Node 24+** (for the React build), Windows with the
**WebView2 runtime** (preinstalled on Win10/11).

```sh
# Build the UI into the shell's wwwroot, then build everything
npm --prefix src/web install
npm --prefix src/web run build
dotnet build

# Tests
dotnet test                          # .NET (logic + API contracts)
npm --prefix src/web test            # Vitest (UI logic + components)

# Run the app (system tray + WebView2 window)
dotnet run --project src/SyncSentinel

# Headless host smoke check (real Kestrel: ping, static, config, run -> recorded)
dotnet run --project src/SyncSentinel -- --smoke
```

## Packaging

Two artifacts, both self-contained (bundle the .NET runtime, WebView2 loader, SQLite,
and the React assets — no runtime install required):

```sh
npm --prefix src/web run build

# Portable single-file exe (~62 MB; self-extracts at startup) -> publish/
dotnet publish src/SyncSentinel -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish

# Multi-file build that feeds the installer -> publish-app/
dotnet publish src/SyncSentinel -c Release -r win-x64 --self-contained -o publish-app

# Build the installer (needs Inno Setup) -> installer/SyncSentinel-Setup.exe
ISCC /DAppVersion=1.0.0 installer/SyncSentinel.iss
```

Autostart (a per-user `HKCU\…\Run` entry launching `SyncSentinel.exe --tray`) is
toggled by the **Start automatically on login** setting and reconciled at startup.
Closing the window hides to the tray; exit via the tray menu; a second launch surfaces
the running instance. The installer/uninstaller drive the app via `--quit` (stop a
running instance gracefully) and `--uninstall [--purge-data]` (remove its footprint);
see [`docs/adr/0002-installer-packaging.md`](docs/adr/0002-installer-packaging.md).

## CI / releases

- **CI** (`.github/workflows/ci.yml`) runs on every push to `main` and on PRs:
  installs the web deps, runs Vitest, builds the UI + solution, and runs the .NET tests.
- **Releases** (`.github/workflows/release.yml`) run when a version tag is pushed —
  they re-run the tests, publish the portable exe, build the installer, and attach both
  `SyncSentinel-Setup.exe` and `SyncSentinel.exe` to a GitHub release:

  ```sh
  git tag v1.0.0 && git push origin v1.0.0
  ```

## License

[MIT](LICENSE) © 2026 Akhila Abesinghe
