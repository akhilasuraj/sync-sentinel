# SyncSentinel

A Windows tray app that mirrors local dev folders to OneDrive-synced destinations
on a per-job schedule, using **robocopy** as the copy engine and an **elegant web
UI** in the system tray. Generalizes the `project-backup` PowerShell/robocopy tool
into a UI-driven, scheduled, multi-job application.

Read `CONTEXT.md` for the ubiquitous language and `docs/adr/` for architecture
decisions before working in an area. The full design is in `DESIGN.md`.

## Stack & layout

Single self-contained `.exe`, Windows-only (robocopy). Targets **.NET 10 (LTS)**,
**Node 24 (LTS)**, React 19 / TS 6 / Vite 8 — pinned in `DESIGN.md §2.1`.

```
src/SyncSentinel.Core/    ASP.NET Core wiring + domain logic — NO WinForms, fully unit-testable
src/SyncSentinel/         WinExe shell: WinForms tray + WebView2; hosts Kestrel on 127.0.0.1:<ephemeral>
src/web/                  React + TS + Vite UI; builds into src/SyncSentinel/wwwroot (gitignored)
tests/SyncSentinel.Tests/ xUnit; ASP.NET Core TestServer for API/hub, real-filesystem for the runner
```

Architecture: a single process — WinForms tray shell hosts a WebView2 pointed at an
in-process ASP.NET Core server (loopback only). **REST** for CRUD; a **SignalR** hub
(`/hubs/status`) pushes live job status + log lines to React. The React build is
served as embedded static files. See `docs/adr/0001-architecture.md` for why this
stack over Electron / Tauri / WPF.

### Key types (Core)

- **`ApiHost`** — the shared wiring seam: `ConfigureServices` + `MapEndpoints`. Used
  by both the shell (real Kestrel) and the tests (in-memory TestServer) so both
  exercise identical endpoints/hub.
- **Domain** (`Domain.cs`) — `Job` (persisted: references exclusion sets by id,
  optional flags override, interval, enabled), `FolderExclusionSet`,
  `FileExclusionSet`, `GlobalSettings`, and the `SyncSentinelConfig` aggregate.
- **`BackupJob`** — the *resolved/effective* run inputs (flat source → destination +
  flags + exclude folders/files) the robocopy layer consumes.
- **`ConfigStore`** — load/save `config.json` under `%APPDATA%\SyncSentinel`; first
  run writes the `DefaultConfig` seed.
- **`ConfigService`** — in-memory config owner backed by `ConfigStore`; persisted CRUD
  for jobs + sets, settings updates, and `ResolveJob(id)`.
- **`JobResolver.Resolve`** — flattens a `Job` into a `BackupJob`: union of attached
  folder/file sets → `/XD` / `/XF`, `flagsOverride ?? defaultFlags`. Pure.
- **`RobocopyCommand.Build`** — composes the robocopy arg token list. Pure.
- **`RobocopyResult.FromExitCode`** — maps the bitmapped exit code to
  Success / Warning (bit 8) / Error (bit 16). Pure.
- **`RobocopyRunner`** — spawns robocopy, streams each stdout/stderr line to a
  callback, returns the parsed result.
- **`JobRunCoordinator.RunAsync`** — runs one resolved job to completion, broadcasting
  `runStarted` / `log` / `runFinished` over the `StatusHub`.
- **`Schedule.IsDue`** — pure due-policy: interval anchored to last finish; never-run
  ⇒ due (first run / catch-up); disabled ⇒ never.
- **`RunQueue`** — global FIFO with one running slot; enqueue de-dups (no self-overlap);
  front-enqueue jumps the queue.
- **`Scheduler`** — composes the above: `Tick()` enqueues due jobs, `RunNow(id)` jumps,
  `PumpAsync()` drains one-at-a-time through the executor and records each finish.
  Clock + executor injected for deterministic tests. `QueuePumpService` drains
  continuously (all hosts); `SchedulerTickService` auto-schedules (shell only).

## Build & run

```
# Tests (run from repo root)
dotnet test

# Build the UI into wwwroot (after React changes), then the app
npm --prefix src/web run build
dotnet build

# Run the app (tray + WebView2)
dotnet run --project src/SyncSentinel

# Headless host smoke check (real Kestrel socket + static serving)
dotnet run --project src/SyncSentinel -- --smoke
```

## Development workflow

- **TDD.** Pure logic and the API/hub are driven test-first (RED→GREEN, one behavior
  at a time). The runner is covered by a real-filesystem integration test (real
  robocopy on scratch dirs — no mocking, matching project-backup's philosophy). Only
  the inherently-GUI glue (WinForms/WebView2) is verified by `--smoke` + a real run.
- Build is incremental by phase (walking skeleton → one real run → CRUD → scheduler →
  history → packaging); each phase is a reviewable commit. See `DESIGN.md`.

## Agent skills

### Issue tracker

Issues are tracked in GitHub Issues (`akhilasuraj/sync-sentinel`) via the `gh` CLI; external PRs are not a triage surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Default label vocabulary — `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
