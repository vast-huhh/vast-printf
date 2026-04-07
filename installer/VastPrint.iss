#define MyAppName "Vast-Print"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Vast-Print"
#define MyAppExeName "VastPrint.App.exe"
#define LibreOfficeSourceDir "C:\Program Files\LibreOffice"
#define VcRedistFile ".\prereqs\VC_redist.x64.exe"

[Setup]
AppId={{7CDBD1D8-233E-46B7-96E5-15BA415E2090}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir=..\artifacts\installer
OutputBaseFilename=Vast-Print-Setup-win-x64
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\VastPrint.App\Assets\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#LibreOfficeSourceDir}\*"; DestDir: "{app}\LibreOffice"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#VcRedistFile}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
