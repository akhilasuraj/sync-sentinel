# ADR-0001: Tray app — .NET backend + React/WebView2 UI, robocopy engine

- **Status:** Accepted (2026-06-26)
- **Deciders:** Akhila Abesinghe

## Context

SyncSentinel generalizes a working PowerShell/robocopy backup into a UI-driven,
scheduled, multi-job desktop tool. Hard requirements shaped the choice:

- **Live per-job status + streaming logs + history** (feature 6) — the app must own
  and monitor the running process; a detached scheduler (Windows Task Scheduler)
  can't stream a running job's output or report live status cleanly.
- **Per-job intervals** (feature 5) — an always-present scheduler.
- **robocopy as the engine** (feature 4) — Windows-only by nature.
- **Modern, elegant UI; tiny footprint; single `.exe`.**
- **Builder's skills:** strong C#/.NET (backend microservices); also JS/TS/React.
  **No** WPF/XAML experience. Open-source / portfolio piece.

## Decision

A single-process, login-autostart **system-tray app**:

- **Run model — tray app with its own scheduler.** Autostarts on login, lives in the
  tray, runs an in-process scheduler, and spawns/monitors robocopy directly. Runs
  while logged in — exactly when dev files change.
- **Stack — .NET 10 (LTS) backend + React/TS UI in the system WebView2.** A thin
  WinForms shell provides the tray icon and hosts a WebView2 control pointed at an
  in-process **ASP.NET Core (Kestrel)** server bound to `127.0.0.1` on a random port.
  **REST** for CRUD; a **SignalR** hub pushes live status + log lines. The React build
  is embedded as static files. Ships as a single self-contained `.exe`. (Target
  versions for the whole stack — .NET 10 LTS, Node 24 LTS, React 19, etc. — are pinned
  in DESIGN.md §2.1, verified current as of June 2026.)
- **Engine — robocopy via `System.Diagnostics.Process`**, stdout/stderr captured
  directly (no `/LOG`/`/TEE`), streamed to the UI and written to a per-run `.log`.
- **Scheduling — interval anchored to last finish, no self-overlap, global FIFO
  serialization** (one robocopy at a time).
- **Persistence — `config.json` + SQLite run history** under `%APPDATA%\SyncSentinel`.
- **Lifecycle — HKCU `Run` autostart, close-to-tray, single-instance mutex.**

## Alternatives considered

- **Config UI → Windows Task Scheduler.** Reuses today's mechanism, but jobs run
  detached, so live status/log streaming (feature 6) is compromised. Rejected.
- **Windows Service + separate UI.** Runs 24/7 even logged out, but two processes +
  IPC + service install is far heavier; dev files only change while logged in.
  Rejected as over-engineered for the need.
- **Electron + React.** Familiar React UI, but bundles ~150 MB of Chromium and is
  RAM-heavy — fails the tiny-footprint goal; also abandons the C# skillset for the
  hard backend logic. Rejected.
- **Tauri v2 + React.** Smallest footprint and trendy, but the backend (scheduler,
  process control, parsing) would be in **Rust**, a new language — and that backend
  is where most of the work is. Learning-curve risk on a side project. Rejected.
- **WPF / WinUI 3 (native XAML).** Native and integrates well, but the builder has no
  XAML experience and "modern/elegant" is harder to reach there; WinUI's tray +
  unpackaged-deployment story is also rough. Rejected in favor of a web UI the
  builder can style fluently with React.
- **WebView2 in-process bridge (no HTTP server)** instead of Kestrel+SignalR.
  Smaller and portless, but bespoke messaging, harder to dev/test the UI in a normal
  browser, and more hand-rolled streaming. Rejected for the cleaner, testable,
  real-time SignalR architecture.
- **Blazor Hybrid (all C#).** One language end-to-end, but skips the builder's React
  strength and produces a heavier exe. Viable; not chosen.

## Consequences

- **Gain:** uses both of the builder's strengths (C# backend + React UI); tiny RAM
  (system WebView2, no bundled Chromium); real-time status/logs via SignalR; a
  testable, standard architecture; a strong full-stack portfolio piece.
- **Cost/risk:** a loopback HTTP server + random port (bound to `127.0.0.1` only,
  never network-exposed); a JS build pipeline alongside the .NET build; embedding the
  React build into the exe; depends on the WebView2 runtime (evergreen on Win10/11).
- **Windows-only** is intentional (robocopy); no cross-platform path.
- The destructive `/MIR` purge gives destination-root reconciliation (no orphaned old
  locations) — the property that motivated the robocopy approach in `project-backup`.
