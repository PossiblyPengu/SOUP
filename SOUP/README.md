# S.O.U.P - S.A.M. Operations Utilities Pack

A suite of inventory management tools built with WPF and .NET 8.

## 📦 Modules

### AllocationBuddy

Track and manage inventory allocation across multiple store locations.

### EssentialsBuddy

Monitor essential items and stock levels with dictionary-based matching.

### ExpireWise

Track product expiration dates with visual status indicators.

### SwiftLabel

Quick store label generation utility.

### OrderLog

Track and manage orders with drag-and-drop organization.

## 🚀 Quick Start

### Prerequisites

- .NET 8 SDK
- Windows 10/11

### Running the Application

```powershell
# Run the application (Debug mode)
.\scripts\run.ps1

# Run in Release mode
.\scripts\run.ps1 -Release

# Run without building (uses last build)
.\scripts\run.ps1 -NoBuild

# Run only the OrderLog widget
.\scripts\run-widget.ps1
```

### Building

```powershell
# Debug build
.\scripts\build.ps1

# Release build with clean
.\scripts\build.ps1 -Release -Clean
```

## 📁 Project Structure

```text
SOUP/
├── src/                        # Main application source
│   ├── Behaviors/              # WPF attached behaviors
│   ├── Converters/             # Value converters
│   ├── Core/                   # Domain entities and interfaces
│   │   ├── Common/             # Base classes
│   │   ├── Entities/           # Domain models
│   │   └── Interfaces/         # Repository interfaces
│   ├── Data/                   # Data access layer
│   ├── Features/               # Feature modules (OrderLog, etc.)
│   ├── Helpers/                # Utility classes
│   ├── Infrastructure/         # Implementation details
│   │   ├── Data/               # Database context
│   │   ├── Repositories/       # Repository implementations
│   │   └── Services/           # Service implementations
│   ├── Models/                 # UI models
│   ├── Services/               # Application services
│   ├── Themes/                 # Light/Dark theme resources
│   ├── ViewModels/             # MVVM ViewModels
│   ├── Views/                  # XAML views
│   └── Windows/                # Application windows
├── installer/                  # Inno Setup installer
├── scripts/                    # Build and run scripts
└── tools/                      # Development utilities
```

## 🛠️ Development

### Building

```powershell
# Using scripts (recommended)
.\scripts\build.ps1 -Release -Clean -Restore

# Or directly with dotnet
dotnet build src/SOUP.csproj -c Release
```

### Publishing

```powershell
# Publish both framework-dependent and self-contained
.\scripts\publish.ps1

# Or directly
dotnet publish src/SOUP.csproj -c Release -o publish-framework
```

### Creating Installer

See `installer/README.md` for Inno Setup instructions.

## 📝 Features

- **Dark/Light Theme** - Toggle between themes with automatic persistence
- **Data Persistence** - Session data saved to `%APPDATA%\SOUP\`
- **Auto-Archive** - Automatic archiving when importing new data
- **Dictionary Matching** - Match items against a central dictionary database
- **Import/Export** - Support for Excel and CSV file formats
- **Copy to Clipboard** - Quick copy functionality for allocation data

## 📄 License

Copyright © 2024-2025 PossiblyPengu

## 🤝 Contributing

This is a private project. Please contact the author for contribution guidelines.

