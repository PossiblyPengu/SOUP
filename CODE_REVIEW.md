# SOUP Project - Code Review Report

**Date:** January 5, 2026  
**Scope:** Full SOUP WPF Application (.NET 8)

---

## Executive Summary

The SOUP project is a well-structured, production-ready WPF application with comprehensive error handling and resource management. The code demonstrates good architectural practices including MVVM patterns, dependency injection, and proper disposal patterns. However, there are several logic errors and potential issues that should be addressed.

---

## Critical Issues

### 1. **OrderLogRepository - Delete Operation Uses Wrong ID Type** 🔴
**Location:** [src/Features/OrderLog/Services/OrderLogRepository.cs](src/Features/OrderLog/Services/OrderLogRepository.cs#L125)  
**Severity:** High

**Problem:**
```csharp
foreach (var id in idsToDelete)
{
    _collection.Delete(id);  // ❌ Passing Guid directly
}
```

The LiteDB `Delete()` method expects a `BsonValue`, but the code passes a `Guid` directly. This will cause a runtime error.

**Fix:**
```csharp
foreach (var id in idsToDelete)
{
    _collection.Delete(new BsonValue(id));  // ✓ Convert to BsonValue
}
```

**Related Code:**
- [LiteDbRepository.cs](src/Infrastructure/Repositories/LiteDbRepository.cs#L34) shows correct usage with `FindById(new BsonValue(id))`

---

### 2. **LiteDbRepository - Unnecessary BsonValue Wrapping** 🟡
**Location:** [src/Infrastructure/Repositories/LiteDbRepository.cs](src/Infrastructure/Repositories/LiteDbRepository.cs#L34)  
**Severity:** Medium

**Problem:**
```csharp
var result = Collection.FindById(new BsonValue(id));
```

The `FindById()` method can accept a `Guid` directly and will handle the conversion internally. Creating a `BsonValue` is unnecessary overhead.

**Fix:**
```csharp
var result = Collection.FindById(id);
```

---

## Logic Errors & Design Issues

### 3. **OrderLogViewModel - Thread Safety of HashSets** 🟡
**Location:** [src/Features/OrderLog/ViewModels/OrderLogViewModel.cs](src/Features/OrderLog/ViewModels/OrderLogViewModel.cs#L36-L38)  
**Severity:** Medium

**Problem:**
```csharp
private readonly HashSet<Guid> _itemIds = new();
private readonly HashSet<Guid> _archivedItemIds = new();
```

These HashSets are used for O(1) membership checks, which is good. However, they're modified across multiple operations without synchronization, while the underlying collections may be accessed from different threads.

**Concern:**
- LoadAsync clears both collections and repopulates them
- SaveAsync may be called concurrently
- ObservableCollection itself is not thread-safe

**Recommendation:**
```csharp
// Option 1: Use lock for synchronization
private readonly object _lockObj = new();

private void AddToArchived(OrderItem item)
{
    lock (_lockObj)
    {
        if (_archivedItemIds.Contains(item.Id)) return;
        ArchivedItems.Add(item);
        _archivedItemIds.Add(item.Id);
    }
}

// Option 2: Use ConcurrentBag or ConcurrentDictionary if full thread-safety is needed
```

---

### 4. **App.xaml.cs - Title Mismatch in Logging** 🟡
**Location:** [src/App.xaml.cs](src/App.xaml.cs#L45)  
**Severity:** Low (Bug in docs/naming)

**Problem:**
```csharp
Log.Information("Starting S.A.P (S.A.M. Add-on Pack)");
```

Should be "S.O.U.P" not "S.A.P". This is a documentation/naming bug.

**Fix:**
```csharp
Log.Information("Starting S.O.U.P (S.A.M. Operations Utilities Pack)");
```

---

### 5. **MainWindow.xaml.cs - Incorrect Window Closing Logic** 🟡
**Location:** [src/MainWindow.xaml.cs](src/MainWindow.xaml.cs#L27-L39)  
**Severity:** Medium

**Problem:**
```csharp
private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
{
    // Check if the Order Log widget is still open
    var widgetOpen = Application.Current.Windows
        .OfType<Windows.OrderLogWidgetWindow>()
        .Any(w => w.IsVisible);
    
    // If widget is not open, shutdown the app
    if (!widgetOpen)
    {
        Application.Current.Shutdown();
    }
}
```

**Issues:**
1. This closes the main window but does NOT cancel the event (`e.Cancel` is never set)
2. The widget window might be hidden but still alive, so `IsVisible` check may not be reliable
3. Calling `Shutdown()` when the window is already closing is redundant and could cause conflicts

**Better Approach:**
```csharp
private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
{
    var widgetOpen = Application.Current.Windows
        .OfType<Windows.OrderLogWidgetWindow>()
        .Any(w => w != null && w.IsVisible);
    
    // Don't explicitly shutdown - let the app lifecycle handle it
    // Only prevent closing if widget is open
    if (widgetOpen)
    {
        e.Cancel = true;  // Keep window open while widget is visible
        this.Hide();      // Hide the window instead
    }
}
```

---

### 6. **WindowSettingsService - Multi-Monitor Window Restoration** 🟡
**Location:** [src/Services/WindowSettingsService.cs](src/Services/WindowSettingsService.cs#L143-L164)  
**Severity:** Low

**Problem:**
```csharp
private static bool IsPositionVisible(double left, double top, double width, double height)
{
    var windowRect = new System.Drawing.Rectangle(
        (int)left, (int)top, (int)width, (int)height);

    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
    {
        if (screen.WorkingArea.IntersectsWith(windowRect))
        {
            return true;
        }
    }
    return false;
}
```

**Issue:** If a window was maximized on a monitor that's no longer connected, the restored size might be too large for the current monitor setup. The code returns true if ANY part intersects, but the window might be partially off-screen.

**Improvement:**
```csharp
private static bool IsPositionVisible(double left, double top, double width, double height)
{
    var windowRect = new System.Drawing.Rectangle(
        (int)left, (int)top, (int)width, (int)height);

    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
    {
        var intersection = System.Drawing.Rectangle.Intersect(
            windowRect, screen.WorkingArea);
        
        // Ensure significant portion (>50%) of window is visible
        if (intersection.Width * intersection.Height > 
            (windowRect.Width * windowRect.Height) * 0.5)
        {
            return true;
        }
    }
    return false;
}
```

---

## Code Quality & Best Practices

### ✅ Excellent Practices

1. **Proper Disposal Pattern**
   - Multiple ViewModels implement IDisposable correctly
   - Event unsubscription in Dispose methods prevents memory leaks
   - LiteDbContext properly disposed

2. **Dependency Injection**
   - Clean service registration in App.xaml.cs
   - Proper use of Singleton, Transient patterns
   - Logger injection throughout

3. **Resource Management**
   - Exception handlers at multiple levels (AppDomain, TaskScheduler, Dispatcher)
   - Proper shutdown sequence in OnExit
   - File locking handled with semaphores (OrderLogRepository)

4. **MVVM Architecture**
   - Community Toolkit MVVM used properly
   - Clear separation of concerns
   - Observable patterns implemented correctly

5. **Performance Optimizations**
   - HashSets for O(1) lookups instead of Contains()
   - ConcurrentDictionaries for thread-safe lookups
   - Debouncing for file operations (GroupStateStore)

### ⚠️ Improvements Needed

1. **Error Logging with Empty Catch Blocks**
   ```csharp
   catch { }  // ❌ Silent failures in several places
   ```
   This happens in dispatcher exception handlers. Consider at least Debug logging.

2. **Null Coalescing Could Be Cleaner**
   ```csharp
   var items?.Where(i => !i.IsPracticallyEmpty).ToList() ?? new List<OrderItem>()
   ```
   The null-conditional with null-coalescing is safe but verbose. Consider null-checking earlier.

3. **Magic Numbers**
   - Timeout values hardcoded in multiple ViewModels
   - Consider extracting to configuration

---

## Potential Runtime Issues

### 7. **OrderLogViewModel SaveAsync - Race Condition Risk** 🟡
**Location:** [src/Features/OrderLog/ViewModels/OrderLogViewModel.cs](src/Features/OrderLog/ViewModels/OrderLogViewModel.cs#L190-210)  
**Severity:** Low

```csharp
[RelayCommand]
public async Task SaveAsync()
{
    try
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].Order = i;  // ⚠️ Modifying collection while iterating
        }
```

If Items collection is modified during iteration (from UI or background), this could throw an exception.

**Fix:**
```csharp
var itemsToSave = Items.ToList();  // Snapshot
for (int i = 0; i < itemsToSave.Count; i++)
{
    itemsToSave[i].Order = i;
}
await _orderLogService.SaveAsync(itemsToSave);
```

---

## Summary of Fixes Required

| Priority | Issue | Location | Fix Complexity |
|----------|-------|----------|-----------------|
| 🔴 High | Delete with Guid instead of BsonValue | OrderLogRepository.cs | 1 line |
| 🟡 Medium | BsonValue wrapping unnecessary | LiteDbRepository.cs | 1 line |
| 🟡 Medium | HashSet thread safety | OrderLogViewModel.cs | 5-10 lines |
| 🟡 Medium | Window closing logic | MainWindow.xaml.cs | 3-5 lines |
| 🟡 Low | Incorrect app name in log | App.xaml.cs | 1 line |
| 🟡 Low | Window restoration for multi-monitor | WindowSettingsService.cs | 5-8 lines |
| 🟡 Low | SaveAsync race condition | OrderLogViewModel.cs | 2 lines |

---

## Recommendations

1. **Immediate:** Fix the BsonValue bug in OrderLogRepository (Critical crash risk)
2. **Short-term:** Add thread-safety to OrderLogViewModel collections
3. **Short-term:** Review and fix MainWindow closing logic
4. **Medium-term:** Extract magic numbers to configuration
5. **Ongoing:** Consider adding unit tests for critical data persistence paths

---

## Architecture Assessment

**Overall Grade: A-**

The project demonstrates solid architectural practices with proper separation of concerns, good error handling, and appropriate use of design patterns. The identified issues are relatively minor and straightforward to fix. The codebase is maintainable and follows .NET best practices.
