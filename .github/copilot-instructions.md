# Copilot Instructions for Cshp Workspace

**SOUP** - WPF inventory suite for retail store operations.

## Prerequisites

- .NET 9 SDK required ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- Windows 10/11 (Build 19041+)

## SOUP Architecture

### Core Patterns

**ViewModel pattern** - Use CommunityToolkit.Mvvm source generators:
```csharp
public partial class MyViewModel : ObservableObject  // Or ViewModelBase for shell VMs
{
    [ObservableProperty]
    private string _myField;  // Generates MyField property + INotifyPropertyChanged

    [RelayCommand]
    private void DoAction() { }  // Generates DoActionCommand

    partial void OnMyFieldChanged(string value) { }  // Optional change callback
}
```

**Entity pattern** - All entities inherit `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted` for soft-delete)

**DI registration** - Singleton ViewModels preserve state across navigation; Transient for dialogs. See [App.xaml.cs](SOUP/src/App.xaml.cs) `ConfigureServices()`

### Adding New Modules

1. Create feature folder: `Features/{ModuleName}/` with `Models/`, `ViewModels/`, `Views/`, `Services/`
2. Register in DI: Add to `ConfigureServices()` in [App.xaml.cs](SOUP/src/App.xaml.cs)
3. Update view mapping: Add case to [ViewModelToViewConverter.cs](SOUP/src/Converters/ViewModelToViewConverter.cs)
4. Add launch command in [LauncherViewModel.cs](SOUP/src/ViewModels/LauncherViewModel.cs)
5. Update `ModuleConfiguration` if installer-controllable

### Theming

Use `{DynamicResource BrushName}` for theme-aware colors. Key brushes in [DarkTheme.xaml](SOUP/src/Themes/DarkTheme.xaml) / [LightTheme.xaml](SOUP/src/Themes/LightTheme.xaml):
- `BackgroundBrush`, `SurfaceBrush`, `SurfaceHoverBrush`
- `TextPrimaryBrush`, `TextSecondaryBrush`, `TextTertiaryBrush`
- `BorderBrush`, `AccentBrush` (in [ModernStyles.xaml](SOUP/src/Themes/ModernStyles.xaml))

### Data Storage

- **Main DB**: `%APPDATA%\SOUP\Data\SOUP.db` via `SqliteDbContext`
- **Shared dictionaries**: `%APPDATA%\SOUP\Shared\dictionaries.db` via `DictionaryDbContext.Instance`
- **OrderLog DB**: `%APPDATA%\SOUP\OrderLog\orders.db` via `OrderLogRepository`
- **Settings**: JSON files in `%APPDATA%\SOUP\`
- **Logs**: `%APPDATA%\SOUP\Logs\` (Serilog, 7-day retention)

All SQLite databases use WAL mode for multi-process concurrent access.

## Error Handling Standards

**Exception Handling (Critical Pattern)**

```csharp
// ❌ NEVER do this - silent failures hide bugs
try { DoSomething(); }
catch { }  // BANNED - removes all error context

// ✅ ALWAYS log exceptions
try 
{
    DoSomething();
}
catch (Exception ex)
{
    _logger?.LogWarning(ex, "Operation failed but can continue");
}

// ✅ Re-throw critical errors
try 
{
    CriticalOperation();
}
catch (Exception ex) when (ex is InvalidOperationException or OutOfMemoryException)
{
    _logger?.LogError(ex, "Critical failure - aborting");
    throw;
}
catch (Exception ex)
{
    _logger?.LogWarning(ex, "Non-critical exception");
}
```

**Why This Matters:**
- Empty `catch { }` blocks hide bugs and make debugging impossible
- Always log with context so issues can be diagnosed in production
- Re-throw critical errors so caller can handle appropriately

[See CODE-AUDIT-REPORT.md](../../SOUP/CODE-AUDIT-REPORT.md#11-empty-exception-handlers-security--debugging-risk) for full context

---

## Service Injection Standards

**Dependency Injection Pattern (Standardized)**

```csharp
// ✅ PREFERRED: Constructor injection in ViewModels/Services
public partial class MyViewModel : ViewModelBase
{
    private readonly IMyService _service;
    private readonly ILogger<MyViewModel> _logger;
    
    public MyViewModel(IMyService service, ILogger<MyViewModel> logger)
    {
        _service = service;
        _logger = logger;  // Always inject logger
    }
}

// ✅ ACCEPTABLE: Service locator in Windows/Pages (if constructor injection not feasible)
public partial class MyWindow : Window
{
    private readonly IMyService _service;
    
    public MyWindow()
    {
        InitializeComponent();
        _service = App.GetRequiredService<IMyService>();  // Use GetRequired for required services
    }
}

// ❌ WRONG: No null checks on GetService<T>()
var service = App.GetService<IMyService>();
service.DoSomething();  // CRASHES if service is null!

// ✅ CORRECT: Always check null for GetService<T>()
var optionalService = App.GetService<OptionalService>();
if (optionalService != null)
{
    optionalService.DoSomething();
}

// OR use null-conditional:
app.GetService<OptionalService>()?.DoSomething();
```

**Guidelines:**
- Always use `GetRequiredService<T>()` for services that app cannot function without
- Only use `GetService<T>()` for truly optional services, and check for null
- Prefer constructor injection in ViewModels and Services
- Code-behind should use service locator pattern only when necessary
- Logger is injected in all classes that log

[See CODE-AUDIT-REPORT.md](../../SOUP/CODE-AUDIT-REPORT.md#12-inconsistent-service-injection-pattern) for full context

---

## Development Workflow

```powershell
.\scripts\dev.ps1 build    # Quick debug build
.\scripts\dev.ps1 run      # Build and run
.\scripts\dev.ps1 watch    # Hot reload mode
.\scripts\dev.ps1 widget   # OrderLog widget only (--widget flag)
.\scripts\dev.ps1 clean    # Clean bin/obj
```

## Code Conventions

- **Nullable enabled** globally - handle nulls explicitly
- **Global usings** in [GlobalUsings.cs](SOUP/src/GlobalUsings.cs) - no need to import `System.Collections.Generic`, `CommunityToolkit.Mvvm.*`, `Microsoft.Extensions.Logging`
- **Central package versions** in [Directory.Packages.props](SOUP/Directory.Packages.props)
- **Suppressed warnings** documented in [Directory.Build.props](SOUP/Directory.Build.props) - don't re-enable CA1848, MA0004, etc.
