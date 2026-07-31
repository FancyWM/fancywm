; Inno Setup script for FancyWM. Unsigned by design: this fork has no code-signing
; certificate. An unsigned classic installer only trips a SmartScreen warning the user
; can dismiss, unlike an unsigned MSIX, which Windows refuses to install outright.
;
; Build:  iscc /DSourceDir=<publish dir> /DAppVersion=<x.y.z> installer\FancyWM.iss

#ifndef SourceDir
  #error SourceDir must be defined, e.g. /DSourceDir=..\publish\installer
#endif

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef OutputDir
  #define OutputDir "..\publish"
#endif

#define AppName "FancyWM"
#define AppExeName "FancyWM-GUI.exe"
#define AppPublisher "purr"
#define AppURL "https://github.com/purr/fancywm"

[Setup]
AppId={{8E9B4C31-6D2A-4F7E-9C5B-1A3D7E2F8B04}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=FancyWM-Setup-x64
SetupIconFile=..\FancyWM\Icon4.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Per-user install: {autopf} resolves to {localappdata}\Programs, so setup never needs
; admin and never shows a UAC prompt. FancyWM is a per-user window manager anyway --
; its single-instance mutex and autostart shortcut are both per-session.
PrivilegesRequired=lowest

; x64 only. RuntimeIdentifiers in FancyWM.GUI.csproj is win-x64.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Matches the mutex in Startup.cs. Setup refuses to overwrite a running instance;
; Restart Manager closes anything still holding the files.
AppMutex=FancyWM.SingleInstance
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Creates {userstartup}\FancyWM.lnk -- the exact path Autostart.IsEnabledLegacyAsync
; probes, so the in-app "run at startup" toggle stays in sync with this checkbox.
Name: "startupicon"; Description: "Start {#AppName} when I sign in"; GroupDescription: "Additional options:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Administrator mode registers a Task Scheduler task (ElevationTask.TaskName, kept in
; sync with the name below) so elevating prompts for UAC only once. Nothing else takes
; it back out, so uninstalling would leave a task pointing at a deleted executable.
;
; Best-effort by design: registering the task needs elevation and so does deleting it,
; but this installer is per-user and the uninstaller normally runs non-elevated — see
; the same caveat on ElevationTask.Unregister. It succeeds when the user happens to run
; the uninstaller elevated and is a no-op otherwise. A surviving task is inert rather
; than harmful: it has no trigger and only ever runs on demand.
;
; Wrapped in `cmd /c ... & exit /b 0` because schtasks exits non-zero both when the task
; was never registered (administrator mode never enabled, the common case) and when the
; delete is denied for lack of elevation. Neither deserves a failure dialog.
Filename: "{cmd}"; Parameters: "/c schtasks /Delete /TN ""{#AppName}"" /F >nul 2>&1 & exit /b 0"; Flags: runhidden; RunOnceId: "RemoveElevationTask"

[UninstallDelete]
; Written by the app at runtime when autostart is toggled on from Settings, so Inno
; does not track it and would otherwise leave it behind.
Type: files; Name: "{userstartup}\{#AppName}.lnk"
