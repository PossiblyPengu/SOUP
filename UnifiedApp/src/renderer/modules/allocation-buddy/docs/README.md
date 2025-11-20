# Store Allocation Viewer

An Electron desktop application for reading and organizing item allocations from Excel and CSV files, organized by store.

## Features

- **Multiple File Format Support**: Import data from Excel (.xlsx, .xls) and CSV files
- **Flexible Column Detection**: Automatically detects store, item, and quantity columns
- **Smart Dictionary Matching**: Intelligent fuzzy matching finds items and stores even with:
  - Store IDs (101, 107) or names (WATERLOO 1, Toronto)
  - Item codes (GLD-1), SKUs (410021982504), or descriptions (glide, watermelon)
  - Partial matches and keyword searches
- **Data Validation**: Real-time matching statistics with color-coded badges:
  - Shows matched vs unmatched items and stores
  - Clickable warnings dialog with detailed mismatch information
  - Console logging for debugging
- **Dictionary Integration**: Automatic enrichment of data with item descriptions and SKUs
- **Store Ranking System**: Visual rank badges (AA, A, B, C) for stores from the dictionary
- **Two View Modes**:
  - **View by Store**: See all items allocated to each store with rank badges
  - **View by Item**: See which stores receive each item with descriptions and SKUs
- **Advanced Filtering**:
  - Filter by specific store
  - Search items by code, description, or SKU
  - Clear all filters with one click
- **Real-time Statistics**: View total stores, items, and quantities
- **Beautiful UI**: Modern, responsive design with smooth animations and color-coded rank badges

## Installation

1. Install Node.js (if not already installed) from [nodejs.org](https://nodejs.org/)

2. Install dependencies:
```bash
npm install
```

## Running the App

Start the application:
```bash
npm start
```

For development mode with DevTools:
```bash
npm run dev
```

## File Format Requirements

Your Excel or CSV files should have columns for:
- **Store/Location**: Store name or code
- **Item/Product**: Item name, SKU, or description
- **Quantity/Allocation**: Number of units allocated

The app will automatically detect columns with names like:
- Store columns: "Store", "Location", "Shop"
- Item columns: "Item", "Product", "SKU", "Name", "Description"
- Quantity columns: "Quantity", "Qty", "Amount", "Allocation", "Units"

### Example CSV Format:
```csv
Store,Item,Quantity
Store A,Widget 100,50
Store A,Gadget 200,30
Store B,Widget 100,25
Store B,Gadget 200,40
```

### Example Excel Format:
| Store   | Item       | Quantity |
|---------|------------|----------|
| Store A | Widget 100 | 50       |
| Store A | Gadget 200 | 30       |
| Store B | Widget 100 | 25       |

## How to Use

1. **Select a File**: Click the "Select Excel/CSV File" button and choose your file
2. **View Data**: Data will automatically load and display with enriched information from the dictionary
3. **Switch Views**: Toggle between "View by Store" and "View by Item"
4. **Filter Data**:
   - Use the store filter dropdown to view specific stores
   - Search by item code, description, or SKU
   - Store ranks are color-coded: AA (pink), A (blue), B (green), C (orange)
5. **Review Stats**: Check the statistics cards for totals

## Dictionary File

The app includes a `dictionaries.js` file that contains:
- **Items**: Item numbers, descriptions, and SKU arrays for product lookup
- **Stores**: Store IDs, names, and rank classifications (AA, A, B, C)

When you load allocation data, the app automatically:
- Enriches item codes with full descriptions
- Displays SKU information for each item
- Shows store ranks with color-coded badges
- Enables searching by description or SKU, not just item codes

## Project Structure

```
store-allocation-viewer/
├── main.js           # Electron main process
├── renderer.js       # Application logic and data processing
├── index.html        # User interface
├── styles.css        # Styling
├── dictionaries.js   # Store and item dictionary data
├── package.json      # Project configuration
├── sample-data.csv   # Sample data for testing
└── README.md         # This file
```

## Technologies Used

- **Electron**: Desktop application framework
- **XLSX**: Excel file parsing
- **PapaParse**: CSV file parsing
- **Node.js**: Runtime environment

## Building for Distribution

To package the app for distribution, you can use electron-builder or electron-forge:

1. Install electron-builder:
```bash
npm install --save-dev electron-builder
```

2. Add to package.json:
```json
"scripts": {
  "build": "electron-builder"
}
```

3. Build:
```bash
npm run build
```

## License

MIT
