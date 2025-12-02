; SAP Installer Script for Inno Setup 6
; https://jrsoftware.org/isinfo.php

#define MyAppName "SAP"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SAP"
#define MyAppURL ""
#define MyAppExeName "SAP.exe"

[Setup]
; Unique identifier for this application
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=Output
OutputBaseFilename=SAP-Setup-{#MyAppVersion}
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Modern wizard style
WizardStyle=modern
; Require admin for Program Files install
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Uninstall settings
UninstallDisplayIcon={app}\{#MyAppExeName}
; Minimum Windows version (Windows 10)
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (all modules)"
Name: "compact"; Description: "Compact installation (AllocationBuddy only)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "core"; Description: "SAP Core Application"; Types: full compact custom; Flags: fixed
Name: "modules"; Description: "Modules"; Types: full
Name: "modules\allocationbuddy"; Description: "AllocationBuddy - Inventory allocation and matching"; Types: full compact custom; Flags: checkablealone
Name: "modules\essentialsbuddy"; Description: "EssentialsBuddy - Essentials tracking and management"; Types: full custom; Flags: checkablealone
Name: "modules\expirewise"; Description: "ExpireWise - Expiration date tracking"; Types: full custom; Flags: checkablealone
Name: "data"; Description: "Data Files"; Types: full compact
Name: "data\dictionary"; Description: "Dictionary Database (13,000+ items for AllocationBuddy)"; Types: full compact

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Core application files (always installed)
Source: "..\publish\*"; DestDir: "{app}"; Components: core; Flags: ignoreversion recursesubdirs createallsubdirs

; Dictionary database - copy from build machine's AppData if it exists
Source: "{userappdata}\SAP\Shared\dictionaries.db"; DestDir: "{userappdata}\SAP\Shared"; Components: data\dictionary; Flags: external skipifsourcedoesntexist onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[INI]
; Create module configuration file based on user selections
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "AllocationBuddy"; String: "true"; Components: modules\allocationbuddy
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "AllocationBuddy"; String: "false"; Components: not modules\allocationbuddy
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "EssentialsBuddy"; String: "true"; Components: modules\essentialsbuddy
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "EssentialsBuddy"; String: "false"; Components: not modules\essentialsbuddy
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "ExpireWise"; String: "true"; Components: modules\expirewise
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "ExpireWise"; String: "false"; Components: not modules\expirewise
Filename: "{userappdata}\SAP\modules.ini"; Section: "Info"; Key: "InstalledVersion"; String: "{#MyAppVersion}"
Filename: "{userappdata}\SAP\modules.ini"; Section: "Info"; Key: "InstallDate"; String: "{code:GetCurrentDate}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up logs on uninstall (optional - user data preserved)
Type: filesandordirs; Name: "{userappdata}\SAP\Logs"
Type: files; Name: "{userappdata}\SAP\modules.ini"

[Code]
function GetCurrentDate(Param: String): String;
begin
  Result := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':');
end;

procedure InitializeWizard;
begin
  // Custom initialization if needed
end;

function InitializeSetup: Boolean;
begin
  Result := True;
end;
