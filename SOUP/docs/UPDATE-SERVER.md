# SOUP Local Update Server Setup

## Quick Start

1. **Publish the app:**
   ```powershell
   .\scripts\publish.ps1
   ```
   This automatically creates both `version.json` and `SOUP-portable.zip`.

2. **Start the server:**
   ```powershell
   .\scripts\serve-updates.ps1
   ```

3. **Test it:**
   - Open SOUP → Settings → About → "Check for Updates"
   - If an update is available, click "Yes" to download and install automatically
   - SOUP will restart with the new version

---

## How Auto-Update Works

1. SOUP checks `http://localhost:8080/version.json` for the latest version
2. If newer, user is prompted to update
3. The portable zip is downloaded with progress indication
4. A batch script extracts files and restarts the app

---

## Files Structure

After publishing, your `publish\` folder contains:
```
publish\
├── version.json         (auto-generated)
└── SOUP-portable.zip    (auto-generated)
```

---

## For Other Machines on the Network

### On the Server Machine (this PC):

1. Update the URL in `src\Services\UpdateService.cs`:
   ```csharp
   private const string UpdateManifestUrl = "http://YOUR-PC-NAME:8080/version.json";
   ```
   Or use your IP address: `http://192.168.x.x:8080/version.json`

2. Rebuild: `.\scripts\publish.ps1`

3. Allow through Windows Firewall:
   ```powershell
   New-NetFirewallRule -DisplayName "SOUP Update Server" -Direction Inbound -Port 8080 -Protocol TCP -Action Allow
   ```

4. Run the server (may need Admin for network access):
   ```powershell
   .\scripts\serve-updates.ps1
   ```

### On Client Machines:

- Install SOUP from the `publish-portable\` folder or installer
- "Check for Updates" will download and install automatically

---

## Releasing a New Version

1. Make your code changes

2. Bump version and publish:
   ```powershell
   .\scripts\publish.ps1 -BumpPatch
   ```
   Or for bigger updates: `-BumpMinor` or `-BumpMajor`

3. Create the ZIP:
   - Delete old `publish\SOUP-portable.zip`
   - Zip `publish-portable\*` → `publish\SOUP-portable.zip`

4. Restart the server (or it will serve the new files automatically)

5. Users click "Check for Updates" → sees new version available

---

## Server Options

```powershell
# Default port 8080
.\scripts\serve-updates.ps1

# Custom port
.\scripts\serve-updates.ps1 -Port 9000
```

---

## Troubleshooting

**"Cannot reach update server"**
- Is `serve-updates.ps1` running?
- Check firewall settings
- Verify URL in UpdateService.cs matches server

**"Update server not configured"**  
- The `version.json` file is missing from `publish\` folder

**Server won't start on network**
- Run PowerShell as Administrator
- Add firewall rule (see above)

**Clients can't connect**
- Use IP address instead of hostname
- Check that port 8080 is open
- Verify both machines are on the same network
