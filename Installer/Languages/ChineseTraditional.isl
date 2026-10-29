; NetAdapterManager Traditional Chinese adaptation, 2026-10-28.
; Derived from the MIT-licensed ChineseSimplified.isl bundled in this repository.
; Original attribution is retained below; see licenses/inno-chinese-translation/LICENSE.
; Script conversion and regional terminology changes by NetAdapterManager contributors.
; *** Inno Setup version 6.5.0+ Chinese Simplified messages ***
;
; To download user-contributed translations of this file, go to:
;   https://jrsoftware.org/files/istrans/
;
; Note: When translating this text, do not add periods (.) to the end of
; messages that didn't have them already, because on those messages Inno
; Setup adds the periods automatically (appending a period would result in
; two periods being displayed).
;
; Maintainer: Zhenghan Yang (Kira)
; Email: 847320916@QQ.com
; Github: https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation
; Encoding: UTF-8
; Translation based on network resource
;

[LangOptions]
; The following three entries are very important. Be sure to read and
; understand the '[LangOptions] section' topic in the help file.
LanguageName=繁體中文
; About LanguageID, to reference link:
; https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c
LanguageID=$0404
; LanguageCodePage should always be set if possible, even if this file is Unicode
; For English it's set to zero anyway because English only uses ASCII characters
LanguageCodePage=950
; If the language you are translating to requires special font faces or
; sizes, uncomment any of the following entries and change them accordingly.
;DialogFontName=
;DialogFontSize=9
;DialogFontBaseScaleWidth=7
;DialogFontBaseScaleHeight=15
;WelcomeFontName=Segoe UI
;WelcomeFontSize=14

[Messages]

; *** Application titles
SetupAppTitle=安裝
SetupWindowTitle=安裝 - %1
UninstallAppTitle=卸載
UninstallAppFullTitle=%1 卸載

; *** Misc. common
InformationTitle=信息
ConfirmTitle=確認
ErrorTitle=錯誤

; *** SetupLdr messages
SetupLdrStartupMessage=現在將安裝 %1。您想要繼續嗎？
LdrCannotCreateTemp=無法創建臨時檔案。安裝程序已中止
LdrCannotExecTemp=無法執行臨時目錄中的檔案。安裝程序已中止
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1。%n%n錯誤 %2: %3
SetupFileMissing=安裝目錄中缺少檔案 %1。請修正這個問題或者獲取程序的新副本。
SetupFileCorrupt=安裝檔案已損壞。請獲取程序的新副本。
SetupFileCorruptOrWrongVer=安裝檔案已損壞，或是與這個安裝程序的版本不兼容。請修正這個問題或獲取新的程序副本。
InvalidParameter=無效的命令行參數：%n%n%1
SetupAlreadyRunning=安裝程序已在運行。
WindowsVersionNotSupported=此程序不支持當前電腦運行的 Windows 版本。
WindowsServicePackRequired=此程序需要 %1 服務包 %2 或更高版本。
NotOnThisPlatform=此程序不能在 %1 上運行。
OnlyOnThisPlatform=此程序只能在 %1 上運行。
OnlyOnTheseArchitectures=此程序只能安裝到為下列處理器架構設計的 Windows 版本中：%n%n%1
WinVersionTooLowError=此程序需要 %1 版本 %2 或更高。
WinVersionTooHighError=此程序不能安裝于 %1 版本 %2 或更高。
AdminPrivilegesRequired=在安裝此程序時您必須以管理員身份登錄。
PowerUserPrivilegesRequired=在安裝此程序時您必須以管理員身份或高級用戶組身份登錄。
SetupAppRunningError=安裝程序檢測到 %1 當前正在運行。%n%n請先關閉正在運行的程序，然后點擊“確定”繼續，或點擊“取消”退出。
UninstallAppRunningError=卸載程序檢測到 %1 當前正在運行。%n%n請先關閉正在運行的程序，然后點擊“確定”繼續，或點擊“取消”退出。

; *** Startup questions
PrivilegesRequiredOverrideTitle=選擇安裝程序安裝模式
PrivilegesRequiredOverrideInstruction=選擇安裝模式
PrivilegesRequiredOverrideText1=%1 可以為所有用戶安裝（需要管理員權限），或僅為您安裝。
PrivilegesRequiredOverrideText2=%1 可以僅為您安裝，或為所有用戶安裝（需要管理員權限）。
PrivilegesRequiredOverrideAllUsers=為所有用戶安裝(&A)
PrivilegesRequiredOverrideAllUsersRecommended=為所有用戶安裝(&A)（推薦）
PrivilegesRequiredOverrideCurrentUser=僅為我安裝(&M)
PrivilegesRequiredOverrideCurrentUserRecommended=僅為我安裝(&M)（推薦）

; *** Misc. errors
ErrorCreatingDir=安裝程序無法創建目錄“%1”
ErrorTooManyFilesInDir=無法在目錄“%1”中創建檔案，因為里面包含太多檔案。

; *** Setup common messages
ExitSetupTitle=退出安裝程序
ExitSetupMessage=安裝程序尚未完成。如果現在退出，將不會安裝該程序。%n%n您之后可以再次運行安裝程序完成安裝。%n%n現在退出安裝程序嗎？
AboutSetupMenuItem=關于安裝程序(&A)...
AboutSetupTitle=關于安裝程序
AboutSetupMessage=%1 版本 %2%n%3%n%n%1 主頁：%n%4
AboutSetupNote=
TranslatorNote=簡體中文翻譯由 Kira（847320916@qq.com）維護。項目地址：https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation

; *** Buttons
ButtonBack=< 上一步(&B)
ButtonNext=下一步(&N) >
ButtonInstall=安裝(&I)
ButtonOK=確定
ButtonCancel=取消
ButtonYes=是(&Y)
ButtonYesToAll=全是(&A)
ButtonNo=否(&N)
ButtonNoToAll=全否(&O)
ButtonFinish=完成(&F)
ButtonBrowse=瀏覽(&B)...
ButtonWizardBrowse=瀏覽(&R)...
ButtonNewFolder=新建資料夾(&M)

; *** "Select Language" dialog messages
SelectLanguageTitle=選擇安裝語言
SelectLanguageLabel=選擇安裝時使用的語言。

; *** Common wizard text
ClickNext=點擊“下一步”繼續，或點擊“取消”退出安裝程序。
BeveledLabel=
BrowseDialogTitle=瀏覽資料夾
BrowseDialogLabel=在下面的列表中選擇一個資料夾，然后點擊“確定”。
NewFolderName=新建資料夾

; *** "Welcome" wizard page
WelcomeLabel1=歡迎使用 [name] 安裝向導
WelcomeLabel2=即將在您的電腦上安裝 [name/ver]。%n%n建議您在繼續安裝前關閉所有其他應用程序。

; *** "Password" wizard page
WizardPassword=密碼
PasswordLabel1=此安裝程序需要密碼驗證。
PasswordLabel3=請輸入密碼，然后點擊“下一步”繼續。密碼區分大小寫。
PasswordEditLabel=密碼(&P)：
IncorrectPassword=您輸入的密碼不正確，請重新輸入。

; *** "License Agreement" wizard page
WizardLicense=許可協議
LicenseLabel=請在繼續安裝前閱讀以下重要信息。
LicenseLabel3=請閱讀下列許可協議。在繼續安裝前您必須同意這些協議條款。
LicenseAccepted=我同意此協議(&A)
LicenseNotAccepted=我不同意此協議(&D)

; *** "Information" wizard pages
WizardInfoBefore=信息
InfoBeforeLabel=請在繼續安裝前閱讀以下重要信息。
InfoBeforeClickLabel=準備好繼續安裝后，點擊“下一步”。
WizardInfoAfter=信息
InfoAfterLabel=請在繼續安裝前閱讀以下重要信息。
InfoAfterClickLabel=準備好繼續安裝后，點擊“下一步”。

; *** "User Information" wizard page
WizardUserInfo=用戶信息
UserInfoDesc=請輸入您的信息。
UserInfoName=用戶名(&U)：
UserInfoOrg=組織(&O)：
UserInfoSerial=序列號(&S)：
UserInfoNameRequired=請輸入用戶名。

; *** "Select Destination Location" wizard page
WizardSelectDir=選擇目標位置
SelectDirDesc=您想將 [name] 安裝在哪里？
SelectDirLabel3=安裝程序將安裝 [name] 到下面的資料夾中。
SelectDirBrowseLabel=點擊“下一步”繼續。如果您想選擇其他資料夾，點擊“瀏覽”。
DiskSpaceGBLabel=至少需要有 [gb] GB 的可用磁碟空間。
DiskSpaceMBLabel=至少需要有 [mb] MB 的可用磁碟空間。
CannotInstallToNetworkDrive=安裝程序無法安裝到一個網路驅動器。
CannotInstallToUNCPath=安裝程序無法安裝到一個 UNC 路徑。
InvalidPath=您必須輸入一個帶驅動器盤符的完整路徑，例如：%n%nC:\App%n%n或UNC路徑：%n%n\\server\share
InvalidDrive=您選定的驅動器或 UNC 共享不存在或不能訪問。請選擇其他位置。
DiskSpaceWarningTitle=磁碟空間不足
DiskSpaceWarning=安裝程序至少需要 %1 KB 的可用空間才能安裝，但選定驅動器只有 %2 KB 的可用空間。%n%n您確定要繼續嗎？
DirNameTooLong=資料夾名稱或路徑太長。
InvalidDirName=資料夾名稱無效。
BadDirName32=資料夾名稱不能包含下列任何字符：%n%n%1
DirExistsTitle=資料夾已存在
DirExists=資料夾：%n%n%1%n%n已經存在。您確定安裝到這個資料夾中嗎？
DirDoesntExistTitle=資料夾不存在
DirDoesntExist=資料夾：%n%n%1%n%n不存在。您想要創建此資料夾嗎？

; *** "Select Components" wizard page
WizardSelectComponents=選擇組件
SelectComponentsDesc=您想安裝哪些程序組件？
SelectComponentsLabel2=選中您想安裝的組件；取消您不想安裝的組件。然后點擊“下一步”繼續。
FullInstallation=完全安裝
; if possible don't translate 'Compact' as 'Minimal' (I mean 'Minimal' in your language)
CompactInstallation=簡潔安裝
CustomInstallation=自定義安裝
NoUninstallWarningTitle=組件已存在
NoUninstallWarning=安裝程序檢測到下列組件已安裝在您的電腦中：%n%n%1%n%n取消選中這些組件不會卸載它們。%n%n您確定要繼續嗎？
ComponentSize1=%1 KB
ComponentSize2=%1 MB
ComponentsDiskSpaceGBLabel=當前選擇的組件需要至少 [gb] GB 的磁碟空間。
ComponentsDiskSpaceMBLabel=當前選擇的組件需要至少 [mb] MB 的磁碟空間。

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=選擇附加任務
SelectTasksDesc=您想要安裝程序執行哪些附加任務？
SelectTasksLabel2=選擇您想要安裝程序在安裝 [name] 時執行的附加任務，然后點擊“下一步”。

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=選擇開始菜單資料夾
SelectStartMenuFolderDesc=安裝程序應該在哪里放置程序的捷徑？
SelectStartMenuFolderLabel3=安裝程序將在下列“開始”菜單資料夾中創建程序的捷徑。
SelectStartMenuFolderBrowseLabel=點擊“下一步”繼續。如果您想選擇其他資料夾，點擊“瀏覽”。
MustEnterGroupName=您必須輸入一個資料夾名稱。
GroupNameTooLong=資料夾名稱或路徑太長。
InvalidGroupName=資料夾名稱無效。
BadGroupName=資料夾名稱不能包含下列任何字符：%n%n%1
NoProgramGroupCheck2=不創建開始菜單資料夾(&D)

; *** "Ready to Install" wizard page
WizardReady=準備安裝
ReadyLabel1=安裝程序準備就緒，現在可以開始安裝 [name] 到您的電腦。
ReadyLabel2a=點擊“安裝”繼續此安裝程序。如果您想重新查看或修改任何設置，點擊“上一步”。
ReadyLabel2b=點擊“安裝”繼續此安裝程序。
ReadyMemoUserInfo=用戶信息：
ReadyMemoDir=目標位置：
ReadyMemoType=安裝類型：
ReadyMemoComponents=已選擇組件：
ReadyMemoGroup=開始菜單資料夾：
ReadyMemoTasks=附加任務：

; *** TDownloadWizardPage wizard page and DownloadTemporaryFile
DownloadingLabel2=正在下載檔案...
ButtonStopDownload=停止下載(&S)
StopDownload=您確定要停止下載嗎？
ErrorDownloadAborted=下載已中止。
ErrorDownloadFailed=下載失敗：%1 %2。
ErrorDownloadSizeFailed=獲取大小失敗：%1 %2。
ErrorProgress=無效的進度：%1 / %2。
ErrorFileSize=檔案大小錯誤：預期 %1，實際 %2。

; *** TExtractionWizardPage wizard page and ExtractArchive
ExtractingLabel=正在提取檔案...
ButtonStopExtraction=停止提取(&S)
StopExtraction=您確定要停止提取嗎？
ErrorExtractionAborted=提取已中止。
ErrorExtractionFailed=提取失敗：%1

; *** Archive extraction failure details
ArchiveIncorrectPassword=密碼不正確。
ArchiveIsCorrupted=壓縮包已損壞。
ArchiveUnsupportedFormat=不支持的壓縮包格式。

; *** "Preparing to Install" wizard page
WizardPreparing=正在準備安裝
PreparingDesc=安裝程序正在準備安裝 [name] 到您的電腦。
PreviousInstallNotCompleted=先前的程序安裝或卸載未完成，需要您重啟電腦以完成該安裝。%n%n在重啟電腦后，再次運行安裝程序以完成 [name] 的安裝。
CannotContinue=安裝程序不能繼續。請點擊“取消”退出。
ApplicationsFound=以下應用程序正在使用將由安裝程序更新的檔案。建議您允許安裝程序自動關閉這些應用程序。
ApplicationsFound2=以下應用程序正在使用將由安裝程序更新的檔案。建議您允許安裝程序自動關閉這些應用程序。安裝完成后，安裝程序將嘗試重新啟動這些應用程序。
CloseApplications=自動關閉應用程序(&A)
DontCloseApplications=不要關閉應用程序(&D)
ErrorCloseApplications=安裝程序無法自動關閉所有應用程序。建議您在繼續之前，關閉所有在使用需要由安裝程序更新的檔案的應用程序。
PrepareToInstallNeedsRestart=安裝程序必須重啟您的電腦。電腦重啟后，請再次運行安裝程序以完成 [name] 的安裝。%n%n要立即重啟嗎？

; *** "Installing" wizard page
WizardInstalling=正在安裝
InstallingLabel=安裝程序正在安裝 [name] 到您的電腦，請稍候。

; *** "Setup Completed" wizard page
FinishedHeadingLabel=完成 [name] 安裝向導
FinishedLabelNoIcons=安裝程序已在您的電腦中安裝了 [name]。
FinishedLabel=安裝程序已在您的電腦中安裝了 [name]。您可以通過已安裝的捷徑運行此應用程序。
ClickFinish=點擊“完成”退出安裝程序。
FinishedRestartLabel=為完成 [name] 的安裝，安裝程序必須重新啟動您的電腦。要立即重啟嗎？
FinishedRestartMessage=為完成 [name] 的安裝，安裝程序必須重新啟動您的電腦。%n%n要立即重啟嗎？
ShowReadmeCheck=是，我想查閱自述檔案
YesRadio=是，立即重啟電腦(&Y)
NoRadio=否，稍后重啟電腦(&N)
; used for example as 'Run MyProg.exe'
RunEntryExec=運行 %1
; used for example as 'View Readme.txt'
RunEntryShellExec=查閱 %1

; *** "Setup Needs the Next Disk" stuff
ChangeDiskTitle=安裝程序需要下一張磁碟
SelectDiskLabel2=請插入磁碟 %1 并點擊“確定”。%n%n如果這個磁碟中的檔案可以在下列資料夾之外的資料夾中找到，請輸入正確的路徑或點擊“瀏覽”。
PathLabel=路徑(&P)：
FileNotInDir2=“%2”中找不到檔案“%1”。請插入正確的磁碟或選擇其他資料夾。
SelectDirectoryLabel=請指定下一張磁碟的位置。

; *** Installation phase messages
SetupAborted=安裝程序未完成安裝。%n%n請修正這個問題并重新運行安裝程序。
AbortRetryIgnoreSelectAction=選擇操作
AbortRetryIgnoreRetry=重試(&T)
AbortRetryIgnoreIgnore=忽略錯誤并繼續(&I)
AbortRetryIgnoreCancel=取消安裝
RetryCancelSelectAction=選擇操作
RetryCancelRetry=重試(&T)
RetryCancelCancel=取消

; *** Installation status messages
StatusClosingApplications=正在關閉應用程序...
StatusCreateDirs=正在創建目錄...
StatusExtractFiles=正在提取檔案...
StatusDownloadFiles=正在下載檔案...
StatusCreateIcons=正在創建捷徑...
StatusCreateIniEntries=正在創建 INI 條目...
StatusCreateRegistryEntries=正在創建注冊表條目...
StatusRegisterFiles=正在注冊檔案...
StatusSavingUninstall=正在保存卸載信息...
StatusRunProgram=正在完成安裝...
StatusRestartingApplications=正在重啟應用程序...
StatusRollback=正在撤銷更改...

; *** Misc. errors
ErrorInternal2=內部錯誤：%1。
ErrorFunctionFailedNoCode=%1 失敗。
ErrorFunctionFailed=%1 失敗；錯誤代碼 %2。
ErrorFunctionFailedWithMessage=%1 失敗；錯誤代碼 %2。%n%3
ErrorExecutingProgram=無法執行檔案：%n%1

; *** Registry errors
ErrorRegOpenKey=打開注冊表項時出錯：%n%1\%2
ErrorRegCreateKey=創建注冊表項時出錯：%n%1\%2
ErrorRegWriteKey=寫入注冊表項時出錯：%n%1\%2

; *** INI errors
ErrorIniEntry=在檔案“%1”中創建 INI 條目時出錯。

; *** File copying errors
FileAbortRetryIgnoreSkipNotRecommended=跳過此檔案(&S)（不推薦）
FileAbortRetryIgnoreIgnoreNotRecommended=忽略錯誤并繼續(&I)（不推薦）
SourceIsCorrupted=源檔案已損壞。
SourceDoesntExist=源檔案“%1”不存在。
SourceVerificationFailed=源檔案驗證失敗：%1
VerificationSignatureDoesntExist=簽名檔案“%1”不存在。
VerificationSignatureInvalid=簽名檔案“%1”無效。
VerificationKeyNotFound=簽名檔案“%1”使用了未知的密鑰。
VerificationFileNameIncorrect=檔案名不正確。
VerificationFileTagIncorrect=檔案標簽不正確。
VerificationFileSizeIncorrect=檔案大小不正確。
VerificationFileHashIncorrect=檔案哈希值不正確。
ExistingFileReadOnly2=無法替換已存在的檔案，它是只讀的。
ExistingFileReadOnlyRetry=移除只讀屬性并重試(&R)
ExistingFileReadOnlyKeepExisting=保留已存在的檔案(&K)
ErrorReadingExistingDest=嘗試讀取已存在的檔案時出錯：
FileExistsSelectAction=選擇操作
FileExists2=檔案已經存在。
FileExistsOverwriteExisting=覆蓋已存在的檔案(&O)
FileExistsKeepExisting=保留已存在的檔案(&K)
FileExistsOverwriteOrKeepAll=為接下來的沖突檔案執行此操作(&D)
ExistingFileNewerSelectAction=選擇操作
ExistingFileNewer2=已存在的檔案比安裝程序將要安裝的檔案還要新。
ExistingFileNewerOverwriteExisting=覆蓋已存在的檔案(&O)
ExistingFileNewerKeepExisting=保留已存在的檔案(&K)（推薦）
ExistingFileNewerOverwriteOrKeepAll=為接下來的沖突檔案執行此操作(&D)
ErrorChangingAttr=嘗試更改下列已存在的檔案屬性時出錯：
ErrorCreatingTemp=嘗試在目標目錄創建檔案時出錯：
ErrorReadingSource=嘗試讀取下列源檔案時出錯：
ErrorCopying=嘗試復制下列檔案時出錯：
ErrorDownloading=嘗試下載檔案時出錯：
ErrorExtracting=嘗試提取壓縮包時出錯：
ErrorReplacingExistingFile=嘗試替換已存在的檔案時出錯：
ErrorRestartReplace=重啟并替換失敗：
ErrorRenamingTemp=嘗試重命名下列目標目錄中的一個檔案時出錯：
ErrorRegisterServer=無法注冊 DLL/OCX：%1
ErrorRegSvr32Failed=RegSvr32 失敗；退出代碼 %1。
ErrorRegisterTypeLib=無法注冊類型庫：%1

; *** Uninstall display name markings
; used for example as 'My Program (32-bit)'
UninstallDisplayNameMark=%1 (%2)
; used for example as 'My Program (32-bit, All users)'
UninstallDisplayNameMarks=%1 (%2, %3)
UninstallDisplayNameMark32Bit=32 位
UninstallDisplayNameMark64Bit=64 位
UninstallDisplayNameMarkAllUsers=所有用戶
UninstallDisplayNameMarkCurrentUser=當前用戶

; *** Post-installation errors
ErrorOpeningReadme=嘗試打開自述檔案時出錯。
ErrorRestartingComputer=安裝程序無法重啟電腦，請手動重啟。

; *** Uninstaller messages
UninstallNotFound=檔案“%1”不存在。無法卸載。
UninstallOpenError=檔案“%1”不能被打開。無法卸載
UninstallUnsupportedVer=此版本的卸載程序無法識別卸載日志檔案“%1”的格式。無法卸載。
UninstallUnknownEntry=卸載日志中遇到一個未知條目（%1）。
ConfirmUninstall=您確認要完全移除 %1 及其所有組件嗎？
UninstallOnlyOnWin64=僅允許在 64 位 Windows 中卸載此程序。
OnlyAdminCanUninstall=僅使用管理員權限的用戶能完成此卸載。
UninstallStatusLabel=正在從您的電腦中移除 %1，請稍候。
UninstalledAll=已順利從您的電腦中移除 %1。
UninstalledMost=%1 卸載完成。%n%n有部分內容未能被刪除，但您可以手動刪除它們。
UninstalledAndNeedsRestart=為完成 %1 的卸載，需要重啟您的電腦。%n%n要立即重啟嗎？
UninstallDataCorrupted=檔案“%1”已損壞。無法卸載。

; *** Uninstallation phase messages
ConfirmDeleteSharedFileTitle=刪除共享檔案？
ConfirmDeleteSharedFile2=系統表示下列共享檔案已不再有任何程序使用。您希望卸載程序刪除此共享檔案嗎？%n%n如果仍有程序正在使用此檔案，刪除后這些程序可能無法正常運行。如果您不能確定，請選擇“否”，保留此檔案在系統中不會造成任何損害。
SharedFileNameLabel=檔案名：
SharedFileLocationLabel=位置：
WizardUninstalling=卸載狀態
StatusUninstalling=正在卸載 %1...

; *** Shutdown block reasons
ShutdownBlockReasonInstallingApp=正在安裝 %1。
ShutdownBlockReasonUninstallingApp=正在卸載 %1。

; The custom messages below aren't used by Setup itself, but if you make
; use of them in your scripts, you'll want to translate them.

[CustomMessages]

NameAndVersion=%1 版本 %2
AdditionalIcons=附加捷徑：
CreateDesktopIcon=創建桌面捷徑(&D)
CreateQuickLaunchIcon=創建快速啟動欄捷徑(&Q)
ProgramOnTheWeb=%1 網站
UninstallProgram=卸載 %1
LaunchProgram=運行 %1
AssocFileExtension=將 %2 檔案擴展名與 %1 建立關聯(&A)
AssocingFileExtension=正在將 %2 檔案擴展名與 %1 建立關聯...
AutoStartProgramGroupDescription=啟動：
AutoStartProgram=自動啟動 %1
AddonHostProgramNotFound=您選擇的資料夾中無法找到 %1。%n%n您確定要繼續嗎？
