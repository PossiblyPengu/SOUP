# S.A.P Installer

Modern Windows installer for S.A.P (S.A.M. Add-on Pack).

## ✨ Features

- **Modern UI** - Clean, contemporary installer design
- **Module Selection** - Choose which modules to install
- **Self-Contained** - No .NET runtime required
- **Custom Shortcuts** - Desktop and Start Menu shortcuts
- **Version Tracking** - Installation info saved for updates

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
.\build-installer.ps1 -DotnetPath "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
```

## Output

After a successful build:

- **Published application**: `SAP\publish\` folder
- **Installer**: `installer\Output\SAP-Setup-v4.1.0.exe`

## Installation Types

| Type | Description | Modules Included |
|------|-------------|------------------|
| **Full** | All modules (Recommended) | All 4 modules + Dictionary |
| **Minimal** | Core + AllocationBuddy | AllocationBuddy + Dictionary |
| **Custom** | User-selected | Choose which modules to enable |

## Included Modules

| Module | Description |
|--------|-------------|
| 📅 **ExpireWise** | Expiration date tracking and management |
| 📊 **AllocationBuddy** | Store allocation matching and tracking |
| ✅ **EssentialsBuddy** | Essential items inventory tracking |
| 🏷️ **SwiftLabel** | Quick label generation (always included) |

## Data Files

| File | Description |
|------|-------------|
| 📚 **Item Dictionary** | 13,000+ items for quick lookup |

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
