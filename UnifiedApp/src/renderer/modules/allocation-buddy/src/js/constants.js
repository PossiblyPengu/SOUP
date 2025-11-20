/**
 * Application Constants
 * Centralized configuration and magic strings
 * These constants are available globally throughout the application
 */

// Make constants available globally
const APP_CONSTANTS = {};

// Match Types
APP_CONSTANTS.MATCH_TYPES = {
  EXACT: 'exact',
  PARTIAL: 'partial',
  FUZZY: 'fuzzy'
};

// Match Confidence Levels
APP_CONSTANTS.MATCH_CONFIDENCE = {
  NUMBER: 'number',
  SKU: 'sku',
  NAME: 'name',
  KEYWORDS: 'keywords',
  DESCRIPTION: 'description'
};

// Store Ranks
APP_CONSTANTS. STORE_RANKS = {
  AA: 'AA',
  A: 'A',
  B: 'B',
  C: 'C'
};

// View Modes
APP_CONSTANTS. VIEW_MODES = {
  STORE: 'store',
  ITEM: 'item'
};

// File Constraints
APP_CONSTANTS. FILE_CONSTRAINTS = {
  MAX_SIZE: 50 * 1024 * 1024, // 50MB
  ALLOWED_EXTENSIONS: ['.csv', '.xlsx', '.xls']
};

// History Settings
APP_CONSTANTS. HISTORY_SETTINGS = {
  MAX_STATES: 50
};

// Archive Settings
APP_CONSTANTS. ARCHIVE_SETTINGS = {
  DEFAULT_RETENTION_DAYS: 90,
  RETENTION_OPTIONS: [30, 60, 90, 180, 365]
};

// UI Settings
APP_CONSTANTS. UI_SETTINGS = {
  DEBOUNCE_DELAY: 300, // milliseconds
  ANIMATION_DURATION: 300, // milliseconds
  TOAST_DURATION: 2000, // milliseconds
  MODAL_TRANSITION: 300 // milliseconds
};

// Column Names (for CSV/Excel detection)
// Priority order matters - more specific patterns first
APP_CONSTANTS.COLUMN_NAMES = {
  STORE: [
    'store name', 'shop name',  // Prefer full names
    'loc name',                 // Location name
    'store', 'shop', 'location', // General terms
    'loc'                       // Short location code
  ],
  ITEM: [
    'item',                     // Exact match preferred
    'product',                  // Product column
    'sku',                      // SKU column
    'style',                    // Style number
    'item number', 'item code'  // Variations
  ],
  QUANTITY: [
    'qty',                      // Short form preferred
    'quantity',                 // Full form
    'amount', 'units', 'allocation' // Alternatives
  ]
};

// Warning Types
APP_CONSTANTS. WARNING_TYPES = {
  UNMATCHED_ITEM: 'unmatched_item',
  UNMATCHED_STORE: 'unmatched_store',
  FUZZY_MATCH: 'fuzzy_match',
  MISSING_DATA: 'missing_data'
};

// Dialog Types
APP_CONSTANTS. DIALOG_TYPES = {
  ERROR: 'error',
  SUCCESS: 'success',
  INFO: 'info',
  WARNING: 'warning'
};

// Error Messages
APP_CONSTANTS. ERROR_MESSAGES = {
  FILE_TOO_LARGE: 'File size exceeds 50MB limit',
  INVALID_FILE_TYPE: 'Invalid file type. Please select a CSV or Excel file',
  FILE_READ_ERROR: 'Failed to read file',
  PARSE_ERROR: 'Failed to parse file data',
  NO_DATA_FOUND: 'No data found in file',
  COLUMN_DETECTION_FAILED: 'Could not detect required columns (Store, Item, Quantity)',
  MISSING_DICTIONARY: 'Dictionary data not loaded',
  ARCHIVE_SAVE_FAILED: 'Failed to save archive',
  ARCHIVE_LOAD_FAILED: 'Failed to load archive',
  REDISTRIBUTION_FAILED: 'Failed to redistribute allocations',
  EXPORT_FAILED: 'Failed to export data'
};

// Success Messages
APP_CONSTANTS. SUCCESS_MESSAGES = {
  FILE_LOADED: 'File loaded successfully',
  DATA_SAVED: 'Data saved successfully',
  ARCHIVE_SAVED: 'Archive saved successfully',
  ARCHIVE_DELETED: 'Archive deleted successfully',
  COPIED_TO_CLIPBOARD: 'Copied to clipboard',
  REDISTRIBUTION_COMPLETE: 'Redistribution completed successfully',
  EXPORT_COMPLETE: 'Export completed successfully'
};

// Local Storage Keys
APP_CONSTANTS. STORAGE_KEYS = {
  THEME: 'theme',
  ARCHIVE_RETENTION: 'archiveRetentionDays',
  LAST_FILE_PATH: 'lastFilePath',
  VIEW_MODE: 'viewMode'
};

// CSS Classes
APP_CONSTANTS. CSS_CLASSES = {
  ACTIVE: 'active',
  HIDDEN: 'hidden',
  LOADING: 'loading',
  ERROR: 'error',
  SUCCESS: 'success',
  WARNING: 'warning',
  REDISTRIBUTED: 'redistributed',
  COLLAPSED: 'collapsed'
};

// Keyboard Shortcuts
APP_CONSTANTS. KEYBOARD_SHORTCUTS = {
  SEARCH: 'f',
  UNDO: 'z',
  REDO: 'y',
  SAVE: 's',
  CLEAR: 'Delete',
  ESCAPE: 'Escape'
};

// Export Formats
APP_CONSTANTS. EXPORT_FORMATS = {
  CSV: 'csv',
  XLSX: 'xlsx',
  JSON: 'json'
};

// Business Central Settings
APP_CONSTANTS. BUSINESS_CENTRAL = {
  COLUMN_SEPARATOR: '\t',
  ROW_SEPARATOR: '\n'
};
