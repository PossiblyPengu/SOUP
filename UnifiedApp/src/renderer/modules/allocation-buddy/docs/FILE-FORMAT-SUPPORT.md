# File Format Support

## Supported File Formats

The Store Allocation Viewer now supports multiple file formats from different sources.

### Format 1: Simple Allocation Format
**Columns:**
- `Store` or `Store Name` - Store name (e.g., "WATERLOO 1")
- `Item` - Item code (e.g., "GLD-1")
- `Quantity` or `Qty` - Allocation quantity

**Example:**
```csv
Store,Item,Quantity
WATERLOO 1,GLD-1,50
KITCHENER 1,GLD-1,75
```

### Format 2: Business Central Replenishment Format
**File:** `Essential Allocations-10-20-2025.xlsx`

**Columns:**
- `Item No.` - Item code (e.g., "003IE")
- `Location Code` - Store ID (e.g., "101", "102", "103")
- `Maximum Inventory` - Target allocation quantity
- `Reorder Point` - Minimum inventory level

**Features:**
- Automatically matches store IDs (101, 102, etc.) to store names using dictionary
- Uses "Maximum Inventory" as the quantity column
- Supports 6530+ rows of data

**Example:**
```
Item No. | Location Code | Reorder Point | Maximum Inventory
003IE    | 101          | 2             | 2
003IE    | 102          | 4             | 4
```

## Column Detection

The app uses intelligent column detection with these priorities:

### Store Column Detection
1. `Store Name`, `Shop Name`
2. `Location Code` ✨ (Business Central format)
3. `Store Code`
4. Any column containing "store", "shop", "location"

### Item Column Detection
1. `Item`
2. `Item No.` ✨ (Business Central format)
3. `Item Number`
4. `Product`, `SKU`
5. Any column containing "item", "description", "style"

### Quantity Column Detection
1. `Qty`, `Quantity`
2. `Amount`, `Allocation`, `Units`
3. `Maximum Inventory` ✨ (Business Central format)
4. `Max Inv`
5. `Reorder Point` ✨ (Business Central format)
6. Any numeric column (excluding store IDs)

## Dictionary Matching

### Store Matching
- ✅ By Store ID: `101` → WATERLOO 1
- ✅ By Store Name: `WATERLOO 1` → WATERLOO 1
- ✅ Partial match: `WATERLOO` → WATERLOO 1

### Item Matching
- ✅ By Item Number: `003IE` → exact match
- ✅ By SKU: `111557` → matched item
- ✅ By Description keywords: Partial text matching

## Troubleshooting

### Check if file loaded correctly
Open Developer Console (F12) and type:
```javascript
testDictionary()
```

### View column detection
After loading a file, check the Console for:
```
Final column detection: Store="Location Code", Item="Item No.", Quantity="Maximum Inventory"
```

### Common Issues

**Issue:** Store numbers (101, 102) show as quantities
- ✅ **Fixed:** App now excludes "Location Code" from quantity detection

**Issue:** Store IDs don't match store names  
- ✅ **Fixed:** Dictionary lookup now handles both IDs and names

**Issue:** Wrong columns detected
- Check console for column detection log
- Ensure column names match supported patterns
- File should have clear headers in first row

## Testing Your File

1. Load your file in the app
2. Press `F12` to open Developer Console
3. Check for these messages:
   - `Dictionary loaded: [number] item mappings, [number] store mappings`
   - `Final column detection: Store="...", Item="...", Quantity="..."`
   - `Processing [number] data rows`
4. Look for any warnings about unmatched items or stores

## Supported Store IDs

```
101 = WATERLOO 1 (C)
102 = KITCHENER 1 (B)
103 = CAMBRIDGE (B)
104 = LONDON 1 (B)
105 = LONDON 2 (B)
106 = HAMILTON 1 (C)
107 = MISSISSAUGA (A)
108 = ST CATHARINES (B)
109 = HAMILTON 2 (A)
110 = TORONTO 1 (A)
111 = SCARBOROUGH (A)
112 = WINDSOR 1 (A)
... and more
```

See full list in `src/js/dictionaries.js`
