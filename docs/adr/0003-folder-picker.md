# ADR-0003: Folder picker via a native dialog behind a shell seam

- **Status:** Accepted (2026-06-27)
- **Deciders:** Akhila Abesinghe

## Context

A job's **source** and **destination** are absolute Windows paths, entered today as
free text in the job editor (`JobEditor.tsx`). Typing absolute paths is error-prone;
users want a **folder picker**. But the UI is React running inside **WebView2**,
talking to the in-process **loopback ASP.NET Core** server (ADR-0001) — and a browser
cannot hand a web app the absolute path of a folder chosen by a native picker (the
`webkitdirectory` input only yields relative file lists). So "add a folder picker"
forces a decision about *how* a sandboxed web UI obtains a real filesystem path.

## Decision

- **Native Windows folder dialog reached through a Core seam.** Add an `IFolderPicker`
  interface in Core with `Task<string?> PickFolderAsync(string? initialPath, string?
  title)` (selected absolute path, or `null` on cancel) and `bool Available`. The
  default `NoOpFolderPicker` (registered in `ApiHost`, `Available => false`) is used by
  tests and the dev browser; the **tray shell** registers the real implementation
  (`Available => true`). This is the **same seam pattern already used for
  `IAutostart`** — Core stays free of Windows-only/GUI types, and the HTTP surface
  stays testable.
- **Endpoints.**
  - `GET /api/capabilities` → `{ folderPicker: picker.Available }`. The React UI renders
    the Browse affordance **only when true**, so there is no dead button in the dev
    browser or tests.
  - `POST /api/pick-folder` `{ initialPath?, title? }` → `200 { path }` on selection,
    `204` on cancel, `501` when no real picker is registered (defensive; the UI won't
    call it when the capability flag is false).
- **Shell implementation.** Marshals to the WinForms UI thread
  (`BeginInvoke` + a `TaskCompletionSource` the awaiting request thread observes) and
  shows **`FolderBrowserDialog`** — the WinForms folder picker, which uses the modern
  Vista `IFileDialog` under the hood on .NET Core 3.0+ (WinForms has no
  `OpenFolderDialog`; that type is WPF/WinUI). Seeded with `InitialDirectory` = the
  current field value when it exists, titled via `Description` + `UseDescriptionForTitle`
  ("Select source folder" / "Select destination folder"), with `ShowNewFolderButton` so
  a destination can be created.
- **Fields stay editable.** Browse *augments* the text inputs; it does not replace
  them. Typing/pasting still works (power users, a not-yet-created destination, and the
  dev browser where no native dialog exists).
- **Lightweight validity hint.** `GET /api/path-exists?path=` backs a subtle, debounced
  per-field indicator with path-specific semantics: a missing **source** is a *warning*
  (you can't back up a folder that isn't there); a missing **destination** is
  *informational* ("will be created", since robocopy creates it). Non-blocking — it
  never prevents saving.
- **UI** gets a deliberate design pass (the `frontend-design` skill) to shape the
  path inputs (icon + Browse + the hint) rather than a bolted-on button.

## Alternatives considered

- **Custom backend directory browser** (`GET /api/fs?path=` + an in-app folder tree).
  Fully architecture-consistent and identical in dev-browser and WebView2, but
  significantly more work and it reimplements what the Windows dialog already does well
  (drives, UNC, OneDrive, permissions, special folders, "new folder"). Rejected as
  over-built for the need; the native dialog is less code and better UX.
- **WebView2 host-object / `WebMessage` bridge** calling a native dialog from JS,
  bypassing HTTP. This is exactly the bespoke host-messaging ADR-0001 rejected — it
  breaks dev-in-browser and the testable HTTP surface. Rejected.
- **Picker-only (read-only) fields.** Matches "instead of typing" literally but blocks
  pasting a path, blocks specifying a destination that doesn't exist yet, and makes
  paths unsettable in the dev browser. Rejected for editable + Browse.

## Consequences

- **Gain:** native, familiar folder selection with zero edge-case handling of our own;
  Core stays GUI-free behind a tested seam (the endpoints are unit-tested with a fake
  `IFolderPicker`, `path-exists` with a real scratch filesystem); the dev-in-browser and
  test workflows are unaffected (capability flag false → no Browse, typing still works).
- **Cost/risk:** the picker is **shell-only** — unavailable in the dev browser by
  design (mitigated by retaining typing); a new `IFolderPicker` shell implementation is
  GUI glue verified by `--smoke` + a manual run, not unit tests (like `AutostartManager`).
- Establishes a reusable shape — **a shell-only capability exposed through an HTTP
  endpoint + a capability flag** — that future native features (e.g. toasts) can follow.
