; Inno Setup script for Tiller Network Tool (TNT)
; Build with: iscc /DAppVersion=2.0.0 installer\TNT.iss
; Optional flags:
;   /DSourceDir=..\publish\win-x64   (default shown below)
;   /DIncludeNpcap   bundles installer\redist\npcap-installer.exe and offers to install it
;
; iperf3.exe (and any tools) are picked up automatically if present under <SourceDir>\tools\.

#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

#define AppName "Tiller Network Tool"
#define AppShortName "TNT"
#define AppExe "NetConfigTray.exe"
#define AppPublisher "Tiller"

[Setup]
AppId={{B5D9F7C2-3A4E-4F1B-9C8D-2E6F1A7B3C40}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppShortName}
DefaultGroupName={#AppShortName}
DisableProgramGroupPage=yes
OutputBaseFilename=TNT-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#AppExe}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; Description: "Start {#AppShortName} automatically when I sign in"; GroupDescription: "Startup:"
#ifdef IncludeNpcap
Name: "installnpcap"; Description: "Install Npcap (required for LLDP/CDP switch-port discovery)"; GroupDescription: "Optional drivers:"
#endif

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourceDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Bundled native tools (iperf3, etc.) if present in the publish output.
#if FileExists(AddBackslash(SourceDir) + "tools\iperf3\iperf3.exe")
Source: "{#SourceDir}\tools\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs createallsubdirs
#endif
#ifdef IncludeNpcap
Source: "redist\npcap-installer.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
#endif

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startupicon

[Run]
#ifdef IncludeNpcap
Filename: "{tmp}\npcap-installer.exe"; Description: "Installing Npcap"; StatusMsg: "Installing Npcap capture driver..."; Tasks: installnpcap; Flags: waituntilterminated
#endif
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppShortName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\tools"
