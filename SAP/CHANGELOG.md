# Changelog

All notable changes to S.A.P (S.A.M. Add-on Pack) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.1.0] - 2025-12-05

### Added
- **About Dialog** - Press F1 or click About button to view version, modules, and changelog
- **Window Position Persistence** - Windows remember their position and size between sessions
- **Keyboard Shortcuts Panel** - Launcher sidebar now shows available shortcuts
- **New Keyboard Shortcuts**:
  - `Ctrl+T` - Toggle theme (light/dark)
  - `Escape` - Return to launcher from any module
  - `Alt+H` - Alternative home navigation
  - `F1` - Open About dialog

### Changed
- Centralized version information in `AppVersion.cs`
- Updated version badge in launcher to use centralized version

### Improved
- Added comprehensive XML documentation to all code files
- Reorganized project structure to modern C# standards
- Updated .gitignore with comprehensive exclusions
- Added README.md with project documentation

### Fixed
- Cleaned up temporary and backup files from repository

---

## [4.0.0] - 2025-12-01

### Added
- **Fourth Major Release** of S.A.P (S.A.M. Add-on Pack)

#### ExpireWise
- Expiration date tracking and management
- Excel import/export support
- Item dictionary for quick entry
- Visual expiration status indicators

#### AllocationBuddy
- Store allocation management
- RPG-style allocation view
- Session archiving and restore
- Multi-store allocation tracking

#### EssentialsBuddy
- Essential items inventory tracking
- 9-90 bin validation
- Essential vs non-essential item flagging

#### SwiftLabel
- Quick label generation
- Customizable label templates

#### Core Features
- Dark and Light theme support with persistence
- LiteDB database for data persistence
- Modular installer with component selection
- Keyboard shortcuts for module navigation (Alt+1-4)

---

## Version History

| Version | Date       | Description            |
|---------|------------|------------------------|
| 4.1.0   | 2025-12-05 | Quality of Life Update |
| 4.0.0   | 2025-12-01 | Fourth Major Release   |

---

## Versioning

S.A.P uses [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible changes
- **MINOR** version for new functionality (backwards compatible)
- **PATCH** version for bug fixes (backwards compatible)

## Updating the Version

When releasing a new version:

1. Update `SAP.csproj`:
   - `<Version>`
   - `<AssemblyVersion>`
   - `<FileVersion>`

2. Update `Core/AppVersion.cs`:
   - `Version` constant
   - `BuildDate` constant
   - Add new `ChangelogEntry` to `Changelog` list

3. Update this `CHANGELOG.md` file

4. Rebuild and republish the application
