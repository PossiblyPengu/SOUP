# SOUP Build Scripts

Quick and easy build scripts for the SOUP project.

## Quick Start

```powershell
.\scripts\dev.ps1 build      # Quick build
.\scripts\dev.ps1 run        # Build and run
.\scripts\dev.ps1 widget     # Run widget mode
.\scripts\dev.ps1 watch      # Hot reload
.\scripts\dev.ps1 clean      # Clean artifacts
.\scripts\dev.ps1 rebuild    # Clean + build
.\scripts\dev.ps1 info       # Show project info
```

## All Scripts

| Script | Description |
|--------|-------------|
| `dev.ps1` | Quick commands (build, run, widget, watch, clean, etc.) |
| `build.ps1` | Full build with options |
| `run.ps1` | Run the application |
| `run-widget.ps1` | Run in widget mode only |
| `watch.ps1` | Hot reload development |
| `clean.ps1` | Clean build artifacts |
| `publish.ps1` | Publish for distribution |
| `release.ps1` | Full release workflow |
| `test.ps1` | Run unit tests |
| `analyze.ps1` | Code quality checks |
| `git.ps1` | Git helpers |
| `tools.ps1` | Run project tools |

## Detailed Usage

### dev.ps1 - Quick Commands (recommended for daily use)

```powershell
.\scripts\dev.ps1 build      # Quick debug build
.\scripts\dev.ps1 run        # Build and run
.\scripts\dev.ps1 widget     # Run widget mode
.\scripts\dev.ps1 watch      # Hot reload
.\scripts\dev.ps1 clean      # Clean bin/obj
.\scripts\dev.ps1 rebuild    # Clean + build
.\scripts\dev.ps1 restore    # Restore NuGet
.\scripts\dev.ps1 check      # Build with warnings as errors
.\scripts\dev.ps1 format     # Format code
.\scripts\dev.ps1 info       # Show project stats
```

### build.ps1 - Build the project

```powershell
.\scripts\build.ps1                    # Debug build
.\scripts\build.ps1 -Release           # Release build  
.\scripts\build.ps1 -Clean             # Clean before building
.\scripts\build.ps1 -Restore           # Restore packages first
.\scripts\build.ps1 -Verbose           # Show detailed output
.\scripts\build.ps1 -Release -Clean    # Clean release build
```

### run.ps1 - Run the application

```powershell
.\scripts\run.ps1                      # Build and run (Debug)
.\scripts\run.ps1 -Release             # Build and run (Release)
.\scripts\run.ps1 -NoBuild             # Run without building
```

### watch.ps1 - Hot Reload Development

```powershell
.\scripts\watch.ps1                    # Run with hot reload
```

### publish.ps1 - Publish for distribution

```powershell
.\scripts\publish.ps1                  # Publish both versions
.\scripts\publish.ps1 -Framework       # Framework-dependent only (~27 MB)
.\scripts\publish.ps1 -Portable        # Self-contained only (~80 MB)
.\scripts\publish.ps1 -Installer       # Publish + create installer
```

### clean.ps1 - Clean build artifacts

```powershell
.\scripts\clean.ps1                    # Clean bin/obj folders
```

### test.ps1 - Run Tests

```powershell
.\scripts\test.ps1                     # Run all tests
.\scripts\test.ps1 -Filter "OrderLog"  # Filter by name
.\scripts\test.ps1 -Coverage           # With code coverage
.\scripts\test.ps1 -Verbose            # Detailed output
```

### analyze.ps1 - Code Quality

```powershell
.\scripts\analyze.ps1                  # Run all checks
```

Checks for:
- Build warnings
- TODO/FIXME comments
- Large files (>500 lines)
- Empty catch blocks

### git.ps1 - Git Helpers

```powershell
.\scripts\git.ps1 status     # Status with file counts
.\scripts\git.ps1 changes    # Changed files by type
.\scripts\git.ps1 diff       # Diff of staged changes
.\scripts\git.ps1 log        # Recent commits
.\scripts\git.ps1 branch     # Branch info
.\scripts\git.ps1 stash      # Stash changes
.\scripts\git.ps1 unstash    # Pop last stash
```

### tools.ps1 - Project Tools

```powershell
.\scripts\tools.ps1 list           # List tools
.\scripts\tools.ps1 import-dict    # Run ImportDictionary
.\scripts\tools.ps1 inspect-excel  # Run InspectExcel
.\scripts\tools.ps1 inspect-db     # Run InspectOrderDb
.\scripts\tools.ps1 build          # Build all tools
```

## Output Locations

| Type | Location | Notes |
|------|----------|-------|
| Debug Build | `src/bin/Debug/net10.0-windows/` | For development |
| Release Build | `src/bin/Release/net10.0-windows/` | For testing |
| Framework Publish | `publish-framework/` | Requires .NET 10 (~27 MB) |
| Portable Publish | `publish-portable/` | Self-contained (~80 MB) |
| Installer | `installer/SOUP-Setup-X.X.X.exe` | Full installer |

## Requirements

.NET 10 SDK (local or system `dotnet`)
- Inno Setup 6 (for installer) - [Download](https://jrsoftware.org/isdl.php)

## Version Management

Update version in two places:

1. `src/SOUP.csproj` - `<Version>X.X.X</Version>`
2. `installer/SOUP.iss` - `#define MyAppVersion "X.X.X"`
