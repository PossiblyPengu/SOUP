; ============================================================================
;                        S.A.P - S.A.M. Add-on Pack
;                        Installer Script v4.1.0
;
;  Modern Inno Setup 6 installer with custom styling
; ============================================================================
#define MyAppName "S.A.P"
#define MyAppFullName "S.A.M. Add-on Pack"
#define MyAppVersion "4.1.0"
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
; Files to Install
; ============================================================================
[Files]
; Full install - framework-dependent (requires .NET 8 runtime)
Source: "..\publish-framework\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not IsPortableInstall
; Portable install - self-contained (bundled .NET)
Source: "..\publish-portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsPortableInstall

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
  ModulesLabel.Height := 100;
  ModulesLabel.AutoSize := False;
  ModulesLabel.WordWrap := True;
  ModulesLabel.Caption := 
    'Included Modules:' + #13#10 +
    '  * ExpireWise - Expiry date tracking' + #13#10 +
    '  * AllocationBuddy - Resource allocation' + #13#10 +
    '  * EssentialsBuddy - Core workflows' + #13#10 +
    '  * SwiftLabel - Label generation';
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

// Skip tasks page for portable install
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  // Skip the tasks page for portable installs (no desktop icon option needed)
  if (PageID = wpSelectTasks) and PortableMode then
    Result := True;
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
