#define AppName "AJ Dock"
#define AppPublisher "AJXclusiv"
#define AppExeName "AJDock.exe"

#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef Runtime
#define Runtime "win-x64"
#endif

[Setup]
AppId={{8D4F1C42-0372-4A78-9B7B-8AA884176813}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppMutex=AJDock.SingleInstance
DefaultDirName={localappdata}\Programs\AJ Dock
DefaultGroupName=AJ Dock
DisableProgramGroupPage=yes
LicenseFile=TERMS.txt
OutputDir=..\dist\installer
OutputBaseFilename=AJDockSetup-v{#AppVersion}-{#Runtime}
SetupIconFile=..\src\AJDock.App\Assets\AJDock.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "launchafterinstall"; Description: "Launch AJ Dock after installation"; GroupDescription: "After installation:"; Flags: checkedonce

[Files]
Source: "..\dist\publish\{#Runtime}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AJ Dock"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall AJ Dock"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AJ Dock"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch AJ Dock"; Flags: nowait postinstall skipifsilent; Tasks: launchafterinstall

[Code]
function InitializeUninstall(): Boolean;
begin
  Result := MsgBox(
    'Uninstall AJ Dock?'#13#13 +
    'The application files and shortcuts will be removed. Your personal dock settings may remain in your Windows user profile so future builds can reuse them.',
    mbConfirmation,
    MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox(
      'AJ Dock has been uninstalled.'#13#13 +
      'If AJ Dock was hiding the Windows taskbar, restart Windows Explorer or sign out and back in if the taskbar does not reappear automatically.',
      mbInformation,
      MB_OK);
  end;
end;
