; Inno Setup script for DropMyShelf. Build it with installer\build-installer.ps1,
; which publishes the app to bin\publish first and passes the version in.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "DropMyShelf"
#define AppExe "DropMyShelf.exe"
#define AppAuthor "JohanRouyer"
#define AppUrl "https://github.com/JohanRouyer/DropMyShelf"
; The app was called DropMe up to 1.3.2. Upgrades clean up what that name left behind.
#define OldName "DropMe"
#define OldExe "DropMe.exe"

[Setup]
; Unchanged since DropMe, so upgrades replace the old install instead of adding a second one.
AppId={{8F3C2A51-6B7E-4D2A-9C1F-3E5B7A9D0C42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppAuthor}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 {#AppAuthor}
; Shown as a page to accept before installing.
LicenseFile=..\LICENSE.md
; Per-user install: no administrator rights needed.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=..\dist
OutputBaseFilename=DropMyShelf-Setup-{#AppVersion}
SetupIconFile=..\Assets\DropMyShelf.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; DropMyShelf only lives in the tray, so Restart Manager can't close it; [Code] does it instead.
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
StartupGroup=Startup:
StartupTask=Start DropMyShelf when Windows starts

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; The license requires its terms and the copyright notice to travel with every copy.
Source: "..\LICENSE.md"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Left over from versions before the rename to DropMyShelf (1.3.2 and earlier, called DropMe).
Type: files; Name: "{app}\{#OldExe}"
Type: files; Name: "{autoprograms}\{#OldName}.lnk"
Type: files; Name: "{autodesktop}\{#OldName}.lnk"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value the app's own "Start with Windows" menu item toggles.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DropMyShelf"; ValueData: """{app}\{#AppExe}"" --startup"; Tasks: startup
; Its switch in Settings > Apps > Startup, set to on (as Task Manager does).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: binary; ValueName: "DropMyShelf"; ValueData: "02 00 00 00 00 00 00 00 00 00 00 00"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; In-app updates run this installer silently with /update=1: start DropMyShelf again afterwards.
Filename: "{app}\{#AppExe}"; Parameters: "--updated"; Flags: nowait; Check: IsInAppUpdate

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#AppExe} /im {#OldExe}"; Flags: runhidden; RunOnceId: "StopDropMyShelf"

[UninstallDelete]
; History and the images/texts saved from drops (also under the old DropMe name).
Type: filesandordirs; Name: "{userappdata}\DropMyShelf"
Type: filesandordirs; Name: "{%TEMP}\DropMyShelf"
Type: filesandordirs; Name: "{userappdata}\{#OldName}"
Type: filesandordirs; Name: "{%TEMP}\{#OldName}"

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupApprovedKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';

procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExe} /im {#OldExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // A start-with-Windows entry from the DropMe days points at an exe that no longer exists:
  // replace it with one for DropMyShelf.
  if (CurStep = ssPostInstall) and RegValueExists(HKEY_CURRENT_USER, RunKey, '{#OldName}') then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, RunKey, '{#OldName}');
    RegWriteStringValue(HKEY_CURRENT_USER, RunKey, '{#AppName}',
      '"' + ExpandConstant('{app}\{#AppExe}') + '" --startup');
  end;
end;

function IsInAppUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:update|0}') = '1';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // The app may have added itself to startup from its own menu.
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, RunKey, '{#AppName}');
    RegDeleteValue(HKEY_CURRENT_USER, RunKey, '{#OldName}');
    RegDeleteValue(HKEY_CURRENT_USER, StartupApprovedKey, '{#AppName}');
    RegDeleteValue(HKEY_CURRENT_USER, StartupApprovedKey, '{#OldName}');
  end;
end;
