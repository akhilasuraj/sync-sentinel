# Release checklist

The release workflow builds and validates the portable executable, installed
payload, signed cumulative appcast, and shared GitHub/NetSparkle release notes.
It stops before publication if their versions disagree or the feed loses notes,
history, ordering, or signatures.

Before pushing a release tag:

1. Run `npm --prefix src/web test` and `npm --prefix src/web run build`.
2. Run `dotnet test SyncSentinel.slnx -c Release` and
   `dotnet run --project src/SyncSentinel -c Release --no-build -- --smoke`.
3. Install the current release N using `SyncSentinel-Setup.exe` and keep at least
   one configured Job plus run history.
4. Build a candidate N+1 installer with the same Inno AppId and installation
   directory. Point a test appcast at it using the production public version
   format and signing process.
5. Start N in both visible and tray modes. Confirm no progress UI appears before
   update discovery and that the dialog includes the complete notes from N+1.
6. With a Job Running or Queued, choose **Update**. Confirm SyncSentinel remains
   alive, explains the deferral, retains the update, and does not reorder or
   cancel work.
7. Let the Run Queue become idle and install. Confirm the old process, Kestrel,
   WebView2, and tray icon exit; Inno upgrades the existing directory; settings,
   history, and autostart survive; and SyncSentinel relaunches exactly once. On
   Windows, also confirm NetSparkle cached the installer with an `.exe` extension
   and that Apps & features reports the N+1 `DisplayVersion` after Inno exits.
8. On the relaunched N+1 build, run an automatic and manual check. Both must
   report the application current and must not offer N+1 again.
9. Confirm **Remind me later** allows N+1 to return on the next eligible check,
   **Skip this version** suppresses N+1 automatically, and a manual check can
   reveal the skipped release.
10. Run the portable artifact and confirm it only offers to open the GitHub
    release—it must never download or execute the installer.
11. Tamper independently with a test installer and appcast and confirm strict
    Ed25519 verification rejects both.

After the workflow publishes, inspect the GitHub release and downloaded
`appcast.xml`: the release body and current feed item must describe the same
changes, and the appcast must retain every earlier supported release item.
