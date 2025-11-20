# Business Tools Suite

A unified Electron application combining three powerful business tools into one seamless workspace.

## 🎯 What is Business Tools Suite?

Business Tools Suite is a desktop application that provides a centralized launcher for three essential business management tools:

1. **📦 ExpireWise** - Modern expiration tracking and inventory lifecycle management
2. **📊 Allocation Buddy** - Store allocation management with smart categorization
3. **📋 Essentials Buddy** - Business Central bin contents reporting

## ✨ Features

### Unified Benefits
- **Single Installation**: One app, three powerful tools
- **Consistent Design**: Unified purple gradient theme across all modules
- **Dark Mode**: System-wide dark mode toggle
- **Offline First**: All tools work 100% offline
- **Cross-Platform**: Windows, macOS, and Linux support
- **Keyboard Shortcuts**: Quick navigation between modules

### Module Features

#### ExpireWise
- Track product expiration dates by month and year
- Analytics dashboard with charts and insights
- Color-coded alerts for expiring items
- Excel import/export functionality
- Advanced search and filtering

#### Allocation Buddy
- Read and organize store allocations from Excel/CSV
- Smart categorization and ranking system
- Live data updates and filtering
- Advanced sorting by store, rank, or category
- Export processed allocations

#### Essentials Buddy
- Generate inventory status reports from Business Central
- Pre-loaded master data with 189 items
- Color-coded status indicators
- Smart filtering and search
- Excel and CSV export options

## 🚀 Quick Start

### Prerequisites
- Node.js 14.x or higher
- npm or yarn

### Installation

```bash
# Navigate to the UnifiedApp directory
cd UnifiedApp

# Install dependencies
npm install

# Start the application
npm start
```

### Development Mode

```bash
# Run with DevTools open
npm run dev
```

### Building for Distribution

```bash
# Build for Windows
npm run build:win

# Build for all platforms
npm run build:all

# Create portable version
npm run package
```

## 📁 Project Structure

```
UnifiedApp/
├── src/
│   ├── main/
│   │   ├── main.js              # Electron main process
│   │   └── preload.js           # Secure IPC bridge
│   ├── renderer/
│   │   ├── index.html           # Launcher home screen
│   │   ├── css/
│   │   │   ├── unified-theme.css    # Shared design system
│   │   │   └── launcher.css         # Launcher-specific styles
│   │   ├── js/
│   │   │   └── launcher.js          # Launcher logic
│   │   └── modules/
│   │       ├── expirewise/          # ExpireWise module
│   │       ├── allocation-buddy/    # Allocation Buddy module
│   │       └── essentials-buddy/    # Essentials Buddy module
│   └── shared/                  # Shared utilities
├── build/                       # Build assets
├── package.json                 # Dependencies and scripts
└── README.md                    # This file
```

## 🎨 Design System

The app uses a unified design system with:
- **Brand Colors**: Purple gradient (#667eea → #764ba2)
- **Typography**: System fonts (-apple-system, Segoe UI, Roboto)
- **Spacing**: 4px base scale
- **Shadows**: Consistent elevation system
- **Borders**: Rounded corners with consistent radius
- **Dark Mode**: Automatic theme switching

## ⌨️ Keyboard Shortcuts

- `Alt + 1` - Launch ExpireWise
- `Alt + 2` - Launch Allocation Buddy
- `Alt + 3` - Launch Essentials Buddy
- `ESC` - Return to Launcher

## 🛠️ Development

### Adding New Modules

1. Create a new directory in `src/renderer/modules/your-module/`
2. Add `index.html`, CSS, and JS files
3. Register the module in `launcher.js`
4. Update the launcher UI in `index.html`

### Customizing the Theme

Edit `src/renderer/css/unified-theme.css` to modify:
- Color variables
- Typography scale
- Spacing system
- Component styles

### Module Integration

To fully integrate the existing standalone apps:

1. **Copy module files** from the original apps into the respective module directories
2. **Update CSS imports** to use the unified theme
3. **Adapt file paths** for assets and dependencies
4. **Test module functionality** within the unified app

## 📦 Building

The app uses `electron-forge` for development and packaging (see `package.json` scripts).

```bash
# Build for Windows
npm run build:win

# Package for local testing
npm run package

# Create installers for distribution
npm run make
```

Output artifacts will be created by the forge makers and placed in the `out/` or archive directories depending on the maker configuration.

## 🔧 Configuration

### Electron Builder

Configuration is in `package.json` under the `build` key:
- App ID: `com.businesstools.suite`
- Product Name: Business Tools Suite
- Output directory: `dist/`

### Window Settings

Main window configuration in `src/main/main.js`:
- Default size: 1400x900
- Minimum size: 1000x700
- Background: Purple gradient

## 🌐 Module Status

### Current Implementation
All three modules have placeholder interfaces with:
- Unified branding and design
- Navigation to standalone versions
- Feature descriptions
- Quick action buttons

### Full Integration (Next Steps)
To fully integrate each module:

1. **ExpireWise**: Copy files from `../EXPTRACK/src/renderer/`
2. **Allocation Buddy**: Copy files from `../AB/NewAB/src/`
3. **Essentials Buddy**: Copy files from `../ESSB/webapp/`

Then adapt file paths and dependencies for the unified structure.

## 🔒 Security

- Context isolation enabled
- Node integration disabled
- Secure IPC communication via preload script
- Content Security Policy enforced

### Notes on dictionary persistence

Previously, saving dictionaries attempted to write into application source files. The unified app now saves dictionary exports to the user's application data folder (platform-specific) under a `dictionaries/` subfolder. This ensures saved dictionaries persist across updates and works in packaged apps. When migrating, copies saved from previous versions should be placed into `%APPDATA%/Business Tools Suite/dictionaries` (Windows) or the corresponding `app.getPath('userData')` location.

## 📄 License

MIT License - See LICENSE file for details

## 🤝 Contributing

This is a unified workspace combining three separate tools. Each module maintains its own functionality while sharing a common design system.

## 📞 Support

For issues or questions about specific modules, refer to their original documentation:
- ExpireWise: `../EXPTRACK/README.md`
- Allocation Buddy: `../AB/NewAB/BUILD.md`
- Essentials Buddy: `../ESSB/webapp/README.md`

---

**Version**: 1.0.0
**Platform**: Electron
**Node.js**: 14.x or higher
