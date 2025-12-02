# SAP Installer

This folder contains scripts to build and package the SAP application as a Windows installer.

## Prerequisites

1. **.NET SDK 8.0 or later** - For building the application
2. **Inno Setup 6** - For creating the installer
   - Download from: https://jrsoftware.org/isdl.php
   - Install with default options

## Building the Installer

### Quick Build

Run the build script from PowerShell:

```powershell
cd installer
.\build-installer.ps1
```

### Build Options

```powershell
# Use a specific dotnet path (e.g., portable SDK)
.\build-installer.ps1 -DotnetPath "d:\path\to\dotnet.exe"

# Skip the build step (use existing publish folder)
.\build-installer.ps1 -SkipBuild

# Skip installer creation (just build the app)
.\build-installer.ps1 -SkipInstaller

# Debug build
.\build-installer.ps1 -Configuration Debug
```

### Using Portable .NET SDK

If you're using a portable .NET SDK:

```powershell
.\build-installer.ps1 -DotnetPath "d:\CODE\important files\dotnet-sdk-9.0.306-win-x64\dotnet.exe"
```

## Output

After a successful build:

- **Published application**: `publish\` folder in the SAP root directory
- **Installer**: `installer\Output\SAP-Setup-1.0.0.exe`

## Installation Types

The installer offers three installation types:

| Type | Description | Modules Included |
|------|-------------|------------------|
| **Full** | All modules and data | AllocationBuddy, EssentialsBuddy, ExpireWise, Dictionary DB |
| **Compact** | Core + AllocationBuddy | AllocationBuddy, Dictionary DB |
| **Custom** | User-selected | User chooses which modules to enable |

### Available Components

- **SAP Core Application** (required) - The main application framework
- **Modules**
  - **AllocationBuddy** - Inventory allocation and matching
  - **EssentialsBuddy** - Essentials tracking and management
  - **ExpireWise** - Expiration date tracking
- **Data Files**
  - **Dictionary Database** - 13,000+ items for AllocationBuddy matching

## What's Included

The installer packages:
- SAP application (self-contained, no .NET runtime required)
- All dependencies
- Assets and resources
- Module configuration based on user selection

## Installation Details

- **Default install location**: `C:\Program Files\SAP`
- **User data location**: `%APPDATA%\SAP\`
  - `Shared\dictionaries.db` - Dictionary database
  - `Data\SAP.db` - Application data
  - `Logs\` - Log files
  - `modules.ini` - Module enable/disable configuration

## Module Configuration

After installation, module visibility is controlled by `%APPDATA%\SAP\modules.ini`:

```ini
[Modules]
AllocationBuddy=true
EssentialsBuddy=true
ExpireWise=true

[Info]
InstalledVersion=1.0.0
InstallDate=2025-12-02 10:30:00
```

Users can manually edit this file to enable/disable modules, or reinstall with different options.

## Updating the Version

Edit `SAP.iss` and change:
```
#define MyAppVersion "1.0.0"
```

## Manual Inno Setup Compilation

If you prefer to compile manually:

1. Open `SAP.iss` in Inno Setup Compiler
2. Press F9 or click Compile

Note: You must run `build-installer.ps1 -SkipInstaller` first to create the publish folder.
