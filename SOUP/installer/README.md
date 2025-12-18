# SOUP Installer

This folder contains the Inno Setup installer configuration for SOUP.

## Files

| File | Description |
|------|-------------|
| `SOUP.iss` | Inno Setup script - main installer configuration |
| `LICENSE.txt` | License agreement shown during installation |
| `WizardImage.bmp` | (Optional) Large sidebar image (164×314 pixels) |
| `WizardSmallImage.bmp` | (Optional) Small header image (55×55 pixels) |

## Building the Installer

### Prerequisites
- [Inno Setup 6](https://jrsoftware.org/isdl.php) installed
- Application published via `.\scripts\publish.ps1`

### Build Commands

**Recommended:** Use the publish script with `-Installer` flag:
```powershell
.\scripts\publish.ps1 -Installer
```

**Manual build:**
```powershell
# First publish the app
.\scripts\publish.ps1 -Framework

# Then build installer
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" .\installer\SOUP.iss
```

## Output

The installer is created at: `installer-output\SOUP-Setup-{version}.exe`

## Customizing Installer Images

To add custom branding images to the installer:

### Wizard Image (Large Sidebar)
- **File:** `WizardImage.bmp`
- **Size:** 164×314 pixels (or 192×386 for high DPI)
- **Displayed:** Left side of Welcome, License, and Finish pages

### Wizard Small Image (Header)
- **File:** `WizardSmallImage.bmp`  
- **Size:** 55×55 pixels (or 64×64 for high DPI)
- **Displayed:** Top-right corner of all wizard pages

### Creating Images

1. Create BMP images at the sizes above
2. Place them in this `installer` folder
3. Uncomment the lines in `SOUP.iss`:
   ```ini
   WizardImageFile=WizardImage.bmp
   WizardSmallImageFile=WizardSmallImage.bmp
   ```

### Image Guidelines
- Use your app's logo or branding
- Keep it simple and professional
- Test the installer to ensure images display correctly

## Switching Between Framework and Self-Contained

In `SOUP.iss`, find the `[Files]` section:

**Framework-dependent** (default - smaller, requires .NET 8):
```ini
Source: "..\publish-framework\*"; DestDir: "{app}"; ...
```

**Self-contained** (larger, no runtime needed):
```ini
Source: "..\publish-portable\*"; DestDir: "{app}"; ...
```

## Updating Version

When releasing a new version:
1. Update `Version` in `src\SOUP.csproj`
2. Update `#define MyAppVersion` in `SOUP.iss`
3. Rebuild the installer
