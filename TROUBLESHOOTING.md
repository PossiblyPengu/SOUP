# Troubleshooting Guide - S.A.P (S.A.M. Add-on Pack)

## Problem: Apps don't launch from the launcher

### Quick Diagnostic Steps

1. **Build all apps first:**
   ```powershell
   .\build-all.ps1
   ```

2. **Test individual apps directly:**
   ```powershell
   # Test AllocationBuddy
   .\run-allocationbuddy.ps1

   # Test EssentialsBuddy
   .\run-essentialsbuddy.ps1

   # Test ExpireWise
   .\run-expirewise.ps1
   ```

3. **Then test the launcher:**
   ```powershell
   .\run-launcher.ps1
   ```

### Common Issues

#### Issue 1: Apps show console window
**Cause:** `OutputType` set to `Exe` instead of `WinExe`
**Fix:** Already fixed - all apps use `WinExe`

#### Issue 2: Missing executables
**Cause:** Apps not built
**Fix:** Run `.\build-all.ps1`

#### Issue 3: Missing Infrastructure reference
**Cause:** Desktop projects missing Infrastructure dependency
**Fix:** Already fixed - all Desktop projects reference Infrastructure

#### Issue 4: Missing DialogService registration
**Cause:** ViewModels depend on DialogService but it wasn't registered in DI container
**Symptoms:** App crashes on startup with error: "Unable to resolve service for type 'BusinessToolsSuite.Shared.Services.DialogService'"
**Fix:** Already fixed - DialogService registered in all standalone app Program.cs files

#### Issue 5: Apps crash on startup (if you encounter this in the future)
**Possible causes:**
- Missing DLL dependencies
- Database initialization errors
- View/ViewModel binding errors
- Missing service registrations in DI container

**How to diagnose:**
1. Temporarily change `OutputType` to `Exe` in the .csproj file to see console errors
2. Add console debugging output in Program.cs Main() method
3. Check logs at:
   - `%AppData%\AllocationBuddy\Logs\`
   - `%AppData%\EssentialsBuddy\Logs\`
   - `%AppData%\ExpireWise\Logs\`
   - `%AppData%\SAP\Logs\` (launcher)

### Manual Launch Test

To test if an app works, navigate to its directory and run it:

```powershell
# AllocationBuddy
cd SAP/src/AllocationBuddy.Standalone/bin/Debug/net8.0-windows
.\AllocationBuddy.exe

# EssentialsBuddy
cd SAP/src/EssentialsBuddy.Standalone/bin/Debug/net8.0-windows
.\EssentialsBuddy.exe

# ExpireWise
cd SAP/src/ExpireWise.Standalone/bin/Debug/net8.0-windows
.\ExpireWise.exe
```

### Verify Build Output

Check that all executables exist:
```powershell
ls SAP/src/*/bin/Debug/net8.0-windows/*.exe
```

You should see exe files:
1. `SAP/src/AllocationBuddy.Standalone/.../AllocationBuddy.exe`
2. `SAP/src/EssentialsBuddy.Standalone/.../EssentialsBuddy.exe`
3. `SAP/src/ExpireWise.Standalone/.../ExpireWise.exe`
4. `SAP/src/SAP/.../SAP.exe` (launcher)

### Current Configuration Status

✅ All apps configured as `WinExe` (no console window)
✅ All Desktop projects reference Infrastructure
✅ Desktop projects added to all standalone solutions
✅ DialogService registered in all standalone apps
✅ All apps build successfully
✅ All apps launch successfully

### If Apps Still Don't Launch

1. **Check Windows Event Viewer:**
   - Open Event Viewer
   - Go to Windows Logs > Application
   - Look for .NET Runtime errors

2. **Try running with dotnet:**
   ```powershell
   dotnet run --project SAP/src/AllocationBuddy.Standalone/AllocationBuddy.Standalone.csproj
   ```
   This will show any runtime errors in the console.

3. **Verify .NET 8 is installed:**
   ```powershell
   dotnet --version
   ```
   Should show version 8.0.x

### Getting Help

If you're still having issues, check:
1. Log files in `%AppData%\[AppName]\Logs\`
2. Windows Event Viewer for .NET errors
3. Run apps with `dotnet run` to see console output
