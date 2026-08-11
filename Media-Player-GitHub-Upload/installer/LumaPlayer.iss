#ifndef MyAppVersion
  #define MyAppVersion "0.6.0"
#endif

#define MyAppName "Luma Player"
#define MyAppPublisher "Luma Player"
#define MyAppExeName "LumaPlayer.exe"

[Setup]
AppId={{B06AF750-429E-4F1E-9E16-26E708871641}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
DefaultDirName={localappdata}\Programs\Luma Player
DefaultGroupName=Luma Player
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\InstallerOutput
OutputBaseFilename=LumaPlayer-Offline-Setup-v{#MyAppVersion}
SetupIconFile=..\assets\LumaPlayer.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
MinVersion=10.0.22000

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\Release\LumaPlayer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\mpv-2.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\LumaPlayer.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\mpv-COPYING.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Luma Player"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Luma Player"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Registry]
Root: HKCU; Subkey: "Software\Classes\LumaPlayer.Media"; ValueType: string; ValueName: ""; ValueData: "Luma Player Media File"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\LumaPlayer.Media\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\LumaPlayer.Media\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKCU; Subkey: "Software\Classes\LumaPlayer.Media\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\LumaPlayer.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Luma Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\LumaPlayer.exe"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Applications\LumaPlayer.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\LumaPlayer.exe"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\LumaPlayer.exe"; ValueType: string; ValueName: "Path"; ValueData: "{app}"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Luma Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Lightweight hardware-accelerated video and audio player"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "Luma Player"; ValueData: "Software\LumaPlayer\Capabilities"; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\.mp4\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mkv\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mov\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.avi\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.webm\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m4v\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.wmv\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.flv\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ts\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mts\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m2ts\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mpg\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mpeg\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.vob\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ogv\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.3gp\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.aiff\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.aif\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.alac\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ape\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ac3\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.dts\OpenWithProgids"; ValueType: none; ValueName: "LumaPlayer.Media"; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp4"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mkv"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mov"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avi"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webm"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4v"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wmv"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flv"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ts"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mts"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m2ts"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpg"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpeg"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".vob"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogv"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".3gp"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".opus"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aiff"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aif"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".alac"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ape"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ac3"; ValueData: "LumaPlayer.Media"
Root: HKCU; Subkey: "Software\LumaPlayer\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dts"; ValueData: "LumaPlayer.Media"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Luma Player"; Flags: nowait postinstall skipifsilent
Filename: "ms-settings:defaultapps?registeredAppUser=Luma%20Player"; Description: "Choose Luma Player as the default media app"; Flags: shellexec nowait postinstall skipifsilent
