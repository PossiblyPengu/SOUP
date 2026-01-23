# ExpireWise Add Item Process - Comprehensive Refresh Plan

**Created**: 2026-01-23
**Status**: Plan Mode - Awaiting Approval
**Scope**: Complete UI/UX refresh with Quick Add panel + improved bulk dialog

---

## Executive Summary

Refreshing the ExpireWise add item process to provide:
1. **Quick Add Panel** - Inline panel for rapid single-item additions (currently partially implemented in VM, no UI)
2. **Improved Bulk Dialog** - Simplified multi-item entry with better flow and sticky settings
3. **Business Central Integration** - Full use of BC items/locations, remove custom dictionary management
4. **Sticky Settings** - Remember last used store and expiry date
5. **Better UX** - Smoother validation, clearer errors, reduced friction

---

## Key Requirements

✅ User wants **all pain points addressed**:
- Fast single-item additions (Quick Add panel)
- Sticky settings (store, date)
- Better bulk/Excel paste flow
- Smoother unknown SKU handling

✅ **Business Central Integration**:
- Already implemented (BusinessCentralService + DictionarySyncService)
- Items from BC → DictionaryDbContext (local cache) → ExpireWise lookups
- Locations from BC
- **Block/warn users** if item not found in BC (strict validation)
- **Remove** custom "Add to Dictionary" UI (no longer needed)

---

## Current State Analysis

### What Exists (ViewModel)
- `QuickAddSku`, `QuickAddMonth`, `QuickAddYear`, `QuickAddUnits`, `QuickAddExpanded` properties (lines 223-278)
- `ExpireWiseItemService.QuickAddAsync()` method
- Full bulk dialog with verification (ExpirationItemDialog)

### What's Missing
- ❌ No XAML UI for Quick Add panel
- ❌ No command bindings for Quick Add
- ❌ No sticky settings persistence
- ❌ Manual dictionary management still in bulk dialog (overlay panel)
- ❌ Store dropdown resets each time
- ❌ Expiry date resets each time

### What to Remove
- "Add to Dictionary" overlay panel in ExpirationItemDialog
- Manual SKU linking UI
- Custom dictionary entry fields

---

## Implementation Plan

### Phase 1: Review & Setup (30 min)

#### 1.1 Create ExpireWise Settings Model
**File**: `SOUP/src/Features/ExpireWise/Models/ExpireWiseSettings.cs` (NEW)

```csharp
public class ExpireWiseSettings
{
    // Sticky settings
    public string? LastSelectedStore { get; set; }
    public int? LastExpiryMonth { get; set; }
    public int? LastExpiryYear { get; set; }
    public int DefaultUnits { get; set; } = 1;

    // Quick Add preferences
    public bool QuickAddExpanded { get; set; } = false;
    public bool RememberLastLocation { get; set; } = true;
    public bool RememberLastExpiryDate { get; set; } = true;

    // Validation preferences
    public bool BlockUnknownItems { get; set; } = true; // BC integration
    public bool ShowWarningForUnknownItems { get; set; } = true;
}
```

**Integration**: Use existing `SettingsService` to persist as `"ExpireWise.settings.json"`

#### 1.2 Review Business Central Integration
**Files to Review**:
- `BusinessCentralService.cs` - Item/location fetching
- `DictionarySyncService.cs` - Sync logic
- `DictionaryDbContext` - Local cache queries

**Validation**:
- Confirm BC items are in local dictionary
- Test lookup performance
- Verify location sync working

---

### Phase 2: Quick Add Panel UI (1.5 hours)

#### 2.1 Create Quick Add Panel XAML
**File**: `SOUP/src/Features/ExpireWise/Views/QuickAddPanel.xaml` (NEW)

**Design**:
```
┌─ Quick Add ────────────────────────────────────────┐
│ [Collapse ▼]                                       │
│                                                    │
│ SKU/Item #: [__________] 📦                        │
│ Store:      [Dropdown ▼] 📍  Qty: [_3__] units    │
│ Expires:    [Jan ▼] [2027 ▼] 📅                   │
│                                                    │
│ ✓ Item: FLOUR ALL-PURPOSE (SKU-12345)             │
│                                                    │
│              [Add Item] [Add & Continue]           │
└────────────────────────────────────────────────────┘
```

**Features**:
- Collapsible expander (remembers state)
- Real-time BC lookup as user types SKU
- Live item preview with description
- Sticky dropdown values (from settings)
- Two submit modes: "Add Item" (close) or "Add & Continue" (clear and repeat)
- Amber/orange accent to match ExpireWise theme
- Keyboard shortcut: Ctrl+Shift+Q to toggle panel

#### 2.2 Create QuickAddPanel.xaml.cs Code-Behind
**Features**:
- SKU input with debounced lookup (300ms)
- Visual feedback: loading spinner, checkmark when found, warning when not found
- Auto-focus on SKU input when expanded
- Enter key submits (if validated)
- Escape key clears

#### 2.3 Integrate Panel into ExpireWiseView.xaml
**Location**: Insert as Row 1 (after header, before search bar)

```xaml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/> <!-- Header -->
    <RowDefinition Height="Auto"/> <!-- Quick Add Panel (NEW) -->
    <RowDefinition Height="Auto"/> <!-- Search Bar -->
    ...
</Grid.RowDefinitions>
```

---

### Phase 3: ViewModel Integration (1 hour)

#### 3.1 Update ExpireWiseViewModel.cs
**Changes**:
1. Load settings on init: `_settings = await _settingsService.LoadSettingsAsync<ExpireWiseSettings>("ExpireWise");`
2. Wire up existing Quick Add properties to settings
3. Create `QuickAddCommand` (currently missing):
   ```csharp
   [RelayCommand(CanExecute = nameof(CanQuickAdd))]
   private async Task QuickAdd()
   {
       // Validate SKU in BC dictionary
       // Create ExpirationItem
       // Call _itemService.QuickAddAsync()
       // Save settings with last used values
       // Show success toast
   }
   ```
4. Create `QuickAddAndContinueCommand` - same but clears form
5. Auto-populate dropdowns from settings on load
6. Save settings after each successful add

#### 3.2 Update ExpirationItemDialogViewModel.cs
**Simplifications**:
1. Remove `ShowAddToDictionaryPanel` property
2. Remove `ItemToAddToDict`, `NewDictItemNumber`, `NewDictDescription` properties
3. Remove `AddToDictionaryCommand`
4. Remove `DictSuggestions` and search logic
5. Update `ParseAndVerify()` to use BC dictionary ONLY
6. Enhanced error messages: "Item not found in Business Central. Please sync your item database or verify the SKU."

---

### Phase 4: Bulk Dialog Improvements (1.5 hours)

#### 4.1 Update ExpirationItemDialog.xaml
**Remove**:
- Overlay panel for "Add to Dictionary" (entire Border with `ShowAddToDictionaryPanel` binding)
- "+" button next to unverified items
- "🔗 Link SKU" button

**Add**:
1. **Sticky Settings Checkbox** (below expiry date):
   ```xaml
   <CheckBox IsChecked="{Binding RememberSettings}">
       Remember store and expiry date for next time
   </CheckBox>
   ```

2. **Enhanced Verification Results**:
   - ✅ Green checkmark + item details for found items
   - ❌ Red X + "Not found in Business Central" for unknown
   - ⏳ Loading spinner during verification
   - Clear error count: "3 items not found - cannot proceed"

3. **Helpful Instructions**:
   - Add tooltip to SKU input: "Enter one SKU per line, or paste tab-separated data from Excel (SKU[tab]Quantity)"
   - Show format example in placeholder: "SKU-001\nSKU-002[tab]5\n..."

4. **Validation Improvements**:
   - Disable "Add" button if ANY items not found (strict BC validation)
   - Show summary: "12 items verified ✓ | 3 items not found ❌"
   - Offer "Remove Unverified" button to strip invalid rows

#### 4.2 Update ExpirationItemDialog.xaml.cs
**Changes**:
1. Load settings in constructor
2. Pre-populate store/date from sticky settings
3. Save settings on successful submit (if checkbox enabled)
4. Enhance warning dialog for unverified items

---

### Phase 5: Business Central Integration Review (45 min)

#### 5.1 Verify Dictionary Lookup Performance
**Method**: `DictionaryDbContext.SearchItems(string sku)`

**Check**:
- Is lookup indexed?
- Does it search all SKU variants (UPC, vendor item #)?
- Response time < 100ms?

**Optimization**: Add indexes if needed

#### 5.2 Enhanced Error Messages
**When item not found**:
- "Item '{sku}' not found in Business Central"
- "Last sync: {lastSyncTime}"
- Button: "Sync Now" (triggers BC sync)
- Link: "Open Dictionary Management"

#### 5.3 Sync Status Indicator
**Add to ExpireWiseView sidebar**:
```xaml
<Border Background="Info" Padding="8" Margin="8,0">
    <StackPanel>
        <TextBlock>Dictionary Status</TextBlock>
        <TextBlock>Items: 1,234 | Last Sync: 2 hours ago</TextBlock>
        <Button Command="{Binding SyncBcCommand}">Sync Now</Button>
    </StackPanel>
</Border>
```

---

### Phase 6: Settings Persistence (30 min)

#### 6.1 Settings Service Integration
**Load on startup** (ExpireWiseViewModel constructor):
```csharp
_settings = await _settingsService.LoadSettingsAsync<ExpireWiseSettings>("ExpireWise");
ApplySettingsToUI();
```

**Save on add** (after successful item add):
```csharp
if (_settings.RememberLastLocation)
    _settings.LastSelectedStore = SelectedStore;
if (_settings.RememberLastExpiryDate)
{
    _settings.LastExpiryMonth = ExpiryMonth;
    _settings.LastExpiryYear = ExpiryYear;
}
await _settingsService.SaveSettingsAsync("ExpireWise", _settings);
```

#### 6.2 Settings UI (Optional)
**Add to ExpireWiseView settings dialog**:
- Toggle: "Remember last store location"
- Toggle: "Remember last expiry date"
- Toggle: "Block items not in Business Central"
- Button: "Reset to defaults"

---

### Phase 7: UX Polish & Testing (1 hour)

#### 7.1 Visual Enhancements
1. **Quick Add Panel**:
   - Smooth expand/collapse animation (0.3s ease)
   - Pulse animation on "Add & Continue" success
   - Auto-clear with fade-out effect

2. **Bulk Dialog**:
   - Progress indicator during verification
   - Success animation on verified items
   - Shake animation on validation error

3. **Toast Notifications**:
   - "Item added: {itemNumber}" (3s)
   - "3 items added successfully" (5s)
   - "Unable to add: Item not found in Business Central" (error, 7s)

#### 7.2 Keyboard Shortcuts
- **Ctrl+Shift+Q**: Toggle Quick Add panel
- **Ctrl+N**: Open bulk add dialog (existing)
- **Enter**: Submit Quick Add form (when validated)
- **Ctrl+Enter**: Add & Continue in Quick Add
- **Escape**: Clear Quick Add form / Close dialog

#### 7.3 Accessibility
- Tab order: SKU → Store → Qty → Month → Year → Buttons
- Screen reader labels for all inputs
- High contrast support
- Focus indicators

---

## Technical Architecture

### Data Flow

#### Quick Add Flow:
```
User enters SKU
    ↓
Debounced lookup (300ms)
    ↓
DictionaryDbContext.SearchItems(sku)
    ↓
BC Item found? → Show preview + enable submit
BC Item not found? → Show error + disable submit
    ↓
User clicks "Add Item"
    ↓
Create ExpirationItem with BC details
    ↓
ExpireWiseItemService.QuickAddAsync()
    ↓
Save to repository → Update UI → Save settings
```

#### Bulk Add Flow (Simplified):
```
User pastes multiple SKUs
    ↓
Parse lines (handle tab-separated quantities)
    ↓
Batch lookup all SKUs in DictionaryDbContext
    ↓
Verify results: Mark found ✅ / not found ❌
    ↓
Show verification summary
    ↓
User clicks "Add" (only enabled if all found)
    ↓
Batch create ExpirationItems
    ↓
ExpireWiseViewModel.AddItemsAsync()
    ↓
Save to repository → Update UI → Save settings
```

---

## Files to Create/Modify

### New Files (6)
1. `SOUP/src/Features/ExpireWise/Models/ExpireWiseSettings.cs` - Settings model
2. `SOUP/src/Features/ExpireWise/Views/QuickAddPanel.xaml` - Quick Add UI
3. `SOUP/src/Features/ExpireWise/Views/QuickAddPanel.xaml.cs` - Quick Add code-behind
4. `SOUP/src/Features/ExpireWise/Converters/BcItemValidationConverter.cs` - Validation visual converter
5. `SOUP/docs/ExpireWise-Refresh-Plan.md` - This plan (already created)
6. `SOUP/docs/ExpireWise-Progress.md` - Progress tracking (will create during implementation)

### Modified Files (4)
1. `SOUP/src/ViewModels/ExpireWiseViewModel.cs` - Settings integration, Quick Add command
2. `SOUP/src/ViewModels/ExpirationItemDialogViewModel.cs` - Remove dictionary management, simplify validation
3. `SOUP/src/Views/ExpireWise/ExpirationItemDialog.xaml` - Remove overlay, add sticky settings
4. `SOUP/src/Views/ExpireWise/ExpireWiseView.xaml` - Add Quick Add panel row

### Files to Review (3)
1. `SOUP/src/Services/External/BusinessCentralService.cs` - Verify methods working
2. `SOUP/src/Services/External/DictionarySyncService.cs` - Check sync logic
3. `SOUP/src/Data/DictionaryDbContext.cs` - Review query performance

---

## Success Criteria

### Functional Requirements
- ✅ Quick Add panel functional with BC lookup
- ✅ Bulk dialog validates against BC only (no custom dict)
- ✅ Sticky settings persist between sessions
- ✅ Unknown items blocked with clear error
- ✅ All items/locations from BC integration

### UX Requirements
- ✅ Single-item add takes < 5 seconds (previously 15-20s with modal)
- ✅ Store/date remembered across sessions
- ✅ Clear error messages for BC validation failures
- ✅ Smooth animations and transitions
- ✅ Keyboard accessible

### Performance Requirements
- ✅ BC lookup < 100ms (local dictionary cache)
- ✅ Batch verification of 50 SKUs < 1 second
- ✅ Settings load/save < 50ms
- ✅ Panel expand/collapse smooth (60fps)

---

## Testing Plan

### Unit Tests
- Settings serialization/deserialization
- SKU parsing logic (single, tab-separated, Excel paste)
- BC dictionary lookup edge cases
- Validation rules

### Integration Tests
- Quick Add full flow (SKU → lookup → add → save → settings persist)
- Bulk add with mixed valid/invalid SKUs
- Settings persistence across app restarts
- BC sync integration

### Manual Testing Checklist
- [ ] Quick Add panel expands/collapses smoothly
- [ ] SKU lookup shows real-time feedback
- [ ] Valid BC item enables submit
- [ ] Invalid item shows error and blocks submit
- [ ] Store dropdown remembers last selection
- [ ] Expiry date remembers last selection
- [ ] "Add & Continue" clears form and keeps dropdowns
- [ ] Bulk dialog validates all SKUs against BC
- [ ] Bulk dialog blocks submit if any invalid
- [ ] Settings persist after app restart
- [ ] Keyboard shortcuts work (Ctrl+Shift+Q, Ctrl+N, Enter, Escape)
- [ ] Tab order logical
- [ ] Toast notifications appear correctly
- [ ] Animations smooth on low-end hardware

---

## Estimated Effort

| Phase | Task | Time |
|-------|------|------|
| 1 | Review & Setup | 30 min |
| 2 | Quick Add Panel UI | 1.5 hrs |
| 3 | ViewModel Integration | 1 hr |
| 4 | Bulk Dialog Improvements | 1.5 hrs |
| 5 | BC Integration Review | 45 min |
| 6 | Settings Persistence | 30 min |
| 7 | UX Polish & Testing | 1 hr |
| **Total** | | **~6.5 hours** |

---

## Risks & Mitigation

### Risk 1: BC Dictionary Not Synced
**Impact**: Quick Add and bulk dialog will fail all lookups
**Mitigation**:
- Add sync status indicator to UI
- Provide "Sync Now" button prominently
- Show last sync time
- Graceful fallback: warn user instead of hard block

### Risk 2: Performance Issues with Large Dictionary
**Impact**: Slow lookups, UI lag
**Mitigation**:
- Profile DictionaryDbContext queries
- Add indexes if needed
- Implement debouncing on Quick Add input
- Show loading spinner during batch verification

### Risk 3: Settings Not Persisting
**Impact**: User frustration, repeated data entry
**Mitigation**:
- Test SettingsService thoroughly
- Add error handling for save failures
- Provide manual "Save Preferences" button
- Log errors with Serilog

---

## Future Enhancements (Out of Scope)

These are good ideas but not part of this refresh:
1. Barcode scanner integration (for warehouse use)
2. Bulk edit existing items from Quick Add panel
3. Recent SKUs history/autocomplete
4. Photo attachment for items
5. Low-stock alerts based on expiry + quantity
6. Integration with order system for auto-reorder
7. Multi-language support for item descriptions
8. Expiry date templates (e.g., "Dairy: +30 days", "Produce: +7 days")

---

## References

### Similar Implementations
- **OrderLog QOL Improvements** (docs/OrderLog-QOL-Progress.md):
  - Services pattern (OrderSearchService, OrderBulkOperationsService)
  - Keyboard shortcuts (KeyboardShortcutManager)
  - Settings persistence
  - Advanced filtering dialog
  - Visual polish patterns

### Key Files
- Settings: `SOUP/src/Infrastructure/Services/SettingsService.cs`
- BC Service: `SOUP/src/Services/External/BusinessCentralService.cs`
- BC Models: `BcItem`, `BcLocation` in BusinessCentralService.cs
- Current Dialog: `SOUP/src/Views/ExpireWise/ExpirationItemDialog.xaml`
- Current VM: `SOUP/src/ViewModels/ExpireWiseViewModel.cs`

---

## Next Steps

1. **Review this plan** with user for approval
2. **Create progress tracking doc** (ExpireWise-Progress.md)
3. **Begin Phase 1** (Review & Setup)
4. **Iterate through phases** with testing after each
5. **Final integration testing** and polish
6. **User acceptance testing**

---

**Status**: 📋 Plan Ready for Review
**Awaiting**: User approval to begin implementation
