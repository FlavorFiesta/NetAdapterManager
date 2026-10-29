#define AppName "网络适配器管理器"
#ifndef AppVersion
#define AppVersion "1.5.2"
#endif
#define AppPublisher "NetAdapterManager"
#define AppExeName "NetAdapterManager.exe"
#ifndef AppId
#define AppId "{8B0F4E6F-7A2E-4C0D-9B7A-5E8A6C2D1F40}"
#endif
#ifndef SourceRoot
#define SourceRoot "..\artifacts\1.5.2\publish"
#endif
#ifndef OutputRoot
#define OutputRoot "..\artifacts\1.5.2"
#endif

[Setup]
AppId={{#AppId}
AppName={cm:ProductName}
AppVersion={#AppVersion}
AppVerName={cm:ProductName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoDescription=NetAdapterManager Setup
VersionInfoProductName=NetAdapterManager
DefaultDirName={localappdata}\Programs\NetAdapterManager
DefaultGroupName=NetAdapterManager
UsePreviousGroup=no
UninstallDisplayName={cm:ProductName}
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=yes
UsePreviousLanguage=no
Uninstallable=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
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
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "chinesetrad"; MessagesFile: "Languages\ChineseTraditional.isl"

[CustomMessages]
english.ProductName=NetAdapterManager
english.LaunchApp=Launch NetAdapterManager
chinesesimp.ProductName=网络适配器管理器
chinesesimp.LaunchApp=启动网络适配器管理器
chinesetrad.ProductName=網路介面卡管理員
chinesetrad.LaunchApp=啟動網路介面卡管理員

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
Name: "{app}\data"

#ifndef SmokeTest
[Icons]
Name: "{userdesktop}\NetAdapterManager"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\NetAdapterManager"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"

[InstallDelete]
Type: files; Name: "{userdesktop}\网络适配器管理器.lnk"
Type: files; Name: "{userprograms}\网络适配器管理器\网络适配器管理器.lnk"
Type: dirifempty; Name: "{userprograms}\网络适配器管理器"
#endif

[UninstallDelete]
Type: files; Name: "{app}\install-language.txt"

[Run]
; 主程序声明 requireAdministrator；用 ShellExecute 触发标准 UAC，避免 CreateProcess 错误 740。
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: shellexec nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not FileExists(ExpandConstant('{app}\settings.json')) then
    if not SaveStringToFile(ExpandConstant('{app}\install-language.txt'), ActiveLanguage(), False) then
      Log('Could not save initial application language. The app will follow the system language.');
end;
