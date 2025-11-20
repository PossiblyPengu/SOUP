const XLSX = require('xlsx');
const fs = require('fs');

// Load the dictionaries file
const dictContent = fs.readFileSync('dictionaries.js', 'utf8');

// Parse the dictionary by evaluating it (create a mock window object)
const window = {};
eval(dictContent);

const DICT = window.DICT;

console.log('📚 Loaded Dictionaries:');
console.log(`   Items: ${DICT.items.length}`);
console.log(`   Stores: ${DICT.stores.length}`);
console.log('');

// Use actual stores from dictionary (all 34 stores)
const stores = DICT.stores;

// Use a good mix of items from dictionary (let's use 50 random items for variety)
const allItems = DICT.items;
const selectedItems = [];

// Select random items ensuring we get a good variety
const itemIndices = new Set();
while (itemIndices.size < Math.min(50, allItems.length)) {
  itemIndices.add(Math.floor(Math.random() * allItems.length));
}

itemIndices.forEach(idx => selectedItems.push(allItems[idx]));

console.log(`📦 Using ${selectedItems.length} items across ${stores.length} stores`);
console.log('');

// Generate allocations with realistic distribution
const allocations = [];

selectedItems.forEach(item => {
  stores.forEach(store => {
    // Random decision whether to allocate to this store (75% probability)
    // Skip STAG WEB (online store) for most items
    if (store.id === 199 && Math.random() > 0.1) {
      return; // Only 10% of items go to web store
    }

    if (Math.random() > 0.25) {
      let baseQty;

      // Allocate more to higher-ranked stores
      switch(store.rank) {
        case 'AA':
          baseQty = Math.floor(Math.random() * 150) + 200; // 200-350 (web store)
          break;
        case 'A':
          baseQty = Math.floor(Math.random() * 80) + 100; // 100-180
          break;
        case 'B':
          baseQty = Math.floor(Math.random() * 60) + 60; // 60-120
          break;
        case 'C':
          baseQty = Math.floor(Math.random() * 40) + 30; // 30-70
          break;
        default:
          baseQty = Math.floor(Math.random() * 50) + 25; // 25-75
      }

      allocations.push({
        Store: store.name,
        'Store ID': store.id,
        Item: item.number,
        Description: item.desc,
        SKU: item.sku[0], // Use first SKU
        Quantity: baseQty
      });
    }
  });
});

console.log(`✨ Generated ${allocations.length} allocation records`);
console.log('');

// Create workbook
const wb = XLSX.utils.book_new();

// Create main allocation sheet
const ws = XLSX.utils.json_to_sheet(allocations);

// Set column widths for better readability
ws['!cols'] = [
  { wch: 20 }, // Store
  { wch: 10 }, // Store ID
  { wch: 18 }, // Item
  { wch: 40 }, // Description
  { wch: 18 }, // SKU
  { wch: 10 }  // Quantity
];

// Add the worksheet to the workbook
XLSX.utils.book_append_sheet(wb, ws, 'Store Allocations');

// Create a summary sheet by store
const storeSummary = stores.map(store => {
  const storeAllocs = allocations.filter(a => a['Store ID'] === store.id);
  const totalQty = storeAllocs.reduce((sum, a) => sum + a.Quantity, 0);
  const itemCount = storeAllocs.length;

  return {
    'Store ID': store.id,
    'Store Name': store.name,
    'Rank': store.rank,
    'Total Items': itemCount,
    'Total Quantity': totalQty,
    'Avg Qty/Item': itemCount > 0 ? Math.round(totalQty / itemCount) : 0
  };
}).sort((a, b) => a['Store ID'] - b['Store ID']);

const wsSummary = XLSX.utils.json_to_sheet(storeSummary);
wsSummary['!cols'] = [
  { wch: 10 }, // Store ID
  { wch: 20 }, // Store Name
  { wch: 8 },  // Rank
  { wch: 12 }, // Total Items
  { wch: 15 }, // Total Quantity
  { wch: 12 }  // Avg Qty/Item
];

XLSX.utils.book_append_sheet(wb, wsSummary, 'Store Summary');

// Create an item summary sheet
const itemSummary = selectedItems.map(item => {
  const itemAllocs = allocations.filter(a => a.Item === item.number);
  const totalQty = itemAllocs.reduce((sum, a) => sum + a.Quantity, 0);
  const storeCount = itemAllocs.length;

  return {
    'Item Code': item.number,
    'Description': item.desc,
    'Primary SKU': item.sku[0],
    'All SKUs': item.sku.length,
    'Stores': storeCount,
    'Total Quantity': totalQty,
    'Avg Qty/Store': storeCount > 0 ? Math.round(totalQty / storeCount) : 0
  };
});

const wsItemSummary = XLSX.utils.json_to_sheet(itemSummary);
wsItemSummary['!cols'] = [
  { wch: 15 }, // Item Code
  { wch: 40 }, // Description
  { wch: 18 }, // Primary SKU
  { wch: 10 }, // All SKUs
  { wch: 10 }, // Stores
  { wch: 15 }, // Total Quantity
  { wch: 14 }  // Avg Qty/Store
];

XLSX.utils.book_append_sheet(wb, wsItemSummary, 'Item Summary');

// Write the file
const filename = 'test-store-allocations.xlsx';
XLSX.writeFile(wb, filename);

// Print statistics
console.log('✅ Test Excel file generated successfully!');
console.log('');
console.log('📊 File Statistics:');
console.log(`   File: ${filename}`);
console.log(`   Total Allocations: ${allocations.length.toLocaleString()}`);
console.log(`   Total Stores: ${stores.length}`);
console.log(`   Total Items: ${selectedItems.length}`);
console.log(`   Sheets: Store Allocations, Store Summary, Item Summary`);
console.log('');
console.log('📈 Store Breakdown by Rank:');
const rankCounts = {};
stores.forEach(s => {
  rankCounts[s.rank] = (rankCounts[s.rank] || 0) + 1;
});
Object.keys(rankCounts).sort().forEach(rank => {
  console.log(`   Rank ${rank}: ${rankCounts[rank]} stores`);
});
console.log('');
const totalQty = allocations.reduce((sum, a) => sum + a.Quantity, 0);
console.log(`📦 Total Quantity Allocated: ${totalQty.toLocaleString()} units`);
console.log(`   Average per allocation: ${Math.round(totalQty / allocations.length)} units`);
console.log('');
console.log('🏪 Sample Stores:');
stores.slice(0, 10).forEach(s => {
  const count = allocations.filter(a => a['Store ID'] === s.id).length;
  console.log(`   ${s.id.toString().padStart(3)} - ${s.name.padEnd(20)} (${s.rank}) - ${count} items`);
});
if (stores.length > 10) {
  console.log(`   ... and ${stores.length - 10} more stores`);
}
