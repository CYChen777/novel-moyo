[Setup]
AppName=NovelMoyo
AppVersion=1.0.2
AppPublisher=NovelMoyo
DefaultDirName={autopf}\NovelMoyo
DefaultGroupName=NovelMoyo
OutputDir=D:\projects\novel-moyo\installer
OutputBaseFilename=NovelMoyo-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\NovelMoyo.exe
PrivilegesRequired=lowest

[Files]
Source: "D:\projects\novel-moyo\src\NovelMoyo\publish\NovelMoyo.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\NovelMoyo"; Filename: "{app}\NovelMoyo.exe"
Name: "{group}\卸载 NovelMoyo"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NovelMoyo"; Filename: "{app}\NovelMoyo.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"
Name: "startupicon"; Description: "开机自动启动"; GroupDescription: "启动选项:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NovelMoyo"; ValueData: """{app}\NovelMoyo.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\NovelMoyo.exe"; Description: "启动 NovelMoyo"; Flags: nowait postinstall skipifsilent
