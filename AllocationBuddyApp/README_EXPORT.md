How to export and build the Allocation Buddy standalone copy

1. From PowerShell run the provided script (adjust the repo root if your workspace is located elsewhere):

```powershell
Set-Location D:\CODE\Cshp\AllocationBuddyApp
.\export-allocationbuddy.ps1 -RepoRoot "D:\CODE\Cshp" -OutDir "D:\CODE\Cshp\AllocationBuddyApp\src"
```

2. The script copies these projects into `AllocationBuddyApp/src`:
- `BusinessToolsSuite.Core`
- `BusinessToolsSuite.Infrastructure`
- `BusinessToolsSuite.Shared`
- `BusinessToolsSuite.Features.AllocationBuddy`
- `BusinessToolsSuite.Tools.ImportTester` (optional test harness)

3. The script will attempt to create a solution `AllocationBuddyApp.sln` and add the copied projects.

4. Open the solution in Visual Studio / VS Code and build. You may need to:
- Restore NuGet packages: `dotnet restore` in the solution directory
- Adjust project references if there are path mismatches
- Update any file paths that assumed the monorepo layout

Notes:
- This export copies source files; it does not remove references to other modules in the original repo. You may need to edit `Program.cs` or DI registrations in the copied projects to remove dependencies on removed modules.
- For a true standalone app you can keep the included `ImportTester` console app to validate parsing/import flows without the full UI.
