# Unified Settings System

## Overview
Implemented a centralized settings management system that allows all modules (loaded in iframes) to access and modify settings through a unified modal dialog in the parent window.

## Architecture

### Components

1. **Settings Modal** (`src/renderer/index.html`)
   - Located in the main window (not in iframes)
   - Tab-based interface with 4 sections:
     - General (theme, auto-save)
     - ExpireWise (expiry warning days, critical warning days)
     - Allocation Buddy (default sort by, default sort direction)
     - Essentials Buddy (low stock threshold, critical stock threshold)

2. **Settings Manager** (`src/renderer/js/settings.js`)
   - Centralized settings state management
   - LocalStorage persistence
   - Default values for all apps
   - Settings change notifications to iframes
   - Tab switching logic
   - Save/Cancel/Reset functionality

3. **CSS Styling** (`src/renderer/css/unified-theme.css`)
   - Modal overlay with backdrop blur
   - Tab navigation styling
   - Form controls styling
   - Responsive layout
   - Smooth animations

### Communication Flow

```
┌─────────────────────────────────────────┐
│         Parent Window (index.html)      │
│  ┌─────────────────────────────────┐   │
│  │   Settings Modal (Overlay)      │   │
│  │   - General                      │   │
│  │   - ExpireWise                   │   │
│  │   - Allocation Buddy             │   │
│  │   - Essentials Buddy             │   │
│  └─────────────────────────────────┘   │
│                   ↕                      │
│            postMessage API               │
│                   ↕                      │
│  ┌─────────────────────────────────┐   │
│  │   Module (iframe)               │   │
│  │   - Settings Button             │   │
│  │   - Receives settings updates   │   │
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

### Message Protocol

#### Module → Parent (Open Settings)
```javascript
window.parent.postMessage({
  type: 'openSettings',
  tab: 'expirewise' // or 'general', 'allocation-buddy', 'essentials-buddy'
}, '*');
```

#### Parent → Module (Settings Changed)
```javascript
iframe.contentWindow.postMessage({
  type: 'settingsChanged',
  settings: { /* all settings */ }
}, '*');
```

#### Parent → Module (Theme Changed)
```javascript
iframe.contentWindow.postMessage({
  type: 'themeChanged',
  theme: 'dark' // or 'light'
}, '*');
```

## Implementation Details

### Settings Storage

All settings are stored in localStorage with prefixed keys:
- `appTheme`: 'dark' | 'light'
- `autoSave`: boolean
- `expireWise-expiryWarningDays`: number (default: 30)
- `expireWise-criticalWarningDays`: number (default: 7)
- `allocationBuddy-defaultSortBy`: string (default: 'name')
- `allocationBuddy-defaultSortDirection`: 'asc' | 'desc' (default: 'asc')
- `essentialsBuddy-lowStockThreshold`: number (default: 10)
- `essentialsBuddy-criticalStockThreshold`: number (default: 3)

### Module Integration

Each module needs to:

1. **Send open request** when settings button is clicked:
   ```javascript
   settingsBtn.addEventListener('click', () => {
     window.parent.postMessage({ type: 'openSettings', tab: 'expirewise' }, '*');
   });
   ```

2. **Listen for settings updates**:
   ```javascript
   window.addEventListener('message', (event) => {
     if (event.data.type === 'settingsChanged') {
       // Reload settings and update UI
     }
   });
   ```

3. **Listen for theme changes**:
   ```javascript
   window.addEventListener('message', (event) => {
     if (event.data.type === 'themeChanged') {
       document.documentElement.setAttribute('data-theme', event.data.theme);
     }
   });
   ```

## Benefits

1. **Single Source of Truth**: All settings in one place
2. **Consistent UX**: Same settings UI across all modules
3. **Reduced Code Duplication**: No need for settings UI in each module
4. **Isolated Modules**: Modules remain independent but can access shared settings
5. **Real-time Updates**: Settings changes are immediately propagated to all modules
6. **Persistent Storage**: Settings survive app restarts

## Theme Integration

The theme toggle in the title bar:
- Updates the parent window theme
- Notifies all loaded modules via postMessage
- Saves preference to localStorage
- Provides visual feedback (☀️ for dark mode, 🌙 for light mode)

## Future Enhancements

- [ ] Settings validation (min/max values)
- [ ] Settings import/export
- [ ] Reset to factory defaults per section
- [ ] Settings search/filter
- [ ] Keyboard shortcuts for settings modal
- [ ] Settings change history/undo

## Files Modified

### Created
- `src/renderer/js/settings.js` - Settings manager implementation

### Modified
- `src/renderer/index.html` - Added settings modal HTML
- `src/renderer/css/unified-theme.css` - Added modal styles
- `src/renderer/js/launcher.js` - Added theme toggle functionality
- `src/renderer/modules/expirewise/js/app.js` - Integrated with unified settings

## Testing Checklist

- [x] Settings modal opens when button clicked in module
- [x] Settings modal opens on correct tab based on source module
- [x] Settings save correctly to localStorage
- [x] Settings changes propagate to all open modules
- [x] Theme toggle works and updates all modules
- [x] Cancel button discards changes
- [x] Reset button restores defaults
- [x] Close button (X) works
- [ ] Settings persist across app restarts (needs testing)
- [ ] Multiple modules can access settings simultaneously (needs testing)

## Notes

- Settings modal uses z-index: 100000 to ensure it appears above all content
- Modal has backdrop blur effect for better visual separation
- Tab switching preserves unsaved changes until Save is clicked
- Settings are validated before saving (e.g., numbers must be positive)
- Notification appears after successful save
