# SyncSentinel

[![CI](https://github.com/akhilasuraj/sync-sentinel/actions/workflows/ci.yml/badge.svg)](https://github.com/akhilasuraj/sync-sentinel/actions/workflows/ci.yml)

A Windows tray app that periodically mirrors your working folders into a
cloud-synced folder — robocopy under the hood, an elegant UI on top.

## The problem

Your work lives on one disk. Some of it is committed and pushed; some of it is
staged-but-not-pushed, or uncommitted, or scratch files that were never in any
repo at all. If that disk dies, everything that wasn't already somewhere else is
simply gone — and the stuff that hurts most is usually the stuff Git never knew
about.

You probably already have a cloud sync client running (OneDrive, Dropbox, Google
Drive, iCloud Drive…). The obvious fix is to move your project folders inside its
synced folder and let it back everything up. But pointing a sync engine directly
at an *active* codebase is painful: every build, every `npm install`, every
branch switch rewrites thousands of files, and the sync client churns through
CPU, disk, and network trying to keep up — while you're trying to work.

## The solution

SyncSentinel keeps your working folders where they are and, on a schedule,
**mirrors** them into a destination folder that your cloud client already syncs.

- Your project folders stay **outside** the sync engine's watch, so there's no
  constant churn while you code.
- The mirror runs every N minutes (your choice), so a copy of your work —
  committed or not, versioned or not — is always making its way to the cloud.
- It uses **robocopy**, so each run only copies what actually changed.

It was built and tested against **OneDrive**, but nothing in it is
OneDrive-specific: the destination is just a folder, so any file-based sync
client that watches a folder works the same way.

## What it does

- **Pick source → destination jobs** in a UI.
- **Named, reusable exclusion sets** — folder-sets and file-sets — composed per job
  (skip `node_modules`, `bin/`, `obj/`, and friends once, reuse everywhere).
- **robocopy underneath**, with editable behavior flags (global default + per-job
  override) and a live command preview.
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

## Using it

1. **Launch** SyncSentinel — it opens a window and lives in the system tray (closing
   the window just hides it there).
2. Go to **Jobs → New job** and give it a name.
3. Set the **Source** — your working folder (e.g. `C:\dev\MyProject`) — and the
   **Destination**: a folder *inside* the one your cloud client already syncs
   (e.g. `…\OneDrive\Backups\MyProject`).
4. *(Optional)* Attach **exclusion sets** to skip noise like `node_modules`, `bin/`,
   or `obj/`. Create these once under **Exclusion Sets**, then reuse them across jobs.
5. Set the **interval** (default 15 min), make sure **Enabled** is checked, and
   **Save**. The job now runs on schedule — or hit **Run now** to trigger it
   immediately — and you can watch live status and streaming logs on the
   **Dashboard**.

## Uninstall

- **Installed**: uninstall from **Settings → Apps** (or the Start-menu entry). This
  removes the program and its shortcuts, clears the login-autostart entry, and asks
  whether to also delete your settings + run history (`%APPDATA%\SyncSentinel`) — keep
  them if you plan to reinstall.
- **Portable**: run `SyncSentinel.exe --uninstall --purge-data` to clear the autostart
  entry and delete your data, then delete the exe. (Omit `--purge-data` to keep your
  settings + history.)

## Built with

| Layer | Choice |
|---|---|
| Backend | C# / .NET — scheduler, robocopy process control, log streaming |
| API / realtime | ASP.NET Core (Kestrel, loopback-only) + SignalR |
| UI | React + TypeScript, rendered in the system **WebView2** (no bundled Chromium) |
| UI styling / build | Tailwind CSS + component kit; Vite bundler |
| Shell | thin WinForms tray host (NotifyIcon) |
| Storage | `config.json` + SQLite under `%APPDATA%\SyncSentinel` |
| Distribution | per-user installer + portable self-contained `.exe` |

Windows-only by design (robocopy).

## Building from source

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

## License

[MIT](LICENSE) © 2026 Akhila Abesinghe
