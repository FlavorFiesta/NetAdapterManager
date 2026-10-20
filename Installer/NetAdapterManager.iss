#define AppName "网络适配器管理器"
#ifndef AppVersion
#define AppVersion "1.4.11"
#endif
#define AppPublisher "NetAdapterManager"
#define AppExeName "NetAdapterManager.exe"
#define AppId "{8B0F4E6F-7A2E-4C0D-9B7A-5E8A6C2D1F40}"
#ifndef SourceRoot
#define SourceRoot "..\artifacts\1.4.11\publish"
#endif
#ifndef OutputRoot
#define OutputRoot "..\artifacts\1.4.11"
#endif

[Setup]
AppId={{#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\NetAdapterManager
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
Uninstallable=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
OutputDir={#OutputRoot}
OutputBaseFilename=NetAdapterManager-v{#AppVersion}-Setup-win-x64
SetupIconFile=..\Assets\AppIcon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
ChangesAssociations=no
CloseApplications=yes
RestartApplications=no
; 保留安装目录中由程序产生的 JSON 文件，升级时不删除用户数据。

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
Name: "{app}\data"

[Icons]
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"

[Run]
; 主程序声明 requireAdministrator；用 ShellExecute 触发标准 UAC，避免 CreateProcess 错误 740。
Filename: "{app}\{#AppExeName}"; Description: "启动{#AppName}"; Flags: shellexec nowait postinstall skipifsilent
