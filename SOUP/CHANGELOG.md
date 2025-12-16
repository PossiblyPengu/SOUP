# Changelog

All notable changes to S.O.U.P (S.A.M. Add-on Pack) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.4.0] - 2025-12-15

### Added

- **S.A.P NUKEM Campaign Mode** - 5-level campaign with unique designed levels
  - Level 1: The Spreadsheet Dimension - Blue office fog atmosphere
  - Level 2: Cubicle Nightmare - Green flickering maze layout
  - Level 3: Server Room Inferno - Red heat haze with lava floors
  - Level 4: Boardroom of Doom - Multi-key puzzle with yellow/brown tones
  - Level 5: CEO's Lair - Boss arena with purple eldritch glow
- **Level Briefing System** - Story briefings before each campaign level
- **Boss Encounters** - Final boss fight with victory condition
- **Main Menu** - Campaign vs Endless mode selection

### Improved

- **Enhanced Graphics Engine**
  - Perlin noise-based procedural textures for walls/floors/ceilings
  - Dynamic lighting system (muzzle flash, projectile glow, pickup auras)
  - Atmospheric fog per level with customizable color
  - Post-processing effects: vignette, color grading, scanlines, chromatic aberration
  - Improved sprite rendering with consistent fog application
- **Movement System** - Velocity-based acceleration with sprint and stamina

### Fixed

- Removed unused variables causing compiler warnings
- Code cleanup and QOL improvements

---

## [4.2.1] - 2025-12-11

### Changed

- Removed legacy theme code and simplified theme handling
- Updated splash screen styling for consistency across all modules

---

## [4.2.0] - 2025-12-05

### Added

- **Installer Options** - Choose between Full and Portable installation
  - Full install: Framework-dependent (~15 MB), requires .NET 8 Runtime
  - Portable install: Self-contained (~75 MB), runs anywhere without dependencies

### Changed (UI)

- **Enhanced Gradients** - More prominent gradient backgrounds in both themes
  - Sidebar uses smooth vertical gradient transitions
  - Module splash screens have vibrant color progressions
  - Title bar now matches sidebar color for seamless appearance
- **About Button** - Moved from launcher sidebar into Settings window
- **Launcher Sidebar** - Removed scroll, fixed app list display
- **S.O.U.P Title** - Now properly follows light/dark theme colors

### Visual Improvements

- Dark theme: Deeper, richer color gradients with purple/indigo/green/pink accents
- Light theme: Brighter, more colorful gradients while maintaining clean aesthetic
- Seamless visual flow from title bar through sidebar to content area

---

## [4.1.0] - 2025-12-05

### Added (Features)

- **About Dialog** - Press F1 or click About button to view version, modules, and changelog
- **Window Position Persistence** - Windows remember their position and size between sessions
- **Keyboard Shortcuts Panel** - Launcher sidebar now shows available shortcuts
- **New Keyboard Shortcuts**:
  - `Ctrl+T` - Toggle theme (light/dark)
  - `Escape` - Return to launcher from any module
  - `Alt+H` - Alternative home navigation
  - `F1` - Open About dialog

### Changed (Internal)

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

### Added (Major Release)

- **Fourth Major Release** of S.O.U.P (S.A.M. Add-on Pack)

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

S.O.U.P uses [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible changes
- **MINOR** version for new functionality (backwards compatible)
- **PATCH** version for bug fixes (backwards compatible)

## Updating the Version

When releasing a new version:

1. Update `SOUP.csproj`:
   - `<Version>`
   - `<AssemblyVersion>`
   - `<FileVersion>`

2. Update `Core/AppVersion.cs`:
   - `Version` constant
   - `BuildDate` constant
   - Add new `ChangelogEntry` to `Changelog` list

3. Update this `CHANGELOG.md` file

4. Rebuild and republish the application

