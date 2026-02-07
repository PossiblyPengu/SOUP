# SOUP Repository Audit Report

**Audit Date:** 2026-02-05  
**SDK Version:** .NET 10.0.101 (Portable SDK)  
**Build Status:** ✅ Successful (0 errors, 12 unique warnings)

---

## 📊 Codebase Statistics

| Metric | Value |
| ------ | ----- |
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
| ---- | ---- | ----- |
| `Features/OrderLog/Models/OrderTemplate.cs` | 30-32 | Null ref |
| `Features/OrderLog/Services/UndoRedoService.cs` | 399, 416 | Null |
| `Features/OrderLog/Services/OrderLogExportService.cs` | 241-252 | Null |

### Analyzer Warnings

| File | Line | Code | Issue |
| ---- | ---- | ---- | ----- |
| `...OrderTemplateEditorDialog.xaml.cs` | 157 | MA0026 | TODO |
| `...OrderLogWidgetView.xaml.cs` | 2663 | MA0134 | Async |

**Recommendation:** Fix nullability warnings; address unobserved async.

---

## 📝 TODO/FIXME Markers

**Total actionable TODOs found:** 4 (excluding Log.Debug statements)

| File | Line | Content |
| ---- | ---- | ------- |
| `...KeyboardShortcutManager.cs` | 117 | Ctrl+T: Go to today |
| `...OrderTemplateEditorDialog.xaml.cs` | 157 | Color picker dialog |
| `...OrderLogWidgetView.xaml.cs` | 2640 | Jump dialog |
| `...OrderLogWidgetView.xaml.cs` | 2649 | Keyboard help |

**Recommendation:** Convert remaining TODOs to GitHub issues or implement them.

---

## 🔥 Complexity Hotspots

These files exceed 1000 lines and should be considered for refactoring:

| File | Lines | Concern |
| ---- | ----- | ------- |
| `ViewModels/ExpireWiseViewModel.cs` | 2,928 | Very large |
| `...OrderLogWidgetView.xaml.cs` | 2,729 | Large code-behind |
| `ViewModels/AllocationBuddyRPGViewModel.cs` | 2,272 | Large ViewModel |
| `...OrderLogViewModel.cs` | 2,219 | Large |
| `...OrderLogFluidDragBehavior.cs` | 1,390 | Complex (acceptable) |
| `ViewModels/EssentialsBuddyViewModel.cs` | 1,280 | Refactor |
| `ViewModels/DictionaryManagementViewModel.cs` | 1,258 | Refactor |
| `Behaviors/ListBoxDragDropBehavior.cs` | 1,016 | Complex (ok) |

**Recommendation:** Prioritize refactoring `ExpireWiseViewModel.cs` and
`OrderLogWidgetView.xaml.cs`.

---

## 🧪 Test Coverage

| Project | Status |
| ------- | ------ |
| `ExpireWise.Tests` | Present (~4 test files) |
| `Infrastructure.Tests` | Present (~3 test files) |
| **Total Test Lines** | ~765 |
| **Coverage Estimate** | Low (~1.8% by line count) |

**Key untested areas:**

- ViewModels (ExpireWise, OrderLog, AllocationBuddy, EssentialsBuddy)
- Services (SpotifyService, ThemeService, DialogService)
- Behaviors (drag/drop, animations)
- Most OrderLog functionality

**Recommendation:** Add unit tests for critical ViewModels/Services:

1. `OrderLogViewModel` business logic
2. `ExpireWiseViewModel` timeline/filtering logic
3. Repository operations
4. Import/export services

---

## 🔒 Security Assessment

### ✅ Good Practices

- **Credentials encrypted:** DPAPI encryption for passwords/secrets
- **SecureString usage:** Passwords held in `SecureString` in memory
- **No hardcoded secrets:** Connection strings built from config

### ⚠️ Areas to Monitor

| Pattern | Files | Notes |
| ------- | ----- | ----- |
| `Process.Start` | Multiple | Legitimate uses |
| Reflection/Invoke | ~30 | WPF binding/hooks |
| ConnectionString | 3 files | Parameterized |

### Recommendations

1. Ensure external config file has appropriate ACLs
2. Consider certificate pinning for Business Central API calls
3. Add audit logging for sensitive operations

---

## 📁 Architecture Overview

```text
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
| ------ | ------- | ---------- |
| **OrderLog** | Order tracking widget | High |
| **ExpireWise** | Expiration tracking | High |
| **AllocationBuddy** | Inventory allocation | Medium |
| **EssentialsBuddy** | Essential items | Medium |
| **SwiftLabel** | Label printing | Low |

---

## 🎯 Prioritized Recommendations

### High Priority

1. **Fix nullability warnings** - 12 warnings in OrderLog
2. **Add tests for OrderLog** - Critical business logic untested
3. **Refactor ExpireWiseViewModel** - 2,928 lines is too large

### Medium Priority

1. **Implement remaining TODOs** - Color picker, keyboard help
2. **Add tests for Import/Export** - Data integrity is critical
3. **Extract services from large ViewModels** - Improve maintainability

### Low Priority

1. **Improve test coverage** - Target 30%+ coverage
2. **Add CI/CD integration** - Run `analyze.ps1` in pipeline
3. **Document architecture** - Update README with module overview

---

## 📈 Trends Since Last Audit (2026-01-20)

| Metric | Previous | Current | Change |
| ------ | -------- | ------- | ------ |
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

### Summary

- **Repo:** d:/CODE/Cshp (SOUP)
- **Audit date:** 2026-01-20

### Environment

- Portable .NET SDK: d:/CODE/important files/dotnet-sdk-10.0.101

### Actions performed

- Built solution. Initial build failed due to locked files in
  `src/obj`. After cleaning `bin`/`obj` folders the build succeeded.
- Ran unused-fields script. Wrote report to
  `SOUP/src/unused_private_fields_report.txt`.
- Searched for `TODO`, `FIXME`, and `HACK` markers (26 matches).
  Script `SOUP/scripts/analyze.ps1` contains check step.
- Ran `dotnet test`; no failures (no test projects found).

### Key outputs

- Build: succeeded; binaries at `SOUP/src/bin/Debug/.../win-x64`
- Unused-fields report: `SOUP/src/unused_private_fields_report.txt`
- TODO/FIXME search: refer to `SOUP/scripts/analyze.ps1`

### Findings

- Locked file errors suggest external process held `src/obj` files.
  For CI, ensure clean workspaces or run pre-build clean step.
- Unused-fields report lists many unused fields; review carefully.
  Repository contains vetting CSVs. Consider automating removals.
- Repository has check scripts - add to CI for pull requests.
- TODO/FIXME occurrences: triage and resolve or convert to issues.
  Extend `analyze.ps1` to fail CI when threshold exceeded.

</details>
