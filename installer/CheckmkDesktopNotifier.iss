; Checkmk Desktop Notifier — per-user installer (Inno Setup 6).
; Independent open-source project. Not affiliated with Checkmk GmbH.
; Compile from repo root after publish:
;   iscc /DMyAppVersion=1.3.0 installer\CheckmkDesktopNotifier.iss
;
; Version fallback below must match Directory.Build.props <Version>.

#define MyAppName "Checkmk Desktop Notifier"
#ifndef MyAppVersion
  #define MyAppVersion "1.3.0"
#endif
#define MyAppPublisher "TimeWizard007"
#define MyAppURL "https://github.com/TimeWizard007/checkmk-desktop-notifier"
#define MyAppExeName "CheckmkDesktopNotifier.exe"
#define MyAppMutex "Local\TimeWizard007.CheckmkDesktopNotifier"
#define AutostartValueName "CheckmkDesktopNotifier"
#define AutostartSubKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{B7C4E91A-5D2F-4A38-9E16-8F0C3B2A1D47}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppComments=Independent open-source desktop notifier. Not affiliated with Checkmk GmbH.
AppCopyright=Copyright (C) 2026 TimeWizard007
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoDescription=Desktop monitor and notifier for Checkmk
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright=Copyright (C) 2026 TimeWizard007
VersionInfoTextVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\CheckmkDesktopNotifier
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
; Unsigned V1 builds. Add SignTool later; do not ship a self-signed cert as trusted.
; SignTool=signtool
OutputDir=..\artifacts
OutputBaseFilename=CheckmkDesktopNotifier-Setup-x64-v{#MyAppVersion}
SetupIconFile=..\src\CheckmkDesktopNotifier.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
AppMutex={#MyAppMutex}
CloseApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
AllowNoIcons=yes
SetupLogging=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"

[CustomMessages]
en.StartWithWindows=Start Checkmk Desktop Notifier with Windows
pl.StartWithWindows=Uruchamiaj Checkmk Desktop Notifier z systemem Windows
en.CreateDesktopShortcut=Create desktop shortcut
pl.CreateDesktopShortcut=Utwórz skrót na pulpicie
en.LaunchAfterInstall=Launch Checkmk Desktop Notifier
pl.LaunchAfterInstall=Uruchom Checkmk Desktop Notifier
en.RemoveUserData=Remove user settings and monitoring state?%n%nThis deletes saved Settings, Seen state, notification preferences, and the custom sound under LocalAppData, and attempts to remove the Credential Manager secret.%n%nChoose No to keep this data for a later reinstall.
pl.RemoveUserData=Usunąć ustawienia i dane użytkownika?%n%nTo usuwa zapisane Ustawienia, stan Seen, preferencje powiadomień i własny dźwięk w LocalAppData oraz próbuje usunąć sekret z Menedżera poświadczeń.%n%nWybierz Nie, aby zachować dane na później.
en.IndependentNote=This is an independent open-source project and is not affiliated with Checkmk GmbH.
pl.IndependentNote=To niezależny projekt open source, niezwiązany z Checkmk GmbH.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb;config\*;*.json.example;*.local.json"

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{cm:IndependentNote}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{cm:IndependentNote}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent

[Code]
function AutostartCommand: String;
begin
  Result := '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"';
end;

function AutostartExists: Boolean;
begin
  Result := RegValueExists(HKCU, '{#AutostartSubKey}', '{#AutostartValueName}');
end;

procedure ApplyAutostartFromWizard;
begin
  if WizardIsTaskSelected('autostart') then
    RegWriteStringValue(HKCU, '{#AutostartSubKey}', '{#AutostartValueName}', AutostartCommand)
  else
    RegDeleteValue(HKCU, '{#AutostartSubKey}', '{#AutostartValueName}');
end;

procedure RepairAutostartPathIfPresent;
begin
  if AutostartExists then
    RegWriteStringValue(HKCU, '{#AutostartSubKey}', '{#AutostartValueName}', AutostartCommand);
end;

procedure CurPageChanged(CurPageID: Integer);
var
  I: Integer;
begin
  if CurPageID = wpSelectTasks then
  begin
    for I := 0 to WizardForm.TasksList.Items.Count - 1 do
    begin
      if WizardForm.TasksList.ItemCaption[I] = ExpandConstant('{cm:StartWithWindows}') then
        WizardForm.TasksList.Checked[I] := AutostartExists;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardSilent then
      RepairAutostartPathIfPresent
    else
      ApplyAutostartFromWizard;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, '{#AutostartSubKey}', '{#AutostartValueName}');
    if MsgBox(ExpandConstant('{cm:RemoveUserData}'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\CheckmkDesktopNotifier'), True, True, True);
      Exec('cmdkey.exe', '/delete:CheckmkDesktopNotifier', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;
