; ============================================================================
; SOUP Installer Script for Inno Setup 6
; ============================================================================
; Build command: ISCC.exe SOUP.iss
; Or use: .\scripts\publish.ps1 -Installer
; ============================================================================

#define MyAppName "SOUP"
#define MyAppVersion "4.6.8"
#define MyAppPublisher "SOUP"
#define MyAppExeName "SOUP.exe"
#define MyAppDescription "Allocation Buddy, Essentials Buddy, and ExpireWise inventory management tools"
#define MyAppURL "https://github.com/your-repo/SOUP"

[Setup]
; Application identity
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright (C) 2024-2025 {#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppDescription}

; Default installation directory
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; Output settings
OutputDir=..\installer-output
OutputBaseFilename=SOUP-Setup-{#MyAppVersion}
SetupIconFile=..\src\soup.ico

; License file (displayed during install)
LicenseFile=LICENSE.txt

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4

; Visual settings - Modern wizard with custom images
WizardStyle=modern
DisableWelcomePage=no
DisableProgramGroupPage=yes

; Custom installer images (optional - uncomment if you create these files)
; WizardImageFile=WizardImage.bmp
; WizardSmallImageFile=WizardSmallImage.bmp

; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Misc
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (all modules)"
Name: "compact"; Description: "Compact installation (core only)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
; Core application (always required)
Name: "core"; Description: "SOUP Core Application"; Types: full compact custom; Flags: fixed
; Feature modules
Name: "modules"; Description: "Feature Modules"; Types: full
Name: "modules\allocation"; Description: "Allocation Buddy - Allocation management and RPG rewards"; Types: full
Name: "modules\essentials"; Description: "Essentials Buddy - Essential items tracking"; Types: full
Name: "modules\expirewise"; Description: "ExpireWise - Expiration date management and analytics"; Types: full
Name: "modules\swiftlabel"; Description: "Swift Label - Quick label printing utility"; Types: full
Name: "modules\orderlog"; Description: "Order Log - Order tracking and history"; Types: full
; Extras
Name: "extras"; Description: "Extras"; Types: full
Name: "extras\funstuff"; Description: "Fun Stuff - Easter eggs and hidden features"; Types: full; Flags: dontinheritcheck
Name: "extras\sampledata"; Description: "Sample data files"; Types: full; Flags: dontinheritcheck
Name: "extras\templates"; Description: "Excel/CSV templates"; Types: full; Flags: dontinheritcheck

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; Core application files (always installed)
; Framework-dependent version (smaller, requires .NET 8 runtime)
Source: "..\publish-framework\*"; DestDir: "{app}"; Components: core; Flags: ignoreversion recursesubdirs createallsubdirs

; Alternatively, use self-contained version (larger, no runtime needed):
; Source: "..\publish-portable\*"; DestDir: "{app}"; Components: core; Flags: ignoreversion recursesubdirs createallsubdirs

; Module configuration files (create markers so app knows what's enabled)
; These are empty marker files the app can check to enable/disable features
Source: "modules\allocation.enabled"; DestDir: "{app}\modules"; Components: modules\allocation; Flags: ignoreversion
Source: "modules\essentials.enabled"; DestDir: "{app}\modules"; Components: modules\essentials; Flags: ignoreversion
Source: "modules\expirewise.enabled"; DestDir: "{app}\modules"; Components: modules\expirewise; Flags: ignoreversion
Source: "modules\swiftlabel.enabled"; DestDir: "{app}\modules"; Components: modules\swiftlabel; Flags: ignoreversion
Source: "modules\orderlog.enabled"; DestDir: "{app}\modules"; Components: modules\orderlog; Flags: ignoreversion
Source: "modules\funstuff.enabled"; DestDir: "{app}\modules"; Components: extras\funstuff; Flags: ignoreversion

; Sample data (optional)
; Source: "..\sample-data\*"; DestDir: "{app}\SampleData"; Components: extras\sampledata; Flags: ignoreversion recursesubdirs

; Templates (optional)
; Source: "..\templates\*"; DestDir: "{app}\Templates"; Components: extras\templates; Flags: ignoreversion recursesubdirs

[Dirs]
; Create modules directory
Name: "{app}\modules"; Components: core

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Store installed components in registry for app to read
Root: HKCU; Subkey: "Software\{#MyAppName}"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; ValueType: dword; ValueName: "AllocationBuddy"; ValueData: "1"; Components: modules\allocation
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; ValueType: dword; ValueName: "EssentialsBuddy"; ValueData: "1"; Components: modules\essentials
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; ValueType: dword; ValueName: "ExpireWise"; ValueData: "1"; Components: modules\expirewise
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; ValueType: dword; ValueName: "SwiftLabel"; ValueData: "1"; Components: modules\swiftlabel
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; ValueType: dword; ValueName: "OrderLog"; ValueData: "1"; Components: modules\orderlog
Root: HKCU; Subkey: "Software\{#MyAppName}\Modules"; ValueType: dword; ValueName: "FunStuff"; ValueData: "1"; Components: extras\funstuff

[Code]
// Check if .NET 8 Desktop Runtime is installed
function IsDotNet8DesktopInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  // Try to run dotnet --list-runtimes and check for Microsoft.WindowsDesktop.App 8.x
  Result := Exec('cmd.exe', '/c dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 8."', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := Result and (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // Check for .NET 8 Desktop Runtime
  if not IsDotNet8DesktopInstalled() then
  begin
    if MsgBox('SOUP requires the .NET 8 Desktop Runtime which was not detected on your system.' + #13#10 + #13#10 +
              'Would you like to download and install it now?' + #13#10 + #13#10 +
              'Click Yes to open the download page, or No to continue anyway.',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;
