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
| Distribution | single self-contained `.exe` | — |

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

Produces one self-contained `SyncSentinel.exe` (~62 MB; bundles the .NET runtime,
WebView2 loader, SQLite, and the React assets — no install required):

```sh
npm --prefix src/web run build
dotnet publish src/SyncSentinel -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Autostart (a per-user `HKCU\…\Run` entry launching `SyncSentinel.exe --tray`) is
toggled by the **Start automatically on login** setting and reconciled at startup.
Closing the window hides to the tray; exit via the tray menu. A second launch
surfaces the running instance rather than starting a new one.

## CI / releases

- **CI** (`.github/workflows/ci.yml`) runs on every push to `main` and on PRs:
  installs the web deps, runs Vitest, builds the UI + solution, and runs the .NET tests.
- **Releases** (`.github/workflows/release.yml`) run when a version tag is pushed —
  they re-run the tests, publish the single-file exe, and attach it to a GitHub release:

  ```sh
  git tag v1.0.0 && git push origin v1.0.0
  ```

## License

[MIT](LICENSE) © 2026 Akhila Abesinghe
