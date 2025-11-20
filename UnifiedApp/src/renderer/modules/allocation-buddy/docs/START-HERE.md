# Store Allocation Viewer - Quick Start Guide

## What You Have
A fully functional Electron app that:
- Reads Excel (.xlsx, .xls) and CSV files
- Shows item allocations organized by store
- **Smart Dictionary Matching** - Automatically finds items and stores using:
  - Store IDs (101, 107), names (WATERLOO 1), or partial names (Toronto)
  - Item codes (GLD-1), SKUs (410021982504), or descriptions (glide, chocolate)
  - Fuzzy matching with keyword search
- Uses your [dictionaries.js](dictionaries.js) file to enrich data with:
  - Full item descriptions
  - SKU information
  - Store rankings (AA, A, B, C) with color-coded badges
- **Data Validation** with real-time matching statistics and warnings

## How to Run (IMPORTANT)

### The app won't start in Git Bash! Use PowerShell instead:

1. **Open PowerShell** (not Git Bash)
   - Press `Win + X` and select "Windows PowerShell"
   - Or search for "PowerShell" in Start menu

2. **Navigate to your project:**
   ```powershell
   cd "e:\CODE\Alpha\AB\NewAB"
   ```

3. **Start the app:**
   ```powershell
   npm start
   ```

## What to Do Next

1. **Click "Select Excel/CSV File"** button
2. **Try these files:**
   - [sample-data.csv](sample-data.csv) - Perfect matches (100% green badges)
   - [test-matching.csv](test-matching.csv) - Tests fuzzy matching (see warnings)
3. **Try the features:**
   - Toggle between "View by Store" and "View by Item"
   - Use the store filter dropdown
   - Search by item code, description, or SKU
   - Notice the color-coded rank badges (AA=pink, A=blue, B=green, C=orange)
   - **Click the warnings badge** to see detailed matching information

## Features You'll See

### View by Store
- Each store card shows all items allocated to it
- Store rank badge (e.g., "A", "B", "C") with color
- Item codes with full descriptions
- SKU information displayed
- Total quantities per store

### View by Item
- Each item card shows which stores receive it
- Full item description
- All SKU codes listed
- Store rankings visible
- Total quantities across all stores

### Smart Search
Search works across:
- Item codes (e.g., "GLD-1")
- Descriptions (e.g., "GLIDE")
- SKU numbers (e.g., "410021982504")

## File Format

Your CSV or Excel files should have columns for:
- **Store**: Store name or ID (matches dictionary: WATERLOO 1, TORONTO 1, etc.)
- **Item**: Item code (matches dictionary: GLD-1, CR2032-3, etc.)
- **Quantity**: Number of units

The app automatically detects columns even with different names!

## Troubleshooting

If you see an error about "whenReady", see [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

**TL;DR: Use PowerShell, not Git Bash**

## Your Dictionary Integration

The app automatically uses your [dictionaries.js](dictionaries.js) which contains:
- **6,958+ items** with descriptions and SKUs
- **34 stores** with IDs, names, and rankings

No configuration needed - it just works!

## Next Steps

1. Replace [sample-data.csv](sample-data.csv) with your actual allocation data
2. **Don't worry about exact matches!** The smart matching handles:
   - Store IDs instead of names (101 = WATERLOO 1)
   - SKU codes instead of item numbers (410021982504 = GLD-1)
   - Partial descriptions (glide = GLD-1, chocolate = SN4N1FCS4)
3. Check the warnings badge to verify fuzzy matches are correct
4. See [SMART-MATCHING.md](SMART-MATCHING.md) for detailed matching strategies

Enjoy your Store Allocation Viewer! 🎉
