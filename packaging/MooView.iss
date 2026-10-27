#ifndef PublishDir
  #define PublishDir "..\artifacts\app"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\artifacts\release"
#endif
#define AppVersion "1.4.0"
[Setup]
AppId={{D97071AE-3FF5-48D8-93CE-41BC4B769352}
AppName=MooView
AppVersion={#AppVersion}
AppPublisher=vegitaprince
AppPublisherURL=https://github.com/vegitaprince/MooView
AppSupportURL=https://github.com/vegitaprince/MooView/issues
AppUpdatesURL=https://github.com/vegitaprince/MooView/releases
DefaultDirName={localappdata}\Programs\MooView
DefaultGroupName=MooView
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ReleaseDir}
OutputBaseFilename=MooView-Setup-{#AppVersion}-x64
SetupIconFile=..\Source\Assets\MooView.ico
UninstallDisplayIcon={app}\MooView.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
korean.DesktopShortcut=바탕 화면 바로 가기 만들기
english.DefaultApp=Choose MooView as the default image app
korean.DefaultApp=MooView를 기본 이미지 앱으로 선택
[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "test-data\*,self-test.txt,ui-test.txt,preview-*.png,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MooView"; Filename: "{app}\MooView.exe"
Name: "{userdesktop}\MooView"; Filename: "{app}\MooView.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\MooView.exe"; Parameters: "--register-only"; Flags: runhidden waituntilterminated
Filename: "{app}\MooView.exe"; Parameters: "--register"; Description: "{cm:DefaultApp}"; Flags: postinstall skipifsilent unchecked
Filename: "{app}\MooView.exe"; Description: "{cm:LaunchProgram,MooView}"; Flags: postinstall nowait skipifsilent
[UninstallRun]
Filename: "{app}\MooView.exe"; Parameters: "--unregister-only"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterMooView"
