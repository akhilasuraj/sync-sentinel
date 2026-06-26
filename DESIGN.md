# SyncSentinel — Design

The authoritative design, produced from a structured design interview. Terminology
is defined in [`CONTEXT.md`](CONTEXT.md); the headline architecture decision and the
alternatives weighed are in [`docs/adr/0001-architecture.md`](docs/adr/0001-architecture.md).

Origin: this app generalizes a working PowerShell/robocopy backup (the
`project-backup` repo) into a UI-driven, scheduled, multi-job tool.

## 1. Goals

1. Pick source → destination **jobs** in a UI.
2. Define named, reusable **exclusion sets** — folder-sets and file-sets.
3. Attach exclusion sets per job (composable).
4. Keep **robocopy** as the engine, with editable parameters.
5. Per-job backup **interval** (default 15 min).
6. **Live** running/idle status + streaming log per job, plus historical logs.
7. Elegant, modern UI; tiny footprint; single `.exe`; Windows-only.

## 2. Architecture

A login-autostart **tray app**, single process:

```
[login] --autostart(HKCU Run, --tray)--> SyncSentinel.exe
  WinForms tray shell (NotifyIcon)
    └─ WebView2  ──http──> http://127.0.0.1:<random port>
         └─ ASP.NET Core (Kestrel, loopback-only)
              REST   /api/jobs, /api/folder-sets, /api/file-sets, /api/settings, /api/runs
              SignalR hub  ──push──>  live job status + streaming log lines
              static files  ──>  embedded React build
    └─ Scheduler + global FIFO run queue
    └─ Robocopy runner (System.Diagnostics.Process, stdout/stderr captured)
```

- **Backend**: C# / **.NET 10 (LTS)**. Owns the scheduler, the run queue, robocopy
  process control, log capture/streaming, and persistence.
- **Realtime**: SignalR pushes status transitions and log lines to React.
- **UI**: React + TypeScript, built to static files **embedded in the exe**, rendered
  in the system **WebView2** (Edge Chromium already on Win10/11 — no bundled browser).
- **Shell**: a thin WinForms host purely for the tray icon + the WebView2 window.
- **Distribution**: a single self-contained `.exe` (~20–40 MB). No runtime install
  (self-contained .NET); WebView2 runtime is evergreen/preinstalled on Win10/11.

Rationale for this stack over Electron / Tauri / WPF / WinUI: see ADR-0001.

### 2.1 Stack versions (verified current, June 2026)

Only **.NET** and **Node.js** have formal LTS tracks; the rest follow latest-stable.

| Component | Target version | Notes |
|---|---|---|
| .NET (+ ASP.NET Core, SignalR, WinForms) | **10 (LTS)** | Released Nov 2025, supported to **Nov 2028**. .NET 8 *and* 9 both EOL **Nov 2026** — do not start on them. |
| Microsoft.Data.Sqlite | 10.x | Ships in lockstep with .NET 10. |
| WebView2 runtime | evergreen | Preinstalled on Win10/11; auto-updates. |
| React | 19 | Latest stable (since Dec 2024); no official LTS program. |
| TypeScript | 6.0 | Latest stable. **7.0** (Go-native compiler, ~10× faster type-checks) is at RC (Jun 2026) — adopt on GA. |
| Tailwind CSS | 4 (4.3.x) | v4 is the current generation. |
| Vite | 8 | Rolldown (Rust) bundler; current stable. |
| Node.js (build toolchain) | **24 (Active LTS)** | 22 = maintenance; 26 is "Current", becomes LTS Oct 2026. Node moves to one-major/year, all-LTS from Oct 2026. |

Re-verify these at build start — several (TS 7.0, Node 26 LTS) are likely to advance within months.

## 3. Domain model

Stored in `config.json` (see §6).

```jsonc
// Job
{
  "id": "uuid",
  "name": "PEMS",
  "source": "C:\\dev\\PEMS",
  "destination": "C:\\Users\\…\\OneDrive - Geveo Australasia\\dev\\PEMS",
  "folderSetIds": ["developer-defaults"],
  "fileSetIds": [],
  "flagsOverride": null,          // null => use global default flags
  "intervalMinutes": 15,
  "enabled": true
}

// FolderExclusionSet
{ "id": "developer-defaults", "name": "DeveloperDefaults",
  "folders": ["bin","obj","dist","build","out","node_modules","packages",".next","target","vendor","__pycache__",".venv","venv",".vs"] }

// FileExclusionSet
{ "id": "binaries", "name": "Binaries", "patterns": ["*.dll","*.exe","*.pdb"] }

// GlobalSettings
{ "defaultFlags": "/MIR /XJ /R:3 /W:5 /FFT /NP /NFL",
  "defaultIntervalMinutes": 15,
  "maxConcurrent": 1,
  "retention": { "runsPerJob": 100, "days": 30 },
  "autostart": true }
```

**Effective command** assembled per run (the app owns everything except the flags):

```
robocopy "<source>" "<destination>"
         <flagsOverride ?? defaultFlags>
         /XD <union of attached folder-sets>
         /XF <union of attached file-sets>
```

The app captures robocopy's stdout/stderr **directly** via process redirection — it
does **not** use `/LOG`/`/TEE` (those existed only to reach a console). It writes its
own per-run `.log` file from the captured stream and pushes lines over SignalR.
Removing `/MIR` from the flags is allowed but warns (changes mirror/purge semantics).

## 4. Scheduling & execution

- **Interval anchor**: each job's timer starts counting at the **last run's finish**.
  A run that overruns the interval simply pushes the next due time out — no pileup.
- **No self-overlap**: a job is never started while its previous run is active.
- **Global serialization**: a single FIFO **run queue**; `maxConcurrent = 1` by
  default (one robocopy at a time) to spare disk + OneDrive sync. Configurable later.
- **Run now**: enqueues a job immediately (jumps/queues ahead).
- **Pause**: per-job enable/disable; paused jobs don't schedule.
- **Catch-up**: on login/wake, any job whose interval lapsed while the machine was
  off is scheduled to run shortly after start.
- **Runner**: spawn robocopy via `Process`, redirect stdout/stderr, stream lines,
  then parse the summary table → counts + exit code. Robocopy exit codes 0–7 =
  success (8+ = error); the bit-8 "some files could not be copied" maps to `Warning`
  or `Error` per policy.

## 5. UI (React)

- **Dashboard** — job cards: name, status chip, last run (time + counts), next due,
  `Run now` / `Pause`, and a live "running" indicator. The currently-running job is
  highlighted; queued jobs show "queued".
- **Job editor** — source/destination folder pickers; attach folder-sets & file-sets
  (multi-select); interval; flags override (with a **reset to default** and a
  read-only **live command preview**).
- **Exclusion sets manager** — CRUD for folder-sets and file-sets.
- **Live log panel** — streaming lines for the running job (directory-level activity,
  per-file noise off by default). Per-job **history** list → open a past run's `.log`.
- **Settings** — default flags, default interval, retention, autostart toggle.

Visual language: modern, elegant (e.g. Tailwind v4 + a component kit such as
shadcn/ui), light/dark.

## 6. Persistence

Under `%APPDATA%\SyncSentinel\`:

```
config.json                 jobs, exclusion sets, global settings (human-readable)
history.db   (SQLite)       run metadata: jobId, status, start/end, duration,
                            counts, exitCode, logPath
logs\<jobId>\<timestamp>.log   full captured robocopy output per run
```

Retention: keep the last **100 runs** per job **and** **30 days**, pruning older runs
(rows + their `.log` files). Both limits configurable.

## 7. Lifecycle & packaging

- **Autostart**: per-user `HKCU\…\Run` key → `SyncSentinel.exe --tray` (no admin),
  toggleable in Settings.
- **Tray**: start hidden in the tray on login; closing the window hides to tray
  (scheduling continues); exit via the tray menu (Open / Run all / Exit).
- **Single instance**: a named mutex; a second launch focuses the existing window.

## 8. Migration from project-backup

- First run **seeds**: jobs `PEMS` (`C:\dev\PEMS` → OneDrive…\dev\PEMS) and `MyWork`
  (`C:\dev\MyWork` → OneDrive\dev); a `UserSecrets` job
  (`%APPDATA%\Microsoft\UserSecrets` → `C:\dev\PEMS\UserSecrets`); and the
  `DeveloperDefaults` folder-set from today's exclude list.
- **Disable** the existing "Project Backup to OneDrive" Windows scheduled task once
  the app is trusted, to avoid double backups. The `project-backup` scripts remain a
  working fallback.

## 9. Deferred / out of scope (for now)

- **VSS snapshots** to copy locked files (e.g. open Visual Studio's `.vs`) — for now
  `.vs` is simply excluded, as in `project-backup`.
- **Failure notifications** (tray balloon / toast on `Error`).
- **maxConcurrent > 1** (bounded parallel jobs).
- Cross-platform (intentionally out — robocopy is Windows-only).

## 10. Suggested repo layout (when building begins)

```
sync-sentinel/
  src/
    SyncSentinel/            # .NET 10: shell, Kestrel, SignalR, scheduler, runner
    web/                     # React + TS UI (built into the exe)
  docs/adr/
  DESIGN.md  CONTEXT.md  README.md
```
