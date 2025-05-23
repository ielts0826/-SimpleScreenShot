; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!
; 参考 Inno Setup 文档了解更多细节！

#define MyAppName "简易截图工具"
#define MyAppVersion "1.0" 
#define MyAppPublisher "LEOVIBE" ; 您可以替换成您的名字或组织
#define MyAppURL "https://leostudyai.top/" ; 如果有相关网址可以替换
#define MyAppExeName "ScreenCaptureTool.exe"
#define PublishSourcePath "E:\screen_shot_tool_develop\bin\Release\net8.0\win-x64\publish" 
; *** 重要: 请确保上面的 PublishSourcePath 路径是您运行 dotnet publish 命令后生成的 publish 文件夹的实际路径！ ***

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{AUTO_GUID}} ; Inno Setup 会自动生成一个 GUID
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
; DisableProgramGroupPage=yes ; 注释掉此行 - Let Inno Setup handle the Start Menu folder page
; Uncomment the following line to run in non administrative install mode (install for current user only.)
; PrivilegesRequired=lowest
OutputBaseFilename=简易截图工具Setup
SetupIconFile=E:\screen_shot_tool_develop\Assets\screenshot_icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

;[Languages]
;Name: "chinese_simplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl" 

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishSourcePath}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishSourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
