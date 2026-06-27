; SyncSentinel — Inno Setup installer (per-user, no admin).
;
; Decisions encoded here are recorded in docs/adr/0002-installer-packaging.md:
;   - per-user install to %LOCALAPPDATA%\Programs\SyncSentinel (PrivilegesRequired=lowest)
;   - the APP owns cleanup: the uninstaller invokes `SyncSentinel.exe --uninstall
;     [--purge-data]` rather than duplicating path/registry knowledge here
;   - data removal is prompted (default: remove); the HKCU Run entry is always removed
;     (by the app's --uninstall), never written by this installer
;   - a running instance is stopped gracefully via `--quit` (closing its window only
;     hides to tray), with AppMutex as the fallback
;   - WebView2 runtime: detect-and-warn (no bundling, no auto-download)
;
; Build locally (from the repo root):
;   dotnet publish src/SyncSentinel -c Release -r win-x64 --self-contained -o publish-app
;   ISCC installer\SyncSentinel.iss
; CI passes the version (from the git tag) and the payload dir:
;   ISCC /DAppVersion=1.2.3 /DPayloadDir=<abs path to publish-app> installer\SyncSentinel.iss

#define AppName "SyncSentinel"
#define AppPublisher "Akhila Abesinghe"
#define AppExeName "SyncSentinel.exe"
#define AppUrl "https://github.com/akhilasuraj/sync-sentinel"

; Version is injected by CI from the git tag; default keeps local builds working.
#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif

; The multi-file self-contained publish output. Overridable for CI.
#ifndef PayloadDir
  #define PayloadDir "..\publish-app"
#endif

[Setup]
; A stable AppId keeps upgrades in place (same entry in Apps & features).
AppId={{D5A44EAA-A4D0-4941-BA34-99F503D25C29}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
; Per-user, no admin — matches the app's HKCU autostart + no-admin model.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayIcon={app}\{#AppExeName}
; Stop / detect a running instance via the app's single-instance mutex (fallback
; to the proactive --quit in [Code] for app versions that understand it).
AppMutex=Local\SyncSentinel.SingleInstance
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=SyncSentinel-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[UninstallDelete]
; WebView2 creates its user-data folder (browser cache) next to the exe at
; runtime; Inno never installed it, so it must be removed explicitly. Then drop
; the install dir once it's empty.
Type: filesandordirs; Name: "{app}\{#AppExeName}.WebView2"
Type: dirifempty; Name: "{app}"

[Run]
; Launch WITHOUT --tray so the window shows; the app then reconciles login
; autostart itself (writing the HKCU Run entry per its saved setting).
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]

const
  { Evergreen WebView2 Runtime client GUID (the braces are part of the value,
    not a comment, because they sit inside a string literal). }
  WebView2ClientKey = '{F3017226-FE2A-4295-8BDB-FCE2F9A65F2C}';

{ Signal a running instance to exit gracefully (closing the window only hides to
  tray). No-op when nothing is running, or for an old build that predates --quit. }
procedure QuitRunningApp();
var
  ResultCode: Integer;
begin
  if FileExists(ExpandConstant('{app}\{#AppExeName}')) then
  begin
    Exec(ExpandConstant('{app}\{#AppExeName}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(800); { give Kestrel + the tray icon a moment to release }
  end;
end;

{ WebView2 Runtime registers its version (pv) under HKLM (machine-wide install)
  or HKCU (per-user install). }
function WebView2Installed(): Boolean;
var
  Pv: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WebView2ClientKey, 'pv', Pv) and (Pv <> '') and (Pv <> '0.0.0.0'))
    or
    (RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2ClientKey, 'pv', Pv) and (Pv <> '') and (Pv <> '0.0.0.0'))
    or
    (RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2ClientKey, 'pv', Pv) and (Pv <> '') and (Pv <> '0.0.0.0'));
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not WebView2Installed() then
  begin
    Result := MsgBox(
      'SyncSentinel needs the Microsoft Edge WebView2 Runtime, which was not found on this PC.'#13#10#13#10
      + 'Most Windows 10/11 systems already have it. If the app shows a blank window after install, '
      + 'get the Evergreen runtime from:'#13#10
      + 'https://developer.microsoft.com/microsoft-edge/webview2/'#13#10#13#10
      + 'Continue installing SyncSentinel anyway?',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES;
  end;
end;

{ Stop a running instance before overwriting its files (fresh install: no-op). }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  QuitRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  QuitRunningApp();
  Result := True;
end;

{ Before files are removed (the exe still exists), let the app remove its own
  footprint: the HKCU Run entry always, and the %APPDATA% data on confirmation.
  Best-effort — a failure here does not block the uninstall. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Params: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Params := '--uninstall';
    if MsgBox(
        'Also remove your SyncSentinel settings and run history?'#13#10#13#10
        + 'Choose No to keep them for a future reinstall.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES then
      Params := Params + ' --purge-data';

    if FileExists(ExpandConstant('{app}\{#AppExeName}')) then
      Exec(ExpandConstant('{app}\{#AppExeName}'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
