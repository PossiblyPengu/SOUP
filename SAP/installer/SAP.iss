; SAP Installer Script for Inno Setup 6
; https://jrsoftware.org/isinfo.php
;
; This installer supports two modes:
;   - Standard: Framework-dependent (smaller, requires .NET 8 runtime)
;   - Portable: Self-contained (larger, no dependencies, all data in install folder)

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
; Allow user to change install directory
DisableDirPage=no
; Output settings
OutputDir=Output
OutputBaseFilename=SAP-Setup-{#MyAppVersion}
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Modern wizard style
WizardStyle=modern
; Allow non-admin installs (for portable mode)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
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
Name: "full"; Description: "Full installation (requires .NET 8 runtime)"
Name: "compact"; Description: "Compact installation (AllocationBuddy only, requires .NET 8)"
Name: "portable"; Description: "Portable installation (self-contained, no dependencies)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "core"; Description: "SAP Core Application"; Types: full compact portable custom; Flags: fixed
Name: "modules"; Description: "Modules"; Types: full portable
Name: "modules\allocationbuddy"; Description: "AllocationBuddy - Inventory allocation and matching"; Types: full compact portable custom; Flags: checkablealone
Name: "modules\essentialsbuddy"; Description: "EssentialsBuddy - Essentials tracking and management"; Types: full portable custom; Flags: checkablealone
Name: "modules\expirewise"; Description: "ExpireWise - Expiration date tracking"; Types: full portable custom; Flags: checkablealone
Name: "data"; Description: "Data Files"; Types: full compact portable
Name: "data\dictionary"; Description: "Dictionary Database (13,000+ items for AllocationBuddy)"; Types: full compact portable

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; Check: not IsPortableMode
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode and not IsPortableMode

[Files]
; Framework-dependent version for standard install (smaller, requires .NET 8)
Source: "..\publish-framework\*"; DestDir: "{app}"; Components: core; Check: not IsPortableMode; Flags: ignoreversion recursesubdirs createallsubdirs

; Self-contained version for portable install (larger, no dependencies)
Source: "..\publish-portable\*"; DestDir: "{app}"; Components: core; Check: IsPortableMode; Flags: ignoreversion recursesubdirs createallsubdirs

; Dictionary database - for standard install (AppData)
Source: "{userappdata}\SAP\Shared\dictionaries.db"; DestDir: "{userappdata}\SAP\Shared"; Components: data\dictionary; Check: not IsPortableMode; Flags: external skipifsourcedoesntexist onlyifdoesntexist uninsneveruninstall

; Dictionary database - for portable install (install folder)
Source: "{userappdata}\SAP\Shared\dictionaries.db"; DestDir: "{app}\Data"; Components: data\dictionary; Check: IsPortableMode; Flags: external skipifsourcedoesntexist onlyifdoesntexist

; Create portable marker file
Source: "..\installer\portable.marker"; DestDir: "{app}"; DestName: "portable.txt"; Check: IsPortableMode; Flags: skipifsourcedoesntexist

[Icons]
; Only create shortcuts for standard install
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Check: not IsPortableMode
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; Check: not IsPortableMode
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Check: not IsPortableMode

[INI]
; Create module configuration file for standard install (AppData)
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "AllocationBuddy"; String: "true"; Components: modules\allocationbuddy; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "AllocationBuddy"; String: "false"; Components: not modules\allocationbuddy; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "EssentialsBuddy"; String: "true"; Components: modules\essentialsbuddy; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "EssentialsBuddy"; String: "false"; Components: not modules\essentialsbuddy; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "ExpireWise"; String: "true"; Components: modules\expirewise; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Modules"; Key: "ExpireWise"; String: "false"; Components: not modules\expirewise; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Info"; Key: "InstalledVersion"; String: "{#MyAppVersion}"; Check: not IsPortableMode
Filename: "{userappdata}\SAP\modules.ini"; Section: "Info"; Key: "InstallDate"; String: "{code:GetCurrentDate}"; Check: not IsPortableMode

; Create module configuration file for portable install (install folder)
Filename: "{app}\Data\modules.ini"; Section: "Modules"; Key: "AllocationBuddy"; String: "true"; Components: modules\allocationbuddy; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Modules"; Key: "AllocationBuddy"; String: "false"; Components: not modules\allocationbuddy; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Modules"; Key: "EssentialsBuddy"; String: "true"; Components: modules\essentialsbuddy; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Modules"; Key: "EssentialsBuddy"; String: "false"; Components: not modules\essentialsbuddy; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Modules"; Key: "ExpireWise"; String: "true"; Components: modules\expirewise; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Modules"; Key: "ExpireWise"; String: "false"; Components: not modules\expirewise; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Info"; Key: "InstalledVersion"; String: "{#MyAppVersion}"; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Info"; Key: "InstallDate"; String: "{code:GetCurrentDate}"; Check: IsPortableMode
Filename: "{app}\Data\modules.ini"; Section: "Info"; Key: "PortableMode"; String: "true"; Check: IsPortableMode

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up logs on uninstall (standard install only - user data preserved)
Type: filesandordirs; Name: "{userappdata}\SAP\Logs"
Type: files; Name: "{userappdata}\SAP\modules.ini"

[Code]
var
  PortablePage: TInputOptionWizardPage;

function IsPortableMode: Boolean;
begin
  // Check if portable type is selected
  Result := WizardSetupType(False) = 'portable';
end;

function GetCurrentDate(Param: String): String;
begin
  Result := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':');
end;

function CreateUninstallRegKey: Boolean;
begin
  // Only create uninstall registry key for standard install
  Result := not IsPortableMode;
end;

procedure InitializeWizard;
begin
  // Update default directory for portable mode
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  // When type selection changes, update the default directory
  if CurPageID = wpSelectDir then
  begin
    if IsPortableMode then
    begin
      // Suggest a portable-friendly location
      WizardForm.DirEdit.Text := ExpandConstant('{userdocs}\SAP-Portable');
    end;
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
end;
