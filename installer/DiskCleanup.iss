; Inno Setup скрипт за Disk Cleanup
; Версията идва от GitHub Actions (/DMyAppVersion=1.0.0).
; За ръчен build на твоя компютър: ISCC.exe /DMyAppVersion=1.0.0 installer\DiskCleanup.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Disk Cleanup"
#define MyAppPublisher "Радослав Чакъров"
#define MyAppURL "https://github.com/waitingboy-collab/disk-cleanup"
#define MyAppExeName "DiskCleanerGui.exe"

[Setup]
; AppId е уникален за тази програма. НЕ го сменяй след първия release,
; иначе ъпдейтите ще се инсталират като отделна програма.
AppId={{5359F302-37C7-470C-AB1F-6F798E87BFBA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\output
OutputBaseFilename=DiskCleanup-Setup
; Ако имаш икона, сложи я в installer\app.ico и махни ";" отпред:
; SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Инсталира без админ права. Самата програма има бутон
; „Рестартирай като администратор“ за чистене на системни папки.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "bulgarian"; MessagesFile: "compiler:Languages\Bulgarian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Взима всичко от папката, в която dotnet publish е сложил програмата.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
