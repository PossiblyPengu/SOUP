# Building Store Allocation Viewer

This guide explains how to package the app into a distributable Windows installer.

## Prerequisites

Make sure all dependencies are installed:
```powershell
npm install
```

## Build Commands

### Quick Build (Portable ZIP)
```powershell
npm run package
```
- Creates a portable version in `out/` folder
- No installation required
- Can run from any folder

### Full Installer Build
```powershell
npm run make
```
- Creates Windows installer (.exe)
- Creates portable ZIP
- Output in `out/make/` folder

### Windows-Only Build
```powershell
npm run build:win
```
- Builds only for Windows platform
- Faster than cross-platform build

## Output Files

After running `npm run make`, you'll find:

### Windows Installer
- **Location**: `out/make/squirrel.windows/x64/`
- **File**: `StoreAllocationViewer-Setup.exe`
- **Type**: Full installer with auto-updates support
- **Size**: ~150-200MB

### Portable Version
- **Location**: `out/make/zip/win32/x64/`
- **File**: `store-allocation-viewer-win32-x64-2.0.0.zip`
- **Type**: Portable app (no installation needed)
- **Size**: ~150-200MB

## Distribution

### For Users
1. **Installer**: Share `StoreAllocationViewer-Setup.exe`
   - Users double-click to install
   - Creates desktop shortcut
   - Adds to Start Menu

2. **Portable**: Share the ZIP file
   - Users extract and run `StoreAllocationViewer.exe`
   - No installation required
   - Can run from USB drive

## Build Configuration

Configuration is in `forge.config.js`:
- **App Name**: StoreAllocationViewer
- **Executable**: StoreAllocationViewer.exe
- **ASAR**: Enabled (packages files into single archive)

## Troubleshooting

### Build Fails
```powershell
# Clear cache and rebuild
rm -r -Force out
npm run make
```

### Missing Dependencies
```powershell
npm install
```

### Large File Size
The app includes:
- Electron runtime (~120MB)
- Node.js libraries (XLSX, PapaCSV)
- Your app code and dictionary

This is normal for Electron apps.

## Testing the Build

After building, test the installer:
1. Navigate to `out/make/squirrel.windows/x64/`
2. Run `StoreAllocationViewer-Setup.exe`
3. Follow installation wizard
4. Test all features

Or test the portable version:
1. Navigate to `out/make/zip/win32/x64/`
2. Extract the ZIP file
3. Run `StoreAllocationViewer.exe` from the extracted folder
4. Test all features

## Version Updates

To update the version:
1. Edit `package.json` - change `"version": "2.0.0"`
2. Rebuild: `npm run make`
3. New installer will have the updated version number
