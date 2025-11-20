# Shared Utilities and Resources

This directory contains reusable code, styles, and resources for the Store Allocation Viewer application.

## Directory Structure

```
shared/
├── utils/              # JavaScript utilities
│   ├── dom.js         # DOM manipulation helpers
│   ├── debounce.js    # Performance utilities
│   ├── sanitize.js    # Security and validation
│   ├── date.js        # Date utilities
│   ├── storage.js     # localStorage wrapper with backup
│   ├── error-boundary.js  # Global error handling
│   ├── toast.js       # Toast notifications
│   ├── accessibility.js   # Accessibility helpers
│   └── index.js       # Main export file
└── styles/            # Shared CSS
    └── design-tokens.css  # Design system variables
```

## Usage

### Importing Utilities

```javascript
// Import all utilities
const utils = require('../../shared/utils');

// Or import specific utilities
const { debounce, Validator, getToast } = require('../../shared/utils');

// Or import from specific modules
const { qs, qsa } = require('../../shared/utils/dom');
```

### Using Design Tokens

```html
<!-- In your HTML -->
<link rel="stylesheet" href="../../shared/styles/design-tokens.css">
```

```css
/* In your CSS */
.my-component {
  background: var(--bg-card);
  color: var(--text-primary);
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-md);
}
```

## Utility Examples

### DOM Utilities

```javascript
const { qs, qsa, createElement } = require('../../shared/utils');

// Query selectors
const button = qs('#myButton');
const cards = qsa('.card');

// Create elements
const card = createElement('div', { class: 'card', id: 'card1' }, [
  createElement('h2', {}, 'Title'),
  createElement('p', {}, 'Content')
]);
```

### Debounce/Throttle

```javascript
const { debounce, throttle } = require('../../shared/utils');

// Debounce search input
const handleSearch = debounce((term) => {
  console.log('Searching for:', term);
}, 300);

searchInput.addEventListener('input', (e) => handleSearch(e.target.value));

// Throttle scroll handler
const handleScroll = throttle(() => {
  console.log('Scrolling...');
}, 100);

window.addEventListener('scroll', handleScroll);
```

### Validation and Sanitization

```javascript
const { Validator, escapeHtml } = require('../../shared/utils');

// Sanitize user input
const cleanName = Validator.sanitizeString(userInput, 200);
const safeQuantity = Validator.sanitizeNumber(quantityInput, 0, 9999);

// Validate dates
if (!Validator.isValidDate(year, month)) {
  console.error('Invalid date');
}

// Escape HTML for safe display
const safeHTML = escapeHtml(userContent);
element.innerHTML = safeHTML;
```

### Storage Manager

```javascript
const { StorageManager } = require('../../shared/utils');

// Create storage instance
const storage = new StorageManager('myapp-data', 1);

// Save data (automatically creates backup)
storage.save({ items: [...], settings: {...} });

// Load data (automatically restores from backup if corrupted)
const data = storage.load();

// Export data
storage.exportToFile('backup.json');

// Import data
await storage.importFromFile(file);
```

### Toast Notifications

```javascript
const { getToast } = require('../../shared/utils');

const toast = getToast();

// Show notifications
toast.success('Item saved successfully!');
toast.error('Failed to load data');
toast.warning('Storage is almost full');
toast.info('5 items expiring soon');

// Custom duration
toast.show('Custom message', 'info', 5000);
```

### Error Boundary

```javascript
const { initErrorBoundary } = require('../../shared/utils');

// Initialize error boundary
initErrorBoundary({
  showDetails: true,
  onError: (error, type) => {
    // Custom error handling
    console.error('Caught error:', error);
  }
});

// Errors are now automatically caught and displayed
```

### Keyboard Shortcuts

```javascript
const { getKeyboardShortcuts } = require('../../shared/utils');

const keyboard = getKeyboardShortcuts();

// Register shortcuts
keyboard.register('Ctrl+K', () => {
  document.getElementById('search').focus();
}, 'Focus search');

keyboard.register('Escape', () => {
  closeModal();
}, 'Close modal');

keyboard.register('Ctrl+S', (e) => {
  e.preventDefault();
  saveData();
}, 'Save data');
```

### Accessibility

```javascript
const { SkipLinks, FocusManager, getLiveRegion } = require('../../shared/utils');

// Add skip link
SkipLinks.addSkipLink('main-content', 'Skip to main content');

// Trap focus in modal
const cleanup = FocusManager.trapFocus(modalElement);
// Later: cleanup();

// Announce to screen readers
const liveRegion = getLiveRegion();
liveRegion.announce('5 items added to cart');
liveRegion.announce('Error loading data', 'assertive');
```

### Date Utilities

```javascript
const {
  formatMonthYear,
  daysUntilExpiry,
  isExpiringSoon
} = require('../../shared/utils');

// Format dates
const displayDate = formatMonthYear(2024, 5); // "June 2024"

// Calculate days
const expiryDate = new Date(2024, 11, 31);
const daysLeft = daysUntilExpiry(expiryDate);

// Check status
if (isExpiringSoon(expiryDate, 30)) {
  console.log('Item expiring soon!');
}
```

## Design System

### Color Palette

```css
/* Primary colors */
var(--purple-500)  /* #667eea */
var(--purple-600)  /* #764ba2 */

/* Backgrounds */
var(--bg-body)     /* Body background gradient */
var(--bg-container) /* Container background */
var(--bg-card)     /* Card background */
var(--bg-secondary) /* Secondary background */

/* Text colors */
var(--text-primary)   /* Primary text */
var(--text-secondary) /* Secondary text */
var(--text-muted)     /* Muted text */

/* Semantic colors */
var(--color-success)  /* #28a745 */
var(--color-warning)  /* #ffc107 */
var(--color-error)    /* #dc3545 */
var(--color-info)     /* #17a2b8 */
```

### Spacing Scale

```css
var(--space-1)  /* 4px */
var(--space-2)  /* 8px */
var(--space-3)  /* 12px */
var(--space-4)  /* 16px */
var(--space-6)  /* 24px */
var(--space-8)  /* 32px */
```

### Typography Scale

```css
var(--text-xs)    /* 12px */
var(--text-sm)    /* 14px */
var(--text-base)  /* 16px */
var(--text-lg)    /* 18px */
var(--text-xl)    /* 20px */
var(--text-2xl)   /* 24px */
var(--text-3xl)   /* 30px */
```

### Shadows

```css
var(--shadow-sm)  /* Small shadow */
var(--shadow-md)  /* Medium shadow */
var(--shadow-lg)  /* Large shadow */
var(--shadow-xl)  /* Extra large shadow */
```

### Border Radius

```css
var(--radius-sm)   /* 4px */
var(--radius-base) /* 8px */
var(--radius-md)   /* 12px */
var(--radius-lg)   /* 16px */
var(--radius-full) /* 9999px - fully rounded */
```

### Utility Classes

```html
<!-- Spacing -->
<div class="p-4 m-2">Padded and margined</div>

<!-- Typography -->
<h1 class="text-2xl font-bold">Large bold heading</h1>
<p class="text-sm text-secondary">Small secondary text</p>

<!-- Layout -->
<div class="flex items-center gap-4">
  <span>Item 1</span>
  <span>Item 2</span>
</div>

<!-- Borders and shadows -->
<div class="rounded-lg shadow-md">Card with shadow</div>
```

## Best Practices

### 1. Always Sanitize User Input

```javascript
// Bad
element.innerHTML = userInput;

// Good
element.innerHTML = escapeHtml(userInput);
```

### 2. Use Debounce for Input Handlers

```javascript
// Bad
input.addEventListener('input', searchFunction);

// Good
input.addEventListener('input', debounce(searchFunction, 300));
```

### 3. Use Storage Manager for Data Persistence

```javascript
// Bad
localStorage.setItem('data', JSON.stringify(data));

// Good
const storage = new StorageManager('myapp-data', 1);
storage.save(data);
```

### 4. Provide User Feedback

```javascript
// Bad
saveData();

// Good
saveData();
toast.success('Data saved successfully!');
```

### 5. Handle Errors Gracefully

```javascript
// Bad
const data = JSON.parse(response);

// Good
const data = safeJsonParse(response, []);
```

## Testing

All utilities have comprehensive unit tests. Run tests with:

```bash
npm test
```

Run tests in watch mode:

```bash
npm test:watch
```

Generate coverage report:

```bash
npm test:coverage
```

## Contributing

When adding new utilities:

1. Create the utility file in the appropriate directory
2. Add comprehensive JSDoc comments
3. Export from `utils/index.js`
4. Create unit tests
5. Update this README with usage examples

## License

MIT
