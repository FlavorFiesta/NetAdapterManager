#define AppName "网络适配器管理器"
#ifndef AppVersion
#define AppVersion "1.7.10"
#endif
#define AppPublisher "NetAdapterManager"
#define AppExeName "NetAdapterManager.exe"
#define StartIconName "NetAdapterManager-" + AppVersion + ".ico"
#ifndef AppId
#define AppId "{8B0F4E6F-7A2E-4C0D-9B7A-5E8A6C2D1F40}"
#endif
#ifndef SourceRoot
#define SourceRoot "..\artifacts\" + AppVersion + "\publish"
#endif
#ifndef OutputRoot
#define OutputRoot "..\artifacts\" + AppVersion
#endif

[Setup]
AppId={{#AppId}
AppName={cm:ProductName}
AppVersion={#AppVersion}
AppVerName={cm:ProductName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoDescription=NetAdapterManager Setup
VersionInfoProductName=NetAdapterManager
VersionInfoVersion={#AppVersion}
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
english.StartMenuShortcut=Create a Start menu shortcut
english.DesktopShortcut=Create a desktop shortcut
chinesesimp.ProductName=网络适配器管理器
chinesesimp.LaunchApp=启动网络适配器管理器
chinesesimp.StartMenuShortcut=添加开始菜单快捷方式
chinesesimp.DesktopShortcut=添加桌面快捷方式
chinesetrad.ProductName=網路介面卡管理員
chinesetrad.LaunchApp=啟動網路介面卡管理員
chinesetrad.StartMenuShortcut=新增開始功能表捷徑
chinesetrad.DesktopShortcut=新增桌面捷徑

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
#ifndef SmokeTest
Source: "..\Assets\AppIcon.ico"; DestDir: "{app}\shortcut-icons"; DestName: "{#StartIconName}"; Tasks: startmenuicon; Flags: ignoreversion
#endif

[Dirs]
Name: "{app}\data"

#ifndef SmokeTest
[Tasks]
Name: "startmenuicon"; Description: "{cm:StartMenuShortcut}"
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"

[Icons]
Name: "{userdesktop}\{cm:ProductName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userprograms}\{cm:ProductName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\shortcut-icons\{#StartIconName}"; Tasks: startmenuicon

[InstallDelete]
Type: files; Name: "{userdesktop}\NetAdapterManager.lnk"; Tasks: desktopicon; Check: IsDifferentLanguage('english')
Type: files; Name: "{userprograms}\NetAdapterManager.lnk"; Tasks: startmenuicon; Check: IsDifferentLanguage('english')
Type: files; Name: "{userdesktop}\网络适配器管理器.lnk"; Tasks: desktopicon; Check: IsDifferentLanguage('chinesesimp')
Type: files; Name: "{userprograms}\网络适配器管理器.lnk"; Tasks: startmenuicon; Check: IsDifferentLanguage('chinesesimp')
Type: files; Name: "{userdesktop}\網路介面卡管理員.lnk"; Tasks: desktopicon; Check: IsDifferentLanguage('chinesetrad')
Type: files; Name: "{userprograms}\網路介面卡管理員.lnk"; Tasks: startmenuicon; Check: IsDifferentLanguage('chinesetrad')
Type: files; Name: "{userprograms}\NetAdapterManager\NetAdapterManager.lnk"
Type: dirifempty; Name: "{userprograms}\NetAdapterManager"
Type: files; Name: "{userprograms}\网络适配器管理器\网络适配器管理器.lnk"
Type: dirifempty; Name: "{userprograms}\网络适配器管理器"
#endif

[UninstallDelete]
Type: files; Name: "{app}\install-language.txt"

[Run]
; 主程序声明 requireAdministrator；用 ShellExecute 触发标准 UAC，避免 CreateProcess 错误 740。
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: shellexec nowait postinstall skipifsilent

[Code]
const
  SHCNE_UPDATEITEM = $00002000;
  SHCNF_PATHW_FLUSHNOWAIT = $00002005;

procedure SHChangeNotify(EventId: LongInt; Flags: Cardinal; Item: String; OtherItem: LongInt);
  external 'SHChangeNotify@shell32.dll stdcall';

function IsDifferentLanguage(LanguageName: String): Boolean;
begin
  Result := ActiveLanguage() <> LanguageName;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;
  if not FileExists(ExpandConstant('{app}\settings.json')) then
    if not SaveStringToFile(ExpandConstant('{app}\install-language.txt'), ActiveLanguage(), False) then
      Log('Could not save initial application language. The app will follow the system language.');
#ifndef SmokeTest
  if WizardIsTaskSelected('startmenuicon') then
  begin
    SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW_FLUSHNOWAIT, ExpandConstant('{userprograms}\{cm:ProductName}.lnk'), 0);
    SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW_FLUSHNOWAIT, ExpandConstant('{app}\{#AppExeName}'), 0);
  end;
#endif
end;
