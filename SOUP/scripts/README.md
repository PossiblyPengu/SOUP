# SOUP Build Scripts

Quick and easy build scripts for the SOUP project.

## Quick Commands (Batch Files)

From the project root:

```cmd
build               # Debug build
build release       # Release build
build clean         # Clean build artifacts

run                 # Build and run (Debug)
run release         # Build and run (Release)
run nobuild         # Run without building

publish             # Publish framework + portable
publish installer   # Publish and create installer
```

## PowerShell Scripts

For more control, use the PowerShell scripts in the `scripts/` folder:

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
.\scripts\clean.ps1 -All               # Clean everything including publish
```

## Output Locations

| Type | Location | Notes |
|------|----------|-------|
| Debug Build | `src/SOUP/bin/Debug/net8.0-windows/` | For development |
| Release Build | `src/SOUP/bin/Release/net8.0-windows/` | For testing |
| Framework Publish | `publish-framework/` | Requires .NET 8 (~27 MB) |
| Portable Publish | `publish-portable/` | Self-contained (~80 MB) |
| Installer | `installer/SOUP-Setup-X.X.X.exe` | Full installer |

## Requirements

- .NET 8 SDK (local or system `dotnet`)
- Inno Setup 6 (for installer) - [Download](https://jrsoftware.org/isdl.php)

## Version Management

Update version in two places:

1. `src/SOUP/SOUP.csproj` - `<Version>X.X.X</Version>`
2. `installer/SOUP.iss` - `#define MyAppVersion "X.X.X"`
