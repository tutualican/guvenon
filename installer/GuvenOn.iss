#ifndef AppVersion
  #define AppVersion "0.1.2"
#endif

[Setup]
AppId={{B7C2E8A4-9D31-4F6A-8C52-1E7A3D9B4F60}
AppName=Guven-ON
AppVersion={#AppVersion}
AppVerName=Guven-ON {#AppVersion}
AppPublisher=GuvenOn
AppPublisherURL=https://github.com/tutualican/guvenon
AppSupportURL=https://github.com/tutualican/guvenon/issues
AppUpdatesURL=https://github.com/tutualican/guvenon/releases
AppCopyright=Copyright (c) 2026 GuvenOn
AppComments=Quiet protection for untrusted networks.
LicenseFile=..\LICENSE
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
UsePreviousLanguage=no
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}.0
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
english.ServiceInstallFailed=The protection service could not be installed. Setup will roll back.
english.LaunchProgram=Launch Guven-ON
english.AutostartTask=Start Guven-ON when I sign in

german.InstallingService=Der Guven-ON-Schutzdienst wird installiert...
german.RemovingService=Der Guven-ON-Schutzdienst wird entfernt...
german.ServiceInstallFailed=Der Guven-ON-Schutzdienst konnte nicht installiert werden. Die Installation wird zurückgerollt.
german.LaunchProgram=Guven-ON starten
german.AutostartTask=Guven-ON bei der Anmeldung starten

turkish.InstallingService=Guven-ON koruma servisi kuruluyor...
turkish.RemovingService=Guven-ON koruma servisi kaldırılıyor...
turkish.ServiceInstallFailed=Guven-ON koruma servisi kurulamadı. Kurulum geri alınacak.
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

[Run]
; The helper writes HKCU and the per-user settings file as the signed-in user,
; even when setup itself was elevated with a different admin account.
Filename: "{app}\guvenon.exe"; Parameters: "--enable-autostart"; Flags: runhidden waituntilterminated runasoriginaluser; Tasks: autostart
Filename: "{app}\guvenon.exe"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{app}\service\guvenonsvc.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "GuvenOnService"
Filename: "{sys}\sc.exe"; Parameters: "stop GuvenOnSvc"; Flags: runhidden waituntilterminated; RunOnceId: "GuvenOnServiceStopFallback"
Filename: "{sys}\sc.exe"; Parameters: "delete GuvenOnSvc"; Flags: runhidden waituntilterminated; RunOnceId: "GuvenOnServiceDeleteFallback"
; Inno only allows runasoriginaluser in [Run], not [UninstallRun]. The
; uninstaller therefore asks the current profile to clean up; on a standard
; UAC consent this is the same user who installed the app.
Filename: "{app}\guvenon.exe"; Parameters: "--cleanup-user"; Flags: runhidden waituntilterminated runascurrentuser; RunOnceId: "GuvenOnUserCleanup"

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\GuvenOn"

[Code]
procedure InstallService;
var
  ResultCode: Integer;
begin
  if (not Exec(ExpandConstant('{app}\service\guvenonsvc.exe'), '--install', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode)) or (ResultCode <> 0) then
  begin
    // The helper may have created the service before failing to start it.
    // Remove it before setup rolls the files back.
    Exec(ExpandConstant('{app}\service\guvenonsvc.exe'), '--uninstall', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    MsgBox(ExpandConstant('{cm:ServiceInstallFailed}'), mbError, MB_OK);
    Abort;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Use the SCM directly. The service binary in {app} belongs to the version
    // being replaced and must not be executed with a newer command-line switch.
    // InstallService() waits for any pending stop before it starts the new one.
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop GuvenOnSvc', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    Sleep(2500);
  end
  else if CurStep = ssPostInstall then
  begin
    InstallService;
  end;
end;
