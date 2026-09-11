# Plezy automatic updater: implementation and Sync Sentinel adaptation

Research date: 2026-09-11. Plezy source was inspected at commit
[`04b0cda922accfbdb510462a3eb96fabccd76002`](https://github.com/edde746/plezy/tree/04b0cda922accfbdb510462a3eb96fabccd76002).

## Executive conclusion

The update window observed in an installed Windows copy of Plezy is not a custom
Flutter dialog. Plezy calls a fork of Flutter's `auto_updater` plugin, which calls
WinSparkle 0.9.2. WinSparkle supplies the native **Install update**, **Skip this
version**, and **Remind me later** UI, downloads the Inno Setup installer, verifies
an Ed25519 signature, closes Plezy, and runs that installer. Plezy's own code mainly
chooses when this path is allowed and points it at an appcast feed.

For Sync Sentinel, use
[`NetSparkleUpdater.UI.WinForms.NetCore`](https://github.com/NetSparkleUpdater/NetSparkle)
(with its core `SparkleUpdater` package) rather than porting Plezy's Flutter bridge
or writing a WinSparkle P/Invoke layer. NetSparkle is a native C# fit for this .NET
10 WinForms shell, has the same three choices, supports `.exe` installers and
Ed25519 verification, and exposes shutdown/install hooks. Its upstream currently
declares .NET 6+ support and its WinForms/Core packages at 3.1.0
([project files](https://github.com/NetSparkleUpdater/NetSparkle/tree/0765d9fc5b060dd6d2aee35a3599dfe10a9ba1f0/src)).

## What Plezy actually does

### Startup flow

1. Release builds enable update checks with
   `--dart-define=ENABLE_UPDATE_CHECK=true`; source/dev builds default to disabled
   ([workflow](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/.github/workflows/build.yml#L32-L42),
   [service](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/update_service.dart#L29-L39)).
2. The main screen schedules an unawaited update check at the end of its first
   post-frame startup work
   ([main screen](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/screens/main_screen.dart#L607-L617)).
3. The persisted `auto_check_updates_on_startup` preference defaults to `true`; if
   it is off, startup returns without checking
   ([setting](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/settings_service.dart#L603-L610),
   [startup branch](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/screens/main_screen.dart#L819-L828)).
4. An installed Windows copy is recognized by `unins000.exe` beside `plezy.exe`.
   Store/MSIX, winget-marked, and portable copies do not use the native installer
   path
   ([platform selection](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/update_service.dart#L34-L47),
   [install detection](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/update_service.dart#L84-L96)).
5. On the native path, Plezy sets the feed URL and calls
   `checkForUpdates(inBackground: true)`. The forked plugin maps this to
   `win_sparkle_check_update_without_ui()`: the check itself is invisible, but an
   available update opens WinSparkle's window
   ([Plezy service](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/update_service.dart#L50-L75),
   [plugin bridge](https://github.com/edde746/auto_updater/blob/9e150f71e17495b7361aedbe6df22e89ad52c254/packages/auto_updater_windows/windows/auto_updater.cpp#L62-L106),
   [WinSparkle API contract](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/include/winsparkle.h#L637-L655)).

This means the installed Windows native path checks on every enabled app startup.
Plezy's six-hour cooldown belongs only to its GitHub-API fallback, not the
WinSparkle call.

### Feed and release pipeline

The native feed is
`https://cdn.jsdelivr.net/gh/edde746/plezy@appcast/appcast.xml`
([constant](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/update_service.dart#L17-L25)).
The feed is an RSS/Sparkle appcast containing:

- an incrementing internal build number (`sparkle:version`) and display version;
- release notes;
- a Windows enclosure pointing to the version-specific GitHub Release installer;
- its byte length and Ed25519 signature;
- `/SILENT /SP-` Inno Setup arguments;
- `sparkle:os="windows"`.

Plezy constructs this appcast in its release workflow
([generation](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/.github/workflows/build.yml#L966-L1034)).
When a release is published, another workflow downloads the attached appcast and
force-publishes it as the sole file on the `appcast` branch; jsDelivr serves that
branch
([publication workflow](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/.github/workflows/update-packages.yml#L113-L139)).

### Choice behavior and persistence

WinSparkle creates the three buttons itself
([UI source](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/src/ui.cpp#L539-L550)):

- **Install update** starts a background download. After signature verification,
  WinSparkle asks the application to shut down and launches the installer with the
  appcast arguments. Plezy's Inno script is upgrade-in-place and contains special
  handling for an old, non-writable machine-wide installation
  ([download/install UI](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/src/ui.cpp#L652-L667),
  [Plezy installer](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/windows/build-installer.ps1#L164-L194)).
- **Skip this version** writes the appcast's internal version to
  `SkipThisVersion`; future background checks suppress exactly that version.
  A manual user-requested check intentionally ignores the skip
  ([write and postpone behavior](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/src/ui.cpp#L633-L648),
  [comparison semantics](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/src/updatechecker.cpp#L269-L295),
  [manual exception](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/src/updatechecker.cpp#L347-L357)).
- **Remind me later** saves no suppression. It aborts this attempt, so Plezy's
  explicit startup check offers the same version on the next launch
  ([WinSparkle source](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/src/ui.cpp#L642-L648)).

WinSparkle's default persistence location is
`HKCU\Software\<CompanyName>\<ProductName>\WinSparkle`
([API documentation](https://github.com/vslavik/winsparkle/blob/9f43e81c1dfcdacd4e8f24a2503d332870ae6ab8/include/winsparkle.h#L234-L256)).
Plezy stamps company `com.edde746` and product `Plezy`, so its native skip/check
state is under `HKCU\Software\com.edde746\Plezy\WinSparkle`
([Windows resources](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/windows/runner/Runner.rc#L77-L114)).

### Non-native fallback is different

Portable Windows and other supported non-native builds call GitHub's
`/repos/edde746/plezy/releases/latest` endpoint. That code records a check before
the request, enforces a six-hour cooldown, compares numeric version components,
and persists a skipped display version in Flutter shared preferences
([fallback implementation](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/services/update_service.dart#L98-L210)).
Its custom Flutter dialog offers **Later**, **Skip this version**, and **View
release**; it opens the GitHub release in a browser and does not install anything
([dialog](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/lib/utils/update_dialog.dart#L9-L64)).

## Packaging and security requirements

- The update target must be an installer capable of replacing the running
  installation. WinSparkle explicitly supports Inno Setup and documents
  `sparkle:installerArguments`; its recommended Inno value is
  `/SILENT /SP- /NOICONS`
  ([official publishing guide](https://winsparkle.org/guides/publishing-updates/)).
- The app needs a trustworthy current version and every feed version must increase.
  Plezy embeds the human version plus an incrementing build number and its plugin
  compares the appcast build number to the executable `FILEVERSION` build component
  ([plugin implementation](https://github.com/edde746/auto_updater/blob/9e150f71e17495b7361aedbe6df22e89ad52c254/packages/auto_updater_windows/windows/auto_updater.cpp#L62-L85)).
- Updates must be signed independently of transport TLS. Plezy embeds the Ed25519
  public key in its executable resources, stores the private key in the
  `SPARKLE_PRIVATE_KEY` Actions secret, signs the installer in CI, and puts the
  signature in the appcast
  ([embedded key](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/windows/runner/Runner.rc#L110-L114),
  [CI signing](https://github.com/edde746/plezy/blob/04b0cda922accfbdb510462a3eb96fabccd76002/.github/workflows/build.yml#L594-L615)).
  WinSparkle says Ed25519 signatures are required to prevent tampering
  ([official project documentation](https://github.com/vslavik/winsparkle#update-signing-ed25519-signatures)).
- Ed25519 update signing is not Windows Authenticode signing. Sync Sentinel may
  remain an unsigned Windows application as documented in
  [ADR-0002](../adr/0002-installer-packaging.md), but SmartScreen's unknown-publisher
  behavior remains. A future code-signing certificate improves publisher identity
  and reputation; it does not replace updater signature verification.
- Native auto-install should be offered only for an installed copy. Plezy uses the
  Inno uninstaller as that signal. A portable copy should instead open the GitHub
  release or explicitly offer conversion to an installed copy.

## Recommended Sync Sentinel design

Sync Sentinel is already unusually well prepared: CI stamps the assembly version,
builds a multi-file payload and a stable-AppId per-user Inno installer, publishes
the installer to GitHub Releases, and the installer already calls `--quit` before
overwriting files ([release workflow](../../.github/workflows/release.yml),
[installer](../../installer/SyncSentinel.iss), [startup/version](../../src/SyncSentinel/Program.cs)).

Implement the following vertical slice:

1. Add the `NetSparkleUpdater.UI.WinForms.NetCore` package to the WinForms shell,
   embed an Ed25519 public key, and create one long-lived `SparkleUpdater`. Use
   `StartLoop(true, true)` after the form exists to force Plezy-like checking on
   every startup. NetSparkle documents that `StartLoop(true)` checks on the first
   idle event, shows an update dialog, and offers ignore/skip, remind later, or
   download/install
   ([upstream quick start](https://github.com/NetSparkleUpdater/NetSparkle/blob/0765d9fc5b060dd6d2aee35a3599dfe10a9ba1f0/README.md#quick-start),
   [WinForms sample](https://github.com/NetSparkleUpdater/NetSparkle/blob/0765d9fc5b060dd6d2aee35a3599dfe10a9ba1f0/src/NetSparkle.Samples.NetCore.WinForms/Form1.cs)).
2. Gate native updates on `unins000.exe` beside `SyncSentinel.exe`. For the portable
   release, provide only a release-page notification initially. This preserves the
   project's two supported packaging modes without silently turning portable into
   installed.
3. Set `CustomInstallerArguments` to at least `/SILENT /SP- /NORESTART` and set
   `RelaunchAfterUpdate = true`. The existing Inno `[Run]` has `skipifsilent`, so
   the updater—not Setup—should relaunch the application. Connect NetSparkle's
   `CloseApplication`/async shutdown event to the same real exit path used by
   `MainForm.ExitApplication`, not ordinary window-close-to-tray. NetSparkle
   documents that the application must exit for the downloaded installer to run
   ([shutdown contract](https://github.com/NetSparkleUpdater/NetSparkle/blob/0765d9fc5b060dd6d2aee35a3599dfe10a9ba1f0/README.md#basic-usage)).
4. Add `CheckForUpdatesOnStartup` (default `true`) to `GlobalSettings`, expose it in
   the React Settings tab, and add a manual **Check for updates** action. A manual
   check should show “up to date” and should be able to reveal a previously skipped
   version; an automatic check should be silent unless an update exists.
5. Prefer NetSparkle's `JSONConfiguration` with an explicit file beneath
   `StoragePaths.Root` over its Windows-default registry configuration. That keeps
   skipped-version/check-time state inside Sync Sentinel's existing data ownership
   and means `--uninstall --purge-data` removes it automatically. NetSparkle
   explicitly supports substituting JSON/custom configuration for its Windows
   registry default
   ([configuration options](https://github.com/NetSparkleUpdater/NetSparkle/blob/0765d9fc5b060dd6d2aee35a3599dfe10a9ba1f0/README.md#other-options)).
6. Extend the release job after `SyncSentinel-Setup.exe` is built: require an
   Ed25519 private-key secret, sign the installer, generate `appcast.xml` and its
   detached signature in strict mode, then upload all three. Use a stable HTTPS
   URL such as `releases/latest/download/appcast.xml`, or copy Plezy's isolated
   `appcast`-branch publication. NetSparkle strict mode verifies both the enclosure
   and the adjacent appcast signature; its official generator handles both
   ([appcast/signing documentation](https://github.com/NetSparkleUpdater/NetSparkle/blob/0765d9fc5b060dd6d2aee35a3599dfe10a9ba1f0/README.md#app-cast)).
7. Fail release publication if signing or feed generation is missing. Publishing a
   new installer without a valid feed/signature should not silently strand or
   weaken installed clients.

Keep update networking and installation in the Windows shell. If testability calls
for a seam, put only an `IUpdateService` contract and update-status DTO in Core;
the NetSparkle adapter belongs beside `Program`/`MainForm`, just as WebView2 and
folder-picker concerns do today.

## Acceptance checks before shipping

- Install version N, publish N+1, launch N, and confirm no UI appears until the
  update-available dialog.
- Verify **Remind me later** causes N+1 to return on the next launch; verify **Skip
  this version** suppresses N+1 but not N+2; verify manual check can reveal N+1.
- Complete an update while Sync Sentinel is in the tray; confirm the real process,
  Kestrel, WebView2, and tray icon exit, the Inno install upgrades the same AppId and
  directory, and the new version relaunches once.
- Confirm `%APPDATA%\SyncSentinel` settings/history and the HKCU autostart preference
  survive upgrade, while uninstall-with-purge removes updater state too.
- Tamper with the installer and appcast independently and confirm strict signature
  verification rejects both.
- Launch the portable executable and confirm it never attempts to overwrite itself
  or run the installed-build update path.
