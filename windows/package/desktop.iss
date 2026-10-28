#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\layout\App"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif
#ifdef UpdaterLab
  #define AppIdValue "LIMOD.XBar.Desktop.UpdaterLab"
  #define InstallFolder "X-Bar Updater Lab"
#else
  #define AppIdValue "LIMOD.XBar.Desktop"
  #define InstallFolder "X-Bar"
#endif

[Setup]
AppId={#AppIdValue}
AppName=X-Bar
AppVersion={#AppVersion}
AppPublisher=LIMOD
AppPublisherURL=https://github.com/yooongZa/xbar
AppSupportURL=https://github.com/yooongZa/xbar/issues
DefaultDirName={localappdata}\Programs\{#InstallFolder}
DefaultGroupName={#InstallFolder}
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=XBar-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\XBar.exe
VersionInfoVersion={#AppVersion}
VersionInfoProductName=X-Bar
VersionInfoCompany=LIMOD
VersionInfoProductVersion={#AppVersion}
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=*.exe,*.dll
SetupLogging=yes
LicenseFile=..\..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-UPDATER-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

#ifndef UpdaterLab
[Icons]
Name: "{userprograms}\X-Bar"; Filename: "{app}\XBar.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\xbar"; ValueType: string; ValueData: "URL:X-Bar"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\xbar"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\xbar\DefaultIcon"; ValueType: string; ValueData: "{app}\XBar.exe,0"
Root: HKCU; Subkey: "Software\Classes\xbar\shell\open\command"; ValueType: string; ValueData: """{app}\XBar.exe"" ""%1"""
; The application owns the startup choice. Installation and updates never enable it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "X-Bar"; Flags: uninsdeletevalue
#endif

[Run]
Filename: "{app}\XBar.exe"; Description: "X-Bar"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\XBar.exe"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "CloseXBar"

; User data in LocalAppData\XBar is intentionally preserved on uninstall.
