[Setup]
AppName=myit.center Ticket Tool
AppVersion=1.0.0
AppPublisher=myit.center
DefaultDirName={commonpf}\MyitCenter\TrayTicketTool
DefaultGroupName=myit.center
OutputDir=output
OutputBaseFilename=MyitCenter.TrayTicketTool_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes
SetupIconFile=..\src\MyitCenter.TrayTicketTool\Resources\tray-icon.ico

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{commonstartup}\myit.center Ticket Tool"; Filename: "{app}\MyitCenter.TrayTicketTool.exe"; Comment: "myit.center Ticket Tool"

[Run]
Filename: "{app}\MyitCenter.TrayTicketTool.exe"; Description: "Ticket Tool starten"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM MyitCenter.TrayTicketTool.exe"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: files; Name: "{commonstartup}\myit.center Ticket Tool.lnk"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Beende laufende Instanz vor Update
    Exec('taskkill', '/F /IM MyitCenter.TrayTicketTool.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

var
  ResultCode: Integer;
