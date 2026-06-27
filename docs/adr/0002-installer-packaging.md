# ADR-0002: Per-user Inno Setup installer + app-owned uninstall cleanup

- **Status:** Accepted (2026-06-27)
- **Deciders:** Akhila Abesinghe

## Context

v0.1.0 ships only as a single self-contained `.exe` attached to a GitHub release —
no installer and no clean uninstall. There is no Start-menu entry, no Add/Remove
Programs entry, and nothing removes the data the app writes to
`%APPDATA%\SyncSentinel` or the `HKCU\…\Run` autostart entry the app creates on
first launch. The goal is a *simple installer* and a *good uninstaller* that removes
the app data and the registry autostart entry.

The app is deliberately **no-admin and per-user** (ADR-0001): autostart is a per-user
`HKCU\…\Run` key, data lives under `%APPDATA%\SyncSentinel`, and WebView2 writes its
user-data folder next to the exe. The installer must not undermine that model.

## Decision

- **Per-user install, no admin** — to `%LOCALAPPDATA%\Programs\SyncSentinel`. Matches
  the existing HKCU-autostart + no-admin model, raises no UAC prompt, and lets the
  uninstaller cleanly own everything for that user. A per-machine (Program Files)
  install could reach neither other users' `%APPDATA%` nor their HKCU Run entries.
- **Inno Setup** builds `SyncSentinel-Setup.exe` (script `installer/SyncSentinel.iss`,
  version injected from the git tag). Free, single-file output, first-class per-user
  installs, and Pascal scripting for the orchestration below.
- **The app owns the cleanup, not the installer script.** A Core
  `UninstallCleaner(StoragePaths, IAutostart)` is the single source of truth for what
  gets removed, reusing `AutostartManager` (registry) and `StoragePaths` (data root) —
  both already injectable and tested. It is exposed on the CLI:
  `SyncSentinel.exe --uninstall` always removes the `HKCU\…\Run` entry, and
  `--purge-data` additionally deletes `%APPDATA%\SyncSentinel` recursively. Inno's
  `[UninstallRun]` invokes it; the installer script duplicates no path or registry
  knowledge and so cannot drift from the app.
- **Data policy — prompt, default to remove.** The uninstaller asks whether to also
  remove settings + run history (defaulting to yes); the registry autostart entry is
  removed *unconditionally* (a stale Run key would point at a deleted exe).
- **Graceful stop via `--quit`.** Closing the window only hides to tray, so a polite
  `WM_CLOSE` won't terminate a running instance. A new `--quit` flag signals the
  running instance through a named event (`Local\SyncSentinel.Quit`, alongside the
  existing Show event); Inno detects the instance via the app's single-instance mutex
  (`AppMutex`) and runs `--quit` before touching files. Both `--quit` and `--uninstall`
  branch early in `Program.cs` (like `--smoke`): no mutex acquisition, no Kestrel, no
  WebView2.
- **The app keeps owning autostart.** The installer does *not* write the Run key; it
  launches the app at finish, and the app reconciles the key from `Settings.Autostart`
  (default true) using `Environment.ProcessPath`. One source of truth.
- **WebView2.** The user-data folder stays next to the exe — inside the per-user
  install dir, writable. It is created at *runtime*, so Inno never recorded installing
  it and won't auto-delete it (Inno only removes files it installed, plus empty dirs);
  the uninstaller therefore removes it explicitly via `[UninstallDelete]`, then drops
  the now-empty install dir. The installer does a registry **detect-and-warn** for the
  Evergreen runtime: if missing, a friendly message + download link instead of a blank
  white window.
- **Ship both artifacts.** Each release attaches the **portable build** (single-file
  `-p:PublishSingleFile=true`, for no-install use) *and* the **installer**, whose
  payload is a normal multi-file self-contained publish — no single-file
  self-extraction, so the installed build starts faster. Both stay self-contained (no
  .NET runtime prerequisite). `release.yml` installs Inno on the windows-latest runner,
  compiles the script, and uploads both.
- **Unsigned, documented.** No code-signing certificate; the README documents the
  SmartScreen "unknown publisher" warning and the *More info → Run anyway* workaround. A
  cert can be wired into `release.yml` later without redesign.

## Alternatives considered

- **Per-machine (Program Files) install.** Needs admin, breaks the no-admin model,
  can't write the next-to-exe WebView2 folder, and can't clean other users' data on
  uninstall. Rejected.
- **WiX / MSI.** Per-user MSI is awkward and custom uninstall actions are verbose;
  heavier than needed for a personal tray app. Rejected.
- **MSIX.** Sandboxing (registry virtualization, restricted filesystem) collides with
  robocopy, arbitrary source/dest paths, and the HKCU Run key; also needs signing.
  Rejected.
- **Velopack / Squirrel.** Nice if auto-update were a goal, but adds an update framework
  beyond "simple"; data purge would still be custom. Rejected (auto-update is deferred).
- **Cleanup logic in the Inno Pascal script.** Zero app changes, but hard-codes the data
  path + Run key in an untested script that drifts from `StoragePaths`/`AutostartManager`.
  Rejected for the testable Core seam.
- **`taskkill /F` to stop the app.** Reliable but ungraceful — interrupts an in-flight
  robocopy run and skips clean Kestrel shutdown. Rejected for graceful `--quit`.
- **Framework-dependent installed build.** ~5–10 MB setup, but reintroduces the .NET
  runtime prerequisite the design deliberately avoided. Rejected.

## Consequences

- **Gain:** a one-click per-user install with Start-menu + Add/Remove-Programs entries; an
  uninstall that removes the program, the autostart entry, and (on request) all data;
  cleanup logic that is unit/integration-tested rather than buried in an opaque installer
  script; faster cold start for the installed build.
- **Cost/risk:** a second packaging path (Inno + two `dotnet publish` invocations) in the
  release pipeline; `Program.cs` grows two early-exit branches (`--quit`, `--uninstall`);
  unsigned binaries still trip SmartScreen until a cert is added.
- The **portable build** remains a first-class, supported artifact — the installer is
  additive, not a replacement.
