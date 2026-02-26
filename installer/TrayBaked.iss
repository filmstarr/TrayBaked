; TrayBaked installer (Inno Setup)
; --------------------------------
; This is a lightweight installer that:
;  - Copies the published TrayBaked binaries to Program Files
;  - Shows a normal Windows setup wizard (including install location)
;
; Usage:
; 1. Build / publish TrayBaked (for example):
;      dotnet publish TrayBaked\TrayBaked.csproj -c Release -r win-x64 --self-contained false
; 2. Open this script in Inno Setup and build it to produce TrayBaked-Setup.exe.

#define MyAppName "TrayBaked"
#define MyAppPublisher "TrayBaked"
#define MyAppExeName "TrayBaked.exe"

[Setup]
AppId={{C8B1B191-7F8D-4A1F-83B1-0B8B6B9E7E10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/filmstarr/TrayBaked
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=TrayBaked-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
DisableDirPage=no
DisableProgramGroupPage=no
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Application binaries (published output)
; Adjust path if you publish to a different folder/framework.
Source: "..\publish\TrayBaked\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch TrayBaked after install (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "Launch TrayBaked"; Flags: nowait postinstall skipifsilent
