# SOUP Repository Audit Report

**Audit Date:** 2026-02-05  
**SDK Version:** .NET 10.0.101 (Portable SDK)  
**Build Status:** ✅ Successful (0 errors, 12 unique warnings)

---

## 📊 Codebase Statistics

| Metric | Value |
|--------|-------|
| Source Files (`.cs` + `.xaml`) | 204 |
| C# Lines of Code | ~43,000 |
| XAML Lines of Code | ~22,000 |
| **Total Lines** | **~65,000** |
| Test Lines | ~765 |
| Private Fields | 326 |

---

## ⚠️ Build Warnings (12 Unique)

### Nullability Warnings (CS8601/CS8619)
| File | Line | Issue |
|------|------|-------|
| `Features/OrderLog/Models/OrderTemplate.cs` | 30-32 | 3x possible null reference assignment |
| `Features/OrderLog/Services/UndoRedoService.cs` | 399, 416 | Dictionary nullability mismatch |
| `Features/OrderLog/Services/OrderLogExportService.cs` | 241-252 | 6x possible null reference assignment |

### Analyzer Warnings
| File | Line | Code | Issue |
|------|------|------|-------|
| `Features/OrderLog/Views/OrderTemplateEditorDialog.xaml.cs` | 157 | MA0026 | TODO comment in production code |
| `Features/OrderLog/Views/OrderLogWidgetView.xaml.cs` | 2663 | MA0134 | Unobserved async call |

**Recommendation:** Fix nullability warnings to improve null-safety; address the unobserved async call.

---

## 📝 TODO/FIXME Markers

**Total actionable TODOs found:** 4 (excluding Log.Debug statements)

| File | Line | Content |
|------|------|---------|
| `Features/ExpireWise/Helpers/KeyboardShortcutManager.cs` | 117 | `// Ctrl+T: Go to today's month (TODO: Implement TodayCommand)` |
| `Features/OrderLog/Views/OrderTemplateEditorDialog.xaml.cs` | 157 | `// TODO: Open color picker dialog (OrderColorPickerWindow)` |
| `Features/OrderLog/Views/OrderLogWidgetView.xaml.cs` | 2640 | Jump dialog not yet fully implemented |
| `Features/OrderLog/Views/OrderLogWidgetView.xaml.cs` | 2649 | Keyboard help not yet implemented |

**Recommendation:** Convert remaining TODOs to GitHub issues or implement them.

---

## 🔥 Complexity Hotspots

These files exceed 1000 lines and should be considered for refactoring:

| File | Lines | Concern |
|------|-------|---------|
| `ViewModels/ExpireWiseViewModel.cs` | 2,928 | Very large - consider splitting into partial classes or services |
| `Features/OrderLog/Views/OrderLogWidgetView.xaml.cs` | 2,729 | Large code-behind - extract more to ViewModel/behaviors |
| `ViewModels/AllocationBuddyRPGViewModel.cs` | 2,272 | Large ViewModel - consider service extraction |
| `Features/OrderLog/ViewModels/OrderLogViewModel.cs` | 2,219 | Large - already has services but could extract more |
| `Features/OrderLog/Behaviors/OrderLogFluidDragBehavior.cs` | 1,390 | Complex drag behavior - acceptable for specialized code |
| `ViewModels/EssentialsBuddyViewModel.cs` | 1,280 | Consider service extraction |
| `ViewModels/DictionaryManagementViewModel.cs` | 1,258 | Consider service extraction |
| `Behaviors/ListBoxDragDropBehavior.cs` | 1,016 | Complex drag/drop - acceptable |

**Recommendation:** Prioritize refactoring `ExpireWiseViewModel.cs` and `OrderLogWidgetView.xaml.cs`.

---

## 🧪 Test Coverage

| Project | Status |
|---------|--------|
| `ExpireWise.Tests` | Present (~4 test files) |
| `Infrastructure.Tests` | Present (~3 test files) |
| **Total Test Lines** | ~765 |
| **Coverage Estimate** | Low (~1.8% by line count) |

**Key untested areas:**
- ViewModels (ExpireWise, OrderLog, AllocationBuddy, EssentialsBuddy)
- Services (SpotifyService, ThemeService, DialogService)
- Behaviors (drag/drop, animations)
- Most OrderLog functionality

**Recommendation:** Add unit tests for critical ViewModels and Services. Focus on:
1. `OrderLogViewModel` business logic
2. `ExpireWiseViewModel` timeline/filtering logic
3. Repository operations
4. Import/export services

---

## 🔒 Security Assessment

### ✅ Good Practices
- **Credentials encrypted:** `ExternalConnectionConfig.cs` uses DPAPI encryption for MySQL passwords and BC client secrets
- **SecureString usage:** Passwords held in `SecureString` in memory
- **No hardcoded secrets:** Connection strings built from encrypted config

### ⚠️ Areas to Monitor
| Pattern | Files | Notes |
|---------|-------|-------|
| `Process.Start` | Multiple | Legitimate uses for opening folders/URLs |
| Reflection/Invoke | ~30 occurrences | Used for WPF binding, animations, keyboard hooks |
| ConnectionString building | 3 files | All use proper parameterization |

### Recommendations
1. Ensure external config file (`external_config.json`) has appropriate ACLs
2. Consider adding certificate pinning for Business Central API calls
3. Add audit logging for sensitive operations

---

## 📁 Architecture Overview

```
SOUP/src/
├── Core/              # Entities, interfaces, common types
├── Data/              # Database contexts (DictionaryDbContext)
├── Features/          # Feature modules
│   ├── ExpireWise/    # Expiration tracking module
│   └── OrderLog/      # Order tracking widget
├── Infrastructure/    # Repositories, services, parsers
├── Services/          # Cross-cutting services
├── ViewModels/        # MVVM ViewModels
├── Views/             # XAML views and code-behind
├── Windows/           # Application windows
├── Themes/            # Dark/Light theme resources
├── Behaviors/         # WPF attached behaviors
└── Converters/        # Value converters
```

### Module Breakdown
| Module | Purpose | Complexity |
|--------|---------|------------|
| **OrderLog** | Order tracking widget (AppBar docked) | High - complex drag/drop, animations |
| **ExpireWise** | Expiration date tracking | High - timeline view, analytics |
| **AllocationBuddy** | Inventory allocation (RPG themed) | Medium - parser heavy |
| **EssentialsBuddy** | Essential items management | Medium |
| **SwiftLabel** | Label printing | Low |

---

## 🎯 Prioritized Recommendations

### High Priority
1. **Fix nullability warnings** - 12 warnings in OrderLog services/models
2. **Add tests for OrderLog** - Critical business logic untested
3. **Refactor ExpireWiseViewModel** - 2,928 lines is too large

### Medium Priority
4. **Implement remaining TODOs** - Color picker dialog, keyboard help
5. **Add tests for Import/Export** - Data integrity is critical
6. **Extract services from large ViewModels** - Improve maintainability

### Low Priority
7. **Improve test coverage** - Target 30%+ coverage
8. **Add CI/CD integration** - Run `analyze.ps1` in pipeline
9. **Document architecture** - Update README with module overview

---

## 📈 Trends Since Last Audit (2026-01-20)

| Metric | Previous | Current | Change |
|--------|----------|---------|--------|
| Warnings | ~24 | 12 unique | 📉 Improved |
| TODOs | 26 | 4 actionable | 📉 Improved |
| Test Coverage | Minimal | Low | ↔️ Same |

---

## 🛠️ Useful Scripts

```powershell
# Run full analysis
.\scripts\analyze.ps1

# Build (portable SDK)
$env:PATH = "d:\CODE\important files\dotnet-sdk-10.0.101-win-x64;$env:PATH"
dotnet build src/SOUP.csproj

# Run specific module
.\scripts\run-orderlog.ps1
.\scripts\run-expirewise.ps1

# Clean build
.\scripts\clean.ps1
```

---

*Audit performed by automated assistant on 2026-02-05.*

---

<details>
<summary>Previous Audit (2026-01-20)</summary>

**Summary**
- **Repo:** d:/CODE/Cshp (SOUP)
- **Audit date:** 2026-01-20

**Environment used**
- Portable .NET SDK: d:/CODE/important files/dotnet-sdk-10.0.101-win-x64 (SDK 10.0.101)

**Actions performed**
- Built solution using the portable SDK. Initial build failed due to locked files in `src/obj` (file-in-use). After cleaning `bin`/`obj` folders under `SOUP/src` the build succeeded.
- Ran the top-level unused-fields script `scripts/evaluate_unused_fields.ps1`. It wrote a detailed report to `SOUP/src/unused_private_fields_report.txt`.
- Searched the repository for `TODO`, `FIXME`, and `HACK` markers (26 matches found). The script `SOUP/scripts/analyze.ps1` already contains a step to check for these.
- Ran `dotnet test` across the solution; no test failures reported (no test projects found or no tests executed).

**Key outputs / locations**
- Build: succeeded after cleaning; built binaries at `SOUP/src/bin/Debug/net10.0-windows10.0.19041.0/win-x64`
- Unused-fields report: SOUP/src/unused_private_fields_report.txt
- TODO/FIXME search: matches found; refer to `SOUP/scripts/analyze.ps1` for the script that aggregates them.

**Findings & recommendations**
- Locked file errors during build suggest an external process (IDE, file indexer, or antivirus) held files under `src/obj`. If CI/build agents see this, ensure clean build workspaces or run a pre-build clean step.
- The unused-fields report lists many private fields that appear unused; review the report and vet removals carefully (the repository contains dedicated vetting CSVs under `SOUP/src/vetted_unused_private_fields_*.csv`). Consider automating safe removals after review.
- The repository contains scripts (`SOUP/scripts/analyze.ps1`) for checks — consider adding them to CI to run on pull requests.
- TODO/FIXME occurrences: triage each marker and either resolve, convert to an issue, or add context. The `analyze.ps1` script can be extended to fail CI when a threshold is exceeded.

</details>
