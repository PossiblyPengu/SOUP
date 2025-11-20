# Changelog

## Version 2.0 - Smart Dictionary Matching (Latest)

### New Features

#### Intelligent Fuzzy Matching
- **Smart Item Matching** with 4-tier strategy:
  1. Exact match by item number (GLD-1)
  2. Exact match by SKU (410021982504)
  3. Partial match by item number prefix (GLD → GLD-1)
  4. Fuzzy match by description keywords (glide, chocolate, watermelon)

- **Smart Store Matching** with 4-tier strategy:
  1. Exact match by store ID (101, 107, 199)
  2. Exact match by store name (WATERLOO 1, case-insensitive)
  3. Partial match by name contains (Toronto → TORONTO 1)
  4. Fuzzy match by name keywords (Barrie → BARRIE 1)

#### Data Validation & Feedback
- **Real-time matching statistics** displayed as color-coded badges:
  - Green: 100% match rate (perfect!)
  - Yellow: 80-99% match rate (check warnings)
  - Red: <80% match rate (needs attention)

- **Interactive Warnings Dialog**:
  - Click warnings badge to see detailed report
  - Lists all unmatched items and stores
  - Shows fuzzy matches with confidence levels
  - Groups by warning type for easy review
  - Includes row numbers for quick fixes

- **Console Logging**:
  - Detailed matching statistics
  - Column detection information
  - Unmatched items/stores lists
  - Performance metrics

#### Enhanced Column Detection
- Now recognizes more column name variations:
  - Store: `Store`, `Location`, `Shop`, `ID`, `StoreID`
  - Item: `Item`, `Product`, `SKU`, `Name`, `Description`, `Number`
  - Quantity: `Quantity`, `Qty`, `Amount`, `Allocation`, `Units`

### Improvements
- Case-insensitive matching for all inputs
- Keyword-based description search
- Better handling of partial names
- Automatic normalization of store/item codes
- Performance optimized with Map-based lookups

### Files Added
- [test-matching.csv](test-matching.csv) - Test file with various input formats
- [SMART-MATCHING.md](SMART-MATCHING.md) - Detailed matching documentation
- [CHANGELOG.md](CHANGELOG.md) - This file

### Technical Changes
- Added `findItemInDictionary()` with multi-strategy matching
- Added `findStoreInDictionary()` with multi-strategy matching
- Added `matchingStats` object for tracking
- Added `showWarnings()` modal dialog
- Enhanced `processData()` with validation
- Enhanced `showFileInfo()` with statistics
- Added warning dialog CSS styles
- Added badge color variants (success, info, warning, error)

---

## Version 1.0 - Initial Release

### Features
- Excel (.xlsx, .xls) and CSV file support
- Dictionary integration with item descriptions and SKUs
- Store ranking system (AA, A, B, C) with color-coded badges
- Two view modes: by Store and by Item
- Real-time filtering and search
- Statistics dashboard
- Beautiful gradient UI with smooth animations

### Dictionary Support
- 6,958+ items with descriptions and SKU arrays
- 34 stores with IDs, names, and rank classifications
- Automatic data enrichment

### Core Files
- [main.js](main.js) - Electron main process
- [renderer.js](renderer.js) - Application logic
- [index.html](index.html) - User interface
- [styles.css](styles.css) - Styling
- [dictionaries.js](dictionaries.js) - Dictionary data
- [sample-data.csv](sample-data.csv) - Sample allocation data

---

## Future Enhancements (Potential)

- Export functionality (CSV, Excel, PDF)
- Custom dictionary editor
- Bulk import/batch processing
- Historical tracking and comparisons
- Advanced analytics and charts
- Multiple dictionary support
- Custom matching rules configuration
- API integration for live data
- Mobile/tablet responsive optimization
- Dark mode theme
