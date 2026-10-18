#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{B7C2E8A4-9D31-4F6A-8C52-1E7A3D9B4F60}
AppName=Guven-ON
AppVersion={#AppVersion}
AppVerName=Guven-ON {#AppVersion}
AppPublisher=GuvenOn
AppPublisherURL=https://github.com/guvenon/guvenon
AppSupportURL=https://github.com/guvenon/guvenon/issues
AppUpdatesURL=https://github.com/guvenon/guvenon/releases
AppCopyright=Copyright (c) 2026 GuvenOn
AppComments=Quiet protection for untrusted networks.
DefaultDirName={autopf}\Guven-ON
DefaultGroupName=Guven-ON
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=admin
MinVersion=10.0.22621
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=GuvenOnSetup-{#AppVersion}
SetupIconFile=..\src\GuvenOn.App\Assets\guvenon.ico
UninstallDisplayIcon={app}\guvenon.exe
UninstallDisplayName=Guven-ON
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductName=Guven-ON
VersionInfoCompany=GuvenOn
VersionInfoDescription=Guven-ON installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.InstallingService=Installing the Guven-ON protection service...
english.RemovingService=Removing the Guven-ON protection service...
english.LaunchProgram=Launch Guven-ON
english.AutostartTask=Start Guven-ON when I sign in

german.InstallingService=Der Guven-ON-Schutzdienst wird installiert...
german.RemovingService=Der Guven-ON-Schutzdienst wird entfernt...
german.LaunchProgram=Guven-ON starten
german.AutostartTask=Guven-ON bei der Anmeldung starten

turkish.InstallingService=Guven-ON koruma servisi kuruluyor...
turkish.RemovingService=Guven-ON koruma servisi kaldırılıyor...
turkish.LaunchProgram=Guven-ON'u başlat
turkish.AutostartTask=Oturum açtığımda Guven-ON'u başlat

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"; Flags: checkedonce
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\publish\svc\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{commonappdata}\GuvenOn"
Name: "{commonappdata}\GuvenOn\logs"

[Icons]
Name: "{autoprograms}\Guven-ON"; Filename: "{app}\guvenon.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Guven-ON"; Filename: "{app}\guvenon.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GuvenOn"; ValueData: """{app}\guvenon.exe"" --tray"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\service\guvenonsvc.exe"; Parameters: "--install"; StatusMsg: "{cm:InstallingService}"; Flags: runhidden waituntilterminated
Filename: "{app}\guvenon.exe"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\service\guvenonsvc.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "GuvenOnService"

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\GuvenOn"
Type: filesandordirs; Name: "{localappdata}\GuvenOn"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Stop the previous service before replacing its binaries on upgrade.
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop GuvenOnSvc', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    Sleep(1500);
  end;
end;

