# Context — SyncSentinel

A Windows tray app that mirrors local dev folders to OneDrive-synced destinations
on a per-job schedule, using robocopy as the copy engine. This file is the
ubiquitous language — use these terms exactly in code, issues, and docs.

## Glossary

- **Job** — one backup unit: a `source` folder mirrored to a `destination`, with a
  set of attached exclusion sets, an interval, editable robocopy flags, and an
  enabled/paused state. The `UserSecrets` backup is modeled as an ordinary job, not
  a special case.
- **FolderExclusionSet** — a named, reusable list of directory names to exclude
  (e.g. `DeveloperDefaults = bin, obj, node_modules, …, .vs`). Feeds robocopy `/XD`.
- **FileExclusionSet** — a named, reusable list of file patterns to exclude
  (e.g. `Binaries = *.dll, *.exe, *.pdb`). Feeds robocopy `/XF`.
- **Composable** — a job may attach **any number** of folder-sets and file-sets;
  the effective excludes are the **union** of all attached sets.
- **Effective command** — the full robocopy invocation the app assembles for a job:
  `robocopy <source> <destination> <flags> /XD <∪ folder-sets> /XF <∪ file-sets>`.
  Shown read-only as a live preview in the job editor.
- **Behavior flags** — the editable robocopy options (`/MIR /XJ /R /W /FFT …`). A
  global **default flags** value applies unless a job sets a **flags override**.
  Everything else (source, dest, `/XD`, `/XF`) is composed by the app, not typed.
- **Run** — a single execution of a job. Has a status, start/end time, duration,
  parsed robocopy counts (copied / skipped / extras / failed), exit code, and a
  `.log` file. Run metadata is recorded in the history store.
- **Run status** — `Idle` · `Queued` · `Running` · `Success` · `Warning` (extras /
  non-fatal) · `Error`.
- **Scheduler** — the in-process timer set per job. The interval is anchored to the
  **last finish** (not wall-clock), so a slow run never causes a pileup. A job never
  overlaps itself.
- **Run queue** — the single global FIFO queue. Only **one** robocopy runs at a
  time; other due jobs wait. **Run now** enqueues immediately / jumps the queue.
- **Tray shell** — the thin WinForms host: system-tray icon + the WebView2 window.
  Closing the window hides to tray (scheduling continues); exit is via the tray menu.
- **Autostart** — whether SyncSentinel launches automatically on Windows login, as a
  per-user setting (no admin needed). Toggled in Settings; the change takes effect
  **immediately**, not only at next launch, and the saved preference is reconciled with
  the system on every startup.
- **Loopback server** — the in-process ASP.NET Core (Kestrel) server bound to
  `127.0.0.1` on a random free port; never network-exposed. Serves the REST API,
  the SignalR hub, and the embedded React assets.

## Decisions

Architecture and the alternatives weighed are recorded in
[`docs/adr/0001-architecture.md`](docs/adr/0001-architecture.md). Record new
cross-cutting decisions as further ADRs under `docs/adr/`.
