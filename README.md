# SyncSentinel

A Windows tray app that keeps your dev folders mirrored to OneDrive-synced
destinations on a per-job schedule — robocopy under the hood, an elegant UI on top.

> **Status: design phase.** This repo currently holds the documented design only
> (no application code yet). See [`DESIGN.md`](DESIGN.md) for the full design,
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

## License

[MIT](LICENSE) © 2026 Akhila Abesinghe
