// Access secure Electron API and libraries through preload script
// XLSX and Papa are exposed via contextBridge in preload.js
// Access them directly from window object

// Utility: Debounce function for performance optimization
function debounce(func, wait = 300) {
  let timeout;
  return function executedFunction(...args) {
    const later = () => {
      clearTimeout(timeout);
      func.apply(this, args);
    };
    clearTimeout(timeout);
    timeout = setTimeout(later, wait);
  };
}

let currentData = [];
let organizedData = {};
let currentView = 'store'; // 'store' or 'item'
let excludedStores = new Set(); // Stores that are excluded from parsing
let includedData = {}; // Data with excluded stores removed
let excludedData = {}; // Data from excluded stores only
let redistributedItems = new Set(); // Track items that were redistributed for highlighting
let currentRedistributionPlan = null; // Temporary storage for redistribution dialog
let currentRedistributionMethod = null;

// History Manager for Undo/Redo
class HistoryManager {
  constructor() {
    this.history = [];
    this.currentIndex = -1;
    this.maxHistory = 50; // Limit history to prevent memory issues
  }

  /**
   * Save current state to history
   */
  pushState(description) {
    // Remove any states after current index (if user did undo then makes new change)
    this.history = this.history.slice(0, this.currentIndex + 1);

    // Create snapshot of current state
    const state = {
      description,
      timestamp: Date.now(),
      organizedData: JSON.parse(JSON.stringify(organizedData)),
      excludedStores: new Set(excludedStores),
      includedData: JSON.parse(JSON.stringify(includedData)),
      excludedData: JSON.parse(JSON.stringify(excludedData)),
      redistributedItems: new Set(redistributedItems),
    };

    this.history.push(state);
    this.currentIndex++;

    // Trim old history if exceeds max
    if (this.history.length > this.maxHistory) {
      this.history.shift();
      this.currentIndex--;
    }

    this.updateUndoRedoButtons();
  }

  /**
   * Undo to previous state
   */
  undo() {
    if (!this.canUndo()) return null;

    this.currentIndex--;
    const state = this.history[this.currentIndex];
    this.restoreState(state);
    this.updateUndoRedoButtons();
    return state.description;
  }

  /**
   * Redo to next state
   */
  redo() {
    if (!this.canRedo()) return null;

    this.currentIndex++;
    const state = this.history[this.currentIndex];
    this.restoreState(state);
    this.updateUndoRedoButtons();
    return state.description;
  }

  /**
   * Restore a saved state
   */
  restoreState(state) {
    organizedData = JSON.parse(JSON.stringify(state.organizedData));
    excludedStores = new Set(state.excludedStores);
    includedData = JSON.parse(JSON.stringify(state.includedData));
    excludedData = JSON.parse(JSON.stringify(state.excludedData));
    redistributedItems = new Set(state.redistributedItems);

    // Update UI
    displayStoreSelectionList();
    updateStats();
    displayData();
    displayExcludedData();
  }

  canUndo() {
    return this.currentIndex > 0;
  }

  canRedo() {
    return this.currentIndex < this.history.length - 1;
  }

  /**
   * Update undo/redo button states
   */
  updateUndoRedoButtons() {
    const undoBtn = document.getElementById('undoBtn');
    const redoBtn = document.getElementById('redoBtn');

    if (undoBtn) {
      undoBtn.disabled = !this.canUndo();
      undoBtn.title = this.canUndo()
        ? `Undo: ${this.history[this.currentIndex - 1]?.description || ''}`
        : 'Nothing to undo';
    }

    if (redoBtn) {
      redoBtn.disabled = !this.canRedo();
      redoBtn.title = this.canRedo()
        ? `Redo: ${this.history[this.currentIndex + 1]?.description || ''}`
        : 'Nothing to redo';
    }
  }

  /**
   * Clear all history
   */
  clear() {
    this.history = [];
    this.currentIndex = -1;
    this.updateUndoRedoButtons();
  }
}

const historyManager = new HistoryManager();

// Dictionary helper functions
const itemsMap = new Map();
const storesMap = new Map();
const itemsByDesc = new Map(); // For description-based lookup
const storesByName = new Map(); // For fuzzy name matching
let matchingStats = { matched: 0, unmatched: 0, warnings: [] };

// Initialize dictionary maps on load
function initializeDictionaries() {
  if (window.DICT && window.DICT.items) {
    window.DICT.items.forEach(item => {
      // Map by item number (exact)
      itemsMap.set(item.number.toUpperCase(), item);

      // Map by SKU (exact)
      if (item.sku) {
        item.sku.forEach(sku => {
          itemsMap.set(sku.toUpperCase(), item);
        });
      }

      // Map by description keywords for fuzzy matching
      const descWords = item.desc.toUpperCase().split(/\s+/);
      descWords.forEach(word => {
        if (word.length >= 3) { // Only significant words (3+ characters)
          if (!itemsByDesc.has(word)) {
            itemsByDesc.set(word, []);
          }
          itemsByDesc.get(word).push(item);
        }
      });
    });
  }

  if (window.DICT && window.DICT.stores) {
    window.DICT.stores.forEach(store => {
      // Map by ID (exact)
      storesMap.set(store.id, store);

      // Map by full name (exact, case-insensitive)
      storesMap.set(store.name.toUpperCase(), store);

      // Map by name keywords for fuzzy matching
      const nameWords = store.name.toUpperCase().split(/\s+/);
      nameWords.forEach(word => {
        if (!storesByName.has(word)) {
          storesByName.set(word, []);
        }
        storesByName.get(word).push(store);
      });
    });
  }

  console.log(`Dictionary loaded: ${itemsMap.size} item mappings, ${storesMap.size} store mappings`);
}

// Smart item matching with fallback strategies
/**
 * Find item in dictionary using 4-tier matching strategy
 * @param {string|number} inputValue - Item code, SKU, or description to search
 * @returns {Object|null} Match result with item, confidence, and matchType, or null if no match
 * @example
 * // Exact match by number
 * findItemInDictionary('GLD-1') // => { item: {...}, confidence: 'exact', matchType: 'number' }
 *
 * // Exact match by SKU
 * findItemInDictionary('410021982504') // => { item: {...}, confidence: 'exact', matchType: 'sku' }
 *
 * // Fuzzy match by description
 * findItemInDictionary('glide') // => { item: {...}, confidence: 'fuzzy', matchType: 'description', score: 1 }
 */
function findItemInDictionary(inputValue) {
  if (!inputValue) return null;

  const input = String(inputValue).trim().toUpperCase();

  // Ignore very short inputs (likely store ranks like A, B, C, AA)
  // Most legitimate item codes are at least 3 characters
  if (input.length < 3) {
    return null;
  }

  // Strategy 1: Exact match by item number
  if (itemsMap.has(input)) {
    return { item: itemsMap.get(input), confidence: 'exact', matchType: 'number' };
  }

  // Strategy 2: Exact match by SKU
  for (const [key, item] of itemsMap.entries()) {
    if (item.sku && item.sku.some(sku => sku.toUpperCase() === input)) {
      return { item, confidence: 'exact', matchType: 'sku' };
    }
  }

  // Strategy 3: Partial match by item number (starts with)
  for (const [key, item] of itemsMap.entries()) {
    if (key === item.number.toUpperCase() && key.startsWith(input)) {
      return { item, confidence: 'partial', matchType: 'number-prefix' };
    }
  }

  // Description-based fuzzy matching disabled
  return null;
}

/**
 * Find store in dictionary using 4-tier matching strategy
 * @param {string|number} inputValue - Store ID or name to search
 * @returns {Object|null} Match result with store, confidence, and matchType, or null if no match
 * @example
 * // Exact match by ID
 * findStoreInDictionary('101') // => { store: {...}, confidence: 'exact', matchType: 'id' }
 *
 * // Exact match by name
 * findStoreInDictionary('WATERLOO 1') // => { store: {...}, confidence: 'exact', matchType: 'name' }
 *
 * // Partial match
 * findStoreInDictionary('Toronto') // => { store: {...}, confidence: 'partial', matchType: 'name-contains' }
 */
function findStoreInDictionary(inputValue) {
  if (!inputValue) return null;

  const input = String(inputValue).trim();

  // Strategy 1: Exact match by ID (if numeric)
  const numId = parseInt(input);
  if (!isNaN(numId) && storesMap.has(numId)) {
    return { store: storesMap.get(numId), confidence: 'exact', matchType: 'id' };
  }

  // Strategy 2: Exact match by name (case-insensitive)
  const inputUpper = input.toUpperCase();
  if (storesMap.has(inputUpper)) {
    return { store: storesMap.get(inputUpper), confidence: 'exact', matchType: 'name' };
  }

  // Strategy 3: Partial match by name (contains)
  for (const [key, store] of storesMap.entries()) {
    if (typeof key === 'string' && key.includes(inputUpper)) {
      return { store, confidence: 'partial', matchType: 'name-contains' };
    }
  }

  // Strategy 4: Match by name keywords
  const inputWords = inputUpper.split(/\s+/);
  const candidates = [];
  inputWords.forEach(word => {
    if (storesByName.has(word)) {
      storesByName.get(word).forEach(store => {
        const existing = candidates.find(c => c.store.id === store.id);
        if (existing) {
          existing.score++;
        } else {
          candidates.push({ store, score: 1 });
        }
      });
    }
  });

  if (candidates.length > 0) {
    candidates.sort((a, b) => b.score - a.score);
    return { store: candidates[0].store, confidence: 'fuzzy', matchType: 'name-keywords', score: candidates[0].score };
  }

  return null;
}

// Get item details from dictionary (backward compatibility)
function getItemDetails(itemCode) {
  const result = findItemInDictionary(itemCode);
  return result ? result.item : null;
}

// Get store details from dictionary (backward compatibility)
function getStoreDetails(storeIdentifier) {
  const result = findStoreInDictionary(storeIdentifier);
  return result ? result.store : null;
}

// Format item display with description
function formatItemDisplay(itemCode) {
  const details = getItemDetails(itemCode);
  if (details) {
    return {
      code: itemCode,
      description: details.desc,
      displayText: `${itemCode} - ${details.desc}`,
      skus: details.sku || []
    };
  }
  return {
    code: itemCode,
    description: '',
    displayText: itemCode,
    skus: []
  };
}

// Format store display with rank
function formatStoreDisplay(storeIdentifier) {
  const details = getStoreDetails(storeIdentifier);
  if (details) {
    return {
      id: details.id,
      name: details.name,
      rank: details.rank,
      displayText: `${details.name} (${details.rank})`
    };
  }
  return {
    id: null,
    name: storeIdentifier,
    rank: null,
    displayText: storeIdentifier
  };
}

// Sort stores by ID/number
function sortStoresByNumber(storeNames) {
  return storeNames.sort((a, b) => {
    const storeA = getStoreDetails(a);
    const storeB = getStoreDetails(b);

    // If both have IDs, sort by ID numerically
    if (storeA && storeA.id && storeB && storeB.id) {
      return storeA.id - storeB.id;
    }

    // If only one has an ID, prioritize it
    if (storeA && storeA.id) return -1;
    if (storeB && storeB.id) return 1;

    // Otherwise, sort alphabetically by name
    return a.localeCompare(b);
  });
}

// Initialize dictionaries when script loads
initializeDictionaries();

// Diagnostic function to test dictionary (available in console as window.testDictionary())
window.testDictionary = function() {
  console.log('=== DICTIONARY DIAGNOSTIC ===');
  console.log('Dictionary object exists:', !!window.DICT);
  console.log('Items array exists:', !!window.DICT?.items);
  console.log('Stores array exists:', !!window.DICT?.stores);
  console.log('Items count:', window.DICT?.items?.length || 0);
  console.log('Stores count:', window.DICT?.stores?.length || 0);
  console.log('Items Map size:', itemsMap.size);
  console.log('Stores Map size:', storesMap.size);
  
  // Test some lookups
  console.log('\n=== TEST LOOKUPS ===');
  
  // Test item lookup
  const testItem = findItemInDictionary('GLD-1');
  console.log('Test item "GLD-1":', testItem);
  
  // Test store lookup by name
  const testStore1 = findStoreInDictionary('WATERLOO 1');
  console.log('Test store "WATERLOO 1":', testStore1);
  
  // Test store lookup by ID
  const testStore2 = findStoreInDictionary('101');
  console.log('Test store ID "101":', testStore2);
  
  // Show a few sample items
  console.log('\n=== SAMPLE ITEMS (first 5) ===');
  window.DICT?.items?.slice(0, 5).forEach((item, i) => {
    console.log(`${i + 1}. ${item.number} - ${item.desc}`);
  });
  
  // Show a few sample stores
  console.log('\n=== SAMPLE STORES (first 5) ===');
  window.DICT?.stores?.slice(0, 5).forEach((store, i) => {
    console.log(`${i + 1}. ID ${store.id}: ${store.name} (Rank ${store.rank})`);
  });
  
  console.log('\n=== TEST COMPLETE ===');
  return {
    dictionaryLoaded: !!window.DICT,
    itemsCount: window.DICT?.items?.length || 0,
    storesCount: window.DICT?.stores?.length || 0,
    itemsMapSize: itemsMap.size,
    storesMapSize: storesMap.size,
    testsPassed: !!testItem && !!testStore1 && !!testStore2
  };
};

// Loading Overlay Functions
function showLoadingOverlay() {
  const overlay = document.getElementById('loadingOverlay');
  if (overlay) {
    overlay.style.display = 'flex';
    // Force reflow to restart animation
    void overlay.offsetWidth;
    overlay.classList.add('show');
  }
}

function hideLoadingOverlay() {
  const overlay = document.getElementById('loadingOverlay');
  if (overlay) {
    overlay.classList.remove('show');
    setTimeout(() => {
      overlay.style.display = 'none';
    }, 300);
  }
}

function updateLoadingProgress(step) {
  const steps = ['reading', 'parsing', 'matching', 'organizing'];
  steps.forEach((s, index) => {
    const element = document.getElementById(`progress-${s}`);
    if (element) {
      element.classList.remove('active', 'completed');
      if (s === step) {
        element.classList.add('active');
      } else if (steps.indexOf(s) < steps.indexOf(step)) {
        element.classList.add('completed');
        const icon = element.querySelector('.progress-icon');
        if (icon) icon.textContent = '✓';
      }
    }
  });
}

/**
 * Show styled dialog instead of browser alert
 * @param {string} message - Message to display
 * @param {string} title - Optional title (default: "Error")
 * @param {string} type - Dialog type: "error", "success", "info" (default: "error")
 */
function showErrorDialog(message, title = 'Error', type = 'error') {
  const overlay = document.createElement('div');
  overlay.className = 'error-dialog-overlay';

  const dialog = document.createElement('div');
  dialog.className = 'error-dialog';

  // Determine header class and icon based on type
  let headerClass = 'error-dialog-header';
  let icon = '⚠️';

  if (type === 'success') {
    headerClass += ' success';
    icon = '✓';
    if (title === 'Error') title = 'Success';
  } else if (type === 'info') {
    headerClass += ' info';
    icon = 'ℹ️';
    if (title === 'Error') title = 'Information';
  }

  dialog.innerHTML = `
    <div class="${headerClass}">
      <h2>${title}</h2>
      <button class="error-dialog-close" onclick="this.closest('.error-dialog-overlay').remove()">✕</button>
    </div>
    <div class="error-dialog-content">
      <div class="error-dialog-icon">${icon}</div>
      <p>${message}</p>
    </div>
    <div class="error-dialog-actions">
      <button class="btn btn-primary" onclick="this.closest('.error-dialog-overlay').remove()">OK</button>
    </div>
  `;

  overlay.appendChild(dialog);
  document.body.appendChild(overlay);

  // Animate in
  setTimeout(() => overlay.classList.add('show'), 10);
}

// DOM Elements
const selectFileBtn = document.getElementById('selectFileBtn');
const clearBtn = document.getElementById('clearBtn');
const filePathDisplay = document.getElementById('filePathDisplay');
const fileInfo = document.getElementById('fileInfo');
const storeFilter = document.getElementById('storeFilter');
const searchBox = document.getElementById('searchBox');
const clearFiltersBtn = document.getElementById('clearFilters');
const dataDisplay = document.getElementById('dataDisplay');
const allocationsContent = document.getElementById('allocationsContent');
const viewByStoreBtn = document.getElementById('viewByStore');
const viewByItemBtn = document.getElementById('viewByItem');
const totalStoresEl = document.getElementById('totalStores');
const totalItemsEl = document.getElementById('totalItems');
const totalQuantityEl = document.getElementById('totalQuantity');

// Store selection elements
const storeSelectionSection = document.getElementById('storeSelectionSection');
const storeCheckboxList = document.getElementById('storeCheckboxList');
const selectAllStoresBtn = document.getElementById('selectAllStores');
const deselectAllStoresBtn = document.getElementById('deselectAllStores');

// Excluded section elements
const excludedSection = document.getElementById('excludedSection');
const excludedDataDisplay = document.getElementById('excludedDataDisplay');
const exportExcludedBtn = document.getElementById('exportExcluded');
const redistributeEquallyBtn = document.getElementById('redistributeEqually');
const redistributeByRankBtn = document.getElementById('redistributeByRank');

// Undo/Redo buttons
const undoBtn = document.getElementById('undoBtn');
const redoBtn = document.getElementById('redoBtn');
const stickyActions = document.getElementById('stickyActions');

// Event Listeners
selectFileBtn.addEventListener('click', selectFile);
clearBtn.addEventListener('click', clearAll);
clearFiltersBtn.addEventListener('click', clearFilters);
viewByStoreBtn.addEventListener('click', () => switchView('store'));
viewByItemBtn.addEventListener('click', () => switchView('item'));
storeFilter.addEventListener('change', applyFilters);
// Debounce search input for better performance
searchBox.addEventListener('input', debounce(applyFilters, 300));

// Undo/Redo event listeners
undoBtn.addEventListener('click', () => {
  const description = historyManager.undo();
  if (description) {
    showErrorDialog(`Undid: ${description}`, 'Undo', 'info');
  }
});

redoBtn.addEventListener('click', () => {
  const description = historyManager.redo();
  if (description) {
    showErrorDialog(`Redid: ${description}`, 'Redo', 'info');
  }
});

// Store selection event listeners
selectAllStoresBtn.addEventListener('click', selectAllStores);
deselectAllStoresBtn.addEventListener('click', deselectAllStores);

// Redistribution event listeners
exportExcludedBtn.addEventListener('click', exportExcludedData);
redistributeEquallyBtn.addEventListener('click', redistributeEqually);
redistributeByRankBtn.addEventListener('click', redistributeByRank);

async function selectFile() {
  try {
    // Access electronAPI from parent window (since module runs in iframe)
    const electronAPI = window.parent?.electronAPI || window.electronAPI;
    
    if (!electronAPI?.selectFile) {
      throw new Error('File selection API not available');
    }
    
    const filePath = await electronAPI.selectFile({
      filters: [
        { name: 'Excel Files', extensions: ['xlsx', 'xlsm', 'xls'] },
        { name: 'CSV Files', extensions: ['csv'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });
    
    if (filePath) {
      filePathDisplay.value = filePath;
      loadFile(filePath);
    }
  } catch (error) {
    console.error('Error selecting file:', error);
    showErrorDialog(`Failed to open file selection dialog: ${error.message}`);
  }
}

async function loadFile(filePath) {
  try {
    // Access electronAPI from parent window (since module runs in iframe)
    const electronAPI = window.parent?.electronAPI || window.electronAPI;
    
    if (!electronAPI) {
      throw new Error('Electron API not available');
    }
    
    // Show loading overlay
    showLoadingOverlay();
    updateLoadingProgress('reading');

    // Get file stats first to validate and display file info
    const statsResult = await electronAPI.getFileStats(filePath);
    if (!statsResult.success) {
      hideLoadingOverlay();
      showErrorDialog('Failed to access file: ' + statsResult.error);
      return;
    }

    // Display file size info (stats properties are at top level, not nested)
    const fileSize = statsResult.size;
    const fileSizeKB = (fileSize / 1024).toFixed(2);
    const fileSizeMB = (fileSize / (1024 * 1024)).toFixed(2);
    const displaySize = fileSize > 1024 * 1024 ? `${fileSizeMB} MB` : `${fileSizeKB} KB`;
    fileInfo.innerHTML = `<span style="color: var(--text-secondary);">File size: ${displaySize}</span>`;

    const ext = filePath.split('.').pop().toLowerCase();

    // Delay actual file loading to allow animation to start
    setTimeout(async () => {
      try {
        if (ext === 'csv') {
          await loadCSV(filePath);
        } else if (ext === 'xlsx' || ext === 'xls') {
          await loadExcel(filePath);
        } else {
          hideLoadingOverlay();
          showErrorDialog('Unsupported file format. Please select a CSV or Excel file.');
        }
      } catch (error) {
        hideLoadingOverlay();
        console.error('Error loading file:', error);
        showErrorDialog('Error loading file: ' + error.message);
      }
    }, 500);
  } catch (error) {
    hideLoadingOverlay();
    console.error('Error loading file:', error);
    showErrorDialog('Error loading file: ' + error.message);
  }
}

async function loadCSV(filePath) {
  updateLoadingProgress('parsing');

  // Use setTimeout to allow progress update to render
  setTimeout(async () => {
    try {
      // Access electronAPI from parent window
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const buffer = await electronAPI.readFile(filePath);
      
      // Convert Buffer to string
      const fileContent = new TextDecoder('utf-8').decode(buffer);
      window.Papa.parse(fileContent, {
        header: true,
        skipEmptyLines: true,
        complete: function(results) {
          if (!results.data || results.data.length === 0) {
            hideLoadingOverlay();
            showErrorDialog('CSV file is empty or contains no valid data');
            return;
          }

          // Delay to show parsing step
          setTimeout(() => {
            processData(results.data);
            showFileInfo(filePath, results.data.length);
            // Wait for organizing step to complete before hiding overlay
            setTimeout(() => {
              hideLoadingOverlay();
            }, 800);
          }, 400);
        },
        error: function(error) {
          hideLoadingOverlay();
          showErrorDialog('Error parsing CSV: ' + error.message);
        }
      });
    } catch (error) {
      hideLoadingOverlay();
      showErrorDialog('Error loading CSV: ' + error.message);
    }
  }, 400);
}

async function loadExcel(filePath) {
  updateLoadingProgress('parsing');

  // Use setTimeout to allow progress update to render
  setTimeout(async () => {
    try {
      // Access electronAPI from parent window
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const buffer = await electronAPI.readFile(filePath);
      
      // Parse Excel from buffer using XLSX.read (not readFile)
      const workbook = window.XLSX.read(buffer, { type: 'buffer' });

      if (!workbook.SheetNames || workbook.SheetNames.length === 0) {
        hideLoadingOverlay();
        showErrorDialog('Excel file contains no sheets');
        return;
      }

      const firstSheetName = workbook.SheetNames[0];
      const worksheet = workbook.Sheets[firstSheetName];

      if (!worksheet) {
        hideLoadingOverlay();
        showErrorDialog('Failed to read Excel sheet');
        return;
      }

      const data = window.XLSX.utils.sheet_to_json(worksheet);

      if (!data || data.length === 0) {
        hideLoadingOverlay();
        showErrorDialog('Excel file is empty or contains no valid data');
        return;
      }

      // Delay to show parsing step
      setTimeout(() => {
        processData(data);
        showFileInfo(filePath, data.length);
        // Wait for organizing step to complete before hiding overlay
        setTimeout(() => {
          hideLoadingOverlay();
        }, 800);
      }, 400);
    } catch (error) {
      hideLoadingOverlay();
      showErrorDialog('Error loading Excel file: ' + error.message);
    }
  }, 400);
}

/**
 * Process parsed data - detect columns, match against dictionary, organize by store and item
 * @param {Array<Object>} data - Array of row objects from CSV/Excel parsing
 * @returns {void}
 * @description
 * This is the main data processing pipeline that:
 * 1. Detects store, item, and quantity columns automatically
 * 2. Matches each row against dictionary (items and stores)
 * 3. Organizes data by store and by item
 * 4. Tracks matching statistics and warnings
 * 5. Auto-saves to archive
 */
function processData(data) {
  updateLoadingProgress('matching');

  currentData = data;

  // Reset matching stats
  matchingStats = {
    matched: 0,
    unmatched: 0,
    warnings: [],
    itemsMatched: 0,
    itemsUnmatched: 0,
    storesMatched: 0,
    storesUnmatched: 0,
    unmatchedItems: new Set(),
    unmatchedStores: new Set(),
    matchedStoresSet: new Set(),  // Track unique matched stores
    unmatchedStoresSet: new Set() // Track unique unmatched stores
  };

  // Try to identify columns (flexible to handle different formats)
  const sampleRow = data[0];
  const columns = Object.keys(sampleRow);

  /**
   * Filter out repeated header rows from data
   * Some Excel files have header rows repeated throughout the data
   */
  function filterHeaderRows(data, columns) {
    return data.filter(row => {
      // Check if this row looks like a header row
      // Header rows typically have column names as values
      let headerFieldCount = 0;

      columns.forEach(col => {
        const value = String(row[col]).toLowerCase().trim();
        const columnName = String(col).toLowerCase().trim();

        // If value matches or is very similar to column name, it's likely a header
        if (value === columnName ||
            value.includes(columnName) ||
            columnName.includes(value)) {
          headerFieldCount++;
        }
      });

      // If more than half the fields match column names, it's a header row
      return headerFieldCount < columns.length / 2;
    });
  }

  /**
   * Smart column detection using dictionary matching
   * Analyzes first 10 rows to determine which columns contain stores vs items
   */
  function detectColumnsUsingDictionary(data, columns) {
    // First, filter out any repeated header rows
    const cleanData = filterHeaderRows(data, columns);

    if (cleanData.length === 0) {
      console.warn('All rows appear to be headers! Using original data.');
      cleanData = data;
    }

    console.log(`Filtered ${data.length - cleanData.length} header rows from data`);

    const sampleSize = Math.min(10, cleanData.length);
    const columnScores = columns.map(col => ({
      name: col,
      storeMatches: 0,
      itemMatches: 0,
      numericValues: 0,
      emptyValues: 0
    }));

    // Analyze sample rows (using filtered data)
    for (let i = 0; i < sampleSize; i++) {
      const row = cleanData[i];

      columns.forEach((col, idx) => {
        const value = row[col];

        if (!value || value === '') {
          columnScores[idx].emptyValues++;
          return;
        }

        // Check if it's numeric (likely quantity)
        if (!isNaN(parseFloat(value)) && isFinite(value)) {
          columnScores[idx].numericValues++;
          return;
        }

        // Try to match against dictionary
        const storeMatch = findStoreInDictionary(value);
        const itemMatch = findItemInDictionary(value);

        if (storeMatch) {
          columnScores[idx].storeMatches++;
        }
        if (itemMatch) {
          columnScores[idx].itemMatches++;
        }
      });
    }

    // Determine columns based on scores
    // For store column: prefer columns where numeric values look like store IDs (100-199 range)
    let storeCol = null;
    let bestStoreScore = -1;
    
    columnScores.forEach(col => {
      if (col.storeMatches > 0) {
        // Has dictionary matches - use this
        storeCol = col.name;
        return;
      }
      
      // Check if numeric values look like store IDs
      if (col.numericValues > 0) {
        let storeIdCount = 0;
        for (let i = 0; i < sampleSize; i++) {
          const value = parseFloat(cleanData[i][col.name]);
          if (!isNaN(value) && value >= 100 && value <= 199) {
            storeIdCount++;
          }
        }
        if (storeIdCount > bestStoreScore) {
          bestStoreScore = storeIdCount;
          storeCol = col.name;
        }
      }
    });
    
    // Fallback if no store column found
    if (!storeCol) {
      storeCol = columnScores.reduce((best, curr) =>
        curr.storeMatches > best.storeMatches ? curr : best
      ).name;
    }

    const itemCol = columnScores.reduce((best, curr) =>
      curr.itemMatches > best.itemMatches ? curr : best
    ).name;

    // For quantity column, exclude columns that match stores or items
    // Prefer small numeric values (quantities are typically 1-100, not 101-199)
    const quantityCol = columnScores.reduce((best, curr) => {
      // Skip if this is the store or item column
      if (curr.name === storeCol || curr.name === itemCol) return best;
      
      // Check if values look like store IDs (typically 101-199 range) - skip if so
      let looksLikeStoreID = false;
      if (curr.numericValues > 0) {
        // Sample some values to see if they're in the store ID range
        for (let i = 0; i < sampleSize; i++) {
          const value = parseFloat(cleanData[i][curr.name]);
          if (!isNaN(value) && value >= 100 && value <= 199) {
            looksLikeStoreID = true;
            break;
          }
        }
      }
      
      // Prefer columns with more numeric values, but skip if looks like store ID
      if (looksLikeStoreID) return best;
      
      return curr.numericValues > best.numericValues ? curr : best;
    }, columnScores.find(c => c.name !== storeCol && c.name !== itemCol) || columnScores[0]).name;

    // Log detection results
    console.log('Dictionary-based column detection:');
    columnScores.forEach(score => {
      console.log(`  ${score.name}: stores=${score.storeMatches}, items=${score.itemMatches}, numeric=${score.numericValues}`);
    });

    return { storeCol, itemCol, quantityCol };
  }

  // Try dictionary-based detection first
  let detectedCols = detectColumnsUsingDictionary(data, columns);
  let storeCol = detectedCols.storeCol;
  let itemCol = detectedCols.itemCol;
  let quantityCol = detectedCols.quantityCol;

  console.log(`Initial detection: Store="${storeCol}", Item="${itemCol}", Quantity="${quantityCol}"`);
  
  // Show sample data from each detected column
  console.log('Sample data from detected columns:');
  const sampleData = data.slice(0, 3);
  console.log('  Store column samples:', sampleData.map(row => row[storeCol]));
  console.log('  Item column samples:', sampleData.map(row => row[itemCol]));
  console.log('  Quantity column samples:', sampleData.map(row => row[quantityCol]));

  // Fallback to header-based detection if dictionary detection fails
  if (storeCol === itemCol || !storeCol || !itemCol) {
    console.log('Dictionary detection inconclusive, falling back to header-based detection');

    storeCol = columns.find(col =>
      /store\s*name|shop\s*name/i.test(col)
    ) || columns.find(col =>
      /^loc\s*name$/i.test(col)
    ) || columns.find(col =>
      /location\s*code/i.test(col)
    ) || columns.find(col =>
      /store\s*code/i.test(col)
    ) || columns.find(col =>
      /store|shop|location/i.test(col)
    ) || columns.find(col =>
      /^loc$/i.test(col)
    ) || columns[0];

    itemCol = columns.find(col =>
      /^item$/i.test(col)
    ) || columns.find(col =>
      /item\s*no/i.test(col)
    ) || columns.find(col =>
      /item\s*number/i.test(col)
    ) || columns.find(col =>
      /^product$/i.test(col)
    ) || columns.find(col =>
      /^sku$/i.test(col)
    ) || columns.find(col =>
      /item|description|style|number/i.test(col) && !/store|loc|location|rank|qty|quantity/i.test(col)
    ) || columns[1];

    // Find quantity column - explicitly exclude store ID/number columns
    quantityCol = columns.find(col =>
      /^qty$/i.test(col)
    ) || columns.find(col =>
      /quantity|amount|allocation|units?/i.test(col)
    ) || columns.find(col =>
      /maximum\s*inventory/i.test(col)
    ) || columns.find(col =>
      /max\s*inv/i.test(col)
    ) || columns.find(col =>
      /reorder\s*point/i.test(col)
    ) || columns.find(col => {
      // Exclude columns that look like store identifiers
      const colLower = col.toLowerCase();
      return !/store.*id|store.*num|store.*#|loc.*id|loc.*num|location.*code/i.test(col) && 
             col !== storeCol && 
             col !== itemCol;
    }) || columns[2];
  }

  console.log(`Final column detection: Store="${storeCol}", Item="${itemCol}", Quantity="${quantityCol}"`);

  // Filter out header rows from actual data processing
  const cleanedData = filterHeaderRows(data, columns);
  console.log(`Processing ${cleanedData.length} data rows (filtered ${data.length - cleanedData.length} header rows)`);

  // Organize data by store
  organizedData = { byStore: {}, byItem: {} };
  let skippedRowsCount = 0;

  cleanedData.forEach((row, index) => {
    const storeInput = row[storeCol] || 'Unknown Store';
    const itemInput = row[itemCol] || 'Unknown Item';
    const quantity = parseFloat(row[quantityCol]) || 0;

    // Skip rows with zero or no quantity
    if (quantity <= 0) {
      skippedRowsCount++;
      return;
    }

    // Smart matching with dictionary
    const itemMatch = findItemInDictionary(itemInput);
    const storeMatch = findStoreInDictionary(storeInput);

    // Use matched values or original input
    const item = itemMatch ? itemMatch.item.number : itemInput;
    const store = storeMatch ? storeMatch.store.name : storeInput;

    // Track matching statistics
    if (itemMatch) {
      matchingStats.itemsMatched++;
      if (itemMatch.confidence !== 'exact') {
        matchingStats.warnings.push({
          row: index + 1,
          type: 'item-fuzzy',
          input: itemInput,
          matched: item,
          matchType: itemMatch.matchType,
          confidence: itemMatch.confidence
        });
      }
    } else {
      matchingStats.itemsUnmatched++;
      matchingStats.unmatchedItems.add(itemInput);
      matchingStats.warnings.push({
        row: index + 1,
        type: 'item-unmatched',
        input: itemInput,
        message: 'Item not found in dictionary'
      });
    }

    if (storeMatch) {
      matchingStats.storesMatched++;
      matchingStats.matchedStoresSet.add(storeInput); // Track unique matched store
      if (storeMatch.confidence !== 'exact') {
        matchingStats.warnings.push({
          row: index + 1,
          type: 'store-fuzzy',
          input: storeInput,
          matched: store,
          matchType: storeMatch.matchType,
          confidence: storeMatch.confidence
        });
      }
    } else {
      matchingStats.storesUnmatched++;
      matchingStats.unmatchedStores.add(storeInput);
      matchingStats.unmatchedStoresSet.add(storeInput); // Track unique unmatched store
      matchingStats.warnings.push({
        row: index + 1,
        type: 'store-unmatched',
        input: storeInput,
        message: 'Store not found in dictionary'
      });
    }

    // Get enriched data from dictionaries
    const itemInfo = formatItemDisplay(item);
    const storeInfo = formatStoreDisplay(store);

    // Add matching info to data
    itemInfo.matched = !!itemMatch;
    itemInfo.matchConfidence = itemMatch ? itemMatch.confidence : 'none';
    itemInfo.originalInput = itemInput;

    storeInfo.matched = !!storeMatch;
    storeInfo.matchConfidence = storeMatch ? storeMatch.confidence : 'none';
    storeInfo.originalInput = storeInput;

    // Organize by store
    if (!organizedData.byStore[store]) {
      organizedData.byStore[store] = [];
    }
    organizedData.byStore[store].push({
      item,
      itemInfo,
      quantity,
      rawData: row
    });

    // Organize by item
    if (!organizedData.byItem[item]) {
      organizedData.byItem[item] = [];
    }
    organizedData.byItem[item].push({
      store,
      storeInfo,
      quantity,
      rawData: row
    });
  });

  // Log matching results
  console.log('Matching Statistics:');
  console.log(`  Items: ${matchingStats.itemsMatched} matched, ${matchingStats.itemsUnmatched} unmatched`);
  console.log(`  Stores: ${matchingStats.storesMatched} matched, ${matchingStats.storesUnmatched} unmatched`);
  console.log(`  Skipped rows: ${skippedRowsCount} (zero or no quantity)`);
  console.log(`  Total warnings: ${matchingStats.warnings.length}`);

  if (matchingStats.unmatchedItems.size > 0) {
    console.warn('Unmatched items:', Array.from(matchingStats.unmatchedItems));
  }
  if (matchingStats.unmatchedStores.size > 0) {
    console.warn('Unmatched stores:', Array.from(matchingStats.unmatchedStores));
  }

  updateLoadingProgress('organizing');

  // Delay organizing steps to show the progress
  setTimeout(() => {
    updateStoreFilter();
    updateStats();
    displayStoreSelectionList();
    displayData();

    // Expand all stores by default to show items - use setTimeout to ensure DOM is ready
    setTimeout(() => {
      expandAllStores();
      console.log('✅ Stores expanded, checking collapsed state...');
      const collapsedCards = document.querySelectorAll('.store-card.collapsed');
      console.log(`Found ${collapsedCards.length} collapsed cards after expand`);
    }, 100);

    // Save initial state to history
    historyManager.clear();
    historyManager.pushState('Initial data load');

    // Show sticky undo/redo buttons
    if (stickyActions) {
      stickyActions.classList.add('visible');
    }

    // Auto-save to archive
    autoSaveArchive();
  }, 400);
}

// Auto-save current allocation to archive
async function autoSaveArchive() {
  try {
    const filename = filePathDisplay.value.split(/[\\/]/).pop();
    const result = await window.archiveManager.saveArchive({
      organizedData,
      excludedStores,
      redistributedItems
    }, filename);

    if (result.success) {
      console.log('✓ Auto-saved to archive:', result.archiveName);
    }
  } catch (error) {
    console.error('Auto-save to archive failed:', error);
  }
}

// Display store selection checkboxes
function displayStoreSelectionList() {
  const stores = sortStoresByNumber(Object.keys(organizedData.byStore));

  if (stores.length === 0) {
    storeSelectionSection.style.display = 'none';
    return;
  }

  storeSelectionSection.style.display = 'block';
  
  // Show view toolbar and filter bar when data is loaded
  const viewToolbar = document.getElementById('viewToolbar');
  const filterBar = document.getElementById('filterBar');
  if (viewToolbar) viewToolbar.style.display = 'flex';
  if (filterBar) filterBar.style.display = 'block';

  let html = '';
  stores.forEach(store => {
    const storeInfo = formatStoreDisplay(store);
    const items = organizedData.byStore[store];
    const totalQty = items.reduce((sum, item) => sum + item.quantity, 0);
    const isChecked = !excludedStores.has(store);
    const rankClass = storeInfo.rank ? `rank-${storeInfo.rank.toLowerCase()}` : '';
    const rankBadgeHtml = storeInfo.rank ? '<span class="rank-badge ' + rankClass + '">' + storeInfo.rank + '</span>' : '';

    html += `
      <div class="store-checkbox-item">
        <label class="store-checkbox-label">
          <input type="checkbox" class="store-checkbox" data-store="${store}" ${isChecked ? 'checked' : ''}>
          <div class="store-checkbox-info">
            <div class="store-checkbox-name">
              ${storeInfo.name}
              ${rankBadgeHtml}
            </div>
            <div class="store-checkbox-stats">
              ${items.length} items · ${totalQty.toLocaleString()} units
            </div>
          </div>
        </label>
      </div>
    `;
  });

  storeCheckboxList.innerHTML = html;

  // Add live update event listeners to all checkboxes
  attachStoreCheckboxListeners();
}

// Attach event listeners to checkboxes for live updates
function attachStoreCheckboxListeners() {
  const checkboxes = document.querySelectorAll('.store-checkbox');
  checkboxes.forEach(cb => {
    cb.addEventListener('change', handleStoreCheckboxChange);
  });
}

// Handle individual checkbox change with live update
function handleStoreCheckboxChange(event) {
  const checkbox = event.target;
  const store = checkbox.dataset.store;

  if (checkbox.checked) {
    // Include the store
    excludedStores.delete(store);
  } else {
    // Exclude the store
    excludedStores.add(store);
  }

  // Apply changes immediately
  applyStoreSelectionLive();
}

// Select all stores
function selectAllStores() {
  const checkboxes = document.querySelectorAll('.store-checkbox');
  checkboxes.forEach(cb => cb.checked = true);
  excludedStores.clear();
  applyStoreSelectionLive();
}

// Deselect all stores
function deselectAllStores() {
  const checkboxes = document.querySelectorAll('.store-checkbox');
  checkboxes.forEach(cb => cb.checked = false);

  // Add all stores to excluded set
  Object.keys(organizedData.byStore).forEach(store => {
    excludedStores.add(store);
  });

  applyStoreSelectionLive();
}

// Apply store selection (for manual "Apply Selection" button - kept for backwards compatibility)
function applyStoreSelection() {
  const checkboxes = document.querySelectorAll('.store-checkbox');
  excludedStores.clear();

  checkboxes.forEach(cb => {
    if (!cb.checked) {
      excludedStores.add(cb.dataset.store);
    }
  });

  // Separate included and excluded data
  separateDataByStoreSelection();
  updateStats();
  displayData();
  displayExcludedData();
}

// Live update when stores are checked/unchecked
function applyStoreSelectionLive() {
  // Separate included and excluded data
  separateDataByStoreSelection();
  updateStats();
  displayData();
  displayExcludedData();

  // Save state to history
  const numExcluded = excludedStores.size;
  historyManager.pushState(
    numExcluded > 0
      ? `Excluded ${numExcluded} store${numExcluded > 1 ? 's' : ''}`
      : 'Included all stores'
  );
}

// Separate data into included and excluded based on store selection
function separateDataByStoreSelection() {
  includedData = { byStore: {}, byItem: {} };
  excludedData = { byStore: {}, byItem: {} };

  // Separate by store
  Object.keys(organizedData.byStore).forEach(store => {
    const items = organizedData.byStore[store];

    if (excludedStores.has(store)) {
      excludedData.byStore[store] = items;
    } else {
      includedData.byStore[store] = items;
    }
  });

  // Rebuild byItem for included stores only
  Object.keys(organizedData.byItem).forEach(item => {
    const stores = organizedData.byItem[item];

    stores.forEach(storeData => {
      if (excludedStores.has(storeData.store)) {
        // Add to excluded
        if (!excludedData.byItem[item]) {
          excludedData.byItem[item] = [];
        }
        excludedData.byItem[item].push(storeData);
      } else {
        // Add to included
        if (!includedData.byItem[item]) {
          includedData.byItem[item] = [];
        }
        includedData.byItem[item].push(storeData);
      }
    });
  });
}

function updateStoreFilter() {
  storeFilter.innerHTML = '<option value="">All Stores</option>';
  const stores = sortStoresByNumber(Object.keys(organizedData.byStore));
  stores.forEach(store => {
    const option = document.createElement('option');
    option.value = store;
    option.textContent = store;
    storeFilter.appendChild(option);
  });
}

function updateStats() {
  // Use includedData if stores are excluded, otherwise use organizedData
  const dataSource = excludedStores.size > 0 ? includedData : organizedData;

  const stores = Object.keys(dataSource.byStore);
  const items = Object.keys(dataSource.byItem);

  // Calculate total quantity from organized data (more accurate)
  let totalQty = 0;
  Object.values(dataSource.byStore).forEach(itemsList => {
    itemsList.forEach(item => {
      totalQty += item.quantity;
    });
  });

  totalStoresEl.textContent = stores.length;
  totalItemsEl.textContent = items.length;
  totalQuantityEl.textContent = totalQty.toLocaleString();

  // Show stats container when data is loaded
  const statsContainer = document.getElementById('statsContainer');
  if (statsContainer && stores.length > 0) {
    statsContainer.style.display = 'grid';
  }
}

function showFileInfo(filePath, rowCount) {
  const fileName = filePath.split(/[\\/]/).pop();

  let html = `
    <span class="info-badge success">✓ Loaded: ${fileName}</span>
    <span class="info-badge info">${rowCount} rows</span>
  `;

  // Add matching statistics
  if (matchingStats.itemsMatched || matchingStats.itemsUnmatched) {
    const itemMatchRate = Math.round((matchingStats.itemsMatched / (matchingStats.itemsMatched + matchingStats.itemsUnmatched)) * 100);
    const itemBadgeClass = itemMatchRate === 100 ? 'success' : itemMatchRate > 80 ? 'warning' : 'error';
    html += `<span class="info-badge ${itemBadgeClass}">Items: ${matchingStats.itemsMatched}/${matchingStats.itemsMatched + matchingStats.itemsUnmatched} matched (${itemMatchRate}%)</span>`;
  }

  if (matchingStats.matchedStoresSet.size > 0 || matchingStats.unmatchedStoresSet.size > 0) {
    const uniqueMatchedStores = matchingStats.matchedStoresSet.size;
    const uniqueUnmatchedStores = matchingStats.unmatchedStoresSet.size;
    const totalUniqueStores = uniqueMatchedStores + uniqueUnmatchedStores;
    const storeMatchRate = Math.round((uniqueMatchedStores / totalUniqueStores) * 100);
    const storeBadgeClass = storeMatchRate === 100 ? 'success' : storeMatchRate > 80 ? 'warning' : 'error';
    html += `<span class="info-badge ${storeBadgeClass}">Stores: ${uniqueMatchedStores}/${totalUniqueStores} matched (${storeMatchRate}%)</span>`;
  }

  // Add warnings button if there are warnings
  if (matchingStats.warnings.length > 0) {
    html += `<button class="info-badge warning clickable" onclick="showWarnings()" style="cursor: pointer;">⚠️ ${matchingStats.warnings.length} warnings</button>`;
  }

  fileInfo.innerHTML = html;
  fileInfo.style.display = 'block';

  // Show clear button and data view when file is loaded
  clearBtn.style.display = 'inline-block';

  // Hide welcome screen and show data view
  const welcomeScreen = document.getElementById('welcomeScreen');
  const dataView = document.getElementById('dataView');
  if (welcomeScreen) {
    welcomeScreen.style.display = 'none';
  }
  if (dataView) {
    dataView.style.display = 'block';
  }
}

// Show warnings dialog
window.showWarnings = function() {
  let warningsHTML = '<div class="warnings-dialog">';
  warningsHTML += '<div class="warnings-header">';
  warningsHTML += '<h2>Data Matching Warnings</h2>';
  warningsHTML += '<button onclick="closeWarnings()" class="close-btn">✕</button>';
  warningsHTML += '</div>';
  warningsHTML += '<div class="warnings-content">';

  // Group warnings by type
  const unmatchedItems = matchingStats.warnings.filter(w => w.type === 'item-unmatched');
  const unmatchedStores = matchingStats.warnings.filter(w => w.type === 'store-unmatched');
  const fuzzyItems = matchingStats.warnings.filter(w => w.type === 'item-fuzzy');
  const fuzzyStores = matchingStats.warnings.filter(w => w.type === 'store-fuzzy');

  if (unmatchedItems.length > 0) {
    warningsHTML += '<div class="warning-section">';
    warningsHTML += '<h3>❌ Unmatched Items</h3>';
    warningsHTML += '<p>These items were not found in the dictionary:</p>';
    warningsHTML += '<ul>';
    const uniqueItems = [...new Set(unmatchedItems.map(w => w.input))];
    uniqueItems.forEach(item => {
      const count = unmatchedItems.filter(w => w.input === item).length;
      const escapedItem = item.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
      warningsHTML += `<li><strong>${item}</strong> (${count} occurrence${count > 1 ? 's' : ''}) <button class="btn-small btn-secondary" onclick="showAddItemDialog('${escapedItem}')">➕ Add to Dictionary</button></li>`;
    });
    warningsHTML += '</ul>';
    warningsHTML += '</div>';
  }

  if (unmatchedStores.length > 0) {
    warningsHTML += '<div class="warning-section">';
    warningsHTML += '<h3>❌ Unmatched Stores</h3>';
    warningsHTML += '<p>These stores were not found in the dictionary:</p>';
    warningsHTML += '<ul>';
    const uniqueStores = [...new Set(unmatchedStores.map(w => w.input))];
    uniqueStores.forEach(store => {
      const count = unmatchedStores.filter(w => w.input === store).length;
      warningsHTML += `<li><strong>${store}</strong> (${count} occurrence${count > 1 ? 's' : ''})</li>`;
    });
    warningsHTML += '</ul>';
    warningsHTML += '</div>';
  }

  if (fuzzyItems.length > 0) {
    warningsHTML += '<div class="warning-section">';
    warningsHTML += '<h3>⚠️ Fuzzy Item Matches</h3>';
    warningsHTML += '<p>These items were matched with partial/fuzzy logic:</p>';
    warningsHTML += '<ul>';
    fuzzyItems.slice(0, 20).forEach(w => {
      warningsHTML += `<li>Row ${w.row}: <strong>${w.input}</strong> → ${w.matched} (${w.matchType})</li>`;
    });
    if (fuzzyItems.length > 20) {
      warningsHTML += `<li><em>...and ${fuzzyItems.length - 20} more</em></li>`;
    }
    warningsHTML += '</ul>';
    warningsHTML += '</div>';
  }

  if (fuzzyStores.length > 0) {
    warningsHTML += '<div class="warning-section">';
    warningsHTML += '<h3>⚠️ Fuzzy Store Matches</h3>';
    warningsHTML += '<p>These stores were matched with partial/fuzzy logic:</p>';
    warningsHTML += '<ul>';
    fuzzyStores.slice(0, 20).forEach(w => {
      warningsHTML += `<li>Row ${w.row}: <strong>${w.input}</strong> → ${w.matched} (${w.matchType})</li>`;
    });
    if (fuzzyStores.length > 20) {
      warningsHTML += `<li><em>...and ${fuzzyStores.length - 20} more</em></li>`;
    }
    warningsHTML += '</ul>';
    warningsHTML += '</div>';
  }

  warningsHTML += '</div>';
  warningsHTML += '</div>';

  // Create overlay
  const overlay = document.createElement('div');
  overlay.className = 'warnings-overlay';
  overlay.innerHTML = warningsHTML;
  document.body.appendChild(overlay);
};

window.closeWarnings = function() {
  const overlay = document.querySelector('.warnings-overlay');
  if (overlay) {
    overlay.remove();
  }
};

// Show Add Item to Dictionary dialog
window.showAddItemDialog = function(itemCode) {
  // If itemCode is undefined or empty, make the field editable
  const isNewItem = !itemCode || itemCode === 'undefined' || itemCode.trim() === '';
  const itemValue = isNewItem ? '' : itemCode;
  const readonlyAttr = isNewItem ? '' : 'readonly';
  
  let html = '<div class="add-item-overlay">';
  html += '<div class="add-item-dialog">';
  html += '<div class="add-item-header">';
  html += '<h2>➕ Add Item to Dictionary</h2>';
  html += '<button onclick="closeAddItemDialog()" class="close-btn">✕</button>';
  html += '</div>';
  html += '<div class="add-item-content">';
  html += '<p>Add this item to your dictionary:</p>';
  html += '<form id="addItemForm" onsubmit="submitAddItem(event)">';
  html += '<div class="form-group">';
  html += '<label for="itemNumber">Item Number:</label>';
  html += '<input type="text" id="itemNumber" value="' + itemValue + '" ' + readonlyAttr + ' required placeholder="Enter item number">';
  html += '</div>';
  html += '<div class="form-group">';
  html += '<label for="itemDesc">Description:</label>';
  html += '<input type="text" id="itemDesc" placeholder="Enter item description" required>';
  html += '</div>';
  html += '<div class="form-group">';
  html += '<label for="itemSku">SKU (optional, comma-separated):</label>';
  html += '<input type="text" id="itemSku" placeholder="Enter SKU or barcode">';
  html += '</div>';
  html += '<div class="form-actions">';
  html += '<button type="button" onclick="closeAddItemDialog()" class="btn btn-secondary">Cancel</button>';
  html += '<button type="submit" class="btn btn-primary">Add to Dictionary</button>';
  html += '</div>';
  html += '</form>';
  html += '<div class="form-note">';
  html += '<p><strong>Note:</strong> Changes are saved automatically to the dictionary file.</p>';
  html += '</div>';
  html += '</div>';
  html += '</div>';
  html += '</div>';

  const overlay = document.createElement('div');
  overlay.innerHTML = html;
  document.body.appendChild(overlay);

  // Clear editing flags and focus the right field
  setTimeout(() => {
    const numberField = document.getElementById('itemNumber');
    const descField = document.getElementById('itemDesc');
    if (numberField) {
      delete numberField.dataset.editing;
      delete numberField.dataset.originalNumber;
      // Focus on item number if it's a new item, otherwise description
      if (isNewItem && numberField) {
        numberField.focus();
      } else if (descField) {
        descField.focus();
      }
    }
  }, 10);
};

// Close Add Item dialog
window.closeAddItemDialog = function() {
  const overlay = document.querySelector('.add-item-overlay');
  if (overlay) {
    overlay.remove();
  }
};

// Submit Add Item form
window.submitAddItem = function(event) {
  event.preventDefault();
  
  const numberField = document.getElementById('itemNumber');
  const itemNumber = numberField.value.trim();
  const itemDesc = document.getElementById('itemDesc').value.trim();
  const itemSku = document.getElementById('itemSku').value.trim();
  
  if (!itemNumber || !itemDesc) {
    showErrorDialog('Item number and description are required', 'Validation Error');
    return;
  }

  const isEditing = numberField.dataset.editing === 'true';
  const originalNumber = numberField.dataset.originalNumber;
  
  // Create item object
  const itemObj = {
    number: itemNumber,
    desc: itemDesc,
    sku: itemSku ? itemSku.split(',').map(s => s.trim()).filter(s => s) : []
  };
  
  if (isEditing && originalNumber) {
    // Edit mode - update existing item
    const index = window.DICT.items.findIndex(i => i.number === originalNumber);
    if (index !== -1) {
      window.DICT.items[index] = itemObj;
      console.log(`Updated item in dictionary: ${itemNumber} - ${itemDesc}`);
      
      // Reinitialize dictionaries
      initializeDictionaries();
      
      // Auto-save to file
      autoSaveDictionary();
      
      // Close dialog
      closeAddItemDialog();
      
      // Refresh settings if open
      const settingsModal = document.getElementById('settingsModal');
      if (settingsModal && settingsModal.style.display === 'flex') {
        renderSettingsItems();
        updateDictionaryStats();
      }
      
      showErrorDialog(`Item "${itemNumber}" has been updated and saved to file.`, 'Item Updated', 'success');
      return;
    }
  }
  
  // Add mode - add new item
  if (!window.DICT.items) {
    window.DICT.items = [];
  }
  
  // Check for duplicates
  if (window.DICT.items.some(i => i.number === itemNumber)) {
    showErrorDialog(`Item "${itemNumber}" already exists in the dictionary.`, 'Duplicate Item');
    return;
  }
  
  window.DICT.items.push(itemObj);
  
  // Reinitialize dictionaries
  initializeDictionaries();
  
  console.log(`Added item to dictionary: ${itemNumber} - ${itemDesc}`);
  
  // Auto-save to file
  autoSaveDictionary();
  
  // Close dialog
  closeAddItemDialog();
  
  // Refresh settings if open
  const settingsModal = document.getElementById('settingsModal');
  if (settingsModal && settingsModal.style.display === 'flex') {
    renderSettingsItems();
    updateDictionaryStats();
  }
  
  // Show success message
  showErrorDialog(`Item "${itemNumber}" has been added and saved to file.`, 'Item Added', 'success');
  
  // Suggest reloading the file if we're not in settings
  if (!settingsModal || settingsModal.style.display === 'none') {
    if (confirm('Would you like to reload your data file to rematch items with the updated dictionary?')) {
      // Reload the last file
      if (filePathDisplay.value) {
        loadFile(filePathDisplay.value);
      }
    }
  }
};

function switchView(view) {
  currentView = view;

  if (view === 'store') {
    viewByStoreBtn.classList.add('active');
    viewByItemBtn.classList.remove('active');
  } else {
    viewByItemBtn.classList.add('active');
    viewByStoreBtn.classList.remove('active');
  }

  displayData();
}

/**
 * Apply current filter and search values to the data display
 * @returns {void}
 * @description Triggers displayData() which filters based on storeFilter and searchBox values
 */
function applyFilters() {
  displayData();
}

function clearFilters() {
  storeFilter.value = '';
  searchBox.value = '';
  displayData();
}

function clearAll() {
  // Reset all data
  currentData = [];
  organizedData = { byStore: {}, byItem: {} };
  excludedStores.clear();
  includedData = { byStore: {}, byItem: {} };
  excludedData = { byStore: {}, byItem: {} };
  redistributedItems.clear();

  // Reset matching stats
  matchingStats = { matched: 0, unmatched: 0, warnings: [] };

  // Clear UI elements
  filePathDisplay.value = '';
  fileInfo.innerHTML = '';
  fileInfo.style.display = 'none';
  storeFilter.innerHTML = '<option value="">All Stores</option>';
  searchBox.value = '';

  // Reset stats
  totalStoresEl.textContent = '0';
  totalItemsEl.textContent = '0';
  totalQuantityEl.textContent = '0';

  // Hide sections
  storeSelectionSection.style.display = 'none';
  excludedSection.style.display = 'none';

  // Hide stats container
  const statsContainer = document.getElementById('statsContainer');
  if (statsContainer) {
    statsContainer.style.display = 'none';
  }

  // Show welcome screen and hide data view
  const welcomeScreen = document.getElementById('welcomeScreen');
  const dataView = document.getElementById('dataView');
  if (welcomeScreen) {
    welcomeScreen.style.display = 'flex';
  }
  if (dataView) {
    dataView.style.display = 'none';
  }

  // Hide toolbars
  const viewToolbar = document.getElementById('viewToolbar');
  const filterBar = document.getElementById('filterBar');
  if (viewToolbar) viewToolbar.style.display = 'none';
  if (filterBar) filterBar.style.display = 'none';

  // Reset view
  currentView = 'store';
  viewByStoreBtn.classList.add('active');
  viewByItemBtn.classList.remove('active');

  // Hide clear button
  clearBtn.style.display = 'none';

  // Hide sticky undo/redo buttons
  if (stickyActions) {
    stickyActions.classList.remove('visible');
  }

  // Clear history
  historyManager.clear();

  // Display empty state
  displayData();
}

function displayData() {
  if (currentData.length === 0) {
    const emptyStateHtml = `
      <div class="empty-state">
        <div class="empty-icon">📊</div>
        <h3>No Data Loaded</h3>
        <p>Select an Excel or CSV file to get started</p>
      </div>
    `;

    if (allocationsContent) {
      allocationsContent.innerHTML = emptyStateHtml;
    } else {
      dataDisplay.innerHTML = emptyStateHtml;
    }
    return;
  }

  const filterStore = storeFilter.value;
  const searchTerm = searchBox.value.toLowerCase();

  let html = '';

  if (currentView === 'store') {
    html = generateStoreView(filterStore, searchTerm);
  } else {
    html = generateItemView(filterStore, searchTerm);
  }

  console.log('📝 Inserting HTML into DOM, length:', html.length);
  console.log('📍 Target element:', allocationsContent ? 'allocationsContent' : 'dataDisplay');

  if (allocationsContent) {
    allocationsContent.innerHTML = html;
    console.log('✅ HTML inserted into allocationsContent');
  } else {
    dataDisplay.innerHTML = html;
    console.log('✅ HTML inserted into dataDisplay');
  }

  // Show the data display container by adding 'active' class
  if (dataDisplay) {
    dataDisplay.classList.add('active');
    console.log('✅ Added "active" class to #dataDisplay');
  }

  // Verify insertion
  setTimeout(() => {
    const storeCards = document.querySelectorAll('.store-card');
    console.log(`🔍 Found ${storeCards.length} .store-card elements in DOM`);

    // Check container visibility
    const container = allocationsContent || dataDisplay;
    if (container) {
      const containerStyle = window.getComputedStyle(container);
      console.log('📦 Container (#allocationsContent) styles:', {
        display: containerStyle.display,
        visibility: containerStyle.visibility,
        opacity: containerStyle.opacity,
        height: containerStyle.height,
        overflow: containerStyle.overflow,
        position: containerStyle.position,
        width: containerStyle.width
      });
      console.log('📦 Container dimensions:', {
        offsetWidth: container.offsetWidth,
        offsetHeight: container.offsetHeight,
        scrollHeight: container.scrollHeight
      });

      // Check parent containers
      let parent = container.parentElement;
      let level = 1;
      while (parent && level <= 3) {
        const parentStyle = window.getComputedStyle(parent);
        console.log(`📦 Parent ${level} (${parent.className || parent.tagName}):`, {
          display: parentStyle.display,
          width: parentStyle.width,
          height: parentStyle.height,
          offsetWidth: parent.offsetWidth,
          offsetHeight: parent.offsetHeight
        });
        parent = parent.parentElement;
        level++;
      }
    }

    if (storeCards.length > 0) {
      console.log('First store card:', storeCards[0]);
      const firstStoreContent = storeCards[0].querySelector('.store-content');
      if (firstStoreContent) {
        const computedStyle = window.getComputedStyle(firstStoreContent);
        console.log('First store .store-content styles:', {
          display: computedStyle.display,
          maxHeight: computedStyle.maxHeight,
          opacity: computedStyle.opacity,
          height: computedStyle.height
        });
      }

      // Check if first store card is visible
      const rect = storeCards[0].getBoundingClientRect();
      console.log('First store card position:', {
        top: rect.top,
        left: rect.left,
        width: rect.width,
        height: rect.height,
        visible: rect.width > 0 && rect.height > 0
      });
    }
  }, 50);
}

function generateStoreView(filterStore, searchTerm) {
  let html = '<div class="store-cards">';

  // Use includedData if stores are excluded, otherwise use organizedData
  const dataSource = excludedStores.size > 0 ? includedData : organizedData;

  const stores = filterStore ? [filterStore] : sortStoresByNumber(Object.keys(dataSource.byStore));

  console.log('🔍 DEBUG generateStoreView:', {
    totalStores: stores.length,
    dataSource: excludedStores.size > 0 ? 'includedData' : 'organizedData',
    firstStore: stores[0],
    firstStoreItemCount: dataSource.byStore[stores[0]]?.length
  });

  stores.forEach(store => {
    const items = dataSource.byStore[store];
    const storeInfo = formatStoreDisplay(store);

    console.log(`📦 Store: ${storeInfo.name}, Items: ${items?.length || 0}`);

    // Apply search filter (search in item code, description, and SKUs)
    const filteredItems = items.filter(item => {
      if (!searchTerm) return true;
      const itemLower = item.item.toLowerCase();
      const descLower = item.itemInfo.description.toLowerCase();
      const skuMatch = item.itemInfo.skus.some(sku => sku.toLowerCase().includes(searchTerm));
      return itemLower.includes(searchTerm) || descLower.includes(searchTerm) || skuMatch;
    });

    if (filteredItems.length === 0 && searchTerm) return;

    const totalQty = filteredItems.reduce((sum, item) => sum + item.quantity, 0);

    // Determine rank badge color
    const rankClass = storeInfo.rank ? `rank-${storeInfo.rank.toLowerCase()}` : '';

    const storeId = `store-${store.replace(/[^a-zA-Z0-9]/g, '-')}`;

    const rankBadgeHtml = storeInfo.rank ? '<span class="rank-badge ' + rankClass + '" title="Store rank ' + storeInfo.rank + '">' + storeInfo.rank + '</span>' : '';
    
    // Escape store name for safe insertion into HTML
    const escapedStoreName = store.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
    
    html += `
      <div class="store-card" id="${storeId}" data-store="${store}" title="Click header to expand/collapse">
        <div class="store-header" onclick="toggleStoreCollapse('${escapedStoreName}')">
          <div class="store-title">
            <span class="collapse-icon" title="Click to expand/collapse" aria-label="Collapse toggle">▼</span>
            <h3>${storeInfo.name}</h3>
            ${rankBadgeHtml}
            <span class="store-total-badge" title="Total quantity for this store">${totalQty.toLocaleString()} units</span>
          </div>
          <div class="store-header-actions" onclick="event.stopPropagation();">
            <span class="badge" title="${filteredItems.length} items in this store">${filteredItems.length} items</span>
            <button class="btn-copy btn-view" onclick="openStoreModal('${escapedStoreName}')" title="View detailed information about this store" aria-label="View details">👁️ View Details</button>
            <button class="btn-copy btn-bc" onclick="copyStoreData('${escapedStoreName}')" title="Copy store data formatted for Business Central" aria-label="Copy for BC">📋 Copy for BC</button>
          </div>
        </div>
        <div class="store-content">
          <div class="items-list" data-store="${store}">
          ${filteredItems.length > 0 ? filteredItems.map(item => {
            const isRedistributed = redistributedItems.has(item.item);
            const percentage = totalQty > 0 ? ((item.quantity / totalQty) * 100).toFixed(1) : 0;
            const barWidth = Math.min(percentage, 100);
            const redistributedBadge = isRedistributed ? '<span class="redistributed-badge">Redistributed</span>' : '';
            const descriptionHtml = item.itemInfo.description ? '<div class="item-desc">' + item.itemInfo.description + '</div>' : '';
            const skuHtml = item.itemInfo.skus.length > 0 ? '<div class="item-sku">SKU: ' + item.itemInfo.skus[0] + '</div>' : '';
            return `
            <div class="item-row ${isRedistributed ? 'redistributed-item' : ''}" data-code="${item.itemInfo.code}" data-qty="${item.quantity}" data-desc="${item.itemInfo.description || ''}">
              <div class="item-info">
                <div class="item-code">
                  ${item.itemInfo.code}
                  ${redistributedBadge}
                </div>
                ${descriptionHtml}
                ${skuHtml}
                <div class="item-percentage-bar">
                  <div class="percentage-fill" style="width: ${barWidth}%" title="${percentage}% of store total"></div>
                </div>
              </div>
              <div class="item-quantity-section">
                <span class="item-quantity">${item.quantity.toLocaleString()}</span>
                <span class="item-percentage">${percentage}%</span>
              </div>
            </div>
          `;
          }).join('') : '<div class="empty-items">No items to display</div>'}
          </div>
        </div>
      </div>
    `;
  });

  html += '</div>';
  return html;
}

function generateItemView(filterStore, searchTerm) {
  let html = '<div class="item-cards">';

  // Use includedData if stores are excluded, otherwise use organizedData
  const dataSource = excludedStores.size > 0 ? includedData : organizedData;

  const items = Object.keys(dataSource.byItem).sort();

  items.forEach(item => {
    const itemInfo = formatItemDisplay(item);

    // Apply search filter
    if (searchTerm) {
      const itemLower = item.toLowerCase();
      const descLower = itemInfo.description.toLowerCase();
      const skuMatch = itemInfo.skus.some(sku => sku.toLowerCase().includes(searchTerm));
      if (!(itemLower.includes(searchTerm) || descLower.includes(searchTerm) || skuMatch)) {
        return;
      }
    }

    const stores = dataSource.byItem[item];

    // Apply store filter
    const filteredStores = filterStore
      ? stores.filter(s => s.store === filterStore)
      : stores;

    if (filteredStores.length === 0) return;

    const totalQty = filteredStores.reduce((sum, s) => sum + s.quantity, 0);
    const isRedistributed = redistributedItems.has(item);
    const redistributedBadge = isRedistributed ? '<span class="redistributed-badge">Redistributed</span>' : '';
    const descriptionHtml = itemInfo.description ? '<p class="item-description">' + itemInfo.description + '</p>' : '';
    const skuListHtml = itemInfo.skus.length > 0 ? '<p class="item-sku-list">SKUs: ' + itemInfo.skus.join(', ') + '</p>' : '';

    html += `
      <div class="item-card ${isRedistributed ? 'redistributed-item-card' : ''}">
        <div class="item-header">
          <div class="item-title">
            <h3>
              ${itemInfo.code}
              ${redistributedBadge}
            </h3>
            ${descriptionHtml}
            ${skuListHtml}
          </div>
          <span class="badge">${filteredStores.length} stores</span>
        </div>
        <div class="item-total">Total Quantity: <strong>${totalQty.toLocaleString()}</strong></div>
        <div class="stores-list">
          ${filteredStores.map(s => {
            const rankClass = s.storeInfo.rank ? `rank-${s.storeInfo.rank.toLowerCase()}` : '';
            const rankBadgeHtml = s.storeInfo.rank ? '<span class="rank-badge ' + rankClass + '">' + s.storeInfo.rank + '</span>' : '';
            return `
              <div class="store-row">
                <div class="store-name-container">
                  <span class="store-name">${s.storeInfo.name}</span>
                  ${rankBadgeHtml}
                </div>
                <span class="store-quantity">${s.quantity.toLocaleString()}</span>
              </div>
            `;
          }).join('')}
        </div>
      </div>
    `;
  });

  html += '</div>';
  return html;
}

// Display excluded stores data
function displayExcludedData() {
  if (excludedStores.size === 0) {
    excludedSection.style.display = 'none';
    return;
  }

  excludedSection.style.display = 'block';

  let html = '';
  let totalExcludedQty = 0;
  const excludedStoresArray = sortStoresByNumber(Array.from(excludedStores));

  // Calculate totals by item across excluded stores
  const itemTotals = {};
  Object.keys(excludedData.byItem).forEach(item => {
    const stores = excludedData.byItem[item];
    const totalQty = stores.reduce((sum, s) => sum + s.quantity, 0);
    itemTotals[item] = totalQty;
    totalExcludedQty += totalQty;
  });

  html += `<div class="excluded-summary">
    <div class="excluded-stat">
      <span class="excluded-label">Excluded Stores:</span>
      <span class="excluded-value">${excludedStores.size}</span>
    </div>
    <div class="excluded-stat">
      <span class="excluded-label">Total Styles:</span>
      <span class="excluded-value">${Object.keys(excludedData.byItem).length}</span>
    </div>
    <div class="excluded-stat">
      <span class="excluded-label">Total Quantity:</span>
      <span class="excluded-value">${totalExcludedQty.toLocaleString()}</span>
    </div>
  </div>`;

  html += '<div class="excluded-stores-list">';
  html += '<h3>Excluded Stores:</h3>';
  html += '<div class="excluded-stores-grid">';

  excludedStoresArray.forEach(store => {
    const storeInfo = formatStoreDisplay(store);
    const items = excludedData.byStore[store];
    const totalQty = items.reduce((sum, item) => sum + item.quantity, 0);
    const rankClass = storeInfo.rank ? `rank-${storeInfo.rank.toLowerCase()}` : '';
    const rankBadgeHtml = storeInfo.rank ? '<span class="rank-badge ' + rankClass + '">' + storeInfo.rank + '</span>' : '';

    html += `
      <div class="excluded-store-badge">
        <div class="excluded-store-name">
          ${storeInfo.name}
          ${rankBadgeHtml}
        </div>
        <div class="excluded-store-stats">${items.length} items · ${totalQty.toLocaleString()} units</div>
      </div>
    `;
  });

  html += '</div></div>';

  html += '<div class="excluded-items-list">';
  html += '<h3>Items to Redistribute:</h3>';
  html += '<div class="excluded-items-table">';

  // Sort items by total quantity descending
  const sortedItems = Object.keys(itemTotals).sort((a, b) => itemTotals[b] - itemTotals[a]);

  sortedItems.forEach(item => {
    const itemInfo = formatItemDisplay(item);
    const stores = excludedData.byItem[item];
    const totalQty = itemTotals[item];

    const descriptionHtml = itemInfo.description ? '<div class="excluded-item-desc">' + itemInfo.description + '</div>' : '';
    
    html += `
      <div class="excluded-item-row">
        <div class="excluded-item-info">
          <div class="excluded-item-code">${itemInfo.code}</div>
          ${descriptionHtml}
        </div>
        <div class="excluded-item-details">
          <span class="excluded-item-stores">${stores.length} excluded stores</span>
          <span class="excluded-item-qty">${totalQty.toLocaleString()} units</span>
        </div>
      </div>
    `;
  });

  html += '</div></div>';

  excludedDataDisplay.innerHTML = html;
}

// Export excluded data to CSV
function exportExcludedData() {
  if (excludedStores.size === 0) {
    showErrorDialog('No excluded stores to export', 'Nothing to Export');
    return;
  }

  const rows = [];
  rows.push(['Store', 'Item', 'Description', 'Quantity']);

  Object.keys(excludedData.byStore).forEach(store => {
    const items = excludedData.byStore[store];
    items.forEach(item => {
      rows.push([
        store,
        item.item,
        item.itemInfo.description || '',
        item.quantity
      ]);
    });
  });

  const csv = rows.map(row => row.map(cell => `"${cell}"`).join(',')).join('\n');
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `excluded_stores_${new Date().toISOString().split('T')[0]}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

// Redistribute excluded quantities equally among included stores
function redistributeEqually() {
  if (excludedStores.size === 0) {
    showErrorDialog('No excluded stores to redistribute', 'Cannot Redistribute');
    return;
  }

  if (Object.keys(includedData.byStore).length === 0) {
    showErrorDialog('No included stores available for redistribution', 'Cannot Redistribute');
    return;
  }

  const includedStoresArray = Object.keys(includedData.byStore);
  const numIncludedStores = includedStoresArray.length;

  // Create redistribution plan
  const redistributionPlan = {};

  Object.keys(excludedData.byItem).forEach(item => {
    const stores = excludedData.byItem[item];
    const totalQty = stores.reduce((sum, s) => sum + s.quantity, 0);
    const qtyPerStore = Math.floor(totalQty / numIncludedStores);
    const remainder = totalQty % numIncludedStores;

    redistributionPlan[item] = {
      totalQty,
      qtyPerStore,
      remainder,
      itemInfo: formatItemDisplay(item)
    };
  });

  // Display redistribution dialog
  showRedistributionDialog(redistributionPlan, 'equal');
}

// Redistribute by rank (A gets more, C gets less)
function redistributeByRank() {
  if (excludedStores.size === 0) {
    showErrorDialog('No excluded stores to redistribute', 'Cannot Redistribute');
    return;
  }

  if (Object.keys(includedData.byStore).length === 0) {
    showErrorDialog('No included stores available for redistribution', 'Cannot Redistribute');
    return;
  }

  // Calculate rank weights (A=3, B=2, C=1)
  const rankWeights = { 'A': 3, 'B': 2, 'C': 1 };
  let totalWeight = 0;
  const storeRanks = {};

  Object.keys(includedData.byStore).forEach(store => {
    const storeInfo = formatStoreDisplay(store);
    const rank = storeInfo.rank || 'C';
    const weight = rankWeights[rank] || 1;
    storeRanks[store] = { rank, weight };
    totalWeight += weight;
  });

  // Create redistribution plan
  const redistributionPlan = {};

  Object.keys(excludedData.byItem).forEach(item => {
    const stores = excludedData.byItem[item];
    const totalQty = stores.reduce((sum, s) => sum + s.quantity, 0);

    const allocations = {};
    let allocatedTotal = 0;

    Object.keys(includedData.byStore).forEach(store => {
      const { weight } = storeRanks[store];
      const allocation = Math.floor((totalQty * weight) / totalWeight);
      allocations[store] = allocation;
      allocatedTotal += allocation;
    });

    // Distribute remainder to highest rank stores
    const remainder = totalQty - allocatedTotal;

    redistributionPlan[item] = {
      totalQty,
      allocations,
      remainder,
      itemInfo: formatItemDisplay(item)
    };
  });

  showRedistributionDialog(redistributionPlan, 'rank');
}

// Show redistribution confirmation dialog
function showRedistributionDialog(plan, method) {
  // Store plan and method globally for the Apply button
  currentRedistributionPlan = plan;
  currentRedistributionMethod = method;
  
  let html = '<div class="redistribution-dialog">';
  html += '<div class="redistribution-header">';
  html += `<h2>Redistribution Plan - ${method === 'equal' ? 'Equal Distribution' : 'Rank-Based Distribution'}</h2>`;
  html += '<button onclick="closeRedistributionDialog()" class="close-btn">✕</button>';
  html += '</div>';
  html += '<div class="redistribution-content">';

  html += '<p>This will add the following quantities to the included stores:</p>';
  html += '<div class="redistribution-summary">';

  const includedStoresArray = sortStoresByNumber(Object.keys(includedData.byStore));

  if (method === 'equal') {
    html += '<table class="redistribution-table">';
    html += '<thead><tr><th>Item</th><th>Total Excluded Qty</th><th>Qty per Store</th><th>Remainder</th></tr></thead>';
    html += '<tbody>';

    Object.keys(plan).forEach(item => {
      const { totalQty, qtyPerStore, remainder, itemInfo } = plan[item];
      html += `<tr>
        <td>${itemInfo.displayText}</td>
        <td>${totalQty.toLocaleString()}</td>
        <td>${qtyPerStore.toLocaleString()} × ${includedStoresArray.length} stores</td>
        <td>${remainder}</td>
      </tr>`;
    });

    html += '</tbody></table>';
  } else {
    html += '<table class="redistribution-table">';
    html += '<thead><tr><th>Item</th><th>Store</th><th>Rank</th><th>Allocation</th></tr></thead>';
    html += '<tbody>';

    Object.keys(plan).forEach(item => {
      const { totalQty, allocations, itemInfo } = plan[item];

      html += `<tr class="item-total-row">
        <td colspan="3"><strong>${itemInfo.displayText}</strong> (Total: ${totalQty.toLocaleString()})</td>
        <td></td>
      </tr>`;

      includedStoresArray.forEach(store => {
        const storeInfo = formatStoreDisplay(store);
        const allocation = allocations[store] || 0;

        // Only show stores that are receiving items (allocation > 0)
        if (allocation === 0) return;

        const rankClass = storeInfo.rank ? `rank-${storeInfo.rank.toLowerCase()}` : '';
        const rankDisplay = storeInfo.rank || 'C';

        html += `<tr>
          <td></td>
          <td>${storeInfo.name}</td>
          <td><span class="rank-badge ${rankClass}">${rankDisplay}</span></td>
          <td>${allocation.toLocaleString()}</td>
        </tr>`;
      });
    });

    html += '</tbody></table>';
  }

  html += '</div>';

  html += '<div class="redistribution-actions">';
  html += '<button onclick="closeRedistributionDialog()" class="btn btn-secondary">Cancel</button>';
  html += '<button onclick="applyRedistribution()" class="btn btn-primary">Apply Redistribution</button>';
  html += '</div>';

  html += '</div>';
  html += '</div>';

  const overlay = document.createElement('div');
  overlay.className = 'redistribution-overlay';
  overlay.innerHTML = html;
  document.body.appendChild(overlay);
}

window.closeRedistributionDialog = function() {
  const overlay = document.querySelector('.redistribution-overlay');
  if (overlay) {
    overlay.remove();
  }
  // Clear the globals
  currentRedistributionPlan = null;
  currentRedistributionMethod = null;
};

window.applyRedistribution = function() {
  const plan = currentRedistributionPlan;
  const method = currentRedistributionMethod;
  
  if (!plan || !method) {
    console.error('No redistribution plan available');
    return;
  }
  
  const includedStoresArray = Object.keys(includedData.byStore);

  if (method === 'equal') {
    Object.keys(plan).forEach(item => {
      const { qtyPerStore, remainder } = plan[item];

      includedStoresArray.forEach((store, index) => {
        let allocation = qtyPerStore;
        // Add remainder to first stores
        if (index < remainder) {
          allocation += 1;
        }

        // Add to organizedData
        if (!organizedData.byStore[store]) {
          organizedData.byStore[store] = [];
        }

        const existingItem = organizedData.byStore[store].find(i => i.item === item);
        if (existingItem) {
          existingItem.quantity += allocation;
        } else {
          const itemInfo = formatItemDisplay(item);
          organizedData.byStore[store].push({
            item,
            itemInfo,
            quantity: allocation,
            rawData: { redistributed: true }
          });
        }

        // Update byItem view
        if (!organizedData.byItem[item]) {
          organizedData.byItem[item] = [];
        }

        const existingStore = organizedData.byItem[item].find(s => s.store === store);
        if (existingStore) {
          existingStore.quantity += allocation;
        } else {
          const storeInfo = formatStoreDisplay(store);
          organizedData.byItem[item].push({
            store,
            storeInfo,
            quantity: allocation,
            rawData: { redistributed: true }
          });
        }
      });
    });
  } else {
    // Rank-based redistribution
    Object.keys(plan).forEach(item => {
      const { allocations } = plan[item];

      Object.keys(allocations).forEach(store => {
        const allocation = allocations[store];

        if (!organizedData.byStore[store]) {
          organizedData.byStore[store] = [];
        }

        const existingItem = organizedData.byStore[store].find(i => i.item === item);
        if (existingItem) {
          existingItem.quantity += allocation;
        } else {
          const itemInfo = formatItemDisplay(item);
          organizedData.byStore[store].push({
            item,
            itemInfo,
            quantity: allocation,
            rawData: { redistributed: true }
          });
        }

        // Update byItem view
        if (!organizedData.byItem[item]) {
          organizedData.byItem[item] = [];
        }

        const existingStore = organizedData.byItem[item].find(s => s.store === store);
        if (existingStore) {
          existingStore.quantity += allocation;
        } else {
          const storeInfo = formatStoreDisplay(store);
          organizedData.byItem[item].push({
            store,
            storeInfo,
            quantity: allocation,
            rawData: { redistributed: true }
          });
        }
      });
    });
  }

  // Mark items as redistributed for highlighting
  Object.keys(plan).forEach(item => {
    redistributedItems.add(item);
  });

  // Clear excluded stores and rebuild data
  excludedStores.clear();
  excludedData = { byStore: {}, byItem: {} };
  includedData = { byStore: {}, byItem: {} };

  // Update the store selection checkboxes to show all selected
  displayStoreSelectionList();

  // Hide excluded section since there are no more excluded stores
  excludedSection.style.display = 'none';

  // Update display
  updateStats();
  displayData();

  closeRedistributionDialog();

  // Save state to history
  const methodName = method === 'equal' ? 'equal' : 'rank-based';
  const numItems = Object.keys(plan).length;
  historyManager.pushState(`Redistributed ${numItems} item${numItems > 1 ? 's' : ''} (${methodName})`);

  // Scroll to main data display
  setTimeout(() => {
    dataDisplay.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, 100);

  showErrorDialog('Redistribution applied successfully! Redistributed items are highlighted.', 'Success', 'success');
};

// Copy store data formatted for Business Central
window.copyStoreData = function(storeName) {
  const dataSource = excludedStores.size > 0 ? includedData : organizedData;
  const items = dataSource.byStore[storeName];

  if (!items || items.length === 0) {
    showErrorDialog('No items found for this store', 'No Data');
    return;
  }

  // Format as tab-separated values for pasting into Business Central
  // Format: Item Number\tQuantity
  let clipboardText = '';

  items.forEach(item => {
    clipboardText += `${item.itemInfo.code}\t${item.quantity}\n`;
  });

  // Copy to clipboard
  navigator.clipboard.writeText(clipboardText).then(() => {
    // Collapse and highlight the store card
    const storeId = `store-${storeName.replace(/[^a-zA-Z0-9]/g, '-')}`;
    const storeCard = document.getElementById(storeId);

    if (storeCard) {
      // Add collapsed and copied classes
      storeCard.classList.add('collapsed', 'copied');
    }

    // Show success feedback
    showCopyFeedback(storeName);
  }).catch(err => {
    console.error('Failed to copy:', err);
    showErrorDialog('Failed to copy to clipboard. Please try again.', 'Copy Failed');
  });
};

// Toggle store card collapse
window.toggleStoreCollapse = function(storeName) {
  const storeId = `store-${storeName.replace(/[^a-zA-Z0-9]/g, '-')}`;
  const storeCard = document.getElementById(storeId);

  if (storeCard) {
    if (storeCard.classList.contains('collapsed')) {
      // Expand and clear highlight
      storeCard.classList.remove('collapsed', 'copied');
    } else {
      // Collapse
      storeCard.classList.add('collapsed');
    }
  }
};

// Show visual feedback for successful copy
function showCopyFeedback(storeName) {
  // Create temporary notification
  const notification = document.createElement('div');
  notification.className = 'copy-notification';
  notification.textContent = `✓ Copied ${storeName} items to clipboard!`;
  document.body.appendChild(notification);

  // Animate in
  setTimeout(() => {
    notification.classList.add('show');
  }, 10);

  // Remove after 2 seconds
  setTimeout(() => {
    notification.classList.remove('show');
    setTimeout(() => {
      notification.remove();
    }, 300);
  }, 2000);
}

// ============================================
// Dark Mode / Theme Switching
// ============================================
// Theme is now managed by the unified app parent window
// The iframe receives theme via data-theme attribute set in the <head>
// and listens for themeChange messages from parent

// Get theme toggle button (if it exists) - may not exist in unified app
const themeToggle = document.getElementById('themeToggle');
const themeIcon = themeToggle?.querySelector('.theme-icon');
const themeLabel = themeToggle?.querySelector('.theme-label');

// Update icon and label based on current theme
function updateThemeIcon() {
  if (!themeToggle || !themeIcon || !themeLabel) {
    return; // Theme controls not available in unified app context
  }
  
  const currentTheme = document.documentElement.getAttribute('data-theme');

  if (currentTheme === 'dark') {
    themeIcon.textContent = '🌙';
    themeLabel.textContent = 'Dark';
    themeToggle.title = 'Switch to light mode';
  } else {
    themeIcon.textContent = '☀️';
    themeLabel.textContent = 'Light';
    themeToggle.title = 'Switch to dark mode';
  }
}

// Initialize icon (if button exists)
updateThemeIcon();

// Toggle theme on button click (standalone mode only)
if (themeToggle) {
  themeToggle.addEventListener('click', () => {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
    updateThemeIcon();
  });
}

// ============================================
// Drag & Drop File Upload
// ============================================

const dropZone = document.getElementById('dropZone');
let dragCounter = 0;

// Prevent default drag behaviors on the entire document
['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
  document.body.addEventListener(eventName, preventDefaults, false);
});

function preventDefaults(e) {
  e.preventDefault();
  e.stopPropagation();
}

// Handle drag enter
document.body.addEventListener('dragenter', (e) => {
  dragCounter++;
  if (dragCounter === 1) {
    document.body.classList.add('drag-over');
    dropZone.style.display = 'flex';
    setTimeout(() => dropZone.classList.add('show'), 10);
  }
});

// Handle drag leave
document.body.addEventListener('dragleave', (e) => {
  dragCounter--;
  if (dragCounter === 0) {
    document.body.classList.remove('drag-over');
    dropZone.classList.remove('show');
    setTimeout(() => {
      if (dragCounter === 0) {
        dropZone.style.display = 'none';
      }
    }, 300);
  }
});

// Handle drop
document.body.addEventListener('drop', async (e) => {
  dragCounter = 0;
  document.body.classList.remove('drag-over');
  dropZone.classList.remove('show');
  setTimeout(() => dropZone.style.display = 'none', 300);

  const files = e.dataTransfer.files;

  if (files.length === 0) {
    showErrorDialog('No file was dropped', 'Invalid Drop');
    return;
  }

  if (files.length > 1) {
    showErrorDialog('Please drop only one file at a time', 'Multiple Files');
    return;
  }

  const file = files[0];
  const filePath = file.path; // Electron provides the file path

  // Validate file extension
  const ext = filePath.split('.').pop().toLowerCase();
  if (!['csv', 'xlsx', 'xls'].includes(ext)) {
    showErrorDialog(
      'Invalid file type. Please drop an Excel (.xlsx, .xls) or CSV file.',
      'Unsupported File Type'
    );
    return;
  }

  // Display file path and load the file
  filePathDisplay.value = filePath;
  loadFile(filePath);
});

// ============================================
// Keyboard Shortcuts
// ============================================

document.addEventListener('keydown', (e) => {
  // Ctrl/Cmd + Z - Undo
  if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
    e.preventDefault();
    if (historyManager.canUndo()) {
      undoBtn.click();
    }
  }

  // Ctrl/Cmd + Y or Ctrl/Cmd + Shift + Z - Redo
  if (
    ((e.ctrlKey || e.metaKey) && e.key === 'y') ||
    ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'z')
  ) {
    e.preventDefault();
    if (historyManager.canRedo()) {
      redoBtn.click();
    }
  }

  // Ctrl/Cmd + O - Open file
  if ((e.ctrlKey || e.metaKey) && e.key === 'o') {
    e.preventDefault();
    selectFileBtn.click();
  }

  // Ctrl/Cmd + F - Focus search box
  if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
    e.preventDefault();
    searchBox.focus();
    searchBox.select();
  }

  // Ctrl/Cmd + K - Clear filters
  if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
    e.preventDefault();
    clearFiltersBtn.click();
  }

  // Escape - Clear search
  if (e.key === 'Escape') {
    if (document.activeElement === searchBox && searchBox.value) {
      e.preventDefault();
      searchBox.value = '';
      applyFilters();
    }
  }

  // 1 or V - View by Store
  if ((e.key === '1' || e.key.toLowerCase() === 'v') && !isInputFocused()) {
    e.preventDefault();
    if (currentData.length > 0) {
      switchView('store');
    }
  }

  // 2 or I - View by Item
  if ((e.key === '2' || e.key.toLowerCase() === 'i') && !isInputFocused()) {
    e.preventDefault();
    if (currentData.length > 0) {
      switchView('item');
    }
  }
});

/**
 * Check if an input/textarea element is currently focused
 */
function isInputFocused() {
  const activeElement = document.activeElement;
  return (
    activeElement.tagName === 'INPUT' ||
    activeElement.tagName === 'TEXTAREA' ||
    activeElement.isContentEditable
  );
}

// Display keyboard shortcuts help on initial load (optional)
console.log('%cKeyboard Shortcuts:', 'font-weight: bold; font-size: 14px; color: #667eea');
console.log('Ctrl/Cmd + Z: Undo');
console.log('Ctrl/Cmd + Y: Redo');
console.log('Ctrl/Cmd + O: Open file');
console.log('Ctrl/Cmd + F: Search items');
console.log('Ctrl/Cmd + K: Clear filters');
console.log('Escape: Clear search');
console.log('1 or V: View by Store');
console.log('2 or I: View by Item');


// ============================================
// Store Details Modal
// ============================================

let currentModalStore = null;
let storeModal, storeModalName, storeModalBadge, modalTotalItems, modalTotalQty, modalSearchBox, storeModalContent;

/**
 * Initialize modal elements (called when DOM is ready)
 */
function initModalElements() {
  storeModal = document.getElementById('storeModal');
  storeModalName = document.getElementById('storeModalName');
  storeModalBadge = document.getElementById('storeModalBadge');
  modalTotalItems = document.getElementById('modalTotalItems');
  modalTotalQty = document.getElementById('modalTotalQty');
  modalSearchBox = document.getElementById('modalSearchBox');
  storeModalContent = document.getElementById('storeModalContent');
  
  // Add event listener for modal search
  if (modalSearchBox) {
    modalSearchBox.addEventListener('input', debounce(function() {
      if (!currentModalStore) return;

      const searchTerm = modalSearchBox.value.toLowerCase();
      const dataSource = excludedStores.size > 0 ? includedData : organizedData;
      const items = dataSource.byStore[currentModalStore];

      if (!searchTerm) {
        renderModalItems(items);
        return;
      }

      const filteredItems = items.filter(function(item) {
        return item.item.toLowerCase().includes(searchTerm) || (item.itemInfo.description && item.itemInfo.description.toLowerCase().includes(searchTerm));
      });

      renderModalItems(filteredItems);
    }, 300));
  }
  
  // Add event listener to close modal when clicking outside
  if (storeModal) {
    storeModal.addEventListener('click', function(e) {
      if (e.target === storeModal) {
        window.closeStoreModal();
      }
    });
  }
}

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

/**
 * Open store details modal
 */
window.openStoreModal = function(storeName) {
  if (!storeModal) {
    console.error('Store modal not initialized');
    return;
  }
  
  currentModalStore = storeName;
  const dataSource = excludedStores.size > 0 ? includedData : organizedData;
  const items = dataSource.byStore[storeName];

  if (!items || items.length === 0) {
    showErrorDialog('No items found for this store', 'No Data');
    return;
  }

  storeModalName.textContent = storeName;

  const storeInfo = findStoreInDictionary(storeName);
  if (storeInfo && storeInfo.store && storeInfo.store.rank) {
    const rank = storeInfo.store.rank.toLowerCase();
    storeModalBadge.className = 'store-modal-badge rank-badge rank-' + rank;
    storeModalBadge.textContent = storeInfo.store.rank;
    storeModalBadge.style.display = 'inline-flex';
  } else {
    storeModalBadge.style.display = 'none';
  }

  const totalQty = items.reduce(function(sum, item) { return sum + item.quantity; }, 0);
  modalTotalItems.textContent = items.length;
  modalTotalQty.textContent = totalQty.toLocaleString();

  modalSearchBox.value = '';
  renderModalItems(items);

  storeModal.style.display = 'flex';
  setTimeout(function() { storeModal.classList.add('show'); }, 10);
};

window.closeStoreModal = function() {
  storeModal.classList.remove('show');
  setTimeout(function() {
    storeModal.style.display = 'none';
    currentModalStore = null;
  }, 300);
};

function renderModalItems(items) {
  if (!items || items.length === 0) {
    storeModalContent.innerHTML = '<div class="modal-empty"><div class="modal-empty-icon">📦</div><p class="modal-empty-text">No items found</p></div>';
    return;
  }

  const html = items.map(function(item) {
    const isRedistributed = redistributedItems.has(item.item);
    const redistributedClass = isRedistributed ? 'redistributed' : '';
    const desc = item.itemInfo.description || 'No description';
    return '<div class="modal-item ' + redistributedClass + '"><div class="modal-item-code">' + escapeHtml(item.item) + '</div><div class="modal-item-desc">' + escapeHtml(desc) + '</div><div class="modal-item-qty">' + item.quantity + '</div></div>';
  }).join('');

  storeModalContent.innerHTML = html;
}

window.copyStoreDataFromModal = function() {
  if (!currentModalStore) return;
  window.copyStoreData(currentModalStore);
};

// Global keyboard shortcuts
document.addEventListener('keydown', function(e) {
  // Escape key - Close modals
  if (e.key === 'Escape') {
    if (storeModal && storeModal.classList.contains('show')) {
      window.closeStoreModal();
      return;
    }
    if (archiveModal && archiveModal.classList.contains('show')) {
      window.closeArchiveModal();
      return;
    }
  }

  // Ctrl/Cmd + F - Focus search box
  if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
    e.preventDefault();
    if (searchBox) {
      searchBox.focus();
      searchBox.select();
    }
    return;
  }

  // Ctrl/Cmd + Z - Undo
  if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
    e.preventDefault();
    if (!undoBtn.disabled) {
      undoBtn.click();
    }
    return;
  }

  // Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y - Redo
  if (((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'z') ||
      ((e.ctrlKey || e.metaKey) && e.key === 'y')) {
    e.preventDefault();
    if (!redoBtn.disabled) {
      redoBtn.click();
    }
    return;
  }

  // Ctrl/Cmd + O - Open file
  if ((e.ctrlKey || e.metaKey) && e.key === 'o') {
    e.preventDefault();
    selectFileBtn.click();
    return;
  }
});

// ============================================
// Store Card Enhancements
// ============================================

/**
 * Collapse all store cards
 */
window.collapseAllStores = function() {
  const storeCards = document.querySelectorAll('.store-card');
  storeCards.forEach(card => {
    card.classList.add('collapsed');
  });
};

/**
 * Expand all store cards
 */
window.expandAllStores = function() {
  const storeCards = document.querySelectorAll('.store-card');
  storeCards.forEach(card => {
    card.classList.remove('collapsed', 'copied');
  });
};

// ============================================
// Initialize Archive System
// ============================================

/**
 * Initialize archive manager on app startup
 */
async function initializeArchiveSystem() {
  try {
    // Load archives
    await window.archiveManager.loadArchives();

    // Run cleanup of old archives
    const result = await window.archiveManager.cleanupOldArchives();
    if (result.success && result.deletedCount > 0) {
      console.log(`Cleaned up ${result.deletedCount} old archives on startup`);
    }
  } catch (error) {
    console.error('Failed to initialize archive system:', error);
  }
}

// Initialize on app load
document.addEventListener('DOMContentLoaded', () => {
  initModalElements();
  initializeArchiveSystem();
  initSettingsPanel();
});

// ============================================
// Settings Panel
// ============================================

const settingsModal = document.getElementById('settingsModal');
let currentSettingsTab = 'items';
let settingsSearches = { items: '', stores: '' };

/**
 * Initialize settings panel
 */
function initSettingsPanel() {
  // Tab switching
  const tabs = document.querySelectorAll('.settings-tab');
  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      const tabName = tab.dataset.tab;
      switchSettingsTab(tabName);
    });
  });

  // Search handlers
  const itemSearch = document.getElementById('settingsItemSearch');
  const storeSearch = document.getElementById('settingsStoreSearch');

  if (itemSearch) {
    itemSearch.addEventListener('input', debounce((e) => {
      settingsSearches.items = e.target.value;
      renderSettingsItems();
    }, 300));
  }

  if (storeSearch) {
    storeSearch.addEventListener('input', debounce((e) => {
      settingsSearches.stores = e.target.value;
      renderSettingsStores();
    }, 300));
  }

  // Add Store Form
  const addStoreForm = document.getElementById('addStoreForm');
  if (addStoreForm) {
    addStoreForm.addEventListener('submit', handleAddStore);
  }
}

/**
 * Open settings modal
 */
function openSettingsModal() {
  currentSettingsTab = 'items';
  switchSettingsTab('items');
  settingsModal.style.display = 'flex';
  setTimeout(() => settingsModal.classList.add('show'), 10);
  renderSettingsItems();
  renderSettingsStores();
  updateDictionaryStats();
}

/**
 * Close settings modal
 */
window.closeSettingsModal = function() {
  settingsModal.classList.remove('show');
  setTimeout(() => {
    settingsModal.style.display = 'none';
    settingsSearches = { items: '', stores: '' };
    document.getElementById('settingsItemSearch').value = '';
    document.getElementById('settingsStoreSearch').value = '';
  }, 300);
};

/**
 * Switch between settings tabs
 */
function switchSettingsTab(tabName) {
  currentSettingsTab = tabName;

  // Update tab buttons
  document.querySelectorAll('.settings-tab').forEach(tab => {
    tab.classList.toggle('active', tab.dataset.tab === tabName);
  });

  // Update tab content
  document.querySelectorAll('.settings-tab-content').forEach(content => {
    content.classList.remove('active');
  });
  document.getElementById(`settingsTab${tabName.charAt(0).toUpperCase() + tabName.slice(1)}`).classList.add('active');

  // Load content if needed
  if (tabName === 'items') {
    renderSettingsItems();
  } else if (tabName === 'stores') {
    renderSettingsStores();
  } else if (tabName === 'export') {
    updateDictionaryStats();
  }
}

/**
 * Render items list in settings
 */
function renderSettingsItems() {
  const list = document.getElementById('settingsItemsList');
  const search = settingsSearches.items.toUpperCase();

  // Get unique items from the dictionary array (not the map which has duplicates)
  const items = window.DICT.items || [];
  const filteredItems = search
    ? items.filter(item => {
        const matchNumber = item.number.toUpperCase().includes(search);
        const matchDesc = item.desc.toUpperCase().includes(search);
        const matchSku = item.sku && item.sku.some(s => s.toUpperCase().includes(search));
        return matchNumber || matchDesc || matchSku;
      })
    : items;

  // Update tab count
  document.querySelector('[data-tab="items"]').textContent = `Items (${items.length})`;

  if (filteredItems.length === 0) {
    list.innerHTML = `
      <div class="settings-empty">
        <div class="settings-empty-icon">🔍</div>
        <p>${search ? 'No items found' : 'No items in dictionary'}</p>
      </div>
    `;
    return;
  }

  // Sort by item number
  filteredItems.sort((a, b) => a.number.localeCompare(b.number));

  // Render items (limit to 100 for performance)
  const itemsHTML = filteredItems.slice(0, 100).map(item => {
    const skuText = item.sku && item.sku.length > 0
      ? `<div class="settings-item-sku">SKU: ${item.sku.join(', ')}</div>`
      : '';

    return `
      <div class="settings-item" data-item-number="${escapeHtml(item.number)}">
        <div class="settings-item-info">
          <div class="settings-item-number">${escapeHtml(item.number)}</div>
          <div class="settings-item-desc">${escapeHtml(item.desc)}</div>
          ${skuText}
        </div>
        <div class="settings-item-actions">
          <button class="btn-icon" onclick="editDictItem('${escapeHtml(item.number)}')" title="Edit">✏️</button>
          <button class="btn-icon delete" onclick="deleteDictItem('${escapeHtml(item.number)}')" title="Delete">🗑️</button>
        </div>
      </div>
    `;
  }).join('');

  list.innerHTML = itemsHTML + (filteredItems.length > 100
    ? `<div class="settings-empty"><p>Showing first 100 of ${filteredItems.length} items</p></div>`
    : '');
}

/**
 * Render stores list in settings
 */
function renderSettingsStores() {
  const list = document.getElementById('settingsStoresList');
  const search = settingsSearches.stores.toUpperCase();

  // Get unique stores from the dictionary array (not the map which has duplicates)
  const stores = window.DICT.stores || [];
  const filteredStores = search
    ? stores.filter(store => {
        const matchId = String(store.id).includes(search);
        const matchName = store.name.toUpperCase().includes(search);
        return matchId || matchName;
      })
    : stores;

  // Update tab count
  document.querySelector('[data-tab="stores"]').textContent = `Stores (${stores.length})`;

  if (filteredStores.length === 0) {
    list.innerHTML = `
      <div class="settings-empty">
        <div class="settings-empty-icon">🔍</div>
        <p>${search ? 'No stores found' : 'No stores in dictionary'}</p>
      </div>
    `;
    return;
  }

  // Sort by ID
  filteredStores.sort((a, b) => a.id - b.id);

  const storesHTML = filteredStores.map(store => {
    const rankBadge = store.rank ? `<span class="rank-badge rank-${store.rank.toLowerCase()}">${store.rank}</span>` : '';
    return `
      <div class="settings-item" data-store-id="${store.id}">
        <div class="settings-item-info">
          <div class="settings-item-number">${store.id} - ${escapeHtml(store.name)} ${rankBadge}</div>
        </div>
        <div class="settings-item-actions">
          <button class="btn-icon" onclick="editDictStore(${store.id})" title="Edit">✏️</button>
          <button class="btn-icon delete" onclick="deleteDictStore(${store.id})" title="Delete">🗑️</button>
        </div>
      </div>
    `;
  }).join('');

  list.innerHTML = storesHTML;
}

/**
 * Update dictionary statistics
 */
function updateDictionaryStats() {
  const totalItems = window.DICT.items ? window.DICT.items.length : 0;
  const totalStores = window.DICT.stores ? window.DICT.stores.length : 0;
  const itemsWithSkus = window.DICT.items ? window.DICT.items.filter(item => item.sku && item.sku.length > 0).length : 0;
  const dictSize = Math.round(JSON.stringify(window.DICT).length / 1024);

  document.getElementById('statTotalItems').textContent = totalItems;
  document.getElementById('statTotalStores').textContent = totalStores;
  document.getElementById('statItemsWithSkus').textContent = itemsWithSkus;
  document.getElementById('statDictSize').textContent = `${dictSize} KB`;
}

/**
 * Auto-save dictionary to file (silent, no confirmation)
 */
async function autoSaveDictionary() {
  try {
    const result = await window.electronAPI.saveDictionary(window.DICT);
    if (result.success) {
      console.log('Dictionary auto-saved to file');
    } else {
      console.error('Auto-save failed:', result.error);
    }
  } catch (error) {
    console.error('Auto-save failed:', error);
  }
}

/**
 * Save dictionary to file permanently (manual, with confirmation)
 */
window.saveDictionaryToFile = async function() {
  try {
    if (!confirm('Save the current dictionary permanently?\n\nThis will update the dictionaries.js file and create a backup.')) {
      return;
    }

    const result = await window.electronAPI.saveDictionary(window.DICT);
    
    if (result.success) {
      showErrorDialog('Dictionary saved successfully! Restart the app to ensure all changes are loaded.', 'Save Complete', 'success');
    } else {
      showErrorDialog(`Failed to save dictionary: ${result.error}`, 'Save Error');
    }
  } catch (error) {
    console.error('Save failed:', error);
    showErrorDialog('Failed to save dictionary', 'Save Error');
  }
};

/**
 * Export dictionary as JSON to user-selected location
 */
window.exportDictionary = async function() {
  try {
    const result = await window.electronAPI.exportDictionary(window.DICT);
    
    if (result.canceled) {
      return;
    }
    
    if (result.success) {
      showErrorDialog(`Dictionary exported to:\n${result.filePath}`, 'Export Complete', 'success');
    } else {
      showErrorDialog(`Failed to export dictionary: ${result.error}`, 'Export Error');
    }
  } catch (error) {
    console.error('Export failed:', error);
    showErrorDialog('Failed to export dictionary', 'Export Error');
  }
};

/**
 * Import dictionary from JSON
 */
window.importDictionary = async function() {
  try {
    const result = await window.electronAPI.importDictionary();
    
    if (result.canceled) {
      return;
    }
    
    if (!result.success) {
      showErrorDialog(`Failed to import dictionary: ${result.error}`, 'Import Error');
      return;
    }
    
    const importedDict = result.data;
    
    // Validate structure
    if (!importedDict.items || !importedDict.stores) {
      showErrorDialog('Invalid dictionary format. Must contain "items" and "stores" arrays.', 'Import Error');
      return;
    }

    // Merge with existing dictionary
    if (confirm(`Import ${importedDict.items.length} items and ${importedDict.stores.length} stores?\n\nThis will add to the existing dictionary.`)) {
      
      // Add items (avoid duplicates)
      const existingNumbers = new Set(window.DICT.items.map(i => i.number));
      const newItems = importedDict.items.filter(i => !existingNumbers.has(i.number));
      window.DICT.items.push(...newItems);

      // Add stores (avoid duplicates)
      const existingIds = new Set(window.DICT.stores.map(s => s.id));
      const newStores = importedDict.stores.filter(s => !existingIds.has(s.id));
      window.DICT.stores.push(...newStores);

      // Reinitialize dictionaries
      initializeDictionaries();

      // Refresh UI
      renderSettingsItems();
      renderSettingsStores();
      updateDictionaryStats();

      showErrorDialog(`Imported ${newItems.length} new items and ${newStores.length} new stores`, 'Import Complete', 'success');
    }
  } catch (error) {
    console.error('Import failed:', error);
    showErrorDialog('Failed to import dictionary', 'Import Error');
  }
};

/**
 * Edit dictionary item
 */
window.editDictItem = function(itemNumber) {
  const item = itemsMap.get(itemNumber.toUpperCase());
  if (!item) return;

  // Show the add item dialog first
  showAddItemDialog(item.number);

  // Wait for dialog to be created, then populate fields
  setTimeout(() => {
    const numberField = document.getElementById('itemNumber');
    const descField = document.getElementById('itemDesc');
    const skuField = document.getElementById('itemSku');
    const title = document.querySelector('.add-item-dialog .add-item-header h2');

    if (numberField) numberField.value = item.number;
    if (descField) descField.value = item.desc;
    if (skuField) skuField.value = item.sku ? item.sku.join(', ') : '';
    if (title) title.textContent = '✏️ Edit Item in Dictionary';

    // Mark as editing mode by storing the original number
    numberField.dataset.editing = 'true';
    numberField.dataset.originalNumber = item.number;
  }, 10);
};

/**
 * Delete dictionary item
 */
window.deleteDictItem = function(itemNumber) {
  if (!confirm(`Delete item ${itemNumber}?`)) return;

  const index = window.DICT.items.findIndex(i => i.number === itemNumber);
  if (index !== -1) {
    window.DICT.items.splice(index, 1);
    initializeDictionaries();
    autoSaveDictionary();
    renderSettingsItems();
    updateDictionaryStats();
    showErrorDialog('Item deleted and saved to file', 'Success', 'success');
  }
};

/**
 * Edit dictionary store
 */
window.editDictStore = function(storeId) {
  const store = Array.from(storesMap.values()).find(s => s.id === storeId);
  if (!store) return;

  // Populate the add store dialog with existing data
  document.getElementById('newStoreId').value = store.id;
  document.getElementById('newStoreName').value = store.name;
  document.getElementById('newStoreRank').value = store.rank || '';

  // Change dialog title
  document.querySelector('#addStoreDialog .add-item-header h3').textContent = 'Edit Store';
  
  // Show dialog
  showAddStoreDialog();
};

/**
 * Delete dictionary store
 */
window.deleteDictStore = function(storeId) {
  if (!confirm(`Delete store ${storeId}?`)) return;

  const index = window.DICT.stores.findIndex(s => s.id === storeId);
  if (index !== -1) {
    window.DICT.stores.splice(index, 1);
    initializeDictionaries();
    autoSaveDictionary();
    renderSettingsStores();
    updateDictionaryStats();
    showErrorDialog('Store deleted and saved to file', 'Success', 'success');
  }
};

/**
 * Show add store dialog
 */
window.showAddStoreDialog = function() {
  document.getElementById('addStoreDialog').style.display = 'flex';
  setTimeout(() => document.getElementById('addStoreDialog').classList.add('show'), 10);
};

/**
 * Close add store dialog
 */
window.closeAddStoreDialog = function() {
  const dialog = document.getElementById('addStoreDialog');
  dialog.classList.remove('show');
  setTimeout(() => {
    dialog.style.display = 'none';
    document.getElementById('addStoreForm').reset();
    document.querySelector('#addStoreDialog .add-item-header h3').textContent = 'Add New Store';
  }, 300);
};

/**
 * Handle add/edit store form submission
 */
function handleAddStore(e) {
  e.preventDefault();

  const id = parseInt(document.getElementById('newStoreId').value);
  const name = document.getElementById('newStoreName').value.trim();
  const rank = document.getElementById('newStoreRank').value.trim();

  if (!id || !name) {
    showErrorDialog('Store ID and name are required', 'Validation Error');
    return;
  }

  // Check if editing existing store
  const existingIndex = window.DICT.stores.findIndex(s => s.id === id);

  const storeObj = { id, name };
  if (rank) storeObj.rank = rank;

  if (existingIndex !== -1) {
    // Update existing
    window.DICT.stores[existingIndex] = storeObj;
    showErrorDialog('Store updated and saved to file', 'Success', 'success');
  } else {
    // Add new
    window.DICT.stores.push(storeObj);
    showErrorDialog('Store added and saved to file', 'Success', 'success');
  }

  // Reinitialize and refresh
  initializeDictionaries();
  autoSaveDictionary();
  renderSettingsStores();
  updateDictionaryStats();
  closeAddStoreDialog();
}

// ============================================
// Archive Viewer UI
// ============================================

const archiveModal = document.getElementById('archiveModal');
const archiveModalContent = document.getElementById('archiveModalContent');
const archiveSearchBox = document.getElementById('archiveSearchBox');
const archiveRetentionSelect = document.getElementById('archiveRetentionSelect');
const archiveStats = document.getElementById('archiveStats');

let currentArchiveSearch = '';

// Add event listener to retention select
if (archiveRetentionSelect) {
  archiveRetentionSelect.addEventListener('change', (e) => {
    const days = parseInt(e.target.value);
    window.archiveManager.saveRetentionSettings(days);
    showErrorDialog(`Retention period updated to ${days} days`, 'Settings Saved', 'success');
  });
}

// Add event listener to archive search with debouncing
if (archiveSearchBox) {
  archiveSearchBox.addEventListener('input', debounce((e) => {
    currentArchiveSearch = e.target.value;
    renderArchives();
  }, 300));
}

/**
 * Open archive viewer modal
 */
async function openArchiveModal() {
  try {
    // Load archives
    await window.archiveManager.loadArchives();

    // Set retention select value
    archiveRetentionSelect.value = window.archiveManager.retentionDays.toString();

    // Render archives
    renderArchives();

    // Show modal
    archiveModal.style.display = 'flex';
    setTimeout(() => archiveModal.classList.add('show'), 10);
  } catch (error) {
    console.error('Failed to open archive modal:', error);
    showErrorDialog('Failed to load archives', 'Error');
  }
}

/**
 * Close archive viewer modal
 */
window.closeArchiveModal = function() {
  archiveModal.classList.remove('show');
  setTimeout(() => {
    archiveModal.style.display = 'none';
    currentArchiveSearch = '';
    archiveSearchBox.value = '';
  }, 300);
};

/**
 * Render archives list
 */
function renderArchives() {
  const archives = currentArchiveSearch
    ? window.archiveManager.searchArchives(currentArchiveSearch)
    : window.archiveManager.archives;

  // Update stats
  updateArchiveStats(archives);

  if (archives.length === 0) {
    archiveModalContent.innerHTML = `
      <div class="archive-empty">
        <div class="archive-empty-icon">📦</div>
        <h3>No Archives Found</h3>
        <p>${currentArchiveSearch ? 'No archives match your search' : 'Load a file to create your first archive'}</p>
      </div>
    `;
    return;
  }

  // Group by month
  const grouped = {};
  archives.forEach(archive => {
    const date = new Date(archive.timestamp);
    const monthKey = date.toLocaleDateString('en-US', { year: 'numeric', month: 'long' });

    if (!grouped[monthKey]) {
      grouped[monthKey] = [];
    }
    grouped[monthKey].push(archive);
  });

  let html = '<div class="archive-timeline">';

  Object.keys(grouped).forEach(month => {
    html += `
      <div class="archive-month-group">
        <h3 class="archive-month-header">${month}</h3>
        <div class="archive-list">
    `;

    grouped[month].forEach(archive => {
      const date = new Date(archive.timestamp);
      const timeStr = date.toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });

      const sizeKB = (archive.size / 1024).toFixed(1);

      html += `
        <div class="archive-card">
          <div class="archive-card-header">
            <div class="archive-card-icon">📄</div>
            <div class="archive-card-info">
              <div class="archive-card-title">${archive.filename || 'Untitled'}</div>
              <div class="archive-card-meta">
                <span class="archive-card-date">${timeStr}</span>
                <span class="archive-card-size">${sizeKB} KB</span>
              </div>
            </div>
          </div>
          <div class="archive-card-stats">
            <span class="archive-stat-badge">${archive.metadata.totalStores} stores</span>
            <span class="archive-stat-badge">${archive.metadata.totalItems} items</span>
            <span class="archive-stat-badge">${archive.metadata.totalQuantity.toLocaleString()} units</span>
          </div>
          <div class="archive-card-actions">
            <button class="btn btn-secondary btn-small" onclick="loadArchiveData('${archive.archiveName}')" title="Load this archive">
              <span class="icon">📂</span>
              Load
            </button>
            <button class="btn btn-secondary btn-small" onclick="compareWithArchive('${archive.archiveName}')" title="Compare with current">
              <span class="icon">⚖️</span>
              Compare
            </button>
            <button class="btn btn-secondary btn-small" onclick="deleteArchiveConfirm('${archive.archiveName}')" title="Delete this archive">
              <span class="icon">🗑️</span>
              Delete
            </button>
          </div>
        </div>
      `;
    });

    html += `
        </div>
      </div>
    `;
  });

  html += '</div>';
  archiveModalContent.innerHTML = html;
}

/**
 * Update archive statistics
 */
function updateArchiveStats(archives) {
  const total = archives.length;
  const totalSize = archives.reduce((sum, a) => sum + (a.size || 0), 0);
  const sizeKB = (totalSize / 1024).toFixed(1);

  archiveStats.innerHTML = `
    <span class="archive-stat-item">Total: <strong>${total}</strong></span>
    <span class="archive-stat-item">Storage: <strong>${sizeKB} KB</strong></span>
  `;
}

/**
 * Load archive data into main view
 */
window.loadArchiveData = async function(archiveName) {
  try {
    showLoadingOverlay();
    updateLoadingProgress('reading');

    const archive = await window.archiveManager.loadArchive(archiveName);

    if (!archive) {
      hideLoadingOverlay();
      showErrorDialog('Failed to load archive', 'Error');
      return;
    }

    updateLoadingProgress('organizing');

    // Restore data
    organizedData = archive.data.organizedData;
    excludedStores = new Set(archive.data.excludedStores || []);
    redistributedItems = new Set(archive.data.redistributedItems || []);

    // Update UI
    filePathDisplay.value = `[ARCHIVE] ${archive.filename}`;
    showFileInfo(archive.filename, Object.keys(organizedData.byStore).length);

    updateStoreFilter();
    updateStats();
    displayStoreSelectionList();
    displayData();

    // Expand all stores by default to show items - use setTimeout to ensure DOM is ready
    setTimeout(() => {
      expandAllStores();
      console.log('✅ Archive loaded, stores expanded');
    }, 100);

    // Close archive modal
    closeArchiveModal();

    hideLoadingOverlay();
    showErrorDialog('Archive loaded successfully!', 'Success', 'success');
  } catch (error) {
    hideLoadingOverlay();
    console.error('Failed to load archive:', error);
    showErrorDialog('Failed to load archive: ' + error.message, 'Error');
  }
};

/**
 * Compare current data with archive
 */
window.compareWithArchive = async function(archiveName) {
  try {
    const archive = await window.archiveManager.loadArchive(archiveName);

    if (!archive) {
      showErrorDialog('Failed to load archive for comparison', 'Error');
      return;
    }

    // Check if there's current data
    if (!organizedData || !organizedData.byStore || Object.keys(organizedData.byStore).length === 0) {
      showErrorDialog('Please load current allocation data first to compare', 'No Data', 'info');
      return;
    }

    // Generate comparison
    showComparisonView(organizedData, archive.data.organizedData, archive.filename);
  } catch (error) {
    console.error('Failed to compare with archive:', error);
    showErrorDialog('Failed to compare with archive: ' + error.message, 'Error');
  }
};

/**
 * Delete archive with confirmation
 */
window.deleteArchiveConfirm = function(archiveName) {
  const archive = window.archiveManager.archives.find(a => a.archiveName === archiveName);
  const filename = archive ? archive.filename : 'this archive';

  if (confirm(`Are you sure you want to delete "${filename}"?\n\nThis cannot be undone.`)) {
    deleteArchiveNow(archiveName);
  }
};

/**
 * Delete archive
 */
async function deleteArchiveNow(archiveName) {
  try {
    const result = await window.archiveManager.deleteArchive(archiveName);

    if (result.success) {
      renderArchives();
      showErrorDialog('Archive deleted successfully', 'Deleted', 'success');
    } else {
      showErrorDialog('Failed to delete archive', 'Error');
    }
  } catch (error) {
    console.error('Failed to delete archive:', error);
    showErrorDialog('Failed to delete archive: ' + error.message, 'Error');
  }
}

/**
 * Manual cleanup of old archives
 */
window.manualCleanupArchives = async function() {
  try {
    const cutoffDate = new Date();
    cutoffDate.setDate(cutoffDate.getDate() - window.archiveManager.retentionDays);

    const oldArchives = window.archiveManager.archives.filter(archive => {
      return new Date(archive.timestamp) < cutoffDate;
    });

    if (oldArchives.length === 0) {
      showErrorDialog('No old archives to clean up', 'Already Clean', 'info');
      return;
    }

    const message = `Found ${oldArchives.length} archive(s) older than ${window.archiveManager.retentionDays} days.\n\nDelete them now?`;

    if (confirm(message)) {
      const result = await window.archiveManager.cleanupOldArchives();

      if (result.success) {
        renderArchives();
        showErrorDialog(`Deleted ${result.deletedCount} old archive(s)`, 'Cleanup Complete', 'success');
      }
    }
  } catch (error) {
    console.error('Failed to cleanup archives:', error);
    showErrorDialog('Failed to cleanup archives: ' + error.message, 'Error');
  }
};

// ============================================
// Comparison View
// ============================================

/**
 * Show comparison between current and archived data
 */
function showComparisonView(currentData, archivedData, archiveFilename) {
  const comparison = generateComparison(currentData, archivedData);

  // Create comparison HTML
  let html = `
    <div class="comparison-view">
      <div class="comparison-header">
        <h2>⚖️ Allocation Comparison</h2>
        <p>Comparing current data with archived data from "${archiveFilename}"</p>
      </div>
      <div class="comparison-summary">
        <div class="comparison-stat-card">
          <div class="comparison-stat-label">Total Stores</div>
          <div class="comparison-stat-values">
            <span class="comparison-current">${comparison.current.totalStores}</span>
            <span class="comparison-arrow">${comparison.current.totalStores > comparison.archived.totalStores ? '↑' : comparison.current.totalStores < comparison.archived.totalStores ? '↓' : '='}</span>
            <span class="comparison-archived">${comparison.archived.totalStores}</span>
          </div>
        </div>
        <div class="comparison-stat-card">
          <div class="comparison-stat-label">Total Styles</div>
          <div class="comparison-stat-values">
            <span class="comparison-current">${comparison.current.totalItems}</span>
            <span class="comparison-arrow">${comparison.current.totalItems > comparison.archived.totalItems ? '↑' : comparison.current.totalItems < comparison.archived.totalItems ? '↓' : '='}</span>
            <span class="comparison-archived">${comparison.archived.totalItems}</span>
          </div>
        </div>
        <div class="comparison-stat-card">
          <div class="comparison-stat-label">Total Quantity</div>
          <div class="comparison-stat-values">
            <span class="comparison-current">${comparison.current.totalQuantity.toLocaleString()}</span>
            <span class="comparison-arrow">${comparison.current.totalQuantity > comparison.archived.totalQuantity ? '↑' : comparison.current.totalQuantity < comparison.archived.totalQuantity ? '↓' : '='}</span>
            <span class="comparison-archived">${comparison.archived.totalQuantity.toLocaleString()}</span>
          </div>
        </div>
      </div>
      <div class="comparison-details">
        ${generateComparisonDetails(comparison)}
      </div>
    </div>
  `;

  // Show in dialog
  showComparisonDialog(html);
}

/**
 * Generate comparison data
 */
function generateComparison(currentData, archivedData) {
  const current = {
    totalStores: Object.keys(currentData.byStore || {}).length,
    totalItems: Object.keys(currentData.byItem || {}).length,
    totalQuantity: 0,
    stores: currentData.byStore || {}
  };

  const archived = {
    totalStores: Object.keys(archivedData.byStore || {}).length,
    totalItems: Object.keys(archivedData.byItem || {}).length,
    totalQuantity: 0,
    stores: archivedData.byStore || {}
  };

  // Calculate totals
  Object.values(current.stores).forEach(items => {
    items.forEach(item => current.totalQuantity += item.quantity || 0);
  });

  Object.values(archived.stores).forEach(items => {
    items.forEach(item => archived.totalQuantity += item.quantity || 0);
  });

  // Find differences
  const newStores = Object.keys(current.stores).filter(s => !archived.stores[s]);
  const removedStores = Object.keys(archived.stores).filter(s => !current.stores[s]);
  const changedStores = [];

  Object.keys(current.stores).forEach(store => {
    if (archived.stores[store]) {
      const currentQty = current.stores[store].reduce((sum, item) => sum + (item.quantity || 0), 0);
      const archivedQty = archived.stores[store].reduce((sum, item) => sum + (item.quantity || 0), 0);

      if (currentQty !== archivedQty) {
        changedStores.push({
          store,
          currentQty,
          archivedQty,
          diff: currentQty - archivedQty
        });
      }
    }
  });

  return {
    current,
    archived,
    newStores,
    removedStores,
    changedStores
  };
}

/**
 * Generate comparison details HTML
 */
function generateComparisonDetails(comparison) {
  let html = '<div class="comparison-sections">';

  // New stores
  if (comparison.newStores.length > 0) {
    html += `
      <div class="comparison-section">
        <h3 class="comparison-section-title">✨ New Stores (${comparison.newStores.length})</h3>
        <div class="comparison-list">
          ${comparison.newStores.map(store => {
            const storeInfo = window.storeNames[store] || { name: store };
            return `<div class="comparison-item new">${storeInfo.name}</div>`;
          }).join('')}
        </div>
      </div>
    `;
  }

  // Removed stores
  if (comparison.removedStores.length > 0) {
    html += `
      <div class="comparison-section">
        <h3 class="comparison-section-title">🗑️ Removed Stores (${comparison.removedStores.length})</h3>
        <div class="comparison-list">
          ${comparison.removedStores.map(store => {
            const storeInfo = window.storeNames[store] || { name: store };
            return `<div class="comparison-item removed">${storeInfo.name}</div>`;
          }).join('')}
        </div>
      </div>
    `;
  }

  // Changed stores
  if (comparison.changedStores.length > 0) {
    comparison.changedStores.sort((a, b) => Math.abs(b.diff) - Math.abs(a.diff));

    html += `
      <div class="comparison-section">
        <h3 class="comparison-section-title">📊 Changed Quantities (${comparison.changedStores.length})</h3>
        <div class="comparison-table">
          <table>
            <thead>
              <tr>
                <th>Store</th>
                <th>Current</th>
                <th>Archived</th>
                <th>Difference</th>
              </tr>
            </thead>
            <tbody>
              ${comparison.changedStores.map(change => {
                const storeInfo = window.storeNames[change.store] || { name: change.store };
                const diffClass = change.diff > 0 ? 'positive' : 'negative';
                const arrow = change.diff > 0 ? '↑' : '↓';
                return `
                  <tr>
                    <td>${storeInfo.name}</td>
                    <td>${change.currentQty.toLocaleString()}</td>
                    <td>${change.archivedQty.toLocaleString()}</td>
                    <td class="${diffClass}">${arrow} ${Math.abs(change.diff).toLocaleString()}</td>
                  </tr>
                `;
              }).join('')}
            </tbody>
          </table>
        </div>
      </div>
    `;
  }

  if (comparison.newStores.length === 0 && comparison.removedStores.length === 0 && comparison.changedStores.length === 0) {
    html += `
      <div class="comparison-section">
        <div class="comparison-empty">
          <p>✅ No differences found - data is identical!</p>
        </div>
      </div>
    `;
  }

  html += '</div>';
  return html;
}

/**
 * Show comparison in a dialog
 */
function showComparisonDialog(html) {
  const dialogHTML = `
    <div class="comparison-dialog-overlay" id="comparisonDialog">
      <div class="comparison-dialog">
        <div class="comparison-dialog-header">
          <button class="comparison-dialog-close" onclick="closeComparisonDialog()">✕</button>
        </div>
        <div class="comparison-dialog-content">
          ${html}
        </div>
      </div>
    </div>
  `;

  // Remove existing comparison dialog if any
  const existing = document.getElementById('comparisonDialog');
  if (existing) existing.remove();

  // Add to body
  document.body.insertAdjacentHTML('beforeend', dialogHTML);

  // Show with animation
  setTimeout(() => {
    document.getElementById('comparisonDialog').classList.add('show');
  }, 10);
}

/**
 * Close comparison dialog
 */
window.closeComparisonDialog = function() {
  const dialog = document.getElementById('comparisonDialog');
  if (dialog) {
    dialog.classList.remove('show');
    setTimeout(() => dialog.remove(), 300);
  }
};

// ===================================
// Message Handlers for Unified App Integration
// ===================================

/**
 * Listen for messages from parent window (unified app)
 */
window.addEventListener('message', (event) => {
  // Handle requests from unified settings
  if (event.data.type === 'openDictionary') {
    // Open the dictionary modal with specified tab
    const tab = event.data.tab || 'items';
    
    // Open the modal directly
    openSettingsModal();
    
    // Switch to the requested tab
    setTimeout(() => {
      const tabBtn = document.querySelector(`.settings-tab[data-tab="${tab}"]`);
      if (tabBtn) {
        tabBtn.click();
      }
    }, 100);
  } else if (event.data.type === 'openRankings') {
    // Open the rankings section
    // First ensure we have data loaded
    if (!window.processedData || !window.processedData.stores) {
      console.warn('No data loaded. Please load a file first.');
      return;
    }
    
    // Switch to Adjust view which has rankings
    const adjustViewBtn = document.getElementById('adjustViewBtn');
    if (adjustViewBtn) {
      adjustViewBtn.click();
      
      // Scroll to rankings section
      setTimeout(() => {
        const rankingsSection = document.querySelector('.rankings-section');
        if (rankingsSection) {
          rankingsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }, 100);
    }
  } else if (event.data.type === 'openArchives') {
    // Open the archives modal
    if (typeof openArchiveModal === 'function') {
      openArchiveModal();
    } else {
      console.warn('Archive modal function not available');
    }
  } else if (event.data.type === 'settingsChanged') {
    // Handle settings updates from unified app
    console.log('Settings updated from unified app:', event.data.settings);
    // Add any settings handlers here if needed
  }
});
