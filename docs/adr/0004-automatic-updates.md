# ADR-0004: Signed installed updates and notification-only portable updates

- **Status:** Accepted (2026-09-11)
- **Deciders:** Akhila Abesinghe
- **Supersedes:** ADR-0002's decision to defer automatic updates

## Context

SyncSentinel ships both an Inno-installed build and a portable executable. Installed
users expect the app to discover and apply releases in place, while a portable
executable cannot safely replace itself or assume that its containing directory is
an installation. Update installation must also never interrupt queued or running
backup work.

## Decision

- An adjacent Inno `unins000.exe` is the distribution boundary. Only that installed
  distribution may download and execute an installer; all other copies are portable.
- Installed builds use NetSparkle with strict Ed25519 verification. Its WinForms UI
  provides Update, Skip, and Remind Later. The stable public key is compiled into the
  shell; the private key exists only in the repository's `SPARKLE_PRIVATE_KEY` Actions
  secret. Release automation fails unless it can publish the installer, `appcast.xml`,
  and the appcast signature together.
- The installed updater compares a normalized public semantic version and stores its
  JSON check/skip state beneath `StoragePaths.Root`; CI source-revision metadata cannot
  make a release appear newer than itself, and purge owns the updater state.
- Release automation generates GitHub notes before signing a cumulative appcast. Each
  retained release has embedded notes or a version-specific notes link, and publication
  fails if versions, history, ordering, notes, enclosure metadata, or signatures drift.
- The existing Inno installer remains per-user and retains its stable AppId and
  directory. NetSparkle invokes it silently, asks the real tray process to exit, and
  relaunches once. Application data remains under `%APPDATA%\SyncSentinel`, so an
  in-place program upgrade preserves configuration and history.
- Portable builds use Core's `PortableUpdateService`: a GitHub latest-release source,
  JSON state under `StoragePaths.Root`, a 24-hour automatic-check cooldown, and a
  native interaction seam. View Release opens GitHub; no portable action downloads
  or runs an installer.
- `IAppUpdateService` is the API boundary. Core owns check modes, status, portable
  policy, release retrieval, and state persistence. The WinForms shell contains only
  NetSparkle and native-dialog/process-launch adapters.
- Before installed handoff, `RunQueue.TryReserveForUpdate` atomically requires an idle
  queue and prevents new work from entering. Failed handoff releases the reservation;
  a busy queue keeps the app alive and retries discovery once it becomes idle.
- Automatic checks default on, run after shell startup, remain silent for current or
  unreachable feeds, and can be toggled in Settings. Enabling the setting triggers a
  check immediately; user-requested checks bypass portable cooldown and skipped-version
  filtering.

The release artifacts remain without an Authenticode certificate, as documented in
ADR-0002. Ed25519 authenticates artifacts to SyncSentinel, but does not remove the
Windows SmartScreen warning.

## Alternatives considered

- **GitHub release checks for every distribution.** Simple, but leaves secure
  download, signature verification, skip/remind persistence, and installer lifecycle
  to custom code. Rejected for installed builds.
- **Self-replacing portable executable.** Risks partial replacement and cannot infer
  ownership of its directory. Rejected; portable is notification-only.
- **Install immediately after an idle snapshot.** A scheduler enqueue can race the
  snapshot. Rejected in favor of an atomic queue reservation.
- **Pause or cancel backup work.** Updates are less important than user data in flight.
  Rejected; the app defers installation and leaves Jobs and Runs untouched.

## Consequences

- Installed releases have a Plezy-style update experience with cryptographic artifact
  verification and silent in-place upgrades.
- Portable users get update awareness without unsafe mutation.
- A lost `SPARKLE_PRIVATE_KEY` requires a deliberate public-key migration through an
  app release signed by the old key; repository administrators must preserve it.
- NetSparkle and its release-feed generator become release dependencies. The native
  windows remain smoke/manual-test territory, while Core policy, API contracts, queue
  safety, and React interactions are automated.
