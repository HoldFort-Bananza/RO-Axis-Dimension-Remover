; Instalator RO Axis Dimension Remover.
;
; NIE dolacza zadnych bibliotek Tekla/Trimble - instaluje tylko wlasny,
; zbudowany .exe programu, a po instalacji uruchamia fetch-dependencies.ps1,
; ktory pobiera wymagane biblioteki Tekla Open API swiezo z publicznego
; NuGet (nuget.org) na komputer uzytkownika, pod jego wlasna licencja Tekli -
; dokladnie to samo, co zrobilby "dotnet restore" przy budowaniu z zrodel.
; Ekran licencji ponizej to oryginalna EULA Trimble/Tekla dla tych bibliotek -
; instalacja wymaga jej zaakceptowania. Wzorzec 1:1 z Radius Dimention Mover
; (ten sam katalog nadrzedny).

#define MyAppName "RO Axis Dimension Remover"
#define MyAppVersion "0.2.2"
#define MyAppPublisher "HoldFort-Bananza"
#define MyAppExeName "RoAxisDimensionRemover.exe"

[Setup]
AppId={{23109251-E763-45D2-BBCB-44B6D3F97B2E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\RoAxisDimensionRemover
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=RoAxisDimensionRemover-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=TeklaEULA.txt
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "Utworz skrot na pulpicie"; GroupDescription: "Dodatkowe skroty:"

[Files]
Source: "..\bin\x64\Debug\net48\RoAxisDimensionRemover.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\x64\Debug\net48\RoAxisDimensionRemover.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\x64\Debug\net48\RoAxisDimensionRemover.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "fetch-dependencies.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Odinstaluj {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\fetch-dependencies.ps1"" -TargetDir ""{app}"""; \
    WorkingDir: "{app}"; \
    StatusMsg: "Pobieranie bibliotek Tekla Open API z NuGet (wymaga internetu)..."; \
    Flags: waituntilterminated shellexec runascurrentuser

Filename: "{app}\{#MyAppExeName}"; Description: "Uruchom {#MyAppName}"; Flags: postinstall nowait skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
