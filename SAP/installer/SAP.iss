; ============================================================================
;                        S.A.P - S.A.M. Add-on Pack
;                        Installer Script v4.3.0
;
;  Modern Inno Setup 6 installer with custom styling and module selection
; ============================================================================
#define MyAppName "S.A.P"
#define MyAppFullName "S.A.M. Add-on Pack"
#define MyAppVersion "4.3.1"
#define MyAppPublisher "PossiblyPengu"
#define MyAppURL "https://github.com/PossiblyPengu/Cshp"
#define MyAppExeName "SAP.exe"
#define MyAppCopyright "(c) 2024-2025 PossiblyPengu"

[Setup]
; ============================================================================
; Application Identity
; ============================================================================
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}

; Version info embedded in installer
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppFullName} Setup
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppFullName}
VersionInfoProductVersion={#MyAppVersion}

; ============================================================================
; Installation Paths
; ============================================================================
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes

; ============================================================================
; Output Configuration
; ============================================================================
OutputDir=..\installer
OutputBaseFilename=SAP-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMADictionarySize=65536
LZMANumFastBytes=273

; ============================================================================
; Visual Styling - Modern Look
; ============================================================================
WizardStyle=modern
WizardSizePercent=110,100

; Show progress during install
ShowLanguageDialog=no
DisableWelcomePage=no
DisableDirPage=no
DisableReadyPage=no
AlwaysShowDirOnReadyPage=yes
AlwaysShowGroupOnReadyPage=yes

; Show component selection page
DisableProgramGroupPage=yes

; ============================================================================
; Security & Permissions
; ============================================================================
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible

; ============================================================================
; Uninstaller Settings
; ============================================================================
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppFullName}
CreateUninstallRegKey=not IsPortableInstall

; ============================================================================
; Misc Settings
; ============================================================================
SetupMutex=SAP_Setup_Mutex_{#MyAppVersion}
AppMutex=SAP_App_Mutex
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=yes
ChangesAssociations=no
ChangesEnvironment=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ============================================================================
; Custom Messages
; ============================================================================
[Messages]
WelcomeLabel1=Welcome to {#MyAppFullName}
WelcomeLabel2=This will install {#MyAppFullName} v{#MyAppVersion} on your computer.%n%nS.A.P. is a suite of tools designed to enhance your S.A.M. workflow.%n%nIt is recommended that you close all other applications before continuing.
FinishedHeadingLabel=Installation Complete!
FinishedLabelNoIcons=Setup has successfully installed {#MyAppFullName}.%n%nYour productivity tools are ready to use.
FinishedLabel=Setup has successfully installed [name] on your computer.%n%nClick Finish to close this wizard.
ClickFinish=Click Finish to exit Setup.
StatusExtractFiles=Extracting files...
StatusCreateIcons=Creating shortcuts...
StatusUninstalling=Removing files...

[CustomMessages]
english.NameAndVersion=%1 version %2
english.AdditionalIcons=Additional shortcuts:
english.CreateDesktopIcon=Create a &desktop shortcut
english.CreateQuickLaunchIcon=Create a &Quick Launch shortcut
english.InstallTypeFull=Full Installation (~15 MB)
english.InstallTypeFullDesc=Requires .NET 8 Runtime. Includes Start Menu shortcuts, registry entries, and uninstaller. Recommended for most users.
english.InstallTypePortable=Portable Installation (~75 MB)
english.InstallTypePortableDesc=Self-contained with bundled .NET. No shortcuts or registry entries. Runs anywhere without dependencies.

; ============================================================================
; Component Definitions
; ============================================================================
[Types]
Name: "full"; Description: "Full Installation (all modules)"
Name: "compact"; Description: "Compact Installation (core modules only)"
Name: "custom"; Description: "Custom Installation"; Flags: iscustom

[Components]
; Core is always required
Name: "core"; Description: "S.A.P Core (required)"; Types: full compact custom; Flags: fixed
; Main modules
Name: "modules"; Description: "Productivity Modules"; Types: full compact custom
Name: "modules\allocation"; Description: "AllocationBuddy - Resource allocation tracking"; Types: full compact custom
Name: "modules\essentials"; Description: "EssentialsBuddy - Essential items workflow"; Types: full compact custom
Name: "modules\expirewise"; Description: "ExpireWise - Expiration date management"; Types: full compact custom
Name: "modules\swiftlabel"; Description: "SwiftLabel - Quick label generation"; Types: full custom
Name: "modules\orderlog"; Description: "OrderLog - Order tracking widget"; Types: full custom
; Fun Stuff (Easter Eggs)
Name: "funstuff"; Description: "Fun Stuff (Optional)"; Types: full custom
Name: "funstuff\nukem"; Description: "S.A.P NUKEM - Hidden retro FPS Easter egg"; Types: full custom

; ============================================================================
; Files to Install
; ============================================================================
[Files]
; Core files (always installed) - Full install
Source: "..\publish-framework\SAP.exe"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\SAP.dll"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\SAP.pdb"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\SAP.xml"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\SAP.deps.json"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\SAP.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\*.dll"; DestDir: "{app}"; Flags: ignoreversion; Check: not IsPortableInstall
Source: "..\publish-framework\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not IsPortableInstall

; Core files - Portable install  
Source: "..\publish-portable\SAP.exe"; DestDir: "{app}"; Flags: ignoreversion; Check: IsPortableInstall
Source: "..\publish-portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsPortableInstall

; Module configuration file (tracks which modules are enabled)
Source: "..\installer\module_config.json"; DestDir: "{app}"; Flags: ignoreversion; AfterInstall: WriteModuleConfig

[Dirs]
; User data directory (writable by standard user)
Name: "{app}\Data"; Permissions: users-modify

; ============================================================================
; Shortcuts (Full install only)
; ============================================================================
[Icons]
; Start Menu (full install only)
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Launch {#MyAppFullName}"; Check: not IsPortableInstall
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; Comment: "Uninstall {#MyAppFullName}"; Check: not IsPortableInstall

; Desktop (optional, full install only)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "Launch {#MyAppFullName}"; Check: not IsPortableInstall

; ============================================================================
; Installation Tasks (User Choices) - Full install only
; ============================================================================
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

; ============================================================================
; Registry Entries (Full install only)
; ============================================================================
[Registry]
; App registration (full install only)
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey; Check: not IsPortableInstall
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey; Check: not IsPortableInstall

; ============================================================================
; Run After Install
; ============================================================================
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppFullName}"; Flags: nowait postinstall skipifsilent shellexec

; ============================================================================
; Uninstall - Clean up
; ============================================================================
[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; ============================================================================
; Pascal Script - Advanced Customization
; ============================================================================
[Code]
var
  ModulesLabel: TNewStaticText;
  ProgressLabel: TNewStaticText;
  InstallTypePage: TInputOptionWizardPage;
  PortableMode: Boolean;

// Check if portable install was selected
function IsPortableInstall: Boolean;
begin
  Result := PortableMode;
end;

// Write module configuration based on user selections
procedure WriteModuleConfig;
var
  ConfigFile: String;
  ConfigContent: String;
begin
  ConfigFile := ExpandConstant('{app}\module_config.json');
  
  ConfigContent := '{' + #13#10;
  ConfigContent := ConfigContent + '  "version": "{#MyAppVersion}",' + #13#10;
  ConfigContent := ConfigContent + '  "modules": {' + #13#10;
  
  // Check each module selection
  if WizardIsComponentSelected('modules\allocation') then
    ConfigContent := ConfigContent + '    "allocationBuddy": true,' + #13#10
  else
    ConfigContent := ConfigContent + '    "allocationBuddy": false,' + #13#10;
    
  if WizardIsComponentSelected('modules\essentials') then
    ConfigContent := ConfigContent + '    "essentialsBuddy": true,' + #13#10
  else
    ConfigContent := ConfigContent + '    "essentialsBuddy": false,' + #13#10;
    
  if WizardIsComponentSelected('modules\expirewise') then
    ConfigContent := ConfigContent + '    "expireWise": true,' + #13#10
  else
    ConfigContent := ConfigContent + '    "expireWise": false,' + #13#10;
    
  if WizardIsComponentSelected('modules\swiftlabel') then
    ConfigContent := ConfigContent + '    "swiftLabel": true,' + #13#10
  else
    ConfigContent := ConfigContent + '    "swiftLabel": false,' + #13#10;
    
  if WizardIsComponentSelected('modules\orderlog') then
    ConfigContent := ConfigContent + '    "orderLog": true,' + #13#10
  else
    ConfigContent := ConfigContent + '    "orderLog": false,' + #13#10;
    
  // Fun Stuff (Easter Eggs)
  if WizardIsComponentSelected('funstuff\nukem') then
    ConfigContent := ConfigContent + '    "sapNukem": true' + #13#10
  else
    ConfigContent := ConfigContent + '    "sapNukem": false' + #13#10;
    
  ConfigContent := ConfigContent + '  }' + #13#10;
  ConfigContent := ConfigContent + '}' + #13#10;
  
  SaveStringToFile(ConfigFile, ConfigContent, False);
end;

// Initialize wizard with custom styling
procedure InitializeWizard;
begin
  // Create install type selection page (after welcome, before directory)
  InstallTypePage := CreateInputOptionPage(wpWelcome,
    'Select Installation Type',
    'How would you like to install {#MyAppFullName}?',
    'Please select an installation type, then click Next.',
    True, False);
  
  InstallTypePage.Add('Full Installation (Recommended)');
  InstallTypePage.Add('Portable Installation');
  InstallTypePage.Values[0] := True; // Default to full install
  
  // Add module info label on welcome page
  ModulesLabel := TNewStaticText.Create(WizardForm);
  ModulesLabel.Parent := WizardForm.WelcomePage;
  ModulesLabel.Left := WizardForm.WelcomeLabel2.Left;
  ModulesLabel.Top := WizardForm.WelcomeLabel2.Top + WizardForm.WelcomeLabel2.Height + 20;
  ModulesLabel.Width := WizardForm.WelcomeLabel2.Width;
  ModulesLabel.Height := 120;
  ModulesLabel.AutoSize := False;
  ModulesLabel.WordWrap := True;
  ModulesLabel.Caption := 
    'Available Modules:' + #13#10 +
    '  * AllocationBuddy - Resource allocation tracking' + #13#10 +
    '  * EssentialsBuddy - Essential items workflow' + #13#10 +
    '  * ExpireWise - Expiration date management' + #13#10 +
    '  * SwiftLabel - Quick label generation' + #13#10 +
    '  * OrderLog - Order tracking widget' + #13#10 +
    '  * S.A.P NUKEM - Hidden retro FPS Easter egg';
  ModulesLabel.Font.Style := [];
  ModulesLabel.Font.Color := clGray;
  
  // Add progress label on install page
  ProgressLabel := TNewStaticText.Create(WizardForm);
  ProgressLabel.Parent := WizardForm.InstallingPage;
  ProgressLabel.Left := WizardForm.StatusLabel.Left;
  ProgressLabel.Top := WizardForm.ProgressGauge.Top + WizardForm.ProgressGauge.Height + 10;
  ProgressLabel.Width := WizardForm.StatusLabel.Width;
  ProgressLabel.Caption := '';
  ProgressLabel.Font.Color := clGray;
  
  // Customize wizard form appearance
  WizardForm.WelcomeLabel1.Font.Size := 14;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  
  WizardForm.FinishedHeadingLabel.Font.Size := 14;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
end;

// Custom page captions and install type handling
procedure CurPageChanged(CurPageID: Integer);
begin
  // Update portable mode based on selection
  if CurPageID = InstallTypePage.ID then
  begin
    WizardForm.NextButton.Caption := 'Next';
  end
  else if CurPageID = InstallTypePage.ID + 1 then
  begin
    // User just left the install type page, capture their selection
    PortableMode := InstallTypePage.Values[1];
  end;
  
  case CurPageID of
    wpWelcome:
      WizardForm.NextButton.Caption := 'Get Started';
    wpSelectDir:
      WizardForm.NextButton.Caption := 'Next';
    wpSelectProgramGroup:
      WizardForm.NextButton.Caption := 'Next';
    wpSelectTasks:
      WizardForm.NextButton.Caption := 'Next';
    wpReady:
      WizardForm.NextButton.Caption := 'Install';
    wpInstalling:
      WizardForm.NextButton.Caption := 'Installing...';
    wpFinished:
      WizardForm.NextButton.Caption := 'Finish';
  else
    WizardForm.NextButton.Caption := 'Next';
  end;
end;

// Skip tasks page for portable install, but always show components
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  // Skip the tasks page for portable installs (no desktop icon option needed)
  if (PageID = wpSelectTasks) and PortableMode then
    Result := True;
  // Never skip the components page
  if PageID = wpSelectComponents then
    Result := False;
end;

// Show percentage during installation
procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
var
  Percent: Integer;
begin
  if MaxProgress > 0 then
  begin
    Percent := (CurProgress * 100) div MaxProgress;
    WizardForm.StatusLabel.Caption := Format('Installing files... %d%%', [Percent]);
  end;
end;

// Validate before install
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  // Add any pre-install checks here
end;

// Called when setup starts
function InitializeSetup: Boolean;
begin
  PortableMode := False; // Default to full install
  Result := True;
end;

// Called when setup ends
procedure DeinitializeSetup;
begin
  // Cleanup if needed
end;
