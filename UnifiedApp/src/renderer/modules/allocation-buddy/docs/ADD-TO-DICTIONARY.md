# Add to Dictionary Feature

## Overview

When loading data files, if items are not found in the dictionary, you can now easily add them for the current session.

## How It Works

### 1. Load Your Data File
When you load a file with unmatched items, you'll see a warning badge in the file info area.

### 2. View Warnings
Click the "⚠️ warnings" badge to see details about unmatched items and stores.

### 3. Add Unmatched Items
For each unmatched item, you'll see an **"➕ Add to Dictionary"** button.

### 4. Fill in Item Details
A dialog will appear asking for:
- **Item Number** (pre-filled, read-only)
- **Description** (required) - Enter a clear description of the item
- **SKU** (optional) - Enter barcode or SKU number if available

### 5. Add to Session
Click "Add to Dictionary" to add the item to your session.

### 6. Reload Data (Optional)
After adding items, you'll be prompted to reload your data file. This will rematch all items with the updated dictionary.

## Example Workflow

```
1. Load file: Essential Allocations-10-20-2025.xlsx
   └─ Shows: Items: 6500/6530 matched (99%)
           ⚠️ 60 warnings

2. Click "⚠️ 60 warnings"
   └─ See list of unmatched items:
      • NEWITEM123 (30 occurrences) [➕ Add to Dictionary]
      • TESTPROD456 (30 occurrences) [➕ Add to Dictionary]

3. Click "➕ Add to Dictionary" next to NEWITEM123
   └─ Dialog opens:
      Item Number: NEWITEM123
      Description: [Enter description]
      SKU: [Optional]

4. Fill in:
   Description: "New Test Product Widget"
   SKU: "123456789"

5. Click "Add to Dictionary"
   └─ Item added to session
   └─ Prompt: "Would you like to reload your data file?"

6. Click "OK" to reload
   └─ Now shows: Items: 6530/6530 matched (100%) ✅
```

## Important Notes

### Session Only
- Items added this way are **stored in memory only**
- They will be lost when you close the app
- To permanently add items, update `src/js/dictionaries.js`

### Permanent Addition
To permanently add items to the dictionary:

1. Open `src/js/dictionaries.js`
2. Find the `"items"` array
3. Add your item in the correct format:
   ```javascript
   {
     "number": "NEWITEM123",
     "desc": "New Test Product Widget",
     "sku": [
       "123456789"
     ]
   },
   ```
4. Make sure to add a comma after the previous item
5. Save the file

### Bulk Additions
If you have many items to add:
1. Export the unmatched items list
2. Create a proper dictionary entry for each
3. Add them to `dictionaries.js` all at once
4. Restart the app

## Benefits

✅ **Quick fixes** for missing items  
✅ **Continue working** without editing dictionary files  
✅ **Test items** before permanent addition  
✅ **No file editing** required during data import  
✅ **Immediate results** - reload and see matches  

## Tips

- **Be accurate**: Enter clear, detailed descriptions
- **Add SKUs**: Helps with future matching
- **Use consistent naming**: Follow existing dictionary patterns
- **Document permanent items**: After session testing, add important items permanently
- **Keep backups**: Before editing dictionary files directly

## Troubleshooting

**Issue**: Button doesn't appear  
- Ensure you have unmatched items (check warnings)
- Refresh the warnings dialog

**Issue**: Item not matching after adding  
- Check spelling of item number
- Reload the data file after adding
- Verify item was added (check console: `itemsMap.size`)

**Issue**: Items lost after closing app  
- This is expected - session-only storage
- Add items permanently to `dictionaries.js`

## Technical Details

### Storage
Items are added to:
- `window.DICT.items` array
- `itemsMap` Map for fast lookup
- `itemsByDesc` Map for description search

### Matching
After adding an item:
- Exact match by item number (case-insensitive)
- Exact match by SKU (if provided)
- Fuzzy match by description keywords

### Console Verification
```javascript
// Check if item was added
testDictionary()

// Manual lookup
findItemInDictionary('NEWITEM123')
```
