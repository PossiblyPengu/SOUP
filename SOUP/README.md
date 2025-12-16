# S.O.U.P - S.A.M. Operations Utilities Pack

A suite of inventory management tools built with WPF and .NET 8.

## ðŸ“¦ Modules

### AllocationBuddy
Track and manage inventory allocation across multiple store locations.

### EssentialsBuddy
Monitor essential items and stock levels with dictionary-based matching.

### ExpireWise
Track product expiration dates with visual status indicators.

### SwiftLabel
Quick store label generation utility.

## ðŸš€ Quick Start

### Prerequisites
- .NET 8 SDK (or use the local SDK in `important files` folder)
- Windows 10/11

### Running the Application

```powershell
# Run the full suite
.\run-suite.ps1

# Or use the scripts folder for more options
.\scripts\run-suite.ps1 -Release  # Build in Release mode
.\scripts\run-suite.ps1 -NoBuild  # Skip build step
```

### Individual Modules

```powershell
.\scripts\run-allocationbuddy.ps1
.\scripts\run-essentialsbuddy.ps1
.\scripts\run-expirewise.ps1
```

## ðŸ“ Project Structure

```
SOUP/
â”œâ”€â”€ src/
â”‚   â”œâ”€â”€ SOUP/                    # Main application
â”‚   â”‚   â”œâ”€â”€ Behaviors/          # WPF attached behaviors
â”‚   â”‚   â”œâ”€â”€ Converters/         # Value converters
â”‚   â”‚   â”œâ”€â”€ Core/               # Domain entities and interfaces
â”‚   â”‚   â”‚   â”œâ”€â”€ Common/         # Base classes
â”‚   â”‚   â”‚   â”œâ”€â”€ Entities/       # Domain models
â”‚   â”‚   â”‚   â””â”€â”€ Interfaces/     # Repository interfaces
â”‚   â”‚   â”œâ”€â”€ Data/               # Data access layer
â”‚   â”‚   â”œâ”€â”€ Helpers/            # Utility classes
â”‚   â”‚   â”œâ”€â”€ Infrastructure/     # Implementation details
â”‚   â”‚   â”‚   â”œâ”€â”€ Data/           # Database context
â”‚   â”‚   â”‚   â”œâ”€â”€ Repositories/   # Repository implementations
â”‚   â”‚   â”‚   â””â”€â”€ Services/       # Service implementations
â”‚   â”‚   â”œâ”€â”€ Models/             # UI models
â”‚   â”‚   â”œâ”€â”€ Services/           # Application services
â”‚   â”‚   â”œâ”€â”€ Themes/             # Light/Dark theme resources
â”‚   â”‚   â”œâ”€â”€ ViewModels/         # MVVM ViewModels
â”‚   â”‚   â”œâ”€â”€ Views/              # XAML views
â”‚   â”‚   â””â”€â”€ Windows/            # Application windows
â”‚   â”œâ”€â”€ AllocationBuddy.Standalone/
â”‚   â”œâ”€â”€ EssentialsBuddy.Standalone/
â”‚   â””â”€â”€ ExpireWise.Standalone/
â”œâ”€â”€ installer/                  # Inno Setup installer
â”œâ”€â”€ publish/                    # Published releases
â””â”€â”€ tools/                      # Development utilities
scripts/                        # Build and run scripts
```

## ðŸ› ï¸ Development

### Building

```powershell
# Using local SDK
$dotnet = "D:\CODE\important files\dotnet-sdk-8.0.404-win-x64\dotnet.exe"
& $dotnet build SAP\src\SAP\SAP.csproj
```

### Publishing

```powershell
& $dotnet publish SAP\src\SAP\SAP.csproj -c Release -o SAP\publish
```

### Creating Installer

See `SOUP/installer/README.md` for Inno Setup instructions.

## ðŸ“ Features

- **Dark/Light Theme** - Toggle between themes with automatic persistence
- **Data Persistence** - Session data saved to `%APPDATA%\SAP\`
- **Auto-Archive** - Automatic archiving when importing new data
- **Dictionary Matching** - Match items against a central dictionary database
- **Import/Export** - Support for Excel and CSV file formats
- **Copy to Clipboard** - Quick copy functionality for allocation data

## ðŸ“„ License

Copyright Â© 2024-2025 PossiblyPengu

## ðŸ¤ Contributing

This is a private project. Please contact the author for contribution guidelines.

