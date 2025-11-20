/**
 * ExpireWise - Bundled Application (Offline Compatible)
 * All modules combined into a single file for offline use
 */

/* eslint-disable no-case-declarations */
/* global Chart, zoomPlugin, XLSX, CustomDropdown, ModernDropdown */
(function () {
  'use strict';

  // Loading message removed - use DEBUG.log for development logging

  // =======================================
  // CONSTANTS
  // =======================================

  const APP_CONSTANTS = {
    VERSION: '1.0.1',
    STORAGE_KEY: 'expireWise-data-v3',
    EXPIRY_THRESHOLD_DAYS: 60,
    MAX_RECOMMENDATIONS: 50,
    MAX_VISIBLE_NOTIFICATIONS: 5,
    SEARCH_DEBOUNCE_MS: 500,
    SAVE_DEBOUNCE_MS: 1000,
    PRINT_PREVIEW_DELAY_MS: 1000,
    CHART_UPDATE_MODE: 'none',
    RANK_PRIORITIES: {
      AA: 4,
      A: 3,
      B: 2,
      C: 1,
      D: 0,
    },
  };

  // =======================================
  // UTILITIES
  // =======================================

  // DOM utilities
  const qs = (selector, element = document) => element.querySelector(selector);
  const qsa = (selector, element = document) => element.querySelectorAll(selector);

  // Date utilities
  function getCurrentYearMonth() {
    const now = new Date();
    return {
      year: now.getFullYear(),
      month: now.getMonth(),
    };
  }

  function formatDateForInput(year, month) {
    return `${year}-${String(month + 1).padStart(2, '0')}`;
  }

  // Utility functions (currently unused but kept for future use)
  // eslint-disable-next-line no-unused-vars
  function parseDateFromInput(value) {
    const [yearStr, monthStr] = value.split('-');
    return {
      year: parseInt(yearStr, 10),
      month: parseInt(monthStr, 10) - 1,
    };
  }

  // eslint-disable-next-line no-unused-vars
  function formatMonthYear(year, month) {
    const months = [
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];
    return `${months[month]} ${year}`;
  }

  // Debug utility - conditional logging for production
  const DEBUG = {
    enabled: localStorage.getItem('debug') === 'true' || location.hostname === 'localhost',
    log: (...args) => DEBUG.enabled && DEBUG.log(...args),
    warn: (...args) => DEBUG.enabled && DEBUG.warn(...args),
    info: (...args) => DEBUG.enabled && DEBUG.info(...args),
    error: (...args) => console.error(...args), // Always log errors
    group: (...args) => DEBUG.enabled && console.group(...args),
    groupEnd: () => DEBUG.enabled && console.groupEnd(),
    table: (...args) => DEBUG.enabled && console.table(...args),
  };

  // Enable debug mode: localStorage.setItem('debug', 'true')
  // Disable debug mode: localStorage.removeItem('debug')

  // Security utilities
  function escapeHtml(unsafe) {
    if (typeof unsafe !== 'string') {
      return unsafe;
    }
    return unsafe
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // Safe JSON parse wrapper
  function safeJsonParse(str, fallback = null) {
    try {
      return JSON.parse(str);
    } catch (error) {
      DEBUG.error('JSON parse error:', error);
      return fallback;
    }
  }

  // Helper utilities
  function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }

  function generateId() {
    return `exp-${Date.now()}-${Math.random().toString(36).substring(2, 11)}`;
  }

  // Event listener cleanup helper
  class EventManager {
    constructor() {
      this.listeners = [];
    }

    addEventListener(element, event, handler, options) {
      element.addEventListener(event, handler, options);
      this.listeners.push({ element, event, handler, options });
    }

    removeAllListeners() {
      this.listeners.forEach(({ element, event, handler, options }) => {
        element.removeEventListener(event, handler, options);
      });
      this.listeners = [];
    }

    destroy() {
      this.removeAllListeners();
    }
  }

  // Base component class with automatic cleanup
  // eslint-disable-next-line no-unused-vars
  class Component {
    constructor() {
      this.eventManager = new EventManager();
      this.subscriptions = [];
    }

    // Wrapper for addEventListener that tracks listeners
    addEventListener(element, event, handler, options) {
      this.eventManager.addEventListener(element, event, handler, options);
    }

    // Wrapper for appState.subscribe that tracks subscriptions
    subscribe(event, callback) {
      const unsubscribe = appState.subscribe(event, callback);
      this.subscriptions.push(unsubscribe);
      return unsubscribe;
    }

    // Cleanup method to be called when component is destroyed
    destroy() {
      // Clean up event listeners
      this.eventManager.destroy();

      // Clean up subscriptions
      this.subscriptions.forEach(unsubscribe => unsubscribe());
      this.subscriptions = [];
    }
  }

  function formatExpiryKey(year, month) {
    return `${year}-${String(month + 1).padStart(2, '0')}`;
  }

  // Format expiry date for display (2025-11 → Nov 2025)
  function formatExpiryDisplay(expiryKey) {
    if (!expiryKey) {
      return '';
    }
    const months = [
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];
    const [year, month] = expiryKey.split('-').map(Number);
    return `${months[month - 1]} ${year}`;
  }

  function parseExpiryKey(key) {
    // Handle different input types
    if (!key || typeof key !== 'string') {
      DEBUG.warn('parseExpiryKey: Invalid key:', key);
      return { year: new Date().getFullYear(), month: new Date().getMonth() };
    }

    // If it's already in YYYY-MM format
    if (key.includes('-') && key.length <= 7) {
      const [yearStr, monthStr] = key.split('-');
      const year = parseInt(yearStr, 10);
      const month = parseInt(monthStr, 10) - 1;

      // Validate ranges
      if (isNaN(year) || year < 2000 || year > 2100) {
        DEBUG.error(`Invalid year in expiry key: ${key}`);
        return { year: new Date().getFullYear(), month: new Date().getMonth() };
      }

      if (isNaN(month) || month < 0 || month > 11) {
        DEBUG.error(`Invalid month in expiry key: ${key}`);
        return { year: new Date().getFullYear(), month: new Date().getMonth() };
      }

      return { year, month };
    }

    // If it's a date string, parse it
    try {
      const date = new Date(key);
      if (isNaN(date.getTime())) {
        throw new Error('Invalid date');
      }
      return {
        year: date.getFullYear(),
        month: date.getMonth(),
      };
    } catch {
      DEBUG.warn('parseExpiryKey: Could not parse date:', key);
      return { year: new Date().getFullYear(), month: new Date().getMonth() };
    }
  }

  // =======================================
  // CONSTANTS
  // =======================================

  const CONSTANTS = {
    STORAGE_KEY: 'expireWise-data-v3',
    FILE_HANDLE_KEY: 'expireWise-fileHandle',
    VERSION: '3.0.0',
    FEATURES: {
      ANALYTICS: true,
      PWA: true,
      AUTO_SAVE: true,
      FILE_SYSTEM: 'showSaveFilePicker' in window,
      NOTIFICATIONS: 'Notification' in window,
    },
  };

  // =======================================
  // STATE MANAGEMENT
  // =======================================

  class AppState {
    constructor() {
      this.data = {
        items: [],
        lastModified: null,
        version: CONSTANTS.VERSION,
      };

      this.ui = {
        viewYear: new Date().getFullYear(),
        viewMonth: new Date().getMonth(),
        currentTab: 'main',
        theme: 'dark',
        sortBy: 'desc',
        sortOrder: 'asc',
        groupBy: null,
        searchQuery: '',
      };

      this.dict = {
        items: [],
        stores: [],
      };

      this.listeners = new Map();
    }

    subscribe(event, callback) {
      if (!this.listeners.has(event)) {
        this.listeners.set(event, []);
      }
      this.listeners.get(event).push(callback);

      // Return unsubscribe function to prevent memory leaks
      return () => {
        const callbacks = this.listeners.get(event);
        if (callbacks) {
          const index = callbacks.indexOf(callback);
          if (index > -1) {
            callbacks.splice(index, 1);
          }
        }
      };
    }

    emit(event, data) {
      if (this.listeners.has(event)) {
        this.listeners.get(event).forEach(callback => callback(data));
      }
    }

    // Clear all listeners for cleanup
    clearListeners(event) {
      if (event) {
        this.listeners.delete(event);
      } else {
        this.listeners.clear();
      }
    }

    addItem(item) {
      item.id = item.id || generateId();
      item.createdAt = item.createdAt || new Date().toISOString();
      item.updatedAt = new Date().toISOString();

      this.data.items.push(item);
      this.data.lastModified = new Date().toISOString();

      this.emit('item:added', item);
      this.emit('data:changed', this.data);
      this.emit('save:pending');
    }

    updateItem(id, updates) {
      const item = this.data.items.find(item => item.id === id);
      if (!item) {
        return false;
      }

      Object.assign(item, updates, {
        updatedAt: new Date().toISOString(),
      });

      this.data.lastModified = new Date().toISOString();

      this.emit('item:updated', item);
      this.emit('data:changed', this.data);
      this.emit('save:pending');

      return true;
    }

    removeItem(id) {
      const index = this.data.items.findIndex(item => item.id === id);
      if (index === -1) {
        return false;
      }

      const item = this.data.items.splice(index, 1)[0];
      this.data.lastModified = new Date().toISOString();

      this.emit('item:removed', item);
      this.emit('data:changed', this.data);
      this.emit('save:pending');

      return true;
    }

    getItems() {
      return [...this.data.items];
    }

    getItem(id) {
      return this.data.items.find(item => item.id === id);
    }

    getItemsForMonth(year, month) {
      DEBUG.log('🔍 getItemsForMonth called with:', { year, month });

      if (!this.data?.items) {
        DEBUG.log('❌ No data.items found');
        return [];
      }

      DEBUG.log('📋 Total items in data:', this.data.items.length);

      const targetKey = formatExpiryKey(year, month);
      DEBUG.log('🎯 Target key for filtering:', targetKey);

      const filtered = this.data.items.filter(item => {
        const matches = item.expiry === targetKey;
        DEBUG.log(
          `🔍 Item: "${item.desc?.substring(0, 20)}" | Expiry: "${item.expiry}" | Target: "${targetKey}" | Match: ${matches}`
        );
        return matches;
      });

      DEBUG.log('✅ Filtered items count:', filtered.length);
      return filtered;
    }

    setViewDate(year, month) {
      this.ui.viewYear = year;
      this.ui.viewMonth = month;
      this.emit('view:changed', { year, month });
    }

    setCurrentTab(tab) {
      this.ui.currentTab = tab;
      this.emit('tab:changed', tab);
    }

    setTheme(theme) {
      this.ui.theme = theme;
      this.emit('theme:changed', theme);
    }

    setDictionary(dict) {
      console.log('📖 AppState.setDictionary called with:', dict?.items?.length || 0, 'items');
      this.dict = dict;
      this.emit('dictionary:loaded', dict);
      console.log('📖 AppState.dict now contains:', this.dict?.items?.length || 0, 'items');
    }

    clearData() {
      this.data = {
        items: [],
        lastModified: new Date().toISOString(),
        version: CONSTANTS.VERSION,
      };

      this.emit('data:cleared');
      this.emit('data:changed', this.data);
      this.emit('save:pending');
    }

    loadData(data) {
      if (data && data.items) {
        this.data = {
          ...data,
          version: CONSTANTS.VERSION,
          lastModified: data.lastModified || new Date().toISOString(),
        };
      }

      this.emit('data:loaded', this.data);
    }
  }

  const appState = new AppState();

  // =======================================
  // SERVICES
  // =======================================

  // Encryption Service (basic obfuscation for localStorage)
  class EncryptionService {
    constructor() {
      // Simple base64 encoding for basic obfuscation
      // Note: This is NOT secure encryption, just obfuscation
      // For production, consider using Web Crypto API with a user-provided key
      this.enabled = localStorage.getItem('expireWise-encryption') === 'true';
    }

    encode(data) {
      if (!this.enabled) {
        return data;
      }
      try {
        const jsonStr = JSON.stringify(data);
        // Use TextEncoder for proper UTF-8 encoding
        const bytes = new TextEncoder().encode(jsonStr);
        const binString = Array.from(bytes, byte => String.fromCodePoint(byte)).join('');
        return btoa(binString);
      } catch (error) {
        DEBUG.error('Encoding failed:', error);
        return data;
      }
    }

    decode(encoded) {
      if (!this.enabled) {
        return encoded;
      }
      try {
        // Use TextDecoder for proper UTF-8 decoding
        const binString = atob(encoded);
        const bytes = Uint8Array.from(binString, char => char.codePointAt(0));
        const jsonStr = new TextDecoder().decode(bytes);
        return safeJsonParse(jsonStr, null);
      } catch (error) {
        DEBUG.error('Decoding failed:', error);
        return null;
      }
    }
  }

  const encryptionService = new EncryptionService();

  // Storage Service
  class StorageService {
    constructor() {
      this.fileHandle = null;
      this.supportsFileSystem = 'showSaveFilePicker' in window;
      this.isElectron = typeof window.electronAPI !== 'undefined';
      this.dataFilePath = null;

      // Initialize data file path in Electron
      if (this.isElectron) {
        this._initDataPath();
      }
    }

    async _initDataPath() {
      try {
        // Check for custom save path first
        const customPath = localStorage.getItem('expirewise-custom-save-path');
        if (customPath) {
          this.dataFilePath = customPath;
          DEBUG.log('✓ Using custom data file path:', this.dataFilePath);
        } else {
          // Use default path
          const userDataPath = await window.electronAPI.getAppPath('userData');
          this.dataFilePath = `${userDataPath}/expirewise-data.json`;
          DEBUG.log('✓ Using default data file path:', this.dataFilePath);
        }
      } catch (error) {
        DEBUG.error('Failed to initialize data path:', error);
      }
    }

    async save(data) {
      try {
        // Encode data if encryption is enabled
        const dataToSave = encryptionService.enabled
          ? encryptionService.encode(data)
          : JSON.stringify(data);

        // Save to localStorage (as backup)
        localStorage.setItem(CONSTANTS.STORAGE_KEY, dataToSave);

        // Auto-save to file system in Electron
        if (this.isElectron && this.dataFilePath) {
          try {
            const result = await window.electronAPI.writeFile(
              this.dataFilePath,
              JSON.stringify(data, null, 2)
            );
            if (result.success) {
              DEBUG.log('✓ Data saved to file:', this.dataFilePath);
            } else {
              DEBUG.error('File save failed:', result.error);
            }
          } catch (error) {
            DEBUG.error('Electron file save error:', error);
          }
        } else if (this.fileHandle && this.supportsFileSystem) {
          // Save to file if handle exists (for web File System Access API)
          await this.saveToFile(data);
        }

        return true;
      } catch (error) {
        DEBUG.error('Save failed:', error);
        return false;
      }
    }

    async load() {
      try {
        // Try loading from Electron file system first
        if (this.isElectron && this.dataFilePath) {
          try {
            const result = await window.electronAPI.readFile(this.dataFilePath);
            if (result.success && result.data) {
              const parsed = safeJsonParse(result.data, null);
              if (parsed) {
                DEBUG.log('✓ Data loaded from file:', this.dataFilePath);
                // Also save to localStorage for backup
                const dataToStore = encryptionService.enabled
                  ? encryptionService.encode(parsed)
                  : JSON.stringify(parsed);
                localStorage.setItem(CONSTANTS.STORAGE_KEY, dataToStore);
                return parsed;
              }
            }
          } catch (error) {
            DEBUG.log('File not found or error loading, using localStorage:', error.message);
          }
        }

        // Fallback to localStorage
        const saved = localStorage.getItem(CONSTANTS.STORAGE_KEY);
        if (!saved) {
          return null;
        }

        // Decode data if encryption is enabled
        if (encryptionService.enabled) {
          return encryptionService.decode(saved);
        }

        return safeJsonParse(saved, null);
      } catch (error) {
        DEBUG.error('Load failed:', error);
        return null;
      }
    }

    async requestFileHandle() {
      if (!this.supportsFileSystem) {
        return null;
      }

      try {
        this.fileHandle = await window.showSaveFilePicker({
          suggestedName: 'expireWise-data.json',
          types: [
            {
              description: 'JSON files',
              accept: { 'application/json': ['.json'] },
            },
          ],
        });

        return this.fileHandle;
      } catch (error) {
        console.error('File handle request failed:', error);
        return null;
      }
    }

    async saveToFile(data) {
      if (!this.fileHandle) {
        return false;
      }

      try {
        const writable = await this.fileHandle.createWritable();
        await writable.write(JSON.stringify(data, null, 2));
        await writable.close();
        return true;
      } catch (error) {
        console.error('File save failed:', error);
        return false;
      }
    }

    getStatus() {
      return {
        supportsFileSystem: this.supportsFileSystem,
        hasFileHandle: !!this.fileHandle,
        hasLocalStorage: !!localStorage.getItem(CONSTANTS.STORAGE_KEY),
        isElectron: this.isElectron,
        dataFilePath: this.dataFilePath,
      };
    }

    clear() {
      localStorage.removeItem(CONSTANTS.STORAGE_KEY);
      this.fileHandle = null;
    }
  }

  const storageService = new StorageService();
  window.storageService = storageService;

  // Data Service
  class DataService {
    constructor() {
      this.saveDebounced = debounce(() => this.saveData(), 1000);

      // Auto-save on data changes
      appState.subscribe('save:pending', () => {
        this.saveDebounced();
      });

      // Save immediately before page unload to prevent data loss
      window.addEventListener('beforeunload', () => {
        // Bypass debounce and save immediately
        const data = appState.data;
        if (data && data.items && data.items.length > 0) {
          storageService.save(data);
        }
      });
    }

    async loadData() {
      try {
        const data = await storageService.load();
        if (data) {
          appState.loadData(data);
          DEBUG.log('✓ Data loaded:', data.items?.length || 0, 'items');
          return true;
        }
        return false;
      } catch (error) {
        console.error('Load data failed:', error);
        return false;
      }
    }

    async saveData() {
      try {
        const success = await storageService.save(appState.data);
        if (success) {
          appState.emit('save:completed');
        }
        return success;
      } catch (error) {
        console.error('Save data failed:', error);
        return false;
      }
    }

    async clearAllData() {
      storageService.clear();
      appState.clearData();
      notificationService.success('All data cleared');
      return true;
    }

    exportData() {
      return {
        ...appState.data,
        exportedAt: new Date().toISOString(),
      };
    }

    async importData(data, importMode = 'overwrite') {
      if (!data || !Array.isArray(data.items)) {
        throw new Error('Invalid data format');
      }

      // Validate imported items
      const validItems = data.items.filter(
        item => item.desc && item.location && item.expiry && item.units
      );

      let finalItems = [];
      const existingItems = appState.data?.items || [];

      switch (importMode) {
        case 'overwrite':
          // Replace all existing data
          finalItems = validItems;
          DEBUG.log('📥 Overwrite mode: Replacing all existing data');
          break;

        case 'merge':
          // Add all imported items to existing data
          finalItems = [...existingItems, ...validItems];
          DEBUG.log('📥 Merge mode: Adding to existing data');
          break;

        case 'update':
          // Smart merge: update existing items by SKU/Number, add new ones
          finalItems = [...existingItems];

          validItems.forEach(importedItem => {
            // Find existing item by SKU or Item Number
            const existingIndex = finalItems.findIndex(
              existing =>
                (importedItem.sku && existing.sku === importedItem.sku) ||
                (importedItem.number && existing.number === importedItem.number)
            );

            if (existingIndex >= 0) {
              // Update existing item
              finalItems[existingIndex] = {
                ...finalItems[existingIndex],
                ...importedItem,
                id: finalItems[existingIndex].id, // Keep original ID
                updatedAt: new Date().toISOString(),
              };
              DEBUG.log(`🔄 Updated existing item: ${importedItem.desc}`);
            } else {
              // Add new item
              finalItems.push(importedItem);
              DEBUG.log(`➕ Added new item: ${importedItem.desc}`);
            }
          });
          DEBUG.log('📥 Smart merge mode: Updated/added items intelligently');
          break;

        default:
          finalItems = validItems;
      }

      appState.data = {
        items: finalItems,
        lastModified: new Date().toISOString(),
        version: CONSTANTS.VERSION,
      };

      await this.saveData();

      DEBUG.log('📊 About to emit data:loaded event with items:', validItems.length);
      DEBUG.log('📊 AppState after setting data:', {
        totalItems: appState.data.items.length,
        sampleItem: appState.data.items[0],
      });

      appState.emit('data:loaded', appState.data);

      notificationService.success(`Imported ${validItems.length} items`);
    }

    getAllItems() {
      return appState.data?.items || [];
    }
  }

  const dataService = new DataService();
  window.dataService = dataService;

  // Enhanced Export Service
  class ExcelService {
    constructor() {
      this.exportFormats = {
        xlsx: 'Excel Spreadsheet',
        csv: 'CSV (Comma-separated values)',
        json: 'JSON Data File',
      };
    }

    async exportToExcel() {
      if (!window.XLSX) {
        notificationService.error('Excel library not loaded');
        return;
      }

      try {
        const items = appState.getItems();

        if (items.length === 0) {
          notificationService.warning('No items to export');
          return;
        }

        // Prepare data for export - matching your Excel format
        const exportData = items.map(item => {
          const expiryDate = new Date(item.expiry);
          const currentDate = new Date();
          const daysUntilExpiry = Math.ceil((expiryDate - currentDate) / (1000 * 60 * 60 * 24));

          let urgency = 'Low';
          if (daysUntilExpiry < 0) {
            urgency = 'Expired';
          } else if (daysUntilExpiry <= 30) {
            urgency = 'Critical';
          } else if (daysUntilExpiry <= 60) {
            urgency = 'High';
          }

          return {
            'Item Description': item.desc,
            SKU: item.sku || '',
            'Item Number': item.number || '',
            Location: item.location,
            'Units Expiring': item.units,
            'Expiry Month': expiryDate.toLocaleDateString('en-US', { month: 'long' }),
            'Expiry Year': expiryDate.getFullYear(),
            'Expiry Date': expiryDate.toLocaleDateString('en-US'),
            'Days Until Expiry': daysUntilExpiry,
            Urgency: urgency,
          };
        });

        // Create workbook and worksheet
        const wb = window.XLSX.utils.book_new();
        const ws = window.XLSX.utils.json_to_sheet(exportData);

        // Add worksheet to workbook
        window.XLSX.utils.book_append_sheet(wb, ws, 'Expiring Items');

        // Generate filename
        const timestamp = new Date().toISOString().split('T')[0];
        const defaultFilename = `ExpireWise_Export_${timestamp}.xlsx`;

        // Use Electron file dialog
        if (window.electronAPI && window.electronAPI.saveFile) {
          const filePath = await window.electronAPI.saveFile({
            defaultPath: defaultFilename,
            filters: [
              { name: 'Excel Files', extensions: ['xlsx'] },
              { name: 'All Files', extensions: ['*'] }
            ]
          });

          if (filePath) {
            // Write workbook to buffer
            const wbout = window.XLSX.write(wb, { type: 'array', bookType: 'xlsx' });
            
            // Save using Electron API
            await window.electronAPI.writeFile(filePath, wbout);
            
            notificationService.success(`Exported ${items.length} items to ${filePath.split(/[\\/]/).pop()}`);
          }
        } else {
          // Fallback for non-Electron environment
          window.XLSX.writeFile(wb, defaultFilename);
          notificationService.success(`Exported ${items.length} items to ${defaultFilename}`);
        }
      } catch (error) {
        console.error('Excel export failed:', error);
        notificationService.error(`Export failed: ${error.message}`);
      }
    }

    async importFromExcel(file, importMode = 'overwrite') {
      if (!window.XLSX) {
        notificationService.error('Excel library not loaded');
        return;
      }

      try {
        const arrayBuffer = await file.arrayBuffer();
        const workbook = window.XLSX.read(arrayBuffer, { type: 'array' });

        // Get first worksheet
        const firstSheetName = workbook.SheetNames[0];
        const worksheet = workbook.Sheets[firstSheetName];

        // Convert to JSON
        const jsonData = window.XLSX.utils.sheet_to_json(worksheet);

        if (jsonData.length === 0) {
          notificationService.warning('No data found in Excel file');
          return;
        }

        // Debug: Show column names and first row
        DEBUG.log('📊 Excel columns found:', Object.keys(jsonData[0] || {}));
        DEBUG.log('📊 First row sample:', jsonData[0]);

        // Transform data to app format - matching your Excel column names
        const items = jsonData
          .map(row => {
            let expiryDate = row['Expiry Date'] || row['Expiry'] || '';

            // Convert expiry date to app format (YYYY-M)
            if (expiryDate) {
              DEBUG.log(
                `🔍 Raw expiry date from Excel: "${expiryDate}" (type: ${typeof expiryDate})`
              );

              try {
                let date;

                // Handle Excel date numbers (Excel stores dates as numbers since 1900-01-01)
                if (typeof expiryDate === 'number') {
                  // Excel date number to JavaScript Date
                  // Excel epoch is 1899-12-30 (not 1900-01-01) due to leap year bug
                  const epoch = new Date(1899, 11, 30);
                  date = new Date(epoch.getTime() + expiryDate * 86400000);

                  // Handle Excel 1900 leap year bug
                  if (expiryDate < 60) {
                    date.setDate(date.getDate() - 1);
                  }

                  DEBUG.log(`📅 Converted Excel number ${expiryDate} to date: ${date}`);
                } else {
                  // Try to parse as string
                  date = new Date(expiryDate);
                  DEBUG.log(`📅 Parsed string "${expiryDate}" to date: ${date}`);
                }

                if (!isNaN(date.getTime())) {
                  const year = date.getFullYear();
                  const month = date.getMonth();
                  const formattedKey = formatExpiryKey(year, month);
                  DEBUG.log(`📅 Final conversion: Date(${year}, ${month}) → "${formattedKey}"`);
                  expiryDate = formattedKey;
                } else {
                  DEBUG.warn(`❌ Invalid date result: ${date}`);
                  expiryDate = ''; // Clear invalid date
                }
              } catch (error) {
                console.error('❌ Date conversion error:', error);
                expiryDate = ''; // Clear on error
              }
            } else {
              DEBUG.log('⚠️ No expiry date provided');
            }

            return {
              id: generateId(),
              number: row['Item Number'] || '',
              sku: row['SKU'] || '',
              desc: row['Item Description'] || '',
              location: row['Location'] || '',
              units: parseInt(row['Units Expiring'] || 1, 10),
              expiry: expiryDate,
              createdAt: new Date().toISOString(),
              updatedAt: new Date().toISOString(),
            };
          })
          .filter(item => item.desc && item.location && item.expiry);

        if (items.length === 0) {
          notificationService.warning('No valid items found in Excel file');
          return;
        }

        // Import data
        DEBUG.log('📥 About to import items:', items.length);
        DEBUG.log('📥 Sample item before import:', items[0]);
        DEBUG.log('📥 Import mode:', importMode);

        await dataService.importData({ items }, importMode);

        DEBUG.log('✅ Import completed successfully');

        // Debug appState after import
        DEBUG.log('📋 AppState after import:', {
          totalItems: appState.data?.items?.length || 0,
          currentView: { viewYear: appState.ui.viewYear, viewMonth: appState.ui.viewMonth },
          sampleItem: appState.data?.items?.[0],
        });

        // Note: UI will be updated automatically via data:loaded event emitted by dataService.importData()
      } catch (error) {
        console.error('Excel import failed:', error);
        notificationService.error(`Import failed: ${error.message}`);
      }
    }

    // Enhanced export methods
    async showExportDialog() {
      // Get user preferences for default values
      let defaultFormat = 'xlsx';
      let defaultDataScope = 'all';

      if (window.userPreferences) {
        const preferences = window.userPreferences.getPreferences();
        defaultFormat = preferences.export.defaultFormat;
        defaultDataScope = preferences.export.defaultScope;
      }

      return new Promise(resolve => {
        const modal = document.createElement('div');
        modal.className = 'alert-modal-overlay';
        modal.innerHTML = `
          <div class="alert-modal">
            <div class="alert-modal-header">
              <h3>📊 Export Data</h3>
              <button class="close-alert-modal" title="Close">×</button>
            </div>
            <div class="alert-modal-body">
              <div class="export-options">
                <div class="export-section">
                  <h4>Data Selection</h4>
                  <div class="export-data-options">
                    <label>
                      <input type="radio" name="dataScope" value="all" ${defaultDataScope === 'all' ? 'checked' : ''}>
                      All items (${appState.getItems().length} items)
                    </label>
                    <label id="filteredOption" style="display: none;">
                      <input type="radio" name="dataScope" value="filtered">
                      Filtered items (<span id="filteredCount">0</span> items)
                    </label>
                    <label>
                      <input type="radio" name="dataScope" value="current" ${defaultDataScope === 'current' ? 'checked' : ''}>
                      Current month only
                    </label>
                    <label>
                      <input type="radio" name="dataScope" value="range" ${defaultDataScope === 'range' ? 'checked' : ''}>
                      Date range
                    </label>
                  </div>
                  
                  <div class="date-range-section" id="dateRangeSection" style="display: none;">
                    <div class="date-inputs">
                      <input type="month" id="exportFromDate" placeholder="From" />
                      <span>to</span>
                      <input type="month" id="exportToDate" placeholder="To" />
                    </div>
                  </div>
                </div>

                <div class="export-section">
                  <h4>Export Format</h4>
                  <div class="export-format-options">
                    <label>
                      <input type="radio" name="exportFormat" value="xlsx" ${defaultFormat === 'xlsx' ? 'checked' : ''}>
                      📊 Excel Spreadsheet (.xlsx)
                    </label>
                    <label>
                      <input type="radio" name="exportFormat" value="csv" ${defaultFormat === 'csv' ? 'checked' : ''}>
                      📄 CSV File (.csv)
                    </label>
                    <label>
                      <input type="radio" name="exportFormat" value="json">
                      🔗 JSON Data (.json)
                    </label>
                  </div>
                </div>

                <div class="export-section">
                  <h4>Include Columns</h4>
                  <div class="column-options">
                    <label><input type="checkbox" value="number" checked> Item Number</label>
                    <label><input type="checkbox" value="sku" checked> SKU</label>
                    <label><input type="checkbox" value="desc" checked> Description</label>
                    <label><input type="checkbox" value="location" checked> Location</label>
                    <label><input type="checkbox" value="units" checked> Units</label>
                    <label><input type="checkbox" value="expiry" checked> Expiry Date</label>
                    <label><input type="checkbox" value="created" checked> Date Created</label>
                    <label><input type="checkbox" value="updated"> Date Updated</label>
                  </div>
                </div>
              </div>
            </div>
            <div class="alert-modal-footer">
              <button class="btn small outline" id="cancelExport">Cancel</button>
              <button class="btn small" id="previewExport">Preview</button>
              <button class="btn small accent" id="performExport">📥 Export</button>
            </div>
          </div>
        `;

        document.body.appendChild(modal);

        // Handle filtered items option
        const searchComponent = window.app?.components?.search;
        if (searchComponent && searchComponent.hasActiveFilters()) {
          const filteredOption = modal.querySelector('#filteredOption');
          const filteredCount = modal.querySelector('#filteredCount');
          const filteredItems = searchComponent.getFilteredItemsCount();

          filteredCount.textContent = filteredItems;
          filteredOption.style.display = 'block';
        }

        // Event handlers
        modal.querySelector('input[value="range"]').addEventListener('change', e => {
          const rangeSection = modal.querySelector('#dateRangeSection');
          rangeSection.style.display = e.target.checked ? 'block' : 'none';
        });

        modal.querySelector('.close-alert-modal').addEventListener('click', () => {
          document.body.removeChild(modal);
          resolve(null);
        });

        modal.querySelector('#cancelExport').addEventListener('click', () => {
          document.body.removeChild(modal);
          resolve(null);
        });

        modal.querySelector('#previewExport').addEventListener('click', () => {
          const options = this.getExportOptions(modal);
          this.showExportPreview(options);
        });

        modal.querySelector('#performExport').addEventListener('click', () => {
          const options = this.getExportOptions(modal);
          document.body.removeChild(modal);
          resolve(options);
        });
      });
    }

    getExportOptions(modal) {
      const dataScope = modal.querySelector('input[name="dataScope"]:checked').value;
      const format = modal.querySelector('input[name="exportFormat"]:checked').value;

      const columns = Array.from(modal.querySelectorAll('.column-options input:checked')).map(
        input => input.value
      );

      const options = {
        dataScope,
        format,
        columns,
        fromDate: modal.querySelector('#exportFromDate').value,
        toDate: modal.querySelector('#exportToDate').value,
      };

      return options;
    }

    getExportData(options) {
      let items = [];

      switch (options.dataScope) {
        case 'all':
          items = appState.getItems();
          break;
        case 'filtered':
          const searchComponent = window.app?.components?.search;
          items = searchComponent ? searchComponent.getFilteredItems() : appState.getItems();
          break;
        case 'current':
          const { viewYear, viewMonth } = appState.ui;
          items = appState.getItemsForMonth(viewYear, viewMonth);
          break;
        case 'range':
          items = appState.getItems().filter(item => {
            if (!options.fromDate && !options.toDate) {
              return true;
            }
            if (options.fromDate && item.expiry < options.fromDate) {
              return false;
            }
            if (options.toDate && item.expiry > options.toDate) {
              return false;
            }
            return true;
          });
          break;
      }

      return items;
    }

    formatExportData(items, options) {
      const columnMappings = {
        number: 'Item Number',
        sku: 'Item SKU',
        desc: 'Item Description',
        location: 'Location',
        units: 'Units Expiring',
        expiry: 'Expiry Date',
        created: 'Date Created',
        updated: 'Date Updated',
      };

      return items.map(item => {
        const row = {};

        options.columns.forEach(col => {
          switch (col) {
            case 'number':
              row[columnMappings.number] = item.number || '';
              break;
            case 'sku':
              row[columnMappings.sku] = item.sku || '';
              break;
            case 'desc':
              row[columnMappings.desc] = item.desc;
              break;
            case 'location':
              row[columnMappings.location] = item.location;
              break;
            case 'units':
              row[columnMappings.units] = item.units;
              break;
            case 'expiry':
              row[columnMappings.expiry] = item.expiry;
              break;
            case 'created':
              row[columnMappings.created] = item.createdAt
                ? new Date(item.createdAt).toLocaleDateString()
                : '';
              break;
            case 'updated':
              row[columnMappings.updated] = item.updatedAt
                ? new Date(item.updatedAt).toLocaleDateString()
                : '';
              break;
          }
        });

        return row;
      });
    }

    async performExport(options) {
      try {
        const items = this.getExportData(options);
        const formattedData = this.formatExportData(items, options);

        if (formattedData.length === 0) {
          notificationService.warning('No data to export');
          return;
        }

        const timestamp = new Date().toISOString().split('T')[0];
        const basename = `ExpireWise_Export_${timestamp}`;

        switch (options.format) {
          case 'xlsx':
            await this.exportAsExcel(formattedData, basename);
            break;
          case 'csv':
            this.exportAsCSV(formattedData, basename);
            break;
          case 'json':
            this.exportAsJSON(items, basename);
            break;
        }

        notificationService.success(
          `Exported ${items.length} items as ${options.format.toUpperCase()}`
        );
      } catch (error) {
        console.error('Export failed:', error);
        notificationService.error(`Export failed: ${error.message}`);
      }
    }

    async exportAsExcel(data, basename) {
      if (!window.XLSX) {
        throw new Error('Excel library not loaded');
      }

      const wb = window.XLSX.utils.book_new();
      const ws = window.XLSX.utils.json_to_sheet(data);

      // Auto-size columns
      const cols = Object.keys(data[0] || {}).map(() => ({ wch: 15 }));
      ws['!cols'] = cols;

      window.XLSX.utils.book_append_sheet(wb, ws, 'Expiring Items');
      window.XLSX.writeFile(wb, `${basename}.xlsx`);
    }

    exportAsCSV(data, basename) {
      if (data.length === 0) {
        return;
      }

      const headers = Object.keys(data[0]);
      const csvContent = [
        headers.join(','),
        ...data.map(row =>
          headers
            .map(header => {
              const value = row[header] || '';
              // Escape CSV values
              return typeof value === 'string' && value.includes(',')
                ? `"${value.replace(/"/g, '""')}"`
                : value;
            })
            .join(',')
        ),
      ].join('\n');

      this.downloadFile(csvContent, `${basename}.csv`, 'text/csv');
    }

    exportAsJSON(data, basename) {
      const jsonContent = JSON.stringify(
        {
          exported: new Date().toISOString(),
          version: CONSTANTS.VERSION,
          itemCount: data.length,
          items: data,
        },
        null,
        2
      );

      this.downloadFile(jsonContent, `${basename}.json`, 'application/json');
    }

    downloadFile(content, filename, mimeType) {
      const blob = new Blob([content], { type: mimeType });
      const url = URL.createObjectURL(blob);

      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      a.style.display = 'none';

      document.body.appendChild(a);
      a.click();

      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }

    showExportPreview(options) {
      const items = this.getExportData(options);
      const previewData = items.slice(0, 5); // Preview first 5 rows

      const modal = document.createElement('div');
      modal.className = 'alert-modal-overlay';
      modal.innerHTML = `
        <div class="alert-modal">
          <div class="alert-modal-header">
            <h3>📋 Export Preview</h3>
            <button class="close-alert-modal">×</button>
          </div>
          <div class="alert-modal-body">
            <p><strong>${items.length}</strong> items will be exported in <strong>${options.format.toUpperCase()}</strong> format.</p>
            <div class="preview-table-container">
              <table class="preview-table">
                <thead>
                  <tr>
                    ${options.columns.map(col => `<th>${col}</th>`).join('')}
                  </tr>
                </thead>
                <tbody>
                  ${previewData
                    .map(
                      item => `
                    <tr>
                      ${options.columns.map(col => `<td>${item[col] || ''}</td>`).join('')}
                    </tr>
                  `
                    )
                    .join('')}
                </tbody>
              </table>
              ${items.length > 5 ? `<p class="preview-note">... and ${items.length - 5} more items</p>` : ''}
            </div>
          </div>
          <div class="alert-modal-footer">
            <button class="btn small" id="closePreview">Close</button>
          </div>
        </div>
      `;

      document.body.appendChild(modal);

      modal.querySelector('.close-alert-modal').addEventListener('click', () => {
        document.body.removeChild(modal);
      });

      modal.querySelector('#closePreview').addEventListener('click', () => {
        document.body.removeChild(modal);
      });
    }
  }

  const excelService = new ExcelService();
  window.excelService = excelService;

  // Enhanced Notification Service

  // Modern Notification Service - Built from scratch
  class NotificationService {
    constructor() {
      this.container = null;
      this.notifications = [];
      this.maxVisible = 5;
      this.init();
    }

    init() {
      this.injectStyles();
      this.createContainer();
    }

    injectStyles() {
      if (document.getElementById('toast-notifications-styles')) {
        return;
      }

      const style = document.createElement('style');
      style.id = 'toast-notifications-styles';
      style.textContent = `
        .toast-container {
          position: fixed;
          top: 20px;
          right: 20px;
          z-index: 999999;
          display: flex;
          flex-direction: column;
          gap: 12px;
          pointer-events: none;
        }

        .toast {
          pointer-events: all;
          min-width: 320px;
          max-width: 450px;
          background: var(--bg-primary);
          border-radius: 12px;
          padding: 16px 20px;
          box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2), 0 4px 12px rgba(0, 0, 0, 0.1);
          display: flex;
          align-items: flex-start;
          gap: 12px;
          transform: translateX(450px);
          opacity: 0;
          transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
          border-left: 4px solid var(--border-color);
          position: relative;
          overflow: hidden;
        }

        .toast.show {
          transform: translateX(0);
          opacity: 1;
        }

        .toast.hide {
          transform: translateX(450px);
          opacity: 0;
          transition: all 0.2s ease-in;
        }

        .toast-icon {
          width: 24px;
          height: 24px;
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 14px;
          flex-shrink: 0;
          font-weight: bold;
        }

        .toast.success {
          border-left-color: var(--bg-success);
        }

        .toast.success .toast-icon {
          background: var(--bg-success);
          color: white;
        }

        .toast.error {
          border-left-color: var(--bg-danger);
        }

        .toast.error .toast-icon {
          background: var(--bg-danger);
          color: white;
        }

        .toast.warning {
          border-left-color: var(--bg-warning);
        }

        .toast.warning .toast-icon {
          background: var(--bg-warning);
          color: var(--text-primary);
        }

        .toast.info {
          border-left-color: var(--bg-accent);
        }

        .toast.info .toast-icon {
          background: var(--bg-accent);
          color: white;
        }

        .toast-content {
          flex: 1;
          min-width: 0;
        }

        .toast-message {
          color: var(--text-primary);
          font-size: 14px;
          line-height: 1.5;
          word-wrap: break-word;
          margin: 0;
        }

        .toast-close {
          width: 24px;
          height: 24px;
          border: none;
          background: transparent;
          color: var(--text-secondary);
          cursor: pointer;
          border-radius: 6px;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 18px;
          padding: 0;
          opacity: 0.6;
          transition: all 0.2s;
          flex-shrink: 0;
        }

        .toast-close:hover {
          opacity: 1;
          background: rgba(0, 0, 0, 0.1);
        }

        .toast-progress {
          position: absolute;
          bottom: 0;
          left: 0;
          height: 3px;
          background: currentColor;
          opacity: 0.3;
          transition: width linear;
        }

        .toast.success .toast-progress {
          color: var(--bg-success);
        }

        .toast.error .toast-progress {
          color: var(--bg-danger);
        }

        .toast.warning .toast-progress {
          color: var(--bg-warning);
        }

        .toast.info .toast-progress {
          color: var(--bg-accent);
        }

        @media (max-width: 640px) {
          .toast-container {
            top: 12px;
            left: 12px;
            right: 12px;
          }

          .toast {
            min-width: auto;
            max-width: none;
          }
        }
      `;
      document.head.appendChild(style);
    }

    createContainer() {
      this.container = document.createElement('div');
      this.container.className = 'toast-container';
      document.body.appendChild(this.container);
    }

    show(message, type = 'info', duration = 4000) {
      // Remove oldest if at max
      if (this.notifications.length >= this.maxVisible) {
        this.dismiss(this.notifications[0]);
      }

      const toast = this.createToast(message, type, duration);
      this.notifications.push(toast);
      this.container.appendChild(toast);

      // Trigger entrance animation
      requestAnimationFrame(() => {
        toast.classList.add('show');
      });

      // Auto-dismiss
      if (duration > 0) {
        const progress = toast.querySelector('.toast-progress');
        if (progress) {
          progress.style.width = '100%';
          progress.style.transition = `width ${duration}ms linear`;

          requestAnimationFrame(() => {
            requestAnimationFrame(() => {
              progress.style.width = '0%';
            });
          });
        }

        setTimeout(() => {
          this.dismiss(toast);
        }, duration);
      }

      return toast;
    }

    createToast(message, type, duration) {
      const icons = {
        success: '✓',
        error: '✕',
        warning: '⚠',
        info: 'ℹ',
      };

      const toast = document.createElement('div');
      toast.className = `toast ${type}`;
      toast.setAttribute('role', 'alert');
      toast.setAttribute('aria-live', type === 'error' ? 'assertive' : 'polite');

      const icon = document.createElement('div');
      icon.className = 'toast-icon';
      icon.textContent = icons[type] || icons.info;

      const content = document.createElement('div');
      content.className = 'toast-content';

      const msg = document.createElement('p');
      msg.className = 'toast-message';
      msg.textContent = message;

      content.appendChild(msg);

      const closeBtn = document.createElement('button');
      closeBtn.className = 'toast-close';
      closeBtn.innerHTML = '×';
      closeBtn.setAttribute('aria-label', 'Close notification');
      closeBtn.onclick = () => this.dismiss(toast);

      const progress = document.createElement('div');
      progress.className = 'toast-progress';

      toast.appendChild(icon);
      toast.appendChild(content);
      toast.appendChild(closeBtn);
      if (duration > 0) {
        toast.appendChild(progress);
      }

      // Click to dismiss
      toast.addEventListener('click', e => {
        if (e.target !== closeBtn) {
          this.dismiss(toast);
        }
      });

      return toast;
    }

    dismiss(toast) {
      if (!toast || !toast.parentElement) {
        return;
      }

      toast.classList.remove('show');
      toast.classList.add('hide');

      setTimeout(() => {
        if (toast.parentElement) {
          toast.parentElement.removeChild(toast);
          this.notifications = this.notifications.filter(t => t !== toast);
        }
      }, 200);
    }

    success(message, duration) {
      return this.show(message, 'success', duration);
    }

    error(message, duration = 6000) {
      return this.show(message, 'error', duration);
    }

    warning(message, duration) {
      return this.show(message, 'warning', duration);
    }

    info(message, duration) {
      return this.show(message, 'info', duration);
    }

    clear() {
      this.notifications.forEach(toast => this.dismiss(toast));
    }
  }

  const notificationService = new NotificationService();
  window.notificationService = notificationService;

  // Analytics Service
  class AnalyticsService {
    getExpiryStats() {
      const items = appState.getItems();
      const now = new Date();
      const currentMonth = formatExpiryKey(now.getFullYear(), now.getMonth());

      const stats = {
        total: items.length,
        thisMonth: 0,
        nextMonth: 0,
        expired: 0,
        locations: {},
        topItems: {},
      };

      items.forEach(item => {
        // Skip items without required fields
        if (!item.location || !item.desc || !item.expiry) {
          DEBUG.warn('Analytics: Skipping item with missing fields:', item);
          return;
        }

        // Count by location
        stats.locations[item.location] =
          (stats.locations[item.location] || 0) + parseInt(item.units || 1, 10);

        // Count by item description
        stats.topItems[item.desc] =
          (stats.topItems[item.desc] || 0) + parseInt(item.units || 1, 10);

        // Count by expiry period
        const itemDate = parseExpiryKey(item.expiry);
        const itemKey = formatExpiryKey(itemDate.year, itemDate.month);

        if (itemKey < currentMonth) {
          stats.expired += parseInt(item.units || 1, 10);
        } else if (itemKey === currentMonth) {
          stats.thisMonth += parseInt(item.units || 1, 10);
        } else {
          const nextMonthDate = new Date(now.getFullYear(), now.getMonth() + 1);
          const nextMonthKey = formatExpiryKey(
            nextMonthDate.getFullYear(),
            nextMonthDate.getMonth()
          );
          if (itemKey === nextMonthKey) {
            stats.nextMonth += parseInt(item.units || 1, 10);
          }
        }
      });

      return stats;
    }

    getTimelineData() {
      const items = appState.getItems();
      const timeline = {};

      items.forEach(item => {
        const key = item.expiry;
        if (!timeline[key]) {
          timeline[key] = 0;
        }
        timeline[key] += parseInt(item.units || 1, 10);
      });

      // Sort by date and return as array
      return Object.entries(timeline)
        .sort((a, b) => a[0].localeCompare(b[0]))
        .map(([date, count]) => ({ date, count }));
    }

    getTopItems(limit = 10) {
      const stats = this.getExpiryStats();
      return Object.entries(stats.topItems)
        .sort((a, b) => b[1] - a[1])
        .slice(0, limit)
        .map(([item, count]) => ({ item, count }));
    }

    getLocationBreakdown() {
      const stats = this.getExpiryStats();
      return Object.entries(stats.locations)
        .sort((a, b) => b[1] - a[1])
        .map(([location, count]) => ({ location, count }));
    }
  }

  const analyticsService = new AnalyticsService();
  window.analyticsService = analyticsService;

  // Recommendations Service
  class RecommendationsService {
    constructor() {
      // Use centralized rank priorities
      this.rankPriority = APP_CONSTANTS.RANK_PRIORITIES;
    }

    getStoreRank(locationName) {
      // Check if dictionary is loaded with stores
      if (!appState.dict || !appState.dict.stores || !Array.isArray(appState.dict.stores)) {
        DEBUG.warn('💡 Store dictionary not loaded! Using default rank C for all stores.');
        return 'C';
      }
      
      const location = appState.dict.stores.find(loc => loc.name === locationName);
      if (!location) {
        DEBUG.warn(`💡 Location "${locationName}" not found in stores dictionary, using rank C`);
        return 'C';
      }
      
      // Return rank, defaulting to C if not specified
      return location.rank || 'C';
    }

    getRankPriority(rank) {
      // Support all rank types including AA
      return this.rankPriority[rank] || 0; // Unknown ranks get lowest priority
    }

    getTransferRecommendations() {
      const items = appState.getItems();
      DEBUG.log('💡 Recommendations: Total items:', items.length);
      
      // Log store dictionary status
      if (!appState.dict || !appState.dict.stores) {
        DEBUG.warn('💡 Store dictionary not loaded! Recommendations require store ranks.');
        DEBUG.log('💡 appState.dict:', appState.dict);
      } else {
        DEBUG.log('💡 Store dictionary loaded with', appState.dict.stores.length, 'stores');
        DEBUG.log('💡 Sample stores:', appState.dict.stores.slice(0, 3));
      }

      const today = new Date();
      const sixtyDaysFromNow = new Date(today);
      sixtyDaysFromNow.setDate(today.getDate() + 60);

      // Group items by location, item name, and expiry month
      const itemsByLocation = {};

      let itemsExpiringSoon = 0;
      let itemsChecked = 0;
      items.forEach(item => {
        itemsChecked++;
        const expiryDate = new Date(`${item.expiry}-01`);
        const daysUntilExpiry = Math.ceil((expiryDate - today) / (1000 * 60 * 60 * 24));
        
        // Log first few items for debugging
        if (itemsChecked <= 3) {
          DEBUG.log(`💡 Item "${item.desc}" at "${item.location}" expires ${item.expiry} (${daysUntilExpiry} days)`);
        }

        // Skip items that expired more than 30 days ago (but include current month items)
        if (daysUntilExpiry < -30) {
          return;
        }

        if (!itemsByLocation[item.location]) {
          itemsByLocation[item.location] = {};
        }

        const itemName = item.desc || 'Unknown Item';
        const itemNumber = item.number || '';

        if (!itemsByLocation[item.location][itemName]) {
          itemsByLocation[item.location][itemName] = {};
        }

        // Group by expiry month
        const expiryMonth = item.expiry; // Already in YYYY-MM format

        if (!itemsByLocation[item.location][itemName][expiryMonth]) {
          itemsByLocation[item.location][itemName][expiryMonth] = {
            units: 0,
            daysUntilExpiry,
            expiryDate: item.expiry,
            itemNumber,
          };
        }

        const units = parseInt(item.units, 10) || 1;
        itemsByLocation[item.location][itemName][expiryMonth].units += units;

        if (daysUntilExpiry <= 60) {
          itemsExpiringSoon++;
        }
      });

      DEBUG.log('💡 Items expiring within 60 days:', itemsExpiringSoon);
      DEBUG.log('💡 Locations found:', Object.keys(itemsByLocation).length);

      // Generate transfer recommendations
      const recommendations = [];

      let transfersConsidered = 0;
      let rankMatches = 0;

      Object.keys(itemsByLocation).forEach(fromLocation => {
        const fromRank = this.getStoreRank(fromLocation);
        const fromPriority = this.getRankPriority(fromRank);

        if (rankMatches < 3) {
          DEBUG.log(
            `💡 Location "${fromLocation}" has rank: ${fromRank} (priority: ${fromPriority})`
          );
          rankMatches++;
        }

        Object.keys(itemsByLocation[fromLocation]).forEach(itemName => {
          const itemByMonth = itemsByLocation[fromLocation][itemName];

          // Process each expiry month separately
          Object.keys(itemByMonth).forEach(expiryMonth => {
            const itemData = itemByMonth[expiryMonth];

            // Only recommend transfers for items expiring within 60 days
            if (itemData.daysUntilExpiry <= 60 && itemData.units > 0) {
              // Find higher ranked stores and calculate their inventory levels
              const higherRankedStores = [];
              Object.keys(itemsByLocation).forEach(toLocation => {
                if (toLocation !== fromLocation) {
                  const toRank = this.getStoreRank(toLocation);
                  const toPriority = this.getRankPriority(toRank);

                  transfersConsidered++;

                  // Transfer from lower to higher ranked stores
                  if (toPriority > fromPriority) {
                    // Calculate total inventory at destination store for this item
                    let destinationInventory = 0;
                    if (itemsByLocation[toLocation] && itemsByLocation[toLocation][itemName]) {
                      Object.keys(itemsByLocation[toLocation][itemName]).forEach(month => {
                        destinationInventory += itemsByLocation[toLocation][itemName][month].units;
                      });
                    }

                    higherRankedStores.push({
                      location: toLocation,
                      rank: toRank,
                      priority: toPriority,
                      currentInventory: destinationInventory,
                    });
                  }
                }
              });

              // Sort by current inventory (lowest first) to prioritize stores with less stock
              higherRankedStores.sort((a, b) => a.currentInventory - b.currentInventory);

              // Distribute units among higher ranked stores, prioritizing those with lower inventory
              if (higherRankedStores.length > 0) {
                const unitsPerStore = Math.ceil(itemData.units / higherRankedStores.length);

                higherRankedStores.forEach(store => {
                  // Adjust priority based on inventory gap (stores with less inventory get higher priority)
                  const inventoryFactor =
                    store.currentInventory === 0 ? 2 : 1 / (1 + store.currentInventory / 10);

                  recommendations.push({
                    item: itemName,
                    itemNumber: itemData.itemNumber,
                    fromLocation,
                    toLocation: store.location,
                    units: unitsPerStore,
                    daysUntilExpiry: itemData.daysUntilExpiry,
                    expiryDate: itemData.expiryDate,
                    fromRank,
                    toRank: store.rank,
                    destinationInventory: store.currentInventory,
                    priority:
                      (store.priority - fromPriority) *
                      unitsPerStore *
                      (61 - Math.max(itemData.daysUntilExpiry, 0)) *
                      inventoryFactor,
                  });
                });
              }
            }
          });
        });
      });

      DEBUG.log('💡 Transfers considered:', transfersConsidered);

      // Sort by priority (highest first)
      recommendations.sort((a, b) => b.priority - a.priority);

      DEBUG.log('💡 Recommendations: Generated', recommendations.length, 'recommendations');
      if (recommendations.length > 0) {
        DEBUG.log('💡 Top recommendation:', recommendations[0]);
      } else {
        DEBUG.log('💡 No recommendations generated. Possible reasons:');
        DEBUG.log('  - No items expiring within 60 days');
        DEBUG.log('  - All items are in same-ranked stores');
        DEBUG.log('  - No items loaded in inventory');
      }

      // Limit to top 50 recommendations
      return recommendations.slice(0, 50);
    }
  }

  const recommendationsService = new RecommendationsService();
  window.recommendationsService = recommendationsService;

  // User Preferences Service
  class UserPreferencesService {
    constructor() {
      this.defaultPreferences = {
        // Table preferences
        tableColumns: {
          number: { visible: true, order: 0, width: 'auto' },
          sku: { visible: true, order: 1, width: 'auto' },
          desc: { visible: true, order: 2, width: 'auto' },
          location: { visible: true, order: 3, width: 'auto' },
          units: { visible: true, order: 4, width: 'auto' },
          expiry: { visible: true, order: 5, width: 'auto' },
          actions: { visible: true, order: 6, width: 'auto' },
        },

        // Display preferences
        display: {
          theme: 'light', // light, dark, auto
        },
        defaultView: 'main', // main, analytics, recommendations
        itemsPerPage: 50,
        dateFormat: 'YYYY-MM',

        // Behavior preferences
        autoSave: true,
        confirmDeletes: true,
        showWelcomeMessage: true,

        // Notification preferences (integrated with NotificationService)
        notifications: {
          enabled: true,
          position: 'top-right', // top-right, top-left, bottom-right, bottom-left
          duration: 3000,
          sound: false,
        },

        // Export preferences
        defaultExportFormat: 'xlsx',
        includeMetadata: true,

        // Search preferences
        searchHistory: [],
        maxSearchHistory: 10,
        savedFilters: {},
      };

      this.preferences = this.loadPreferences();
    }

    loadPreferences() {
      try {
        const saved = localStorage.getItem('expireWise-preferences');
        if (saved) {
          const parsed = safeJsonParse(saved, null);
          if (parsed) {
            return this.mergePreferences(this.defaultPreferences, parsed);
          }
        }
      } catch (error) {
        DEBUG.error('Failed to load preferences:', error);
      }
      return { ...this.defaultPreferences };
    }

    mergePreferences(defaults, saved) {
      const merged = { ...defaults };

      Object.keys(saved).forEach(key => {
        if (
          typeof defaults[key] === 'object' &&
          defaults[key] !== null &&
          !Array.isArray(defaults[key])
        ) {
          merged[key] = { ...defaults[key], ...saved[key] };
        } else {
          merged[key] = saved[key];
        }
      });

      return merged;
    }

    savePreferences() {
      try {
        localStorage.setItem('expireWise-preferences', JSON.stringify(this.preferences));
        appState.emit('preferences:changed', this.preferences);
      } catch (error) {
        console.error('Failed to save preferences:', error);
      }
    }

    get(key, defaultValue = null) {
      const keys = key.split('.');
      let value = this.preferences;

      for (const k of keys) {
        value = value?.[k];
        if (value === undefined) {
          break;
        }
      }

      return value !== undefined ? value : defaultValue;
    }

    set(key, value) {
      const keys = key.split('.');
      const lastKey = keys.pop();
      let target = this.preferences;

      // Navigate to the parent object
      for (const k of keys) {
        if (!(k in target) || typeof target[k] !== 'object') {
          target[k] = {};
        }
        target = target[k];
      }

      target[lastKey] = value;
      this.savePreferences();
    }

    getPreferences() {
      return this.preferences;
    }

    showPreferencesDialog() {
      const modal = document.createElement('div');
      modal.className = 'alert-modal-overlay';
      modal.style.opacity = '0';
      modal.innerHTML = `
        <div class="alert-modal preferences-modal">
          <div class="alert-modal-header">
            <h3>⚙️ Settings & Preferences</h3>
            <button class="close-alert-modal" title="Close">×</button>
          </div>
          <div class="alert-modal-body">
            <div class="preferences-tabs">
              <button class="pref-tab-btn active" data-tab="display">🎨 Display</button>
              <button class="pref-tab-btn" data-tab="table">📋 Table</button>
              <button class="pref-tab-btn" data-tab="behavior">⚡ Behavior</button>
              <button class="pref-tab-btn" data-tab="notifications">🔔 Notifications</button>
              <button class="pref-tab-btn" data-tab="export">📊 Export</button>
            </div>

            <div class="preferences-content">
              <!-- Display Preferences -->
              <div class="pref-tab-content active" id="display-tab">
                <div class="pref-section">
                  <h4>Theme & Appearance</h4>
                  <div class="pref-group">
                    <label for="themePref">Theme:</label>
                    <select id="themePref">
                      <option value="light">Light Theme</option>
                      <option value="dark">Dark Theme</option>
                      <option value="auto">Auto (System)</option>
                    </select>
                  </div>
                  <div class="pref-group">
                    <label for="defaultViewPref">Default tab on startup:</label>
                    <select id="defaultViewPref">
                      <option value="main">Main Dashboard</option>
                      <option value="analytics">Analytics</option>
                      <option value="recommendations">Recommendations</option>
                    </select>
                  </div>
                  <div class="pref-group">
                    <label for="itemsPerPagePref">Items per page:</label>
                    <select id="itemsPerPagePref">
                      <option value="25">25 items</option>
                      <option value="50">50 items</option>
                      <option value="100">100 items</option>
                      <option value="200">200 items</option>
                    </select>
                  </div>
                </div>
              </div>

              <!-- Table Preferences -->
              <div class="pref-tab-content" id="table-tab" style="display: none;">
                <div class="pref-section">
                  <h4>Table Columns</h4>
                  <div class="column-config">
                    <div class="column-item">
                      <input type="checkbox" id="col-number" checked>
                      <label for="col-number">Item Number</label>
                    </div>
                    <div class="column-item">
                      <input type="checkbox" id="col-sku" checked>
                      <label for="col-sku">SKU</label>
                    </div>
                    <div class="column-item">
                      <input type="checkbox" id="col-desc" checked>
                      <label for="col-desc">Description</label>
                    </div>
                    <div class="column-item">
                      <input type="checkbox" id="col-location" checked>
                      <label for="col-location">Location</label>
                    </div>
                    <div class="column-item">
                      <input type="checkbox" id="col-units" checked>
                      <label for="col-units">Units</label>
                    </div>
                    <div class="column-item">
                      <input type="checkbox" id="col-expiry" checked>
                      <label for="col-expiry">Expiry</label>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Behavior Preferences -->
              <div class="pref-tab-content" id="behavior-tab" style="display: none;">
                <div class="pref-section">
                  <h4>General Behavior</h4>
                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="autoSavePref">
                      Enable auto-save
                    </label>
                  </div>
                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="confirmDeletesPref">
                      Confirm before deleting items
                    </label>
                  </div>
                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="welcomeMessagePref">
                      Show welcome message on startup
                    </label>
                  </div>
                </div>
              </div>

              <!-- Notification Preferences -->
              <div class="pref-tab-content" id="notifications-tab" style="display: none;">
                <div class="pref-section">
                  <h4>General Notifications</h4>
                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="notificationsEnabledPref">
                      Enable notifications
                    </label>
                  </div>
                  <div class="pref-group">
                    <label for="notificationPositionPref">Notification position:</label>
                    <select id="notificationPositionPref">
                      <option value="top-right">Top Right</option>
                      <option value="top-left">Top Left</option>
                      <option value="bottom-right">Bottom Right</option>
                      <option value="bottom-left">Bottom Left</option>
                    </select>
                  </div>
                  <div class="pref-group">
                    <label for="notificationDurationPref">Display duration:</label>
                    <select id="notificationDurationPref">
                      <option value="2000">2 seconds</option>
                      <option value="3000">3 seconds</option>
                      <option value="5000">5 seconds</option>
                      <option value="10000">10 seconds</option>
                    </select>
                  </div>
                </div>

                <div class="pref-section">
                  <h4>Alert Settings</h4>
                  <div class="pref-group">
                    <label for="expiringDaysPref">Alert for items expiring within:</label>
                    <select id="expiringDaysPref">
                      <option value="7">7 days</option>
                      <option value="14">14 days</option>
                      <option value="30">30 days</option>
                      <option value="60">60 days</option>
                      <option value="90">90 days</option>
                    </select>
                  </div>

                  <div class="pref-group">
                    <label for="lowStockThresholdPref">Low stock threshold:</label>
                    <input type="number" id="lowStockThresholdPref" min="1" max="1000" 
                           placeholder="e.g., 10" />
                  </div>

                  <div class="pref-group">
                    <label for="alertFrequencyPref">Alert frequency:</label>
                    <select id="alertFrequencyPref">
                      <option value="daily">Daily</option>
                      <option value="weekly">Weekly</option>
                      <option value="disabled">Disabled</option>
                    </select>
                  </div>

                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="expiredEnabledPref" />
                      Show expired item alerts
                    </label>
                  </div>

                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="showOnStartupPref" />
                      Show alerts on application startup
                    </label>
                  </div>
                </div>
              </div>

              <!-- Export Preferences -->
              <div class="pref-tab-content" id="export-tab" style="display: none;">
                <div class="pref-section">
                  <h4>Default Export Settings</h4>
                  <div class="pref-group">
                    <label for="defaultExportFormatPref">Default export format:</label>
                    <select id="defaultExportFormatPref">
                      <option value="xlsx">Excel (.xlsx)</option>
                      <option value="csv">CSV (.csv)</option>
                      <option value="json">JSON (.json)</option>
                    </select>
                  </div>
                  <div class="pref-group">
                    <label>
                      <input type="checkbox" id="includeMetadataPref">
                      Include metadata in exports
                    </label>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="alert-modal-footer">
            <button class="btn outline small" id="resetPreferences">Reset to Defaults</button>
            <button class="btn ghost small" id="cancelPreferences">Cancel</button>
            <button class="btn small" id="savePreferences">Save Changes</button>
          </div>
        </div>
      `;

      document.body.appendChild(modal);

      // Use requestAnimationFrame to defer non-critical operations and reduce lag
      requestAnimationFrame(() => {
        // Fade in modal smoothly
        modal.style.transition = 'opacity 0.2s ease';
        modal.style.opacity = '1';

        // Defer heavy operations
        requestAnimationFrame(() => {
          // Force unified button styling
          this.forceUnifiedButtonStyling(modal);

          // Populate current values
          this.populatePreferencesForm(modal);

          // Setup tab switching
          this.setupPreferencesTabSwitching(modal);

          // Set first tab as active by default
          const firstTab = modal.querySelector('.pref-tab-btn');
          if (firstTab && !modal.querySelector('.pref-tab-btn.active')) {
            firstTab.classList.add('active');
            const firstTabContent = modal.querySelector(`#${firstTab.dataset.tab}-tab`);
            if (firstTabContent) {
              firstTabContent.classList.add('active');
              firstTabContent.style.display = 'block';
            }
          }
        });
      });

      // Event handlers
      modal.querySelector('.close-alert-modal').addEventListener('click', () => {
        document.body.removeChild(modal);
      });

      modal.querySelector('#cancelPreferences').addEventListener('click', () => {
        document.body.removeChild(modal);
      });

      modal.querySelector('#savePreferences').addEventListener('click', () => {
        const saveBtn = modal.querySelector('#savePreferences');
        this.showSaveButtonFeedback(saveBtn, () => {
          this.savePreferencesFromForm(modal);
          this.applyPreferences(); // Apply preferences immediately
          notificationService.success('Preferences saved successfully');
          // Delay modal removal until save feedback animation completes
          setTimeout(() => {
            if (modal && modal.parentNode) {
              document.body.removeChild(modal);
            }
          }, 2000);
        });
      });

      modal.querySelector('#resetPreferences').addEventListener('click', () => {
        if (confirm('Reset all preferences to default values? This cannot be undone.')) {
          const resetBtn = modal.querySelector('#resetPreferences');
          this.showSaveButtonFeedback(resetBtn, () => {
            this.preferences = { ...this.defaultPreferences };
            this.savePreferences();
            notificationService.success('Preferences reset to defaults');
            // Delay modal removal until save feedback animation completes
            setTimeout(() => {
              if (modal && modal.parentNode) {
                document.body.removeChild(modal);
              }
            }, 2000);
          });
        }
      });
    }

    setupPreferencesTabSwitching(modal) {
      const tabBtns = modal.querySelectorAll('.pref-tab-btn');
      const tabContents = modal.querySelectorAll('.pref-tab-content');

      DEBUG.log('🔧 Setting up preferences tabs:', {
        buttons: tabBtns.length,
        contents: tabContents.length,
      });

      tabBtns.forEach(btn => {
        btn.addEventListener('click', () => {
          const tabName = btn.dataset.tab;
          DEBUG.log(`📋 Switching to tab: ${tabName}`);

          // Remove active class from all buttons
          tabBtns.forEach(b => b.classList.remove('active'));

          // Add active class to clicked button
          btn.classList.add('active');

          // Update active content
          tabContents.forEach(content => {
            content.classList.remove('active');
            content.style.display = 'none';
            const expectedId = `${tabName}-tab`;
            if (content.id === expectedId) {
              content.classList.add('active');
              content.style.display = 'block';
              DEBUG.log(`✅ Activated content: ${content.id}`);
            }
          });
        });
      });
    }

    forceUnifiedButtonStyling(modal) {
      // Apply unified styling to both buttons AND preference tabs
      setTimeout(() => {
        // Style the footer buttons
        const buttons = modal.querySelectorAll('.alert-modal-footer button');
        DEBUG.log('🔍 Found footer buttons:', buttons.length);

        buttons.forEach((btn, index) => {
          DEBUG.log(`Button ${index}:`, btn.className, btn.textContent.trim());

          // Clear all existing styles and classes
          btn.removeAttribute('style');
          btn.className = '';

          // Apply the correct unified classes based on the button's purpose
          if (btn.textContent.includes('Save')) {
            btn.className = 'btn small';
          } else if (btn.textContent.includes('Cancel')) {
            btn.className = 'btn ghost small';
          } else if (btn.textContent.includes('Reset')) {
            btn.className = 'btn outline small';
          } else {
            btn.className = 'btn small'; // Default
          }

          // Force the exact same styling as other unified buttons in the app
          const baseStyles = `
            appearance: none !important;
            border: 1px solid transparent !important;
            cursor: pointer !important;
            user-select: none !important;
            padding: 10px 14px !important;
            border-radius: 10px !important;
            font-weight: 500 !important;
            font-size: 12px !important;
            letter-spacing: 0.2px !important;
            transition: all 0.3s cubic-bezier(0.25, 0.46, 0.45, 0.94) !important;
            display: inline-flex !important;
            align-items: center !important;
            gap: 8px !important;
            position: relative !important;
            overflow: hidden !important;
            text-shadow: 0 1px 2px color-mix(in srgb, var(--shadow-color) 30%, transparent) !important;
            backdrop-filter: blur(8px) !important;
            -webkit-backdrop-filter: blur(8px) !important;
          `;

          if (btn.classList.contains('outline')) {
            btn.style.cssText = `${baseStyles}
              background: linear-gradient(135deg, color-mix(in srgb, var(--bg-card) 70%, transparent), color-mix(in srgb, var(--bg-card) 50%, var(--bg-secondary) 50%)) !important;
              color: var(--text-primary) !important;
              border: 1px solid color-mix(in srgb, var(--border-light) 40%, transparent) !important;
              box-shadow: 0 1px 3px color-mix(in srgb, var(--shadow-color) 12%, transparent), inset 0 1px 0 color-mix(in srgb, var(--text-primary) 3%, transparent) !important;
            `;
          } else if (btn.classList.contains('ghost')) {
            btn.style.cssText = `${baseStyles}
              background: linear-gradient(135deg, color-mix(in srgb, var(--bg-card) 60%, transparent), color-mix(in srgb, var(--bg-card) 40%, var(--bg-secondary) 60%)) !important;
              border: 1px solid color-mix(in srgb, var(--border-light) 30%, transparent) !important;
              color: color-mix(in srgb, var(--text-primary) 80%, transparent) !important;
              box-shadow: 0 1px 3px color-mix(in srgb, var(--shadow-color) 12%, transparent), inset 0 1px 0 color-mix(in srgb, var(--text-primary) 3%, transparent) !important;
            `;
          } else {
            // Primary button styling
            btn.style.cssText = `${baseStyles}
              background: linear-gradient(135deg, var(--accent), color-mix(in srgb, var(--accent) 85%, var(--accent-2) 15%)) !important;
              color: var(--text-inverse) !important;
              box-shadow: 0 1px 3px color-mix(in srgb, var(--shadow-color) 12%, transparent), inset 0 1px 0 color-mix(in srgb, var(--text-primary) 3%, transparent) !important;
            `;
          }
        });

        // Now style the preference tab buttons to match the main navigation tabs exactly
        const tabButtons = modal.querySelectorAll('.pref-tab-btn');
        DEBUG.log('🔍 Found preference tab buttons:', tabButtons.length);

        tabButtons.forEach((tabBtn, index) => {
          DEBUG.log(`Tab ${index}:`, tabBtn.className, tabBtn.textContent.trim());

          // Apply the exact same styling as main navigation tabs
          const tabBaseStyles = `
            flex: 1 !important;
            padding: 14px 18px !important;
            background: linear-gradient(135deg, color-mix(in srgb, var(--bg-card) 70%, transparent), color-mix(in srgb, var(--bg-card) 50%, var(--bg-secondary) 50%)) !important;
            border: 1px solid color-mix(in srgb, var(--border-light) 30%, transparent) !important;
            border-bottom: 1px solid color-mix(in srgb, var(--border-light) 50%, transparent) !important;
            border-radius: 12px 12px 0 0 !important;
            font-weight: 500 !important;
            font-size: 13px !important;
            color: color-mix(in srgb, var(--text-primary) 70%, transparent) !important;
            cursor: pointer !important;
            transition: all 0.3s cubic-bezier(0.25, 0.46, 0.45, 0.94) !important;
            position: relative !important;
            white-space: nowrap !important;
            text-shadow: 0 1px 2px color-mix(in srgb, black 30%, transparent) !important;
            backdrop-filter: blur(8px) !important;
            -webkit-backdrop-filter: blur(8px) !important;
            overflow: hidden !important;
          `;

          tabBtn.style.cssText = tabBaseStyles;

          // Add ::after element for accent bar (CSS handles this via pseudo-elements)

          // Handle active state
          if (tabBtn.classList.contains('active')) {
            tabBtn.style.cssText = `${tabBaseStyles}
              background: linear-gradient(135deg, var(--bg-card), color-mix(in srgb, var(--bg-card) 98%, var(--accent) 2%)) !important;
              border-color: color-mix(in srgb, var(--border-light) 60%, transparent) !important;
              border-bottom: none !important;
              color: var(--text-primary) !important;
              font-weight: 600 !important;
              transform: translateY(1px) !important;
              box-shadow: 0 -2px 8px color-mix(in srgb, black 5%, transparent), inset 0 1px 0 color-mix(in srgb, var(--text-primary) 5%, transparent) !important;
              z-index: 10 !important;
            `;
          }
        });

        DEBUG.log('✅ Applied unified styling to both buttons and preference tabs');
      }, 100);
    }

    showSaveButtonFeedback(button, onComplete) {
      if (!button) {
        return;
      }

      const originalText = button.textContent;
      const originalClasses = button.className;
      const originalStyle = button.getAttribute('style') || '';

      DEBUG.log('🎯 Save feedback starting for button:', originalText);

      // Clear any existing inline styles that might conflict
      button.removeAttribute('style');

      // Show saving state
      button.className = `${originalClasses} saving`;
      button.textContent = 'Saving...';

      // Force the saving colors with inline styles as backup
      button.style.cssText = `
        background: linear-gradient(135deg, #fbbf24, #f59e0b) !important;
        color: white !important;
        border: 1px solid #f59e0b !important;
        pointer-events: none !important;
        transform: scale(0.98) !important;
        box-shadow: 0 2px 8px rgba(251, 191, 36, 0.3) !important;
        transition: all 0.3s ease !important;
      `;

      DEBUG.log('🟡 Applied saving state');

      // Simulate save process (brief delay)
      setTimeout(() => {
        // Show success state
        button.className = `${originalClasses} save-success`;
        button.textContent = 'Saved!';

        // Force the success colors with inline styles as backup
        button.style.cssText = `
          background: linear-gradient(135deg, #10b981, #059669) !important;
          color: white !important;
          border: 1px solid #10b981 !important;
          transform: scale(1.05) !important;
          box-shadow: 0 0 20px rgba(16, 185, 129, 0.6) !important;
          transition: all 0.3s ease !important;
        `;

        DEBUG.log('🟢 Applied success state');

        // Execute the actual save function
        if (onComplete) {
          onComplete();
        }

        // Restore original state after animation
        setTimeout(() => {
          if (button && button.parentNode) {
            // Check if button still exists
            button.className = originalClasses;
            button.textContent = originalText;
            button.setAttribute('style', originalStyle);
            DEBUG.log('↩️ Restored original button state');
          }
        }, 1500);
      }, 500);
    }

    populatePreferencesForm(modal) {
      // Display preferences - get the actual current active theme
      const currentTheme =
        localStorage.getItem('appTheme') ||
        document.documentElement.getAttribute('data-theme') ||
        'dark';
      modal.querySelector('#themePref').value = currentTheme;
      modal.querySelector('#defaultViewPref').value = this.get('defaultView');
      modal.querySelector('#itemsPerPagePref').value = this.get('itemsPerPage');

      // Table preferences
      Object.keys(this.get('tableColumns')).forEach(col => {
        const checkbox = modal.querySelector(`#col-${col}`);
        if (checkbox) {
          checkbox.checked = this.get(`tableColumns.${col}.visible`);
        }
      });

      // Behavior preferences
      modal.querySelector('#autoSavePref').checked = this.get('autoSave');
      modal.querySelector('#confirmDeletesPref').checked = this.get('confirmDeletes');
      modal.querySelector('#welcomeMessagePref').checked = this.get('showWelcomeMessage');

      // Notification preferences
      modal.querySelector('#notificationsEnabledPref').checked = this.get('notifications.enabled');
      modal.querySelector('#notificationPositionPref').value = this.get('notifications.position');
      modal.querySelector('#notificationDurationPref').value = this.get('notifications.duration');

      // Alert settings (from notificationService)
      if (window.notificationService && notificationService.alertSettings) {
        modal.querySelector('#expiringDaysPref').value =
          notificationService.alertSettings.expiringInDays;
        modal.querySelector('#lowStockThresholdPref').value =
          notificationService.alertSettings.lowStockThreshold;
        modal.querySelector('#alertFrequencyPref').value =
          notificationService.alertSettings.alertFrequency;
        modal.querySelector('#expiredEnabledPref').checked =
          notificationService.alertSettings.expiredEnabled;
        modal.querySelector('#showOnStartupPref').checked =
          notificationService.alertSettings.showOnStartup;
      }

      // Export preferences
      modal.querySelector('#defaultExportFormatPref').value = this.get('defaultExportFormat');
      modal.querySelector('#includeMetadataPref').checked = this.get('includeMetadata');
    }

    savePreferencesFromForm(modal) {
      // Display preferences
      this.set('display.theme', modal.querySelector('#themePref').value);
      this.set('defaultView', modal.querySelector('#defaultViewPref').value);
      this.set('itemsPerPage', parseInt(modal.querySelector('#itemsPerPagePref').value, 10));

      // Table preferences
      Object.keys(this.get('tableColumns')).forEach(col => {
        const checkbox = modal.querySelector(`#col-${col}`);
        if (checkbox) {
          this.set(`tableColumns.${col}.visible`, checkbox.checked);
        }
      });

      // Behavior preferences
      this.set('autoSave', modal.querySelector('#autoSavePref').checked);
      this.set('confirmDeletes', modal.querySelector('#confirmDeletesPref').checked);
      this.set('showWelcomeMessage', modal.querySelector('#welcomeMessagePref').checked);

      // Notification preferences
      this.set('notifications.enabled', modal.querySelector('#notificationsEnabledPref').checked);
      this.set('notifications.position', modal.querySelector('#notificationPositionPref').value);
      this.set(
        'notifications.duration',
        parseInt(modal.querySelector('#notificationDurationPref').value, 10)
      );

      // Alert settings (save to notificationService)
      if (window.notificationService && notificationService.alertSettings) {
        notificationService.alertSettings.expiringInDays = parseInt(
          modal.querySelector('#expiringDaysPref').value,
          10
        );
        notificationService.alertSettings.lowStockThreshold = parseInt(
          modal.querySelector('#lowStockThresholdPref').value,
          10
        );
        notificationService.alertSettings.alertFrequency =
          modal.querySelector('#alertFrequencyPref').value;
        notificationService.alertSettings.expiredEnabled =
          modal.querySelector('#expiredEnabledPref').checked;
        notificationService.alertSettings.showOnStartup =
          modal.querySelector('#showOnStartupPref').checked;
        notificationService.saveSettings();
      }

      // Export preferences
      this.set('defaultExportFormat', modal.querySelector('#defaultExportFormatPref').value);
      this.set('includeMetadata', modal.querySelector('#includeMetadataPref').checked);
    }

    // Utility methods for common preferences
    getTableColumnConfig() {
      return this.get('tableColumns');
    }

    isColumnVisible(columnName) {
      return this.get(`tableColumns.${columnName}.visible`, true);
    }

    getNotificationSettings() {
      return this.get('notifications');
    }

    getDefaultExportFormat() {
      return this.get('defaultExportFormat');
    }

    applyPreferences() {
      // Apply table column preferences to ItemsTable if it exists
      if (window.app?.components?.table) {
        window.app.components.table.render();
      }

      // Apply theme preferences
      if (
        this.preferences?.display?.theme &&
        this.preferences.display.theme !== document.documentElement.getAttribute('data-theme')
      ) {
        document.documentElement.setAttribute('data-theme', this.preferences.display.theme);
        if (window.appState) {
          appState.setTheme(this.preferences.display.theme);
        }
        localStorage.setItem('appTheme', this.preferences.display.theme);

        // Update theme toggle if it exists
        const themeToggle = document.querySelector('#themeToggle');
        if (themeToggle && this.preferences?.display?.theme) {
          themeToggle.checked = this.preferences.display.theme === 'dark';
        }
      }

      // Emit event that preferences have been applied
      if (window.appState) {
        appState.emit('preferences:applied', this.preferences);
      }
    }

    resetToDefaults() {
      localStorage.removeItem(this.storageKey);
      this.preferences = { ...this.defaultPreferences };

      // Apply defaults immediately
      this.applyPreferences();
    }
  }

  // UserPreferences will be initialized in ExpireWiseApp init

  // =======================================
  // UI COMPONENTS
  // =======================================

  // Advanced Search Component
  // TODO: Fully integrate Component base class for memory leak prevention
  // Current implementation has memory leak risk from unremoved event listeners
  class AdvancedSearch {
    constructor(container) {
      this.container = container;
      this.filters = {
        search: '',
        location: '',
        dateRange: 'all',
        sortBy: 'desc',
        sortOrder: 'asc',
        unitsMin: '',
        unitsMax: '',
        itemNumber: '',
        sku: '',
        dateFrom: '',
        dateTo: '',
      };
      this.isVisible = false;
      this.debouncedSearch = this.debounce(() => this.performSearch(), 300);

      this.init();
    }

    debounce(func, wait) {
      let timeout;
      return function executedFunction(...args) {
        const later = () => {
          clearTimeout(timeout);
          func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
      };
    }

    init() {
      this.createSearchInterface();
      this.bindEvents();

      // Subscribe to data changes
      appState.subscribe('item:added', () => this.updateFilterOptions());
      appState.subscribe('item:updated', () => this.updateFilterOptions());
      appState.subscribe('item:removed', () => this.updateFilterOptions());
      appState.subscribe('data:loaded', () => this.updateFilterOptions());
    }

    createSearchInterface() {
      const searchHTML = `
        <div class="advanced-search-container">
          <!-- Search Header -->
          <div class="search-header">
            <div class="search-main-row">
              <div class="quick-search-group">
                <input 
                  type="text" 
                  id="advancedSearch" 
                  placeholder="🔍 Search items, locations, SKUs... (Ctrl+F to focus, Esc to clear, Enter to search)" 
                  class="quick-search-input"
                  autocomplete="off"
                />
                <button id="clearSearch" title="Clear search">✕</button>
              </div>
              
              <div class="search-controls">
                <button class="btn small outline" id="toggleAdvanced">
                  <span id="advancedToggleText">Show Filters</span>
                  <span id="advancedToggleIcon">▼</span>
                </button>
                <button class="btn small" id="resetFilters" title="Clear all filters (Ctrl+Shift+R)">
                  Reset All
                </button>
                <span id="active-filter-count" class="active-filter-badge" style="display: none;">
                  0 filters
                </span>
              </div>
            </div>
            
            <!-- Quick Filter Buttons -->
            <div class="quick-filters">
              <button class="quick-filter-btn" data-filter="expiring">⚠️ Expiring Soon</button>
              <button class="quick-filter-btn" data-filter="expired">🔴 Expired</button>
              <button class="quick-filter-btn" data-filter="thisMonth">📅 This Month</button>
              <button class="quick-filter-btn" data-filter="nextMonth">⏭️ Next Month</button>
              <button class="quick-filter-btn" data-filter="lowStock">📦 Low Stock (≤10)</button>
              <button class="quick-filter-btn" data-filter="highStock">📈 High Stock (≥100)</button>
              <button class="quick-filter-btn" data-filter="noSku">🔍 No SKU</button>
            </div>
          </div>

          <!-- Advanced Filters Panel -->
          <div class="advanced-filters" id="advancedFilters" style="display: none;">
            <div class="filters-grid">
              
              <!-- Location Filter -->
              <div class="filter-group">
                <label for="locationFilter">Location</label>
                <select id="locationFilter" class="filter-select">
                  <option value="">All Locations</option>
                </select>
              </div>

              <!-- Item Number Filter -->
              <div class="filter-group">
                <label for="itemNumberFilter">Item Number</label>
                <input type="text" id="itemNumberFilter" class="filter-input" 
                       placeholder="e.g., ST100" list="itemNumbersList" />
                <datalist id="itemNumbersList"></datalist>
              </div>

              <!-- SKU Filter -->
              <div class="filter-group">
                <label for="skuFilter">SKU</label>
                <input type="text" id="skuFilter" class="filter-input" 
                       placeholder="e.g., 128570" />
              </div>

              <!-- Date Range Filter -->
              <div class="filter-group">
                <label for="dateRangeFilter">Expiry Range</label>
                <select id="dateRangeFilter" class="filter-select">
                  <option value="all">All Dates</option>
                  <option value="expired">Already Expired</option>
                  <option value="thisMonth">This Month</option>
                  <option value="nextMonth">Next Month</option>
                  <option value="next3Months">Next 3 Months</option>
                  <option value="next6Months">Next 6 Months</option>
                  <option value="future">Beyond 6 Months</option>
                  <option value="custom">Custom Range</option>
                </select>
              </div>

              <!-- Custom Date Range -->
              <div class="filter-group date-range-custom" id="customDateRange" style="display: none;">
                <label>Custom Date Range</label>
                <div class="date-range-inputs">
                  <input type="month" id="dateFromFilter" class="filter-input" placeholder="From" />
                  <span>to</span>
                  <input type="month" id="dateToFilter" class="filter-input" placeholder="To" />
                </div>
              </div>

              <!-- Units Range Filter -->
              <div class="filter-group">
                <label>Units Range</label>
                <div class="range-inputs">
                  <input type="number" id="unitsMinFilter" class="filter-input" 
                         placeholder="Min" min="0" />
                  <span>to</span>
                  <input type="number" id="unitsMaxFilter" class="filter-input" 
                         placeholder="Max" min="0" />
                </div>
              </div>

              <!-- Sort Options -->
              <div class="filter-group">
                <label for="sortByFilter">Sort By</label>
                <select id="sortByFilter" class="filter-select">
                  <option value="desc">Description</option>
                  <option value="expiry">Expiry Date</option>
                  <option value="location">Location</option>
                  <option value="units">Units</option>
                  <option value="sku">SKU</option>
                  <option value="number">Item Number</option>
                </select>
              </div>

              <div class="filter-group">
                <label for="sortOrderFilter">Order</label>
                <select id="sortOrderFilter" class="filter-select">
                  <option value="asc">Ascending</option>
                  <option value="desc">Descending</option>
                </select>
              </div>

              <!-- Filter Actions -->
              <div class="filter-actions">
                <button class="btn small" id="applyFilters">Apply Filters</button>
                <button class="btn small outline" id="resetFilters2">Reset All</button>
                <button class="btn small ghost" id="savePreset">💾 Save Preset</button>
              </div>
            </div>

            <!-- Active Filters Display -->
            <div class="active-filters" id="activeFiltersContainer" style="display: none;">
              <span class="active-filters-label">Active filters:</span>
              <div class="active-filters-list" id="activeFiltersList"></div>
              <button class="btn small ghost" id="clearAllFilters">Clear All</button>
            </div>
          </div>

          <!-- Search Results Summary -->
          <div class="search-results-summary" id="searchSummary">
            <span id="resultsCount">All items</span>
          </div>
        </div>
      `;

      this.container.innerHTML = searchHTML;
      this.updateFilterOptions();
    }

    bindEvents() {
      // Search input with debouncing
      const searchInput = qs('#advancedSearch', this.container);
      const clearSearchBtn = qs('#clearSearch', this.container);
      
      if (searchInput) {
        // Update clear button visibility based on input
        const updateClearButton = () => {
          if (clearSearchBtn) {
            clearSearchBtn.style.opacity = searchInput.value ? '1' : '0.5';
            clearSearchBtn.style.cursor = searchInput.value ? 'pointer' : 'default';
          }
        };
        
        searchInput.addEventListener('input', e => {
          this.filters.search = e.target.value;
          updateClearButton();
          this.debouncedSearch();
        });
        
        // Keyboard shortcuts for search input
        searchInput.addEventListener('keydown', (e) => {
          if (e.key === 'Escape') {
            searchInput.value = '';
            this.filters.search = '';
            updateClearButton();
            this.performSearch();
            searchInput.blur();
          }
          if (e.key === 'Enter') {
            this.performSearch();
          }
        });
        
        // Initialize clear button state
        updateClearButton();
      }

      // Toggle advanced filters
      const searchToggle = qs('#toggleAdvanced', this.container);
      if (searchToggle) {
        searchToggle.addEventListener('click', () => {
          this.toggleAdvancedFilters();
        });
      }

      // Clear search
      const clearSearch = qs('#clearSearch', this.container);
      if (clearSearch) {
        clearSearch.addEventListener('click', () => {
          if (searchInput && searchInput.value) {
            searchInput.value = '';
            this.filters.search = '';
            searchInput.focus();
            this.performSearch();
          }
        });
      }

      // Global keyboard shortcut: Ctrl+F to focus search (All Items tab only)
      document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
          const activeTab = localStorage.getItem('activeTab');
          if (activeTab === 'all-items' && searchInput) {
            e.preventDefault();
            searchInput.focus();
            searchInput.select();
          }
        }
        // Ctrl+Shift+R to reset all filters
        if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'R') {
          const activeTab = localStorage.getItem('activeTab');
          if (activeTab === 'all-items') {
            e.preventDefault();
            this.resetAllFilters();
          }
        }
      });

      // Filter controls - bind all the new inputs
      const filterInputs = [
        { id: '#locationFilter', key: 'location', event: 'change' },
        { id: '#itemNumberFilter', key: 'itemNumber', event: 'input' },
        { id: '#skuFilter', key: 'sku', event: 'input' },
        { id: '#dateRangeFilter', key: 'dateRange', event: 'change' },
        { id: '#dateFromFilter', key: 'dateFrom', event: 'change' },
        { id: '#dateToFilter', key: 'dateTo', event: 'change' },
        { id: '#unitsMinFilter', key: 'unitsMin', event: 'input' },
        { id: '#unitsMaxFilter', key: 'unitsMax', event: 'input' },
        { id: '#sortByFilter', key: 'sortBy', event: 'change' },
        { id: '#sortOrderFilter', key: 'sortOrder', event: 'change' },
      ];

      filterInputs.forEach(({ id, key, event }) => {
        const element = qs(id, this.container);
        if (element) {
          element.addEventListener(event, e => {
            this.filters[key] = e.target.value;

            // Special handling for date range
            if (key === 'dateRange') {
              const customRange = qs('#customDateRange', this.container);
              if (customRange) {
                customRange.style.display = e.target.value === 'custom' ? 'block' : 'none';
              }
            }

            this.debouncedSearch();
          });
        }
      });

      // Filter action buttons
      qs('#resetFilters', this.container)?.addEventListener('click', () => this.resetAllFilters());
      qs('#resetFilters2', this.container)?.addEventListener('click', () => this.resetAllFilters());
      qs('#savePreset', this.container)?.addEventListener('click', () => this.saveFilterPreset());
      qs('#clearAllFilters', this.container)?.addEventListener('click', () =>
        this.resetAllFilters()
      );

      // Quick filters with active state toggle
      qsa('.quick-filter-btn', this.container).forEach(btn => {
        btn.addEventListener('click', () => {
          // Toggle active state
          const isActive = btn.classList.contains('active');
          
          // Remove active from all quick filters
          qsa('.quick-filter-btn', this.container).forEach(b => b.classList.remove('active'));
          
          if (!isActive) {
            btn.classList.add('active');
            this.applyQuickFilter(btn.dataset.filter);
          } else {
            // If was active, deactivate and reset
            this.resetAllFilters();
          }
        });
      });
    }

    updateFilterOptions() {
      // Update location options
      const locationFilter = qs('#locationFilter', this.container);
      if (locationFilter) {
        const items = appState.getItems();
        const locations = [...new Set(items.map(item => item.location).filter(Boolean))];

        locationFilter.innerHTML = '<option value="">All Locations</option>';
        locations.forEach(location => {
          const option = document.createElement('option');
          option.value = location;
          option.textContent = location;
          locationFilter.appendChild(option);
        });
      }

      // Update item numbers list
      const itemNumbersList = qs('#itemNumbersList', this.container);
      if (itemNumbersList) {
        const items = appState.getItems();
        const numbers = [...new Set(items.map(item => item.number).filter(Boolean))];
        itemNumbersList.innerHTML = numbers
          .map(num => `<option value="${escapeHtml(num)}">`)
          .join('');
      }
    }

    performSearch() {
      let results = [...appState.getItems()];

      // Apply text search across multiple fields
      if (this.filters.search.trim()) {
        const searchTerm = this.filters.search.toLowerCase();

        // Handle special case for "no SKU" search
        if (searchTerm.includes('missing sku') || searchTerm.includes('no sku')) {
          results = results.filter(item => !item.sku || item.sku.trim() === '');
        } else {
          results = results.filter(item => {
            const searchableText = [item.desc, item.number, item.sku, item.location]
              .filter(Boolean)
              .join(' ')
              .toLowerCase();

            return searchableText.includes(searchTerm);
          });
        }
      }

      // Apply other filters
      if (this.filters.location) {
        results = results.filter(item => item.location === this.filters.location);
      }

      if (this.filters.itemNumber) {
        const numberTerm = this.filters.itemNumber.toLowerCase();
        results = results.filter(
          item => item.number && item.number.toLowerCase().includes(numberTerm)
        );
      }

      if (this.filters.sku) {
        const skuTerm = this.filters.sku.toLowerCase();
        results = results.filter(item => item.sku && item.sku.toLowerCase().includes(skuTerm));
      }

      // Apply date range filter
      results = this.filterByDateRange(results, this.filters.dateRange);

      // Apply units range filter
      if (this.filters.unitsMin || this.filters.unitsMax) {
        results = results.filter(item => {
          const units = parseInt(item.units, 10) || 0;
          const min = this.filters.unitsMin ? parseInt(this.filters.unitsMin, 10) : 0;
          const max = this.filters.unitsMax ? parseInt(this.filters.unitsMax, 10) : Infinity;

          return units >= min && units <= max;
        });
      }

      // Apply sorting
      results = this.sortResults(results, this.filters.sortBy, this.filters.sortOrder);

      // Update UI
      this.updateResultsCount(results.length, appState.getItems().length);
      this.updateActiveFiltersDisplay();

      // Notify components about filtered results
      this.notifyFilterResults(results);
    }

    filterByDateRange(items, range) {
      if (range === 'all') {
        return items;
      }

      if (range === 'custom') {
        return this.filterByCustomDateRange(items);
      }

      const now = new Date();
      const currentYear = now.getFullYear();
      const currentMonth = now.getMonth();

      return items.filter(item => {
        const [year, month] = item.expiry.split('-').map(Number);
        const itemDate = new Date(year, month - 1, 1);
        const currentDate = new Date(currentYear, currentMonth, 1);

        switch (range) {
          case 'expired':
            return itemDate < currentDate;
          case 'thisMonth':
            return year === currentYear && month - 1 === currentMonth;
          case 'nextMonth':
            const nextMonth = new Date(currentYear, currentMonth + 1, 1);
            return year === nextMonth.getFullYear() && month - 1 === nextMonth.getMonth();
          case 'next3Months':
            const threeMonthsLater = new Date(currentYear, currentMonth + 3, 1);
            return itemDate >= currentDate && itemDate < threeMonthsLater;
          case 'next6Months':
            const sixMonthsLater = new Date(currentYear, currentMonth + 6, 1);
            return itemDate >= currentDate && itemDate < sixMonthsLater;
          case 'future':
            const futureDate = new Date(currentYear, currentMonth + 6, 1);
            return itemDate >= futureDate;
          default:
            return true;
        }
      });
    }

    filterByCustomDateRange(items) {
      const dateFrom = this.filters.dateFrom;
      const dateTo = this.filters.dateTo;

      if (!dateFrom && !dateTo) {
        return items;
      }

      return items.filter(item => {
        const itemExpiry = item.expiry;
        if (dateFrom && itemExpiry < dateFrom) {
          return false;
        }
        if (dateTo && itemExpiry > dateTo) {
          return false;
        }
        return true;
      });
    }

    sortResults(items, sortBy, order) {
      return items.sort((a, b) => {
        let aVal, bVal;

        switch (sortBy) {
          case 'expiry':
            aVal = a.expiry || '';
            bVal = b.expiry || '';
            break;
          case 'units':
            aVal = parseInt(a.units, 10) || 0;
            bVal = parseInt(b.units, 10) || 0;
            break;
          case 'location':
            aVal = a.location.toLowerCase();
            bVal = b.location.toLowerCase();
            break;
          case 'sku':
            aVal = (a.sku || '').toLowerCase();
            bVal = (b.sku || '').toLowerCase();
            break;
          case 'number':
            aVal = (a.number || '').toLowerCase();
            bVal = (b.number || '').toLowerCase();
            break;
          case 'desc':
          default:
            aVal = a.desc.toLowerCase();
            bVal = b.desc.toLowerCase();
        }

        let result = 0;
        if (aVal < bVal) {
          result = -1;
        }
        if (aVal > bVal) {
          result = 1;
        }

        return order === 'desc' ? -result : result;
      });
    }

    applyQuickFilter(filterType) {
      this.clearAllFilters();

      switch (filterType) {
        case 'expiring':
          this.filters.dateRange = 'next3Months';
          qs('#dateRangeFilter', this.container).value = 'next3Months';
          break;
        case 'expired':
          this.filters.dateRange = 'expired';
          qs('#dateRangeFilter', this.container).value = 'expired';
          break;
        case 'thisMonth':
          this.filters.dateRange = 'thisMonth';
          qs('#dateRangeFilter', this.container).value = 'thisMonth';
          break;
        case 'nextMonth':
          this.filters.dateRange = 'nextMonth';
          qs('#dateRangeFilter', this.container).value = 'nextMonth';
          break;
        case 'lowStock':
          this.filters.unitsMax = '10';
          qs('#unitsMaxFilter', this.container).value = '10';
          break;
        case 'highStock':
          this.filters.unitsMin = '100';
          qs('#unitsMinFilter', this.container).value = '100';
          break;
        case 'noSku':
          this.filters.search = 'Missing SKU';
          qs('#advancedSearch', this.container).value = 'Missing SKU';
          break;
      }

      this.performSearch();
    }

    toggleAdvancedFilters() {
      const advancedPanel = qs('#advancedFilters', this.container);
      const toggleText = qs('#advancedToggleText', this.container);
      const toggleIcon = qs('#advancedToggleIcon', this.container);

      this.isVisible = !this.isVisible;

      if (this.isVisible) {
        advancedPanel.style.display = 'block';
        toggleText.textContent = 'Hide Filters';
        toggleIcon.textContent = '▲';
      } else {
        advancedPanel.style.display = 'none';
        toggleText.textContent = 'Show Filters';
        toggleIcon.textContent = '▼';
      }
    }

    clearAllFilters() {
      this.filters = {
        search: '',
        location: '',
        dateRange: 'all',
        sortBy: 'desc',
        sortOrder: 'asc',
        unitsMin: '',
        unitsMax: '',
        itemNumber: '',
        sku: '',
        dateFrom: '',
        dateTo: '',
      };

      const inputs = {
        '#advancedSearch': '',
        '#locationFilter': '',
        '#itemNumberFilter': '',
        '#skuFilter': '',
        '#dateRangeFilter': 'all',
        '#dateFromFilter': '',
        '#dateToFilter': '',
        '#unitsMinFilter': '',
        '#unitsMaxFilter': '',
        '#sortByFilter': 'desc',
        '#sortOrderFilter': 'asc',
      };

      Object.entries(inputs).forEach(([selector, value]) => {
        const element = qs(selector, this.container);
        if (element) {
          element.value = value;
        }
      });

      const customRange = qs('#customDateRange', this.container);
      if (customRange) {
        customRange.style.display = 'none';
      }
    }

    resetAllFilters() {
      this.clearAllFilters();
      this.performSearch();
    }

    updateResultsCount(filteredCount, totalCount) {
      const summaryEl = qs('#resultsCount', this.container);
      if (!summaryEl) {
        return;
      }

      if (filteredCount === totalCount) {
        summaryEl.textContent = `${totalCount} items`;
        summaryEl.parentElement.classList.remove('filtered');
      } else {
        summaryEl.textContent = `${filteredCount} of ${totalCount} items`;
        summaryEl.parentElement.classList.add('filtered');
      }
    }

    updateActiveFiltersDisplay() {
      const badge = qs('#active-filter-count', this.container);
      if (!badge) return;
      
      // Count active filters (excluding defaults)
      let activeCount = 0;
      
      if (this.filters.search) activeCount++;
      if (this.filters.location) activeCount++;
      if (this.filters.itemNumber) activeCount++;
      if (this.filters.sku) activeCount++;
      if (this.filters.dateRange && this.filters.dateRange !== 'all') activeCount++;
      if (this.filters.unitsMin || this.filters.unitsMax) activeCount++;
      
      if (activeCount > 0) {
        badge.textContent = `${activeCount} filter${activeCount !== 1 ? 's' : ''}`;
        badge.style.display = 'inline-block';
      } else {
        badge.style.display = 'none';
      }
    }

    saveFilterPreset() {
      const presetName = prompt('Enter a name for this filter preset:');
      if (!presetName?.trim()) {
        return;
      }

      const hasActiveFilters = Object.values(this.filters).some(value => value && value !== 'all');

      if (!hasActiveFilters) {
        alert('No active filters to save');
        return;
      }

      const presets = safeJsonParse(localStorage.getItem('expireWise-filterPresets'), {});
      presets[presetName.trim()] = { ...this.filters };
      localStorage.setItem('expireWise-filterPresets', JSON.stringify(presets));

      notificationService.success(`Filter preset "${presetName}" saved`);
    }

    notifyFilterResults(results) {
      appState.emit('search:filtered', {
        items: results || [],
        filters: { ...this.filters },
        hasActiveFilters: this.hasActiveFilters(),
      });
    }

    hasActiveFilters() {
      return Object.entries(this.filters).some(([_key, value]) => value && value !== 'all');
    }

    getFilteredItems() {
      if (!this.hasActiveFilters()) {
        return appState.getItems();
      }

      let results = [...appState.getItems()];

      // Apply the same filtering logic as performSearch
      if (this.filters.search.trim()) {
        const searchTerm = this.filters.search.toLowerCase();

        if (searchTerm.includes('missing sku') || searchTerm.includes('no sku')) {
          results = results.filter(item => !item.sku || item.sku.trim() === '');
        } else {
          results = results.filter(item => {
            const searchableText = [item.desc, item.number, item.sku, item.location]
              .filter(Boolean)
              .join(' ')
              .toLowerCase();
            return searchableText.includes(searchTerm);
          });
        }
      }

      if (this.filters.location) {
        results = results.filter(item => item.location === this.filters.location);
      }

      if (this.filters.itemNumber) {
        const numberTerm = this.filters.itemNumber.toLowerCase();
        results = results.filter(
          item => item.number && item.number.toLowerCase().includes(numberTerm)
        );
      }

      if (this.filters.sku) {
        const skuTerm = this.filters.sku.toLowerCase();
        results = results.filter(item => item.sku && item.sku.toLowerCase().includes(skuTerm));
      }

      results = this.filterByDateRange(results, this.filters.dateRange);

      if (this.filters.unitsMin || this.filters.unitsMax) {
        results = results.filter(item => {
          const units = parseInt(item.units, 10) || 0;
          const min = this.filters.unitsMin ? parseInt(this.filters.unitsMin, 10) : 0;
          const max = this.filters.unitsMax ? parseInt(this.filters.unitsMax, 10) : Infinity;
          return units >= min && units <= max;
        });
      }

      return results;
    }

    getFilteredItemsCount() {
      return this.getFilteredItems().length;
    }

    destroy() {
      if (this.container) {
        this.container.innerHTML = '';
      }
    }
  }

  // (Removed legacy MonthCarousel class)

  // Items Table Component
  class ItemsTable {
    constructor(table, tbody) {
      this.table = table;
      this.tbody = tbody;
      this.sortBy = 'desc';
      this.sortOrder = 'asc';
      this.groupBy = null;
      this.filteredItems = null; // Store filtered items from search

      this.setupEventListeners();
      this.render();
    }

    setupEventListeners() {
      // Subscribe to state changes
      appState.subscribe('view:changed', () => this.render());
      appState.subscribe('item:added', () => this.render());
      appState.subscribe('item:updated', () => this.render());
      appState.subscribe('item:removed', () => this.render());
      appState.subscribe('data:loaded', () => this.render());

      // Subscribe to search results
      appState.subscribe('search:filtered', data => {
        this.filteredItems = data.items;
        this.render();
      });

      // Event delegation for edit/delete buttons
      this.tbody.addEventListener('click', e => {
        const editBtn = e.target.closest('.edit-btn');
        const deleteBtn = e.target.closest('.delete-btn');

        if (editBtn) {
          const itemId = editBtn.dataset.itemId;
          if (window.editItem) {
            window.editItem(itemId);
          }
        }

        if (deleteBtn) {
          const itemId = deleteBtn.dataset.itemId;
          if (window.removeItem) {
            window.removeItem(itemId);
          }
        }
      });

      // Sort headers
      qsa('.sortable', this.table).forEach(header => {
        // Add helpful tooltip
        const field = header.dataset.sort;
        const fieldName = header.textContent.trim();
        header.title = `Click to sort by ${fieldName}`;
        
        header.addEventListener('click', () => {
          // Update sort state
          if (this.sortBy === field) {
            this.sortOrder = this.sortOrder === 'asc' ? 'desc' : 'asc';
          } else {
            this.sortBy = field;
            this.sortOrder = 'asc';
          }

          // Update visual indicators
          qsa('.sortable', this.table).forEach(h => {
            h.classList.remove('sorted-asc', 'sorted-desc');
            // Update tooltip based on current state
            const hField = h.dataset.sort;
            const hFieldName = h.textContent.trim().replace(/[↑↓]/g, '').trim();
            if (h === header) {
              h.title = `Click to sort ${this.sortOrder === 'asc' ? 'descending' : 'ascending'}`;
            } else {
              h.title = `Click to sort by ${hFieldName}`;
            }
          });
          header.classList.add(this.sortOrder === 'asc' ? 'sorted-asc' : 'sorted-desc');

          this.render();
        });
      });
    }

    // Bulk operations removed - using inline edit/delete buttons

    setupBulkActionButtons() {
      // Find or create bulk actions container
      let bulkContainer = document.querySelector('#bulkActionsContainer');
      if (!bulkContainer) {
        bulkContainer = document.createElement('div');
        bulkContainer.id = 'bulkActionsContainer';
        bulkContainer.className = 'bulk-actions-container';
        bulkContainer.style.display = 'none';

        // Insert before the table
        this.table.parentNode.insertBefore(bulkContainer, this.table);
      }

      bulkContainer.innerHTML = `
        <div class="bulk-actions-bar">
          <div class="bulk-selection-info">
            <span id="bulkSelectionCount">0</span> items selected
          </div>
          <div class="bulk-action-buttons">
            <button class="btn small" id="bulkEditLocation">📍 Change Location</button>
            <button class="btn small" id="bulkEditExpiry">📅 Change Expiry</button>
            <button class="btn small" id="bulkEditUnits">📦 Update Units</button>
            <button class="btn small danger" id="bulkDelete">🗑️ Delete Selected</button>
            <button class="btn small ghost" id="bulkDeselectAll">Clear Selection</button>
          </div>
        </div>
      `;

      // Bind bulk action events
      bulkContainer.addEventListener('click', e => {
        const selectedIds = Array.from(this.selectedItems);

        switch (e.target.id) {
          case 'bulkEditLocation':
            this.bulkEditLocation(selectedIds);
            break;
          case 'bulkEditExpiry':
            this.bulkEditExpiry(selectedIds);
            break;
          case 'bulkEditUnits':
            this.bulkEditUnits(selectedIds);
            break;
          case 'bulkDelete':
            this.bulkDelete(selectedIds);
            break;
          case 'bulkDeselectAll':
            this.deselectAll();
            break;
        }
      });
    }

    toggleSelectAll(checked) {
      const checkboxes = qsa('.item-checkbox', this.table);
      checkboxes.forEach(checkbox => {
        checkbox.checked = checked;
        const itemId = checkbox.dataset.id;
        if (checked) {
          this.selectedItems.add(itemId);
        } else {
          this.selectedItems.delete(itemId);
        }
      });
      this.updateBulkActionsVisibility();
    }

    updateSelectAllCheckbox() {
      const selectAllCheckbox = qs('#selectAllCheckbox');
      if (!selectAllCheckbox) {
        return;
      }

      const totalCheckboxes = qsa('.item-checkbox', this.table).length;
      const selectedCount = this.selectedItems.size;

      if (selectedCount === 0) {
        selectAllCheckbox.checked = false;
        selectAllCheckbox.indeterminate = false;
      } else if (selectedCount === totalCheckboxes) {
        selectAllCheckbox.checked = true;
        selectAllCheckbox.indeterminate = false;
      } else {
        selectAllCheckbox.checked = false;
        selectAllCheckbox.indeterminate = true;
      }
    }

    updateBulkActionsVisibility() {
      const bulkContainer = qs('#bulkActionsContainer');
      const bulkCountEl = qs('#bulkSelectionCount');

      if (bulkContainer) {
        bulkContainer.style.display = this.selectedItems.size > 0 ? 'block' : 'none';
      }

      if (bulkCountEl) {
        bulkCountEl.textContent = this.selectedItems.size;
      }
    }

    bulkEditLocation(itemIds) {
      if (itemIds.length === 0) {
        return;
      }

      // Get available locations
      const items = appState.getItems();
      const locations = [...new Set(items.map(item => item.location))];

      // Create a simple select dialog
      const newLocation = prompt(
        `Change location for ${itemIds.length} items.\n\nAvailable locations:\n${locations.join(', ')}\n\nEnter new location:`
      );

      if (!newLocation?.trim()) {
        return;
      }

      // Update all selected items
      let updateCount = 0;
      itemIds.forEach(id => {
        if (appState.updateItem(id, { location: newLocation.trim() })) {
          updateCount++;
        }
      });

      notificationService.success(`Updated location for ${updateCount} items`);
      this.deselectAll();
    }

    bulkEditExpiry(itemIds) {
      if (itemIds.length === 0) {
        return;
      }

      const newExpiry = prompt(
        `Change expiry date for ${itemIds.length} items.\n\nEnter new expiry (YYYY-MM format, e.g., 2025-12):`
      );

      if (!newExpiry?.match(/^\d{4}-\d{2}$/)) {
        if (newExpiry) {
          alert('Invalid format. Please use YYYY-MM format.');
        }
        return;
      }

      let updateCount = 0;
      itemIds.forEach(id => {
        if (appState.updateItem(id, { expiry: newExpiry })) {
          updateCount++;
        }
      });

      notificationService.success(`Updated expiry for ${updateCount} items`);
      this.deselectAll();
    }

    bulkEditUnits(itemIds) {
      if (itemIds.length === 0) {
        return;
      }

      const operation = prompt(
        `Update units for ${itemIds.length} items.\n\nChoose operation:\n- 'set X' to set units to X\n- 'add X' to add X units\n- 'subtract X' to subtract X units\n\nExample: 'set 50' or 'add 10'`
      );

      if (!operation?.trim()) {
        return;
      }

      const match = operation.trim().match(/^(set|add|subtract)\s+(\d+)$/i);
      if (!match) {
        alert('Invalid format. Use: set 50, add 10, or subtract 5');
        return;
      }

      const [, op, valueStr] = match;
      const value = parseInt(valueStr, 10);

      let updateCount = 0;
      itemIds.forEach(id => {
        const item = appState.data.items.find(item => item.id === id);
        if (!item) {
          return;
        }

        let newUnits;
        switch (op.toLowerCase()) {
          case 'set':
            newUnits = value;
            break;
          case 'add':
            newUnits = (parseInt(item.units, 10) || 0) + value;
            break;
          case 'subtract':
            newUnits = Math.max(0, (parseInt(item.units, 10) || 0) - value);
            break;
        }

        if (appState.updateItem(id, { units: newUnits })) {
          updateCount++;
        }
      });

      notificationService.success(`Updated units for ${updateCount} items`);
      this.deselectAll();
    }

    bulkDelete(itemIds) {
      if (itemIds.length === 0) {
        return;
      }

      // Check user preferences for delete confirmation
      let confirmDeletes = true;
      if (window.userPreferences) {
        const preferences = window.userPreferences.getPreferences();
        confirmDeletes = preferences.confirmDeletes;
      }

      if (
        confirmDeletes &&
        !confirm(`Delete ${itemIds.length} selected items? This cannot be undone.`)
      ) {
        return;
      }

      let deleteCount = 0;
      itemIds.forEach(id => {
        if (appState.removeItem(id)) {
          deleteCount++;
        }
      });

      notificationService.success(`Deleted ${deleteCount} items`);
      this.deselectAll();
    }

    deselectAll() {
      this.selectedItems.clear();
      qsa('.item-checkbox', this.table).forEach(checkbox => {
        checkbox.checked = false;
      });
      this.updateBulkActionsVisibility();
      this.updateSelectAllCheckbox();
    }

    setGroupBy(field) {
      this.groupBy = field;
      this.render();
    }

    clearGrouping() {
      this.groupBy = null;
      this.render();
    }

    render() {
      DEBUG.log('🔍 ItemsTable render() called');
      const { viewYear, viewMonth } = appState.ui;
      DEBUG.log('📅 Current view:', { viewYear, viewMonth });

      // Get items for the current month
      const targetKey = formatExpiryKey(viewYear, viewMonth);
      let items;
      
      if (this.filteredItems !== null) {
        // If search is active, filter the search results to only show items for current month
        items = this.filteredItems.filter(item => item.expiry === targetKey);
        DEBUG.log('📋 Using filtered items for current month:', items.length, 'out of', this.filteredItems.length, 'search results');
      } else {
        // No search active, show all items for current month
        items = appState.getItemsForMonth(viewYear, viewMonth);
        DEBUG.log('📋 Items for current month:', items.length);
      }

      DEBUG.log('📋 Total items in appState:', appState.data?.items?.length || 0);
      if (appState.data?.items?.length > 0) {
        DEBUG.log('📋 Sample item from appState:', appState.data.items[0]);
      }
      
      // Update item count
      const itemCount = qs('#item-count');
      if (itemCount) {
        itemCount.textContent = `${items.length} item${items.length !== 1 ? 's' : ''}`;
      }
      
      // Show/hide empty state
      const emptyState = qs('#empty-state');
      const tableContainer = qs('.modern-table-container');
      if (emptyState && tableContainer) {
        if (items.length === 0) {
          tableContainer.classList.add('hidden');
          emptyState.classList.remove('hidden');
          return; // No need to render table
        } else {
          tableContainer.classList.remove('hidden');
          emptyState.classList.add('hidden');
        }
      }

      // Sort items with improved algorithm
      const sortedItems = [...items].sort((a, b) => {
        let aVal = a[this.sortBy];
        let bVal = b[this.sortBy];

        // Handle units as numbers
        if (this.sortBy === 'units') {
          aVal = parseInt(aVal, 10) || 0;
          bVal = parseInt(bVal, 10) || 0;
          return this.sortOrder === 'asc' ? aVal - bVal : bVal - aVal;
        }

        // Handle dates (expiry)
        if (this.sortBy === 'expiry') {
          aVal = aVal || '';
          bVal = bVal || '';
          return this.sortOrder === 'asc' ? aVal.localeCompare(bVal) : bVal.localeCompare(aVal);
        }

        // Handle text fields (desc, location, sku, number)
        aVal = (aVal || '').toString().toLowerCase();
        bVal = (bVal || '').toString().toLowerCase();

        return this.sortOrder === 'asc' ? aVal.localeCompare(bVal) : bVal.localeCompare(aVal);
      });

      // Group items if needed
      if (this.groupBy) {
        this.renderGrouped(sortedItems);
      } else {
        this.renderFlat(sortedItems);
      }
    }

    renderFlat(items) {
      DEBUG.log('🔧 renderFlat called with items:', items.length);

      if (!this.tbody) {
        console.error('❌ tbody element not found');
        return;
      }

      DEBUG.log('📋 Sample item being rendered:', items[0]);
      this.tbody.innerHTML = items
        .map(
          item => `
        <tr data-id="${escapeHtml(item.id)}">
          <td>${escapeHtml(item.desc)}</td>
          <td>${escapeHtml(item.sku || '-')}</td>
          <td>${escapeHtml(item.number || '-')}</td>
          <td>${escapeHtml(item.location)}</td>
          <td class="text-right">${escapeHtml(item.units)}</td>
          <td>${formatExpiryDisplay(item.expiry)}</td>
          <td class="actions-cell">
            <button class="btn small outline edit-btn" data-item-id="${escapeHtml(item.id)}" title="Edit Item" aria-label="Edit ${escapeHtml(item.desc)}">
              <span class="icon" aria-hidden="true">✏️</span>
            </button>
            <button class="btn small danger delete-btn" data-item-id="${escapeHtml(item.id)}" title="Delete Item" aria-label="Delete ${escapeHtml(item.desc)}">
              <span class="icon" aria-hidden="true">🗑️</span>
            </button>
          </td>
        </tr>
      `
        )
        .join('');

      // Inline actions - no bulk operations needed
    }

    renderGrouped(items) {
      const groups = {};
      items.forEach(item => {
        const key = item[this.groupBy] || 'Other';
        if (!groups[key]) {
          groups[key] = [];
        }
        groups[key].push(item);
      });

      let html = '';
      Object.entries(groups).forEach(([groupName, groupItems]) => {
        html += `
          <tr class="group-header">
            <td colspan="7"><strong>${groupName} (${groupItems.length} items)</strong></td>
          </tr>
        `;
        groupItems.forEach(item => {
          html += `
            <tr data-id="${escapeHtml(item.id)}">
              <td>${escapeHtml(item.desc)}</td>
              <td>${escapeHtml(item.sku || '-')}</td>
              <td>${escapeHtml(item.number || '-')}</td>
              <td>${escapeHtml(item.location)}</td>
              <td class="text-right">${escapeHtml(item.units)}</td>
              <td>${formatExpiryDisplay(item.expiry)}</td>
              <td class="actions-cell">
                <button class="btn small outline edit-btn" data-item-id="${escapeHtml(item.id)}" title="Edit ${escapeHtml(item.desc)}" aria-label="Edit ${escapeHtml(item.desc)}">
                  <span class="icon" aria-hidden="true">✏️</span>
                </button>
                <button class="btn small danger delete-btn" data-item-id="${escapeHtml(item.id)}" title="Delete ${escapeHtml(item.desc)}" aria-label="Delete ${escapeHtml(item.desc)}">
                  <span class="icon" aria-hidden="true">🗑️</span>
                </button>
              </td>
            </tr>
          `;
        });
      });

      this.tbody.innerHTML = html;
    }
  }

  // Custom Autocomplete Component
  class Autocomplete {
    constructor(input, options = {}) {
      console.log(
        '🔧 Autocomplete constructor called for input:',
        input?.id,
        'with',
        options.data?.length,
        'items'
      );
      this.input = input;
      this.options = options;
      this.data = options.data || [];
      this.onSelect = options.onSelect || (() => {});
      this.displayKey = options.displayKey || 'value';
      this.minChars = options.minChars || 1;
      this.maxResults = options.maxResults || 50;

      this.dropdown = null;
      this.highlightedIndex = -1;
      this.filteredData = [];

      this.init();
    }

    init() {
      console.log('🔧 Autocomplete init for input:', this.input?.id);

      // Create dropdown element
      const container = document.createElement('div');
      container.className = 'autocomplete-container';
      this.input.parentNode.insertBefore(container, this.input);
      container.appendChild(this.input);

      this.dropdown = document.createElement('div');
      this.dropdown.className = 'autocomplete-dropdown';
      container.appendChild(this.dropdown);

      console.log('🔧 Autocomplete dropdown created:', this.dropdown);

      // Bind events
      this.input.addEventListener('input', e => this.handleInput(e));
      this.input.addEventListener('keydown', e => this.handleKeyDown(e));
      this.input.addEventListener('blur', e => this.handleBlur(e));
      this.input.addEventListener('focus', e => this.handleFocus(e));

      console.log('🔧 Autocomplete events bound for:', this.input?.id);

      // Remove old datalist if exists
      const datalistId = this.input.getAttribute('list');
      if (datalistId) {
        this.input.removeAttribute('list');
        const datalist = document.getElementById(datalistId);
        if (datalist) {
          datalist.remove();
        }
        console.log('🔧 Removed datalist:', datalistId);
      }
    }

    updateData(data) {
      this.data = data;
    }

    handleInput(e) {
      const value = e.target.value;
      console.log('🔍 Autocomplete handleInput:', value, 'minChars:', this.minChars);

      if (value.length < this.minChars) {
        console.log('🔍 Value too short, hiding');
        this.hide();
        return;
      }

      this.filterAndShow(value);
    }

    handleFocus(e) {
      const value = e.target.value;
      if (value.length >= this.minChars) {
        this.filterAndShow(value);
      }
    }

    filterAndShow(value) {
      const searchTerm = value.toLowerCase();
      console.log('🔍 Filtering for:', searchTerm);

      this.filteredData = this.data
        .filter(item => {
          const itemValue = typeof item === 'string' ? item : item[this.displayKey];
          return itemValue && itemValue.toLowerCase().includes(searchTerm);
        })
        .slice(0, this.maxResults);

      console.log('🔍 Filtered results:', this.filteredData.length);

      if (this.filteredData.length > 0) {
        this.render();
        this.show();
      } else {
        this.hide();
      }
    }

    render() {
      if (this.filteredData.length === 0) {
        this.dropdown.innerHTML = '<div class="autocomplete-no-results">No matches found</div>';
        return;
      }

      this.dropdown.innerHTML = this.filteredData
        .map((item, index) => {
          const value = typeof item === 'string' ? item : item[this.displayKey];
          const highlighted = index === this.highlightedIndex ? 'highlighted' : '';
          return `<div class="autocomplete-item ${highlighted}" data-index="${index}">${escapeHtml(value)}</div>`;
        })
        .join('');

      // Add click handlers
      this.dropdown.querySelectorAll('.autocomplete-item').forEach((el, index) => {
        el.addEventListener('mousedown', e => {
          e.preventDefault(); // Prevent blur
          this.selectItem(index);
        });
      });
    }

    handleKeyDown(e) {
      if (!this.dropdown.classList.contains('show')) {
        return;
      }

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          this.highlightedIndex = Math.min(this.highlightedIndex + 1, this.filteredData.length - 1);
          this.render();
          this.scrollToHighlighted();
          break;
        case 'ArrowUp':
          e.preventDefault();
          this.highlightedIndex = Math.max(this.highlightedIndex - 1, -1);
          this.render();
          this.scrollToHighlighted();
          break;
        case 'Enter':
          e.preventDefault();
          if (this.highlightedIndex >= 0) {
            this.selectItem(this.highlightedIndex);
          }
          break;
        case 'Escape':
          this.hide();
          break;
      }
    }

    scrollToHighlighted() {
      const highlighted = this.dropdown.querySelector('.highlighted');
      if (highlighted) {
        highlighted.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    }

    selectItem(index) {
      const item = this.filteredData[index];
      const value = typeof item === 'string' ? item : item[this.displayKey];

      this.input.value = value;
      this.onSelect(item);
      this.hide();
    }

    handleBlur(_e) {
      // Small delay to allow click events to fire
      setTimeout(() => this.hide(), 200);
    }

    show() {
      console.log('👁️ Showing dropdown for:', this.input.id);
      this.dropdown.classList.add('show');
      this.highlightedIndex = -1;
      console.log('👁️ Dropdown classes:', this.dropdown.className);
    }

    hide() {
      console.log('🙈 Hiding dropdown for:', this.input.id);
      this.dropdown.classList.remove('show');
      this.highlightedIndex = -1;
    }
  }

  // Item Form Component
  class ItemForm {
    constructor(form) {
      console.log('🔧 ItemForm constructor called');
      this.form = form;
      this.setupEventListeners();
      console.log(
        '🔧 About to setup autocomplete, appState.dict has:',
        appState.dict?.items?.length || 0,
        'items'
      );
      this.setupAutocomplete();
      this.populateLocations();

      // Re-populate autocomplete when dictionary is loaded
      appState.subscribe('dictionary:loaded', () => {
        DEBUG.log('🔄 Dictionary loaded, refreshing autocomplete...');
        this.setupAutocomplete();
        this.populateLocations();
      });
    }

    setupEventListeners() {
      this.form.addEventListener('submit', e => {
        e.preventDefault();
        const saveBtn = this.form.querySelector('button[type="submit"]');
        if (
          window.userPreferences &&
          typeof window.userPreferences.showSaveButtonFeedback === 'function'
        ) {
          window.userPreferences.showSaveButtonFeedback(saveBtn, () => {
            this.handleSubmit();
          });
        } else {
          this.handleSubmit();
        }
      });

      this.form.addEventListener('reset', () => {
        setTimeout(() => this.updateHint(), 0);
      });

      // Auto-fill expiry to current month
      const expiryInput = qs('#expiry', this.form);
      if (expiryInput && !expiryInput.value) {
        const { year, month } = getCurrentYearMonth();
        expiryInput.value = formatDateForInput(year, month);
      }

      // Setup autofill listeners
      this.setupAutofillListeners();
    }

    setupAutofillListeners() {
      const numberInput = qs('#number', this.form);
      const descInput = qs('#desc', this.form);
      const skuInput = qs('#sku', this.form);

      // When item number changes, auto-fill description and SKU
      if (numberInput) {
        numberInput.addEventListener('input', e => {
          this.autofillFromNumber(e.target.value);
        });
      }

      // When description changes, auto-fill item number and SKU
      if (descInput) {
        descInput.addEventListener('input', e => {
          this.autofillFromDescription(e.target.value);
        });
      }

      // When SKU changes, auto-fill item number and description
      if (skuInput) {
        skuInput.addEventListener('input', e => {
          this.autofillFromSKU(e.target.value);
        });
      }
    }

    autofillFromNumber(number) {
      if (!number || !appState.dict?.items) {
        return;
      }

      const item = appState.dict.items.find(
        i => i.number && i.number.toUpperCase() === number.toUpperCase()
      );

      if (item) {
        const descInput = qs('#desc', this.form);
        const skuInput = qs('#sku', this.form);

        if (descInput && item.desc) {
          descInput.value = item.desc;
        }
        if (skuInput && item.sku) {
          // Handle both string and array SKUs
          const skuValue = Array.isArray(item.sku) ? item.sku[0] : item.sku;
          skuInput.value = skuValue;
        }
      }
    }

    autofillFromDescription(desc) {
      if (!desc || !appState.dict?.items) {
        return;
      }

      const item = appState.dict.items.find(
        i => i.desc && i.desc.toUpperCase() === desc.toUpperCase()
      );

      if (item) {
        const numberInput = qs('#number', this.form);
        const skuInput = qs('#sku', this.form);

        if (numberInput && item.number) {
          numberInput.value = item.number;
        }
        if (skuInput && item.sku) {
          // Handle both string and array SKUs
          const skuValue = Array.isArray(item.sku) ? item.sku[0] : item.sku;
          skuInput.value = skuValue;
        }
      }
    }

    autofillFromSKU(sku) {
      if (!sku || !appState.dict?.items) {
        return;
      }

      const item = appState.dict.items.find(i => {
        if (Array.isArray(i.sku)) {
          return i.sku.some(s => s && s.toString() === sku.toString());
        }
        return i.sku && i.sku.toString() === sku.toString();
      });

      if (item) {
        const numberInput = qs('#number', this.form);
        const descInput = qs('#desc', this.form);

        if (numberInput && item.number) {
          numberInput.value = item.number;
        }
        if (descInput && item.desc) {
          descInput.value = item.desc;
        }
      }
    }

    setupAutocomplete() {
      const dict = appState.dict;
      console.log('📝 Setting up autocomplete. Dictionary items:', dict?.items?.length || 0);

      if (!dict || !dict.items || !Array.isArray(dict.items)) {
        console.warn('⚠️ Cannot setup autocomplete - dictionary not loaded');
        return;
      }

      // Setup autocomplete for Item Number
      const numberInput = qs('#number', this.form);
      if (numberInput) {
        const numbers = dict.items
          .filter(item => item && item.number)
          .map(item => item.number)
          .filter(num => num && typeof num === 'string');
        const uniqueNumbers = [...new Set(numbers)].sort();

        this.numberAutocomplete = new Autocomplete(numberInput, {
          data: uniqueNumbers,
          minChars: 0,
          onSelect: (value) => {
            console.log('📝 Number selected:', value);
            numberInput.value = value;
            this.autofillFromNumber(value);
          }
        });
        console.log('✅ Autocomplete setup for item numbers:', uniqueNumbers.length);
      }

      // Setup autocomplete for Item Description
      const descInput = qs('#desc', this.form);
      if (descInput) {
        const descriptions = dict.items
          .filter(item => item && item.desc)
          .map(item => item.desc)
          .filter(desc => desc && typeof desc === 'string');
        const uniqueDescriptions = [...new Set(descriptions)].sort();

        this.descAutocomplete = new Autocomplete(descInput, {
          data: uniqueDescriptions,
          minChars: 0,
          onSelect: (value) => {
            console.log('📝 Description selected:', value);
            descInput.value = value;
            this.autofillFromDescription(value);
          }
        });
        console.log('✅ Autocomplete setup for descriptions:', uniqueDescriptions.length);
      }

      // Setup autocomplete for SKU
      const skuInput = qs('#sku', this.form);
      if (skuInput) {
        const skus = dict.items
          .filter(item => item && item.sku)
          .flatMap(item => Array.isArray(item.sku) ? item.sku : [item.sku])
          .filter(sku => sku && (typeof sku === 'string' || typeof sku === 'number'))
          .map(sku => sku.toString());
        const uniqueSKUs = [...new Set(skus)].sort();

        this.skuAutocomplete = new Autocomplete(skuInput, {
          data: uniqueSKUs,
          minChars: 0,
          onSelect: (value) => {
            console.log('📝 SKU selected:', value);
            skuInput.value = value;
            this.autofillFromSKU(value);
          }
        });
        console.log('✅ Autocomplete setup for SKUs:', uniqueSKUs.length);
      }
    }

    populateLocations() {
      const locationSelect = qs('#location');
      if (!locationSelect || locationSelect.tagName !== 'SELECT') {
        return;
      }

      const dict = appState.dict;
      let locations = ['Warehouse A', 'Warehouse B', 'Store Front']; // default locations

      // Extract location names from dictionary stores if available
      if (dict.stores && Array.isArray(dict.stores)) {
        locations = dict.stores
          .filter(store => store && typeof store === 'object' && store.name)
          .map(store => store.name);
      }

      // Clear existing options except the placeholder
      locationSelect.innerHTML = '<option value="">Select Location...</option>';

      locations.forEach(location => {
        const option = document.createElement('option');
        option.value = location;
        option.textContent = location;
        locationSelect.appendChild(option);
      });
    }

    handleSubmit() {
      const formData = new FormData(this.form);
      const item = {
        number: formData.get('number').trim(),
        sku: formData.get('sku').trim(),
        desc: formData.get('desc').trim(),
        location: formData.get('location'),
        units: parseInt(formData.get('units'), 10),
        expiry: formData.get('expiry'),
      };

      // Validate
      if (!item.desc || !item.location || !item.units || !item.expiry) {
        notificationService.error('Please fill in all required fields');
        return;
      }

      // Add item
      appState.addItem(item);

      // Reset form
      this.form.reset();
      this.updateHint('Item added successfully!');

      // Re-set expiry to current month
      const expiryInput = qs('#expiry', this.form);
      if (expiryInput) {
        const { year, month } = getCurrentYearMonth();
        expiryInput.value = formatDateForInput(year, month);
      }

      // Update dropdown options
      if (window.updateDropdownOptions) {
        window.updateDropdownOptions();
      }

      notificationService.success('Item added successfully');
    }

    updateHint(message = '') {
      const hintEl = qs('#formHint', this.form);
      if (hintEl) {
        hintEl.textContent = message;
        if (message) {
          setTimeout(() => {
            hintEl.textContent = '';
          }, 3000);
        }
      }
    }
  }

  // Analytics Dashboard Component
  class AnalyticsDashboard {
    constructor(container) {
      this.container = container;
      this.charts = {};

      // Chart configuration properties
      this.topItemsChartType = 'doughnut';
      this.locationChartType = 'bar';
    }

    renderAll() {
      DEBUG.log('📊 Analytics: renderAll() called');

      if (!this.container) {
        console.error('❌ Analytics: No container found');
        return;
      }

      try {
        DEBUG.log('📊 Analytics: Clearing container and creating layout');
        this.clearContainer();
        this.createLayout();

        DEBUG.log('📊 Analytics: Updating stats');
        this.updateStats();

        DEBUG.log('📊 Analytics: Rendering charts');
        this.renderCharts();

        DEBUG.log('✅ Analytics: renderAll() completed successfully');
      } catch (error) {
        console.error('❌ Analytics: Error in renderAll():', error);
      }
    }

    clearContainer() {
      this.container.innerHTML = '';
      // Destroy existing charts
      Object.values(this.charts).forEach(chart => {
        if (chart && chart.destroy) {
          chart.destroy();
        }
      });
      this.charts = {};
    }

    createLayout() {
      this.container.innerHTML = `
        <!-- Analytics Header -->
        <div class="analytics-header">
          <div class="analytics-actions">
            <button class="btn-modern" onclick="window.analyticsDashboard.printReport()" title="Print Report">
              🖨️ Print Report
            </button>
            <button class="btn-modern secondary" onclick="location.reload()" title="Refresh Data">
              🔄 Refresh
            </button>
          </div>
        </div>

        <!-- Key Metrics Cards -->
        <div class="metrics-grid">
          <div class="metric-card primary-metric">
            <div class="metric-icon">📦</div>
            <div class="metric-content">
              <div class="metric-value" id="totalItems">-</div>
              <div class="metric-label">Total Items</div>
              <div class="metric-trend">+12% from last month</div>
            </div>
          </div>
          
          <div class="metric-card danger-metric">
            <div class="metric-icon">⏰</div>
            <div class="metric-content">
              <div class="metric-value" id="thisMonth">-</div>
              <div class="metric-label">Expiring This Month</div>
              <div class="metric-trend">Urgent attention needed</div>
            </div>
          </div>
          
          <div class="metric-card warning-metric">
            <div class="metric-icon">⚠️</div>
            <div class="metric-content">
              <div class="metric-value" id="nextMonth">-</div>
              <div class="metric-label">Expiring Next Month</div>
              <div class="metric-trend">Plan ahead</div>
            </div>
          </div>
          
          <div class="metric-card danger-metric">
            <div class="metric-icon">☠️</div>
            <div class="metric-content">
              <div class="metric-value" id="expired">-</div>
              <div class="metric-label">Expired Items</div>
              <div class="metric-trend">Action required</div>
            </div>
          </div>
        </div>

        <!-- Charts Section -->
        <div class="charts-section">
            <!-- Top Items Chart -->
            <div class="chart-card full-width">
              <div class="chart-header">
                <h3>☠️ Top Items by Quantity</h3>
                <div class="chart-controls">
                  <button class="chart-toggle active" data-top-items-type="doughnut">🍩 Donut</button>
                  <button class="chart-toggle" data-top-items-type="bar">📊 Bar</button>
                  <button class="chart-toggle" data-top-items-type="polarArea">🎯 Polar</button>
                </div>
              </div>
              <div class="chart-body">
                <canvas id="topItemsChart"></canvas>
              </div>
            </div>

            <!-- Location Distribution Chart -->
            <div class="chart-card full-width">
              <div class="chart-header">
                <h3>📍 Items by Location</h3>
                <div class="chart-controls">
                  <button class="chart-toggle" data-type="doughnut">🍩 Donut</button>
                  <button class="chart-toggle active" data-type="bar">📊 Bar</button>
                  <button class="chart-toggle" data-type="polarArea">🎯 Polar</button>
                </div>
              </div>
              <div class="chart-body">
                <canvas id="locationChart"></canvas>
              </div>
            </div>

          <!-- Timeline Chart -->
          <div class="chart-card full-width">
            <div class="chart-header">
              <h3>📅 Expiry Timeline</h3>
              <div class="chart-controls">
                <span class="chart-info">💡 Shift + scroll to zoom • Double-click to reset</span>
              </div>
            </div>
            <div class="chart-body">
              <canvas id="timelineChart"></canvas>
            </div>
          </div>
        </div>

        <!-- Quick Stats Grid -->
        <div class="quick-stats">
          <div class="stat-item">
            <span class="stat-icon">🔥</span>
            <span class="stat-text">Most Common Location: <strong id="topLocation">-</strong></span>
          </div>
          <div class="stat-item">
            <span class="stat-icon">⏰</span>
            <span class="stat-text">Average Days Until Expiry: <strong id="avgDays">-</strong></span>
          </div>
          <div class="stat-item">
            <span class="stat-icon">📈</span>
            <span class="stat-text">Inventory Value: <strong id="totalValue">-</strong></span>
          </div>
          <div class="stat-item">
            <span class="stat-icon">🎯</span>
            <span class="stat-text">Items Added This Week: <strong id="weeklyAdded">-</strong></span>
          </div>
        </div>
      `;
    }

    updateStats() {
      const stats = analyticsService.getExpiryStats();

      // Update main metric cards
      qs('#totalItems', this.container).textContent = stats.total;
      qs('#thisMonth', this.container).textContent = stats.thisMonth;
      qs('#nextMonth', this.container).textContent = stats.nextMonth;
      qs('#expired', this.container).textContent = stats.expired;

      // Update quick stats
      const items = dataService.getAllItems();

      // Calculate most common location
      const locationCounts = {};
      items.forEach(item => {
        locationCounts[item.location] = (locationCounts[item.location] || 0) + 1;
      });
      const topLocation =
        Object.entries(locationCounts).sort(([, a], [, b]) => b - a)[0]?.[0] || 'N/A';

      // Calculate average days until expiry
      const now = new Date();
      const validDays = items
        .map(item => {
          const expiryDate = new Date(item.expiry);
          return Math.ceil((expiryDate - now) / (1000 * 60 * 60 * 24));
        })
        .filter(days => days > 0);
      const avgDays = validDays.length
        ? Math.round(validDays.reduce((a, b) => a + b, 0) / validDays.length)
        : 0;

      // Calculate items added this week
      const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      const weeklyAdded = items.filter(item => {
        const addedDate = new Date(item.dateAdded || item.expiry);
        return addedDate >= weekAgo;
      }).length;

      // Update quick stats display
      const topLocationEl = qs('#topLocation', this.container);
      const avgDaysEl = qs('#avgDays', this.container);
      const totalValueEl = qs('#totalValue', this.container);
      const weeklyAddedEl = qs('#weeklyAdded', this.container);

      if (topLocationEl) {
        topLocationEl.textContent = topLocation;
      }
      if (avgDaysEl) {
        avgDaysEl.textContent = `${avgDays} days`;
      }
      if (totalValueEl) {
        totalValueEl.textContent = `${items.length * 25} items`;
      } // Placeholder calculation
      if (weeklyAddedEl) {
        weeklyAddedEl.textContent = weeklyAdded;
      }
    }

    renderCharts() {
      DEBUG.log('📊 Analytics: Checking Chart.js availability...');

      if (typeof Chart === 'undefined') {
        console.error('❌ Analytics: Chart.js not available');
        this.showChartError('Chart.js library not loaded');
        return;
      }

      // Check if zoom plugin is available and register it
      try {
        if (typeof Chart.register !== 'undefined') {
          // Try different ways the zoom plugin might be exposed
          if (typeof window.chartjsPluginZoom !== 'undefined') {
            Chart.register(window.chartjsPluginZoom.default || window.chartjsPluginZoom);
            DEBUG.log('✅ Analytics: Chart.js zoom plugin registered (window.chartjsPluginZoom)');
          } else if (typeof window.ChartZoom !== 'undefined') {
            Chart.register(window.ChartZoom.default || window.ChartZoom);
            DEBUG.log('✅ Analytics: Chart.js zoom plugin registered (window.ChartZoom)');
          } else if (typeof zoomPlugin !== 'undefined') {
            Chart.register(zoomPlugin);
            DEBUG.log('✅ Analytics: Chart.js zoom plugin registered (zoomPlugin)');
          } else {
            DEBUG.warn(
              '⚠️ Analytics: Chart.js zoom plugin not found - trying without registration'
            );
          }
        }
      } catch (error) {
        DEBUG.warn('⚠️ Analytics: Error registering zoom plugin:', error);
      }

      DEBUG.log('✅ Analytics: Chart.js is available');

      // Set Chart.js global defaults for theme colors
      try {
        const isDarkMode = document.documentElement.getAttribute('data-theme') === 'dark';
        const textColor = isDarkMode ? '#f1f5f9' : '#0f172a';

        if (Chart.defaults && Chart.defaults.plugins && Chart.defaults.plugins.legend) {
          Chart.defaults.plugins.legend.labels.color = textColor;
          console.log('🎨 Set Chart.js global legend color to:', textColor);
        }
      } catch (error) {
        console.warn('⚠️ Could not set Chart.js global defaults:', error);
      }

      try {
        this.renderTopItemsChart();
        this.renderLocationChart();
        this.renderTimelineChart();
        this.setupEventHandlers();
      } catch (error) {
        console.error('❌ Analytics: Error rendering charts:', error);
        this.showChartError(`Error rendering charts: ${error.message}`);
      }
    }

    updateChartColors() {
      DEBUG.log('🎨 Analytics: Recreating charts for theme change');

      // Get current theme
      const isDarkMode = document.documentElement.getAttribute('data-theme') === 'dark';
      const textColor = isDarkMode ? '#f1f5f9' : '#0f172a';
      console.log('🎨 Current theme is dark:', isDarkMode, 'using text color:', textColor);

      // Update Chart.js global defaults
      try {
        if (
          typeof Chart !== 'undefined' &&
          Chart.defaults &&
          Chart.defaults.plugins &&
          Chart.defaults.plugins.legend
        ) {
          Chart.defaults.plugins.legend.labels.color = textColor;
          console.log('🎨 Updated Chart.js global legend color to:', textColor);
        }
      } catch (error) {
        console.warn('⚠️ Could not update Chart.js global defaults:', error);
      }

      try {
        // Recreate all charts to ensure proper theme colors
        // This is more reliable than trying to update existing charts
        this.renderTopItemsChart();
        this.renderLocationChart();
        this.renderTimelineChart();

        DEBUG.log('✅ Analytics: Charts recreated with new theme colors');
      } catch (error) {
        console.error('❌ Analytics: Error recreating charts for theme:', error);
      }
    }

    setupEventHandlers() {
      // Top Items Chart Type Toggles
      const topItemsToggles = this.container.querySelectorAll('[data-top-items-type]');
      topItemsToggles.forEach(toggle => {
        toggle.addEventListener('click', e => {
          topItemsToggles.forEach(t => t.classList.remove('active'));
          e.target.classList.add('active');

          this.topItemsChartType = e.target.dataset.topItemsType;

          if (this.charts.topItems) {
            this.charts.topItems.destroy();
            delete this.charts.topItems;
          }

          this.renderTopItemsChart();
        });
      });

      // Location Chart Type Toggles
      const locationToggles = this.container.querySelectorAll('[data-type]');
      locationToggles.forEach(toggle => {
        toggle.addEventListener('click', e => {
          // Remove active class from all toggles
          locationToggles.forEach(t => t.classList.remove('active'));
          // Add active class to clicked toggle
          e.target.classList.add('active');

          // Update chart type
          this.locationChartType = e.target.dataset.type;

          // Show/hide zoom info based on chart type
          const zoomInfo = qs('#locationZoomInfo', this.container);
          if (zoomInfo) {
            if (this.locationChartType === 'bar' || this.locationChartType === 'polarArea') {
              zoomInfo.style.display = 'inline-block';
            } else {
              zoomInfo.style.display = 'none';
            }
          }

          // Destroy existing chart before creating new one
          if (this.charts.location) {
            this.charts.location.destroy();
            delete this.charts.location;
          }

          this.renderLocationChart();
        });
      });

      // Timeline Chart Filter - REMOVED (shows all data)

      // Setup keyboard shortcuts for zoom
      this.setupKeyboardShortcuts();
    }

    setupKeyboardShortcuts() {
      // Keyboard shortcuts are handled by Chart.js modifierKey option
      // Add fallback zoom handling if plugin not available
      DEBUG.log('📈 Zoom: Hold Shift + scroll on timeline chart to zoom');

      // Fallback: Manual zoom handling if plugin fails
      const timelineCanvas = qs('#timelineChart', this.container);
      const locationCanvas = qs('#locationChart', this.container);

      [timelineCanvas, locationCanvas].forEach(canvas => {
        if (canvas) {
          canvas.addEventListener('wheel', e => {
            if (e.shiftKey) {
              e.preventDefault();
              DEBUG.log('� Shift + scroll detected on chart');
              // Basic zoom feedback
              const chart = Chart.getChart(canvas);
              if (chart && chart.resetZoom) {
                // If zoom plugin is available, this will work
                // If not, at least we prevent default scrolling
              }
            }
          });
        }
      });
    }

    renderTopItemsChart() {
      DEBUG.log('📊 Analytics: Rendering top items chart');

      // Destroy existing chart if it exists
      if (this.charts.topItems) {
        this.charts.topItems.destroy();
        delete this.charts.topItems;
      }

      const canvas = qs('#topItemsChart', this.container);
      if (!canvas) {
        console.error('❌ Analytics: Top items canvas not found');
        return;
      }

      // Always show all locations (filter removed)
      const topItems = analyticsService.getTopItems(8);

      if (topItems.length === 0) {
        canvas.style.display = 'none';
        return;
      }

      // Vibrant color palette
      const colors = [
        'rgba(255, 99, 132, 0.9)', // Red
        'rgba(54, 162, 235, 0.9)', // Blue
        'rgba(255, 206, 86, 0.9)', // Yellow
        'rgba(75, 192, 192, 0.9)', // Teal
        'rgba(153, 102, 255, 0.9)', // Purple
        'rgba(255, 159, 64, 0.9)', // Orange
        'rgba(46, 204, 113, 0.9)', // Green
        'rgba(231, 76, 60, 0.9)', // Dark Red
      ];

      const borderColors = [
        'rgba(255, 99, 132, 1)',
        'rgba(54, 162, 235, 1)',
        'rgba(255, 206, 86, 1)',
        'rgba(75, 192, 192, 1)',
        'rgba(153, 102, 255, 1)',
        'rgba(255, 159, 64, 1)',
        'rgba(46, 204, 113, 1)',
        'rgba(231, 76, 60, 1)',
      ];

      // Get current theme text color - directly check theme attribute
      const isDarkMode = document.documentElement.getAttribute('data-theme') === 'dark';
      const textColor = isDarkMode ? '#f1f5f9' : '#0f172a';

      // Common options for all chart types
      const baseOptions = {
        responsive: true,
        maintainAspectRatio: false,
        layout: {
          padding: {
            right: 20,
          },
        },
        plugins: {
          legend: {
            display: true,
            position: 'right',
            align: 'start',
            maxWidth: 250,
            labels: {
              padding: 10,
              font: { size: 11, weight: '600' },
              color: textColor,
              usePointStyle: true,
              pointStyle: this.topItemsChartType === 'bar' ? 'rectRounded' : 'circle',
              boxWidth: 12,
              boxHeight: 12,
              generateLabels(chart) {
                const data = chart.data;
                // Always check current theme dynamically
                const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
                const labelColor = isDark ? '#f1f5f9' : '#0f172a';
                console.log(
                  '🎨 Top Items generateLabels - isDark:',
                  isDark,
                  'labelColor:',
                  labelColor
                );
                if (chart.isDatasetVisible(0)) {
                  return data.labels.map((label, i) => {
                    const meta = chart.getDatasetMeta(0);
                    const hidden = meta.data[i] ? meta.data[i].hidden : false;
                    return {
                      text: `${label}: ${data.datasets[0].data[i]} units`,
                      fillStyle: data.datasets[0].backgroundColor[i],
                      strokeStyle: data.datasets[0].borderColor[i],
                      color: labelColor,
                      fontColor: labelColor, // Try both color properties
                      lineWidth: 2,
                      hidden,
                      index: i,
                    };
                  });
                }
                return [];
              },
            },
            onClick: (e, legendItem, legend) => {
              const index = legendItem.index;
              const chart = legend.chart;
              const meta = chart.getDatasetMeta(0);

              meta.data[index].hidden = !meta.data[index].hidden;
              chart.update();
            },
          },
          tooltip: {
            callbacks: {
              label: context => {
                const label = context.label || '';
                const value = this.topItemsChartType === 'bar' ? context.parsed.y : context.parsed;
                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                const percentage = ((value / total) * 100).toFixed(1);
                return `${label}: ${value} units (${percentage}%)`;
              },
            },
          },
        },
      };

      // Chart type specific options
      if (this.topItemsChartType === 'bar') {
        baseOptions.indexAxis = 'y';
        baseOptions.scales = {
          x: { beginAtZero: true, title: { display: true, text: 'Quantity (Units)' } },
          y: { ticks: { display: false } },
        };
      }

      const config = {
        type: this.topItemsChartType,
        data: {
          labels: topItems.map(item => item.item),
          datasets: [
            {
              label: 'Quantity',
              data: topItems.map(item => item.count),
              backgroundColor: topItems.map((_, index) => colors[index % colors.length]),
              borderColor: topItems.map((_, index) => borderColors[index % borderColors.length]),
              borderWidth: 3,
              hoverOffset: this.topItemsChartType === 'doughnut' ? 15 : 10,
            },
          ],
        },
        options: baseOptions,
      };

      try {
        this.charts.topItems = new Chart(canvas, config);
        DEBUG.log('✅ Analytics: Top items chart created successfully');
      } catch (error) {
        console.error('❌ Analytics: Error creating top items chart:', error);
      }
    }

    renderLocationChart() {
      DEBUG.log('📊 Analytics: Rendering location chart');

      // Destroy existing chart if it exists
      if (this.charts.location) {
        this.charts.location.destroy();
        delete this.charts.location;
      }

      const canvas = qs('#locationChart', this.container);
      if (!canvas) {
        console.error('❌ Analytics: Location canvas not found');
        return;
      }

      const locations = analyticsService.getLocationBreakdown();

      if (locations.length === 0) {
        canvas.style.display = 'none';
        return;
      }

      const chartType =
        this.locationChartType === 'doughnut'
          ? 'doughnut'
          : this.locationChartType === 'bar'
            ? 'bar'
            : 'polarArea';

      // Get current theme text color - directly check theme attribute
      const isDarkMode = document.documentElement.getAttribute('data-theme') === 'dark';
      const textColor = isDarkMode ? '#f1f5f9' : '#0f172a';

      // Generate unique color for each location using HSL
      const generateColor = (index, total, opacity = 0.8) => {
        const hue = ((index * 360) / total) % 360;
        const saturation = 65 + (index % 3) * 10; // Vary saturation 65-85%
        const lightness = 50 + (index % 2) * 10; // Vary lightness 50-60%
        return `hsla(${hue}, ${saturation}%, ${lightness}%, ${opacity})`;
      };

      const locationCount = locations.length;
      const backgroundColors = locations.map((_, i) => generateColor(i, locationCount, 0.8));
      const borderColors = locations.map((_, i) => generateColor(i, locationCount, 1));

      const config = {
        type: chartType,
        data: {
          labels: locations.map(loc => loc.location),
          datasets: [
            {
              label: chartType === 'bar' ? 'Items Count' : '',
              data: locations.map(loc => loc.count),
              backgroundColor: backgroundColors,
              borderColor: borderColors,
              borderWidth: 2,
              borderRadius: chartType === 'bar' ? 6 : 0,
            },
          ],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          layout: {
            padding: {
              right: 20,
            },
          },
          plugins:
            chartType === 'bar'
              ? {
                  legend: {
                    display: true,
                    position: 'right',
                    align: 'start',
                    maxWidth: 250,
                    labels: {
                      padding: 10,
                      font: { size: 11, weight: '600' },
                      color: textColor,
                      usePointStyle: true,
                      pointStyle: 'rectRounded',
                      boxWidth: 12,
                      boxHeight: 12,
                      generateLabels(chart) {
                        const data = chart.data;
                        // Always check current theme dynamically
                        const isDark =
                          document.documentElement.getAttribute('data-theme') === 'dark';
                        const labelColor = isDark ? '#f1f5f9' : '#0f172a';
                        console.log(
                          '🎨 Location Bar generateLabels - isDark:',
                          isDark,
                          'labelColor:',
                          labelColor
                        );
                        if (chart.isDatasetVisible(0)) {
                          return data.labels.map((label, i) => {
                            const meta = chart.getDatasetMeta(0);
                            const hidden = meta.data[i] ? meta.data[i].hidden : false;
                            return {
                              text: `${label}: ${data.datasets[0].data[i]} items`,
                              fillStyle: data.datasets[0].backgroundColor[i],
                              strokeStyle: data.datasets[0].borderColor[i],
                              color: labelColor,
                              fontColor: labelColor, // Try both color properties
                              lineWidth: 2,
                              hidden,
                              index: i,
                            };
                          });
                        }
                        return [];
                      },
                    },
                    onClick: (e, legendItem, legend) => {
                      const index = legendItem.index;
                      const chart = legend.chart;
                      const meta = chart.getDatasetMeta(0);

                      meta.data[index].hidden = !meta.data[index].hidden;
                      chart.update();
                    },
                  },
                }
              : {
                  legend: {
                    display: true,
                    position: 'right',
                    align: 'start',
                    maxWidth: 250,
                    labels: {
                      padding: 10,
                      font: { size: 11, weight: '600' },
                      color: textColor,
                      usePointStyle: true,
                      pointStyle: 'circle',
                      boxWidth: 12,
                      boxHeight: 12,
                      generateLabels(chart) {
                        const data = chart.data;
                        // Always check current theme dynamically
                        const isDark =
                          document.documentElement.getAttribute('data-theme') === 'dark';
                        const labelColor = isDark ? '#f1f5f9' : '#0f172a';
                        console.log(
                          '🎨 Location Circle generateLabels - isDark:',
                          isDark,
                          'labelColor:',
                          labelColor
                        );
                        if (chart.isDatasetVisible(0)) {
                          return data.labels.map((label, i) => {
                            const meta = chart.getDatasetMeta(0);
                            const hidden = meta.data[i] ? meta.data[i].hidden : false;
                            return {
                              text: `${label}: ${data.datasets[0].data[i]} items`,
                              fillStyle: data.datasets[0].backgroundColor[i],
                              strokeStyle: data.datasets[0].borderColor[i],
                              color: labelColor,
                              fontColor: labelColor, // Try both color properties
                              lineWidth: 2,
                              hidden,
                              index: i,
                            };
                          });
                        }
                        return [];
                      },
                    },
                    onClick: (e, legendItem, legend) => {
                      const index = legendItem.index;
                      const chart = legend.chart;
                      const meta = chart.getDatasetMeta(0);

                      meta.data[index].hidden = !meta.data[index].hidden;
                      chart.update();
                    },
                  },
                },
          animation:
            chartType === 'polarArea'
              ? {
                  animateRotate: true,
                  animateScale: true,
                  duration: 1000,
                }
              : true,
          interaction: {
            intersect: chartType === 'polarArea' ? false : true,
            mode: chartType === 'polarArea' ? 'point' : 'nearest',
          },
        },
      };

      try {
        this.charts.location = new Chart(canvas, config);
        DEBUG.log('✅ Analytics: Location chart created successfully');
      } catch (error) {
        console.error('❌ Analytics: Error creating location chart:', error);
      }
    }

    renderTimelineChart() {
      DEBUG.log('📊 Analytics: Rendering timeline chart');

      // Destroy existing chart if it exists
      if (this.charts.timeline) {
        this.charts.timeline.destroy();
        delete this.charts.timeline;
      }

      const canvas = qs('#timelineChart', this.container);
      if (!canvas) {
        console.error('❌ Analytics: Timeline canvas not found');
        return;
      }

      // Clear canvas context to prevent caching issues
      const ctx = canvas.getContext('2d');
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      const timeline = analyticsService.getTimelineData();

      if (timeline.length === 0) {
        canvas.style.display = 'none';
        return;
      }

      // Format dates to "MMM YYYY" (e.g., "Nov 2025")
      const formattedLabels = timeline.map(point => formatExpiryDisplay(point.date));

      const config = {
        type: 'line',
        data: {
          labels: formattedLabels,
          datasets: [
            {
              label: 'Items Expiring',
              data: timeline.map(point => point.count),
              borderColor: 'rgba(99, 102, 241, 1)',
              backgroundColor: 'rgba(99, 102, 241, 0.1)',
              fill: true,
              tension: 0.4,
              borderWidth: 3,
              pointBackgroundColor: 'rgba(255, 99, 132, 1)',
              pointBorderColor: '#fff',
              pointBorderWidth: 2,
              pointRadius: 6,
              pointHoverRadius: 8,
            },
          ],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          scales: {
            y: { beginAtZero: true },
          },
          plugins: {
            legend: {
              display: false,
            },
            zoom: {
              limits: {
                x: { minRange: 0.1 },
              },
              zoom: {
                wheel: {
                  enabled: true,
                  modifierKey: 'shift',
                  speed: 0.02,
                },
                mode: 'x',
                sensitivity: 3,
              },
              pan: {
                enabled: true,
                mode: 'x',
                modifierKey: 'shift',
                threshold: 5,
              },
            },
          },
        },
      };

      try {
        this.charts.timeline = new Chart(canvas, config);
        
        // Add double-click reset functionality
        canvas.addEventListener('dblclick', () => {
          if (this.charts.timeline && this.charts.timeline.resetZoom) {
            this.charts.timeline.resetZoom();
            DEBUG.log('🔄 Analytics: Timeline chart zoom reset');
          }
        });
        
        DEBUG.log('✅ Analytics: Timeline chart created successfully');
      } catch (error) {
        console.error('❌ Analytics: Error creating timeline chart:', error);
      }
    }

    printReport() {
      // Create a print-friendly version of the dashboard
      const printWindow = window.open('', '_blank');
      if (!printWindow) {
        notificationService.error('Please allow popups to print the report');
        return;
      }

      const title = 'ExpireWise Analytics Report';
      const timestamp = new Date().toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });

      // Temporarily update charts to use print-friendly colors (black text on white)
      const originalColors = {};

      // Helper to update chart colors for printing
      const updateChartForPrint = (chart, chartName) => {
        if (!chart) {
          return;
        }

        try {
          originalColors[chartName] = {
            scales: chart.options.scales ? JSON.parse(JSON.stringify(chart.options.scales)) : null,
            plugins: chart.options.plugins
              ? JSON.parse(JSON.stringify(chart.options.plugins))
              : null,
            generateLabels: chart.options.plugins?.legend?.labels?.generateLabels,
          };

          // Update scale colors to black
          if (chart.options.scales) {
            Object.keys(chart.options.scales).forEach(scaleKey => {
              const scale = chart.options.scales[scaleKey];
              if (scale.ticks) {
                scale.ticks.color = '#000000';
              }
              if (scale.grid) {
                scale.grid.color = '#e0e0e0';
              }
              if (scale.title) {
                scale.title.color = '#000000';
              }
            });
          }

          // Update legend colors to black
          if (chart.options.plugins?.legend?.labels) {
            chart.options.plugins.legend.labels.color = '#000000';

            // Wrap the generateLabels function to force black text
            const originalGenerateLabels = chart.options.plugins.legend.labels.generateLabels;
            if (originalGenerateLabels) {
              chart.options.plugins.legend.labels.generateLabels = function (chart) {
                try {
                  const labels = originalGenerateLabels.call(this, chart);
                  return labels.map(label => ({
                    ...label,
                    color: '#000000',
                  }));
                } catch (e) {
                  console.error('Error in generateLabels wrapper:', e);
                  return originalGenerateLabels.call(this, chart);
                }
              };
            }
          }

          chart.update('none'); // Update without animation
        } catch (e) {
          console.error(`Error updating chart ${chartName} for print:`, e);
        }
      };

      // Update all charts for print
      updateChartForPrint(this.charts.topItems, 'topItems');
      updateChartForPrint(this.charts.location, 'location');
      updateChartForPrint(this.charts.timeline, 'timeline');

      // Collect all chart canvases as images with higher quality
      const chartImages = {};
      if (this.charts.topItems) {
        try {
          chartImages.topItems = this.charts.topItems.toBase64Image('image/png', 1);
        } catch (e) {
          console.error('Failed to capture topItems chart:', e);
        }
      }
      if (this.charts.location) {
        try {
          chartImages.location = this.charts.location.toBase64Image('image/png', 1);
        } catch (e) {
          console.error('Failed to capture location chart:', e);
        }
      }
      if (this.charts.timeline) {
        try {
          chartImages.timeline = this.charts.timeline.toBase64Image('image/png', 1);
        } catch (e) {
          console.error('Failed to capture timeline chart:', e);
        }
      }

      // Restore original colors
      const restoreChartColors = (chart, chartName) => {
        if (!chart || !originalColors[chartName]) {
          return;
        }

        try {
          if (originalColors[chartName].scales) {
            chart.options.scales = originalColors[chartName].scales;
          }
          if (originalColors[chartName].plugins) {
            chart.options.plugins = originalColors[chartName].plugins;
          }
          if (originalColors[chartName].generateLabels && chart.options.plugins?.legend?.labels) {
            chart.options.plugins.legend.labels.generateLabels =
              originalColors[chartName].generateLabels;
          }

          chart.update('none');
        } catch (e) {
          console.error(`Error restoring chart ${chartName} colors:`, e);
        }
      };

      restoreChartColors(this.charts.topItems, 'topItems');
      restoreChartColors(this.charts.location, 'location');
      restoreChartColors(this.charts.timeline, 'timeline');

      // Get metrics
      const totalItems = document.querySelector('#totalItems')?.textContent || '-';
      const expiringThisMonth = document.querySelector('#thisMonth')?.textContent || '-';
      const expiringNextMonth = document.querySelector('#nextMonth')?.textContent || '-';
      const expiredItems = document.querySelector('#expired')?.textContent || '-';

      DEBUG.log('📊 Print Report - Captured data:', {
        totalItems,
        expiringThisMonth,
        expiringNextMonth,
        expiredItems,
        hasTopItems: !!chartImages.topItems,
        hasLocation: !!chartImages.location,
        hasTimeline: !!chartImages.timeline,
      });

      try {
        printWindow.document.write(`
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="UTF-8">
          <title>${title}</title>
          <style>
            @page {
              size: A4;
              margin: 1.5cm;
            }
            * {
              margin: 0;
              padding: 0;
              box-sizing: border-box;
            }
            body {
              font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Arial, sans-serif;
              color: #000;
              background: #fff;
              line-height: 1.6;
            }
            .header {
              text-align: center;
              margin-bottom: 30px;
              padding-bottom: 20px;
              border-bottom: 3px solid #6366f1;
            }
            .header h1 {
              font-size: 28px;
              color: #1e293b;
              margin-bottom: 8px;
            }
            .header .timestamp {
              font-size: 14px;
              color: #64748b;
            }
            .metrics {
              display: grid;
              grid-template-columns: repeat(4, 1fr);
              gap: 20px;
              margin-bottom: 40px;
            }
            .metric {
              padding: 20px;
              border: 2px solid #e2e8f0;
              border-radius: 8px;
              text-align: center;
            }
            .metric-value {
              font-size: 32px;
              font-weight: 700;
              color: #6366f1;
              margin-bottom: 4px;
            }
            .metric-label {
              font-size: 14px;
              color: #64748b;
              text-transform: uppercase;
              letter-spacing: 0.5px;
            }
            .chart-section {
              margin-bottom: 40px;
              page-break-inside: avoid;
            }
            .chart-title {
              font-size: 18px;
              font-weight: 600;
              color: #1e293b;
              margin-bottom: 16px;
              padding-bottom: 8px;
              border-bottom: 2px solid #e2e8f0;
            }
            .chart-image {
              width: 100%;
              height: auto;
              max-height: 400px;
              object-fit: contain;
              border: 1px solid #e2e8f0;
              border-radius: 8px;
              padding: 10px;
              background: #fff;
            }
            .footer {
              margin-top: 40px;
              padding-top: 20px;
              border-top: 2px solid #e2e8f0;
              text-align: center;
              font-size: 12px;
              color: #94a3b8;
            }
            @media print {
              body {
                print-color-adjust: exact;
                -webkit-print-color-adjust: exact;
              }
              .chart-section {
                page-break-inside: avoid;
              }
            }
          </style>
        </head>
        <body>
          <div class="header">
            <h1>📊 ${title}</h1>
            <div class="timestamp">Generated on ${timestamp}</div>
          </div>

          <div class="metrics">
            <div class="metric">
              <div class="metric-value">${totalItems}</div>
              <div class="metric-label">Total Items</div>
            </div>
            <div class="metric">
              <div class="metric-value">${expiringThisMonth}</div>
              <div class="metric-label">Expiring This Month</div>
            </div>
            <div class="metric">
              <div class="metric-value">${expiringNextMonth}</div>
              <div class="metric-label">Expiring Next Month</div>
            </div>
            <div class="metric">
              <div class="metric-value">${expiredItems}</div>
              <div class="metric-label">Expired Items</div>
            </div>
          </div>

          ${
            chartImages.topItems
              ? `
            <div class="chart-section">
              <div class="chart-title">📊 Top Items by Quantity</div>
              <img src="${chartImages.topItems}" alt="Top Items Chart" class="chart-image">
            </div>
          `
              : ''
          }

          ${
            chartImages.location
              ? `
            <div class="chart-section">
              <div class="chart-title">📍 Items by Location</div>
              <img src="${chartImages.location}" alt="Location Chart" class="chart-image">
            </div>
          `
              : ''
          }

          ${
            chartImages.timeline
              ? `
            <div class="chart-section">
              <div class="chart-title">📅 Expiry Timeline</div>
              <img src="${chartImages.timeline}" alt="Timeline Chart" class="chart-image">
            </div>
          `
              : ''
          }

          <div class="footer">
            Generated by ExpireWise • ${timestamp}
          </div>
        </body>
        </html>
      `);

        printWindow.document.close();

        // Show loading notification
        notificationService.info('Preparing print preview...');

        // Wait for images to load before printing
        setTimeout(() => {
          printWindow.focus();
          printWindow.print();

          // Close window after print dialog closes (user prints or cancels)
          setTimeout(() => {
            try {
              printWindow.close();
            } catch {
              DEBUG.log('Print window already closed');
            }
          }, 1000);
        }, 1000);
      } catch (e) {
        console.error('Error creating print window:', e);
        notificationService.error('Failed to generate print report');
        if (printWindow) {
          printWindow.close();
        }
      }
    }

    showChartError(message) {
      const errorDiv = document.createElement('div');
      errorDiv.className = 'alert alert-error';
      errorDiv.innerHTML = `
        <p><strong>Chart Error:</strong> ${message}</p>
        <p>Please ensure all required libraries are loaded.</p>
      `;

      this.container.appendChild(errorDiv);
    }
  }

  // Recommendations Dashboard Component
  class RecommendationsDashboard {
    constructor(container) {
      this.container = container;
      this.sortBy = 'priority'; // Default sort: priority, days, units, item, fromLocation, toLocation
      this.sortOrder = 'desc'; // asc or desc
    }

    renderAll() {
      DEBUG.log('💡 Recommendations: renderAll() called');

      if (!this.container) {
        console.error('❌ Recommendations: No container found');
        return;
      }

      let recommendations = recommendationsService.getTransferRecommendations();

      // Apply sorting
      recommendations = this.sortRecommendations(recommendations);

      this.container.innerHTML = `
        <div class="recommendations-dashboard">
          <div class="recommendations-header">
            <div>
              <h2>📦 Transfer Recommendations</h2>
              <p>Suggested transfers from lower-ranked to higher-ranked stores • Prioritizing items expiring within 60 days</p>
            </div>
            <div style="display: flex; gap: 16px; align-items: center;">
              ${
                recommendations.length > 0
                  ? `
                <button onclick="window.recommendationsDashboard.exportToExcel()" class="btn-modern">
                  <span>📊</span>
                  <span>Export to Excel</span>
                </button>
              `
                  : ''
              }
              <div style="text-align: right;">
                <div style="font-size: 2rem; font-weight: 700; color: var(--brand-primary);">${recommendations.length}</div>
                <div style="font-size: 0.75rem; color: var(--text-tertiary); text-transform: uppercase; letter-spacing: 1px;">Recommendations</div>
              </div>
            </div>
          </div>

          ${
            recommendations.length === 0
              ? `
            <div class="recommendations-empty">
              <div class="recommendations-empty-icon">✅</div>
              <h3>No Transfer Recommendations</h3>
              <p>All items are optimally distributed or not expiring soon.</p>
            </div>
          `
              : `
            <div class="recommendations-sort-controls">
              <label>Sort by:</label>
              <select onchange="window.recommendationsDashboard.setSortBy(this.value)">
                <option value="priority" ${this.sortBy === 'priority' ? 'selected' : ''}>Priority</option>
                <option value="days" ${this.sortBy === 'days' ? 'selected' : ''}>Days Until Expiry</option>
                <option value="units" ${this.sortBy === 'units' ? 'selected' : ''}>Units</option>
                <option value="item" ${this.sortBy === 'item' ? 'selected' : ''}>Item Name</option>
                <option value="fromLocation" ${this.sortBy === 'fromLocation' ? 'selected' : ''}>From Location</option>
                <option value="toLocation" ${this.sortBy === 'toLocation' ? 'selected' : ''}>To Location</option>
              </select>
              <button onclick="window.recommendationsDashboard.toggleSortOrder()" class="recommendations-sort-btn">
                <span>${this.sortOrder === 'asc' ? '↑' : '↓'}</span>
                <span>${this.sortOrder === 'asc' ? 'Ascending' : 'Descending'}</span>
              </button>
            </div>
            <div class="recommendations-grid">
              ${recommendations.map((rec, index) => this.renderRecommendationCard(rec, index)).join('')}
            </div>
          `
          }
        </div>
      `;
    }

    sortRecommendations(recommendations) {
      const sorted = [...recommendations];

      sorted.sort((a, b) => {
        let aVal, bVal;

        switch (this.sortBy) {
          case 'priority':
            aVal = a.priority;
            bVal = b.priority;
            break;
          case 'days':
            aVal = a.daysUntilExpiry;
            bVal = b.daysUntilExpiry;
            break;
          case 'units':
            aVal = a.units;
            bVal = b.units;
            break;
          case 'item':
            aVal = a.item.toLowerCase();
            bVal = b.item.toLowerCase();
            break;
          case 'fromLocation':
            aVal = a.fromLocation.toLowerCase();
            bVal = b.fromLocation.toLowerCase();
            break;
          case 'toLocation':
            aVal = a.toLocation.toLowerCase();
            bVal = b.toLocation.toLowerCase();
            break;
          default:
            aVal = a.priority;
            bVal = b.priority;
        }

        if (typeof aVal === 'string') {
          return this.sortOrder === 'asc' ? aVal.localeCompare(bVal) : bVal.localeCompare(aVal);
        } else {
          return this.sortOrder === 'asc' ? aVal - bVal : bVal - aVal;
        }
      });

      return sorted;
    }

    setSortBy(sortBy) {
      this.sortBy = sortBy;
      this.renderAll();
    }

    toggleSortOrder() {
      this.sortOrder = this.sortOrder === 'asc' ? 'desc' : 'asc';
      this.renderAll();
    }

    renderRecommendationCard(rec, _index) {
      const urgencyColor =
        rec.daysUntilExpiry <= 7
          ? 'var(--danger)'
          : rec.daysUntilExpiry <= 14
            ? 'var(--warning)'
            : 'var(--success)';
      const urgencyBg =
        rec.daysUntilExpiry <= 7
          ? 'rgba(248, 113, 113, 0.1)'
          : rec.daysUntilExpiry <= 14
            ? 'rgba(251, 146, 60, 0.1)'
            : 'rgba(16, 185, 129, 0.1)';

      return `
        <div class="recommendation-card" style="
          background: var(--bg-card);
          border: 1px solid var(--border-light);
          border-radius: 12px;
          padding: 20px;
          display: flex;
          gap: 20px;
          align-items: center;
          transition: all 0.3s ease;
          box-shadow: 0 2px 4px rgba(0,0,0,0.05);
        ">
          <!-- Item Info -->
          <div style="flex: 1; min-width: 0;">
            <div style="font-weight: 600; font-size: 16px; color: var(--text-primary); margin-bottom: 4px;">
              ${rec.item}
            </div>
            ${rec.itemNumber ? `<div style="font-size: 12px; color: var(--text-muted); margin-bottom: 8px;">Item #: ${rec.itemNumber}</div>` : '<div style="margin-bottom: 8px;"></div>'}
            <div style="display: flex; gap: 16px; flex-wrap: wrap; align-items: center;">
              <div style="display: flex; align-items: center; gap: 8px; font-size: 13px;">
                <span style="color: var(--text-muted);">From:</span>
                <span style="
                  background: ${urgencyBg};
                  color: var(--text-primary);
                  padding: 4px 10px;
                  border-radius: 6px;
                  font-weight: 600;
                  border: 1px solid ${urgencyColor};
                ">
                  ${rec.fromLocation} <span style="color: var(--text-muted); font-size: 11px;">(Rank ${rec.fromRank})</span>
                </span>
              </div>
              <div style="color: var(--text-muted); font-size: 18px;">→</div>
              <div style="display: flex; align-items: center; gap: 8px; font-size: 13px;">
                <span style="color: var(--text-muted);">To:</span>
                <span style="
                  background: rgba(16, 185, 129, 0.1);
                  color: var(--text-primary);
                  padding: 4px 10px;
                  border-radius: 6px;
                  font-weight: 600;
                  border: 1px solid var(--success);
                ">
                  ${rec.toLocation} <span style="color: var(--text-muted); font-size: 11px;">(Rank ${rec.toRank}) • Has ${rec.destinationInventory || 0} units</span>
                </span>
              </div>
            </div>
          </div>

          <!-- Stats -->
          <div style="flex-shrink: 0; display: flex; gap: 20px; align-items: center;">
            <div style="text-align: center;">
              <div style="font-size: 24px; font-weight: 700; color: ${urgencyColor};">${rec.units}</div>
              <div style="font-size: 11px; color: var(--text-muted); text-transform: uppercase;">Units</div>
            </div>
            <div style="text-align: center;">
              <div style="font-size: 24px; font-weight: 700; color: ${urgencyColor};">${rec.daysUntilExpiry}</div>
              <div style="font-size: 11px; color: var(--text-muted); text-transform: uppercase;">Days</div>
            </div>
            <div style="text-align: center; min-width: 80px;">
              <div style="font-size: 13px; font-weight: 600; color: var(--text-primary);">${formatExpiryDisplay(rec.expiryDate)}</div>
              <div style="font-size: 11px; color: var(--text-muted); text-transform: uppercase;">Expires</div>
            </div>
          </div>
        </div>
      `;
    }

    exportToExcel() {
      if (!window.XLSX) {
        notificationService.error('Excel library not loaded');
        return;
      }

      try {
        const recommendations = recommendationsService.getTransferRecommendations();

        if (recommendations.length === 0) {
          notificationService.warning('No recommendations to export');
          return;
        }

        const exportData = recommendations.map(rec => ({
          'Item Number': rec.itemNumber,
          Item: rec.item,
          'From Location': rec.fromLocation,
          '': '→',
          'To Location': rec.toLocation,
          'Destination Has': rec.destinationInventory || 0,
          'Transfer Units': rec.units,
          'Days Until Expiry': rec.daysUntilExpiry,
          'Expiry Date': rec.expiryDate,
        }));

        const worksheet = XLSX.utils.json_to_sheet(exportData);

        // Set column widths
        worksheet['!cols'] = [
          { wch: 15 }, // Item Number
          { wch: 40 }, // Item
          { wch: 18 }, // From Location
          { wch: 3 }, // Arrow
          { wch: 18 }, // To Location
          { wch: 14 }, // Destination Has
          { wch: 13 }, // Transfer Units
          { wch: 16 }, // Days Until Expiry
          { wch: 12 }, // Expiry Date
        ];

        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, 'Transfer Recommendations');

        const timestamp = new Date().toISOString().slice(0, 19).replace(/:/g, '-');
        const filename = `Transfer_Recommendations_${timestamp}.xlsx`;

        XLSX.writeFile(workbook, filename);
        notificationService.success(
          `Exported ${recommendations.length} recommendations to ${filename}`
        );
      } catch (error) {
        console.error('Export error:', error);
        notificationService.error('Failed to export recommendations');
      }
    }
  }

  // =======================================
  // MAIN APPLICATION
  // =======================================

  class ExpireWiseApp {
    constructor() {
      this.components = {};
      this.initialized = false;
    }

    async init() {
      if (this.initialized) {
        return;
      }

      DEBUG.log('🚀 Initializing ExpireWise (Offline Bundle)...');

      try {
        // Initialize user preferences
        window.userPreferences = new UserPreferencesService();

        // Listen for settings changes from parent window
        window.addEventListener('message', (event) => {
          if (event.data.type === 'settingsChanged') {
            DEBUG.log('📥 Received settings update from parent:', event.data.settings);
            // Reload settings from localStorage (parent already saved them)
            const expiryWarningDays = parseInt(localStorage.getItem('expireWise-expiryWarningDays') || '30');
            const criticalWarningDays = parseInt(localStorage.getItem('expireWise-criticalWarningDays') || '7');
            DEBUG.log('📝 Updated expiry thresholds:', { expiryWarningDays, criticalWarningDays });
            // Could trigger UI refresh here if needed
            this.updateUI();
          } else if (event.data.type === 'themeChanged') {
            DEBUG.log('🎨 Received theme change from parent:', event.data.theme);
            document.documentElement.setAttribute('data-theme', event.data.theme);
            appState.setTheme(event.data.theme);
          }
        });

        // Initialize theme
        this.initTheme();

        // Load data
        await this.loadData();

        // Initialize UI components
        this.initComponents();

        // Setup event handlers
        this.setupEventHandlers();

        // Setup auto-save
        this.setupAutoSave();

        // Update UI
        this.updateUI();

        this.initialized = true;
        DEBUG.log('✅ ExpireWise initialized successfully');

        // Debug current view state
        DEBUG.log('🔍 Initial view state:', {
          viewYear: appState.ui.viewYear,
          viewMonth: appState.ui.viewMonth,
          totalItems: appState.data?.items?.length || 0,
        });

        // Mark page as loaded
        document.body.classList.add('loaded');
      } catch (error) {
        console.error('❌ Initialization error:', error);
        notificationService.error(
          `Failed to initialize ExpireWise: ${error?.message || 'Unknown error'}`
        );
        throw error; // Re-throw to be caught by the outer handler
      }
    }

    initTheme() {
      const savedTheme = localStorage.getItem('appTheme') || 'dark';
      document.documentElement.setAttribute('data-theme', savedTheme);
      appState.setTheme(savedTheme);

      const themeToggle = qs('#themeToggle');
      const themeText = qs('#themeText');

      if (themeToggle) {
        themeToggle.checked = savedTheme === 'dark';
        themeToggle.addEventListener('change', () => {
          const newTheme = themeToggle.checked ? 'dark' : 'light';
          console.log('🎨 Theme changing to:', newTheme);
          document.documentElement.setAttribute('data-theme', newTheme);
          localStorage.setItem('appTheme', newTheme);
          appState.setTheme(newTheme);

          if (themeText) {
            themeText.textContent = newTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
          }

          // Update chart legend colors for the new theme
          if (
            this.components?.analytics &&
            typeof this.components.analytics.updateChartColors === 'function'
          ) {
            console.log('🎨 Calling updateChartColors on analytics component');
            this.components.analytics.updateChartColors();
          } else {
            console.warn('⚠️ Analytics component or updateChartColors method not available');
            console.log('Analytics component:', this.components?.analytics);
          }
        });
      }

      if (themeText) {
        themeText.textContent = savedTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
      }
    }

    async loadData() {
      const success = await dataService.loadData();

      if (!success) {
        notificationService.warning('Starting with empty data');
      }

      // Load dictionary from window.DICT if available
      if (window.DICT) {
        appState.setDictionary(window.DICT);
        DEBUG.log(
          '✓ Dictionary loaded:',
          window.DICT.items?.length || 0,
          'items,',
          window.DICT.stores?.length || 0,
          'stores'
        );
      } else {
        console.warn('⚠️ window.DICT not found! Autocomplete will not work.');
        console.warn('⚠️ Check if dictionaries.js is loaded before app.js in index.html');
      }

      // Set initial view to current month
      const { year, month } = getCurrentYearMonth();
      DEBUG.log('🗓️ Setting initial view date to:', `${year}-${month + 1}`, '(month is 0-based)');
      appState.setViewDate(year, month);
    }

    initComponents() {
      DEBUG.log('🔧 Initializing components...');

      // Advanced Search
      const searchContainer = qs('#advancedSearchContainer');
      if (searchContainer) {
        this.components.search = new AdvancedSearch(searchContainer);
        DEBUG.log('✓ Advanced Search initialized');

        // Listen for search results to update All Items table
        appState.subscribe('search:filtered', data => {
          if (appState.ui.currentTab === 'all-items') {
            DEBUG.log(
              `🔍 Applying search filters to All Items table: ${data.items.length} items found`
            );
            this.populateAllItemsTab(data.items);
          }
        });
      }

      // Month Carousel removed: scope navigation handled by prev/next buttons only

      // Items Table
      const tableContainer = qs('#itemsTable');
      const tableBody = qs('#itemsTableBody');
      if (tableContainer && tableBody) {
        this.components.table = new ItemsTable(tableContainer, tableBody);
        DEBUG.log('✓ Items Table initialized');
        // Store reference globally for debugging
        window.itemsTable = this.components.table;
      } else {
        console.error('❌ Table elements not found:', { tableContainer, tableBody });
      }

      // Item Form
      const itemForm = qs('#itemForm');
      if (itemForm) {
        this.components.itemForm = new ItemForm(itemForm);
        DEBUG.log('✓ Item Form initialized');
      } else {
        console.error('❌ Item Form (#itemForm) not found in DOM');
      }

      // Vertical Month Scroller (added 2025-10) — lightweight inline version
      const scrollerContainer = qs('#monthScrollerContainer');
      DEBUG.log('🔍 Looking for monthScrollerContainer:', scrollerContainer);
      if (scrollerContainer) {
        try {
          this.initMonthScroller(scrollerContainer);
          DEBUG.log('✓ Month Scroller initialized successfully');
        } catch (error) {
          console.error('❌ Error initializing Month Scroller:', error);
        }
      } else {
        console.error('❌ Month Scroller container (#monthScrollerContainer) not found in DOM');
      }

      // Analytics Dashboard
      const analyticsContainer = qs('#analytics-tab .analytics-container');
      if (analyticsContainer) {
        this.components.analytics = new AnalyticsDashboard(analyticsContainer);
        window.analyticsDashboard = this.components.analytics;
        DEBUG.log('✓ Analytics Dashboard initialized');

        // Render immediately if analytics tab is active
        if (appState.ui.currentTab === 'analytics') {
          setTimeout(() => {
            if (this.components.analytics) {
              this.components.analytics.renderAll();
            }
          }, 200);
        }
      }

      // Initialize Recommendations Dashboard
      const recommendationsContainer = qs('#recommendations-container');
      if (recommendationsContainer) {
        this.components.recommendations = new RecommendationsDashboard(recommendationsContainer);
        window.recommendationsDashboard = this.components.recommendations;
        DEBUG.log('✓ Recommendations Dashboard initialized');

        // Render immediately if recommendations tab is active
        if (appState.ui.currentTab === 'recommendations') {
          setTimeout(() => {
            if (this.components.recommendations) {
              this.components.recommendations.renderAll();
            }
          }, 200);
        }
      }

      DEBUG.log('✓ Components initialized:', Object.keys(this.components));
      
      // Initialize table search box
      this.initTableSearch();
    }
    
    initTableSearch() {
      const searchInput = qs('#item-search');
      const clearBtn = qs('#clear-search-btn');
      const itemCount = qs('#item-count');
      const emptyState = qs('#empty-state');
      const tableContainer = qs('.modern-table-container');
      
      if (!searchInput) {
        DEBUG.log('⚠️ Table search input not found');
        return;
      }
      
      DEBUG.log('✓ Initializing table search');
      
      let searchTimeout;
      
      const performSearch = () => {
        const searchTerm = searchInput.value.toLowerCase().trim();
        const { viewYear, viewMonth } = appState.ui;
        
        // Get items for current month
        let items = appState.getItemsForMonth(viewYear, viewMonth);
        const totalItems = items.length;
        
        // Filter by search term if provided
        if (searchTerm) {
          items = items.filter(item => 
            (item.desc && item.desc.toLowerCase().includes(searchTerm)) ||
            (item.sku && item.sku.toLowerCase().includes(searchTerm)) ||
            (item.number && item.number.toLowerCase().includes(searchTerm)) ||
            (item.location && item.location.toLowerCase().includes(searchTerm))
          );
          
          // Show/hide clear button and add active state
          if (clearBtn) {
            clearBtn.style.display = 'flex';
          }
          searchInput.classList.add('has-value');
        } else {
          // Hide clear button and remove active state
          if (clearBtn) {
            clearBtn.style.display = 'none';
          }
          searchInput.classList.remove('has-value');
        }
        
        // Update count with search indicator
        if (itemCount) {
          if (searchTerm) {
            itemCount.textContent = `${items.length} of ${totalItems} items`;
            itemCount.classList.add('searching');
            itemCount.title = `Showing ${items.length} items matching "${searchTerm}"`;
          } else {
            itemCount.textContent = `${items.length} item${items.length !== 1 ? 's' : ''}`;
            itemCount.classList.remove('searching');
            itemCount.title = '';
          }
        }
        
        // Show/hide empty state
        if (emptyState && tableContainer) {
          if (items.length === 0) {
            tableContainer.classList.add('hidden');
            emptyState.classList.remove('hidden');
            // Update empty state message for search
            if (searchTerm) {
              const emptyMessage = emptyState.querySelector('p');
              if (emptyMessage) {
                emptyMessage.textContent = `No items found matching "${searchTerm}"`;
              }
            }
          } else {
            tableContainer.classList.remove('hidden');
            emptyState.classList.add('hidden');
          }
        }
        
        // Emit filtered items for table to render
        appState.emit('search:filtered', { items });
      };
      
      searchInput.addEventListener('input', (e) => {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(performSearch, 300); // Debounce 300ms
      });
      
      // Clear button handler
      if (clearBtn) {
        clearBtn.addEventListener('click', () => {
          searchInput.value = '';
          searchInput.focus();
          performSearch();
        });
      }
      
      // Keyboard shortcuts
      searchInput.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
          searchInput.value = '';
          performSearch();
          searchInput.blur();
        }
      });
      
      // Global keyboard shortcut: Ctrl+F to focus search
      document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
          // Only if we're in the main tab (not All Items)
          const activeTab = localStorage.getItem('activeTab');
          if (activeTab === 'main' || !activeTab) {
            e.preventDefault();
            searchInput.focus();
            searchInput.select();
          }
        }
      });
      
      // Clear search on month change
      appState.subscribe('view:changed', () => {
        if (searchInput.value) {
          searchInput.value = '';
          performSearch();
        }
      });
    }

    initMonthScroller(container) {
      DEBUG.log('🎠 Initializing Modern Month Navigator');
      
      const monthsShort = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
      const currentDate = new Date();
      const currentYear = currentDate.getFullYear();
      const currentMonth = currentDate.getMonth();
      
      let isUpdating = false; // Prevent rebuild during updates
      
      // Update the display without rebuilding
      const updateDisplay = () => {
        const { viewYear, viewMonth } = appState.ui;
        
        // Update header
        const monthName = container.querySelector('.current-month-name');
        const monthYear = container.querySelector('.current-month-year');
        if (monthName) monthName.textContent = monthsShort[viewMonth];
        if (monthYear) monthYear.textContent = viewYear;
        
        // Update active states
        const monthsList = container.querySelector('.months-list');
        if (monthsList) {
          const items = monthsList.querySelectorAll('.month-item');
          items.forEach(item => {
            const itemYear = parseInt(item.dataset.year);
            const itemMonth = parseInt(item.dataset.month);
            
            if (itemYear === viewYear && itemMonth === viewMonth) {
              item.classList.add('active');
              // Scroll into view without bouncing
              if (!isUpdating) {
                isUpdating = true;
                setTimeout(() => {
                  item.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                  isUpdating = false;
                }, 50);
              }
            } else {
              item.classList.remove('active');
            }
          });
        }
      };
      
      // Build the navigator
      const build = () => {
        const { viewYear, viewMonth } = appState.ui;
        
        // Clear container
        container.innerHTML = '';
        container.className = 'month-scroller';
        
        // Create header with current month display and inline navigation
        const header = document.createElement('div');
        header.className = 'month-nav-header';
        
        const prevMonthBtn = document.createElement('button');
        prevMonthBtn.className = 'month-nav-arrow prev';
        prevMonthBtn.innerHTML = '‹';
        prevMonthBtn.title = 'Previous month in list (←)';
        prevMonthBtn.onclick = () => {
          // Get current state when clicked
          const { viewYear, viewMonth } = appState.ui;
          // Find previous month in the list
          const monthsList = container.querySelector('.months-list');
          if (monthsList) {
            const items = Array.from(monthsList.querySelectorAll('.month-item'));
            const currentIndex = items.findIndex(item => 
              parseInt(item.dataset.year) === viewYear && 
              parseInt(item.dataset.month) === viewMonth
            );
            console.log('Previous nav - currentIndex:', currentIndex, 'of', items.length);
            if (currentIndex > 0) {
              const prevItem = items[currentIndex - 1];
              console.log('Navigating to prev:', prevItem.dataset.year, prevItem.dataset.month);
              appState.setViewDate(
                parseInt(prevItem.dataset.year),
                parseInt(prevItem.dataset.month)
              );
            }
          }
        };
        
        const currentDisplay = document.createElement('div');
        currentDisplay.className = 'current-month-display';
        currentDisplay.innerHTML = `
          <div class="current-month-name">${monthsShort[viewMonth]}</div>
          <div class="current-month-year">${viewYear}</div>
        `;
        
        const nextMonthBtn = document.createElement('button');
        nextMonthBtn.className = 'month-nav-arrow next';
        nextMonthBtn.innerHTML = '›';
        nextMonthBtn.title = 'Next month in list (→)';
        nextMonthBtn.onclick = () => {
          // Get current state when clicked
          const { viewYear, viewMonth } = appState.ui;
          // Find next month in the list
          const monthsList = container.querySelector('.months-list');
          if (monthsList) {
            const items = Array.from(monthsList.querySelectorAll('.month-item'));
            const currentIndex = items.findIndex(item => 
              parseInt(item.dataset.year) === viewYear && 
              parseInt(item.dataset.month) === viewMonth
            );
            console.log('Next nav - currentIndex:', currentIndex, 'of', items.length);
            if (currentIndex >= 0 && currentIndex < items.length - 1) {
              const nextItem = items[currentIndex + 1];
              console.log('Navigating to next:', nextItem.dataset.year, nextItem.dataset.month);
              appState.setViewDate(
                parseInt(nextItem.dataset.year),
                parseInt(nextItem.dataset.month)
              );
            }
          }
        };
        
        header.appendChild(prevMonthBtn);
        header.appendChild(currentDisplay);
        header.appendChild(nextMonthBtn);
        
        // Navigation buttons
        const navControls = document.createElement('div');
        navControls.className = 'month-nav-controls';
        
        const todayBtn = document.createElement('button');
        todayBtn.className = 'month-nav-btn today';
        todayBtn.textContent = 'Current Month';
        todayBtn.title = 'Jump to current month';
        todayBtn.onclick = () => {
          appState.setViewDate(currentYear, currentMonth);
        };
        
        navControls.appendChild(todayBtn);
        
        // Quick jump months list
        const quickJump = document.createElement('div');
        quickJump.className = 'month-quick-jump';
        
        const quickJumpHeader = document.createElement('div');
        quickJumpHeader.className = 'quick-jump-header';
        
        const quickJumpTitle = document.createElement('div');
        quickJumpTitle.className = 'quick-jump-title';
        quickJumpTitle.textContent = 'Quick Jump';
        
        quickJumpHeader.appendChild(quickJumpTitle);
        quickJump.appendChild(quickJumpHeader);
        
        const monthsList = document.createElement('div');
        monthsList.className = 'months-list';
        
        // Generate 24 months before and 24 months after current view (4 years total)
        for (let offset = -24; offset <= 24; offset++) {
          const date = new Date(viewYear, viewMonth + offset, 1);
          const y = date.getFullYear();
          const m = date.getMonth();
          
          const monthItem = document.createElement('button');
          monthItem.className = 'month-item';
          monthItem.dataset.year = y;
          monthItem.dataset.month = m;
          
          // Highlight current actual month
          if (y === currentYear && m === currentMonth) {
            monthItem.classList.add('is-today');
          }
          
          // Highlight selected view month
          if (y === viewYear && m === viewMonth) {
            monthItem.classList.add('active');
          }
          
          monthItem.innerHTML = `
            <span class="month-item-name">${monthsShort[m]}</span>
            <span class="month-item-year">${y}</span>
          `;
          
          monthItem.onclick = () => {
            appState.setViewDate(y, m);
          };
          
          monthsList.appendChild(monthItem);
        }
        
        quickJump.appendChild(monthsList);
        
        // Assemble navigator
        container.appendChild(header);
        container.appendChild(navControls);
        container.appendChild(quickJump);
        
        // Scroll active month into view without animation on initial build
        setTimeout(() => {
          const activeItem = monthsList.querySelector('.month-item.active');
          if (activeItem) {
            activeItem.scrollIntoView({ behavior: 'auto', block: 'center' });
          }
        }, 50);
        
        // Add keyboard navigation
        const handleKeyboard = (e) => {
          if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
          
          const { viewYear, viewMonth } = appState.ui;
          const monthsList = container.querySelector('.months-list');
          
          if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
            e.preventDefault();
            // Navigate to previous month in list
            if (monthsList) {
              const items = Array.from(monthsList.querySelectorAll('.month-item'));
              const currentIndex = items.findIndex(item => 
                parseInt(item.dataset.year) === viewYear && 
                parseInt(item.dataset.month) === viewMonth
              );
              if (currentIndex > 0) {
                const prevItem = items[currentIndex - 1];
                appState.setViewDate(
                  parseInt(prevItem.dataset.year),
                  parseInt(prevItem.dataset.month)
                );
              }
            }
          } else if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
            e.preventDefault();
            // Navigate to next month in list
            if (monthsList) {
              const items = Array.from(monthsList.querySelectorAll('.month-item'));
              const currentIndex = items.findIndex(item => 
                parseInt(item.dataset.year) === viewYear && 
                parseInt(item.dataset.month) === viewMonth
              );
              if (currentIndex >= 0 && currentIndex < items.length - 1) {
                const nextItem = items[currentIndex + 1];
                appState.setViewDate(
                  parseInt(nextItem.dataset.year),
                  parseInt(nextItem.dataset.month)
                );
              }
            }
          } else if (e.key === 'Home') {
            e.preventDefault();
            appState.setViewDate(currentYear, currentMonth);
          }
        };
        
        // Remove old listener if exists
        if (container._keyboardHandler) {
          document.removeEventListener('keydown', container._keyboardHandler);
        }
        container._keyboardHandler = handleKeyboard;
        document.addEventListener('keydown', handleKeyboard);
      };
      
      // Initial build
      build();
      
      // Update on view changes (without rebuilding)
      appState.subscribe('view:changed', () => {
        const { viewYear, viewMonth } = appState.ui;
        
        // Check if we need to rebuild (month out of range)
        const monthsList = container.querySelector('.months-list');
        if (monthsList) {
          const inRange = monthsList.querySelector(
            `.month-item[data-year="${viewYear}"][data-month="${viewMonth}"]`
          );
          
          if (!inRange) {
            // Rebuild if out of range
            build();
          } else {
            // Just update display
            updateDisplay();
          }
        }
      });
    }

    setupEventHandlers() {
      DEBUG.log('🔧 Setting up event handlers...');

      // Ensure DOM is ready
      if (document.readyState === 'loading') {
        DEBUG.log('⏳ DOM still loading, waiting...');
        document.addEventListener('DOMContentLoaded', () => this.setupEventHandlers());
        return;
      }

      // Export Excel
      const exportBtn = qs('#exportFile');
      if (exportBtn) {
        exportBtn.onclick = null;
        exportBtn.removeEventListener('click', this.handleExport); // Remove any existing listeners
        exportBtn.addEventListener('click', e => {
          DEBUG.log('📊 Export button clicked - starting export...');
          e.preventDefault();
          e.stopPropagation();
          this.handleExport();
        });
        DEBUG.log('✓ Export button handler attached');
      } else {
        console.error('❌ Export button (#exportFile) not found in DOM');
      }

      // Import Excel
      const importBtn = qs('#importData');
      if (importBtn) {
        importBtn.onclick = null;
        importBtn.addEventListener('click', async e => {
          DEBUG.log('📥 Import button clicked - opening file picker...');
          DEBUG.log('📥 electronAPI available:', !!window.electronAPI);
          DEBUG.log('📥 selectFile available:', !!window.electronAPI?.selectFile);
          e.preventDefault();
          e.stopPropagation();
          
          // Use Electron file dialog if available
          if (window.electronAPI?.selectFile) {
            try {
              DEBUG.log('📥 Calling electronAPI.selectFile...');
              const filePath = await window.electronAPI.selectFile({
                filters: [
                  { name: 'Excel Files', extensions: ['xlsx', 'xlsm', 'xls'] },
                  { name: 'CSV Files', extensions: ['csv'] },
                  { name: 'All Files', extensions: ['*'] }
                ]
              });
              
              DEBUG.log('📁 File selection result:', filePath);
              
              if (filePath) {
                DEBUG.log('📁 File selected:', filePath);
                // Read the file using Electron API
                const fileData = await window.electronAPI.readFile(filePath);
                const fileName = filePath.split(/[\\/]/).pop();
                
                // Create a File-like object for compatibility
                const file = new File([fileData], fileName, {
                  type: fileName.endsWith('.csv') ? 'text/csv' : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
                });
                
                // Show import confirmation and process
                const importMode = await this.showImportConfirmationModal(file);
                if (importMode) {
                  await excelService.importFromExcel(file, importMode);
                  notificationService.success(`Import completed successfully (${importMode} mode)`);
                }
              } else {
                DEBUG.log('📁 File selection cancelled');
              }
            } catch (error) {
              console.error('Import failed:', error);
              notificationService.error(`Import failed: ${error.message}`);
            }
          } else {
            // Fallback to file input for non-Electron environment
            DEBUG.log('⚠️ electronAPI not available, falling back to file input');
            const fileInput = qs('#dataPicker');
            if (fileInput) {
              fileInput.click();
            } else {
              notificationService.error('File picker not available - electronAPI is missing');
              console.error('❌ electronAPI not available and no file input fallback found');
            }
          }
        });
        DEBUG.log('✓ Import button handlers attached');
      } else {
        console.error('❌ Import button (#importData) not found in DOM');
      }
      
      // Keep the file input handler for fallback
      const fileInput = qs('#dataPicker');
      if (fileInput) {
        fileInput.addEventListener('change', e => {
          DEBUG.log('📁 File selected via input, processing import...');
          this.handleImport(e);
        });
      }

      // Clear All Data
      const clearBtn = qs('#clearData');
      if (clearBtn) {
        clearBtn.onclick = null; // Remove existing onclick
        clearBtn.addEventListener('click', e => {
          DEBUG.log('🗑️ Clear button clicked via addEventListener');
          e.preventDefault();
          e.stopPropagation();
          this.handleClearData();
        });
        DEBUG.log('✓ Clear button handler attached');
      } else {
        console.error('❌ Clear button (#clearData) not found in DOM');
      }

      // Group By Location (Toggle)
      const groupByLocationBtn = qs('#groupByLocation');
      if (groupByLocationBtn) {
        groupByLocationBtn.addEventListener('click', () => {
          if (this.components.table) {
            if (this.components.table.groupBy === 'location') {
              DEBUG.log('🔄 Clear grouping clicked');
              this.components.table.clearGrouping();
              groupByLocationBtn.classList.remove('active');
              groupByLocationBtn.innerHTML = '📍 Group by Location';
            } else {
              DEBUG.log('📍 Group by location clicked');
              this.components.table.setGroupBy('location');
              groupByLocationBtn.classList.add('active');
              groupByLocationBtn.innerHTML = '📍 Ungroup';
            }
          }
        });
        DEBUG.log('✓ Group by location handler attached');
      }

      // View Current Month (jump to present month view)
      const viewCurrentMonthBtn = qs('#viewCurrentMonth');
      if (viewCurrentMonthBtn) {
        const applyCurrentMonthState = ({ year, month }) => {
          const today = new Date();
          const isCurrentMonth = year === today.getFullYear() && month === today.getMonth();
          viewCurrentMonthBtn.classList.toggle('active', isCurrentMonth);
          viewCurrentMonthBtn.setAttribute('aria-pressed', isCurrentMonth ? 'true' : 'false');
        };

        viewCurrentMonthBtn.addEventListener('click', e => {
          DEBUG.log('View current month requested');
          e.preventDefault();
          e.stopPropagation();
          const today = new Date();
          appState.setViewDate(today.getFullYear(), today.getMonth());
        });

        appState.subscribe('view:changed', applyCurrentMonthState);
        applyCurrentMonthState({ year: appState.ui.viewYear, month: appState.ui.viewMonth });
        DEBUG.log('View current month handler attached');
      } else {
        console.error('View Current Month button (#viewCurrentMonth) not found in DOM');
      }

      // Tab Navigation
      this.setupTabs();

      // Add Items button - Open modal
      const addItemBtn = qs('#addItemBtn');
      const addItemModal = qs('#addItemModal');
      const closeAddItemModal = qs('#closeAddItemModal');
      
      if (addItemBtn && addItemModal) {
        addItemBtn.addEventListener('click', () => {
          addItemModal.style.display = 'flex';
          DEBUG.log('✓ Add Items modal opened');
        });
        
        if (closeAddItemModal) {
          closeAddItemModal.addEventListener('click', () => {
            addItemModal.style.display = 'none';
            DEBUG.log('✓ Add Items modal closed');
          });
        }
        
        // Close modal when clicking outside
        addItemModal.addEventListener('click', (e) => {
          if (e.target === addItemModal) {
            addItemModal.style.display = 'none';
            DEBUG.log('✓ Add Items modal closed (outside click)');
          }
        });
        
        // Close modal on Escape key
        document.addEventListener('keydown', (e) => {
          if (e.key === 'Escape' && addItemModal.style.display === 'flex') {
            addItemModal.style.display = 'none';
            DEBUG.log('✓ Add Items modal closed (ESC key)');
          }
        });
        
        DEBUG.log('✓ Add Items modal handlers attached');
      }

      // Notifications button - REMOVED

      // Entry Tab Switching (Single vs Bulk)
      this.setupEntryTabs();

      // Bulk Entry Handlers
      this.setupBulkEntry();

      // Update active scope display
      appState.subscribe('view:changed', ({ year, month }) => {
        this.updateActiveScope(year, month);
      });

      // Update ALL UI components when data changes - immediate refresh
      const dataChangeEvents = [
        'item:added',
        'item:updated',
        'item:removed',
        'data:loaded',
        'data:cleared',
      ];
      dataChangeEvents.forEach(event => {
        appState.subscribe(event, () => {
          DEBUG.log(
            `🔄 Data change event received: ${event} - updating all components immediately`
          );

          // Update all components immediately without delays
          this.updateAllComponents();

          // Special handling for search filters if currently in all-items tab with active search
          const currentTab = localStorage.getItem('activeTab');
          if (
            currentTab === 'all-items' &&
            this.components.search &&
            this.components.search.hasActiveFilters &&
            this.components.search.hasActiveFilters()
          ) {
            // Reapply search filters to new data
            this.components.search.performSearch();
            DEBUG.log('✓ Reapplied search filters to updated data');
          }
        });
      });

      DEBUG.log('✅ Event handlers setup complete');
    }

    setupTabs() {
      const tabButtons = qsa('.tab');
      const tabContents = qsa('.tab-panel');

      tabButtons.forEach(button => {
        button.addEventListener('click', () => {
          const tabName = button.dataset.tab;
          DEBUG.log('🔄 Switching to tab:', tabName);

          // Update active button
          tabButtons.forEach(btn => btn.classList.remove('active'));
          button.classList.add('active');

          // Update active content
          tabContents.forEach(content => {
            content.style.display = content.id === `${tabName}-tab` ? 'block' : 'none';
          });

          // Update state
          appState.setCurrentTab(tabName);

          // Save preference
          localStorage.setItem('activeTab', tabName);

          // Special handling for analytics tab
          if (tabName === 'analytics' && this.components.analytics) {
            setTimeout(() => {
              DEBUG.log('📊 Rendering analytics dashboard...');
              this.components.analytics.renderAll();
            }, 100);
          }

          // Special handling for recommendations tab
          if (tabName === 'recommendations' && this.components.recommendations) {
            setTimeout(() => {
              DEBUG.log('💡 Rendering recommendations dashboard...');
              this.components.recommendations.renderAll();
            }, 100);
          }

          // Special handling for all-items tab
          if (tabName === 'all-items') {
            setTimeout(() => {
              DEBUG.log('📋 Populating ALL ITEMS tab...');
              // When switching to All Items tab, apply any active search filters
              if (
                this.components.search &&
                this.components.search.hasActiveFilters &&
                this.components.search.hasActiveFilters()
              ) {
                // Trigger search to apply current filters
                DEBUG.log('🔍 Reapplying active search filters...');
                this.components.search.performSearch();
              } else {
                // Show all items if no filters active
                DEBUG.log('📋 No active filters, showing all items...');
                this.populateAllItemsTab();
              }
            }, 100);
          }
        });
      });

      // Restore saved tab
      const savedTab = localStorage.getItem('activeTab') || 'main';
      const savedTabButton = qs(`.tab[data-tab="${savedTab}"]`);
      if (savedTabButton) {
        savedTabButton.click();
      }

      DEBUG.log('✓ Tab navigation setup complete');
    }

    populateAllItemsTab(filteredItems = null) {
      try {
        const allItemsTableBody = qs('#allItemsTableBody');
        if (!allItemsTableBody) {
          console.error('❌ All items table body not found');
          return;
        }

        // Initialize sort state if not present
        if (!this.allItemsSortBy) {
          this.allItemsSortBy = 'expiry';
          this.allItemsSortOrder = 'asc';
        }

        // Use filtered items if provided, otherwise get all items from data service
        const allItems = filteredItems || dataService.getAllItems();
        const filterStatus = filteredItems ? ' (filtered)' : '';
        DEBUG.log(`📋 Populating ALL ITEMS table with ${allItems.length} items${filterStatus}`);

        if (allItems.length === 0) {
          allItemsTableBody.innerHTML =
            '<tr><td colspan="7" style="text-align: center; padding: 40px; color: var(--text-muted);">No items found</td></tr>';
          return;
        }

        // Sort items based on current sort state
        allItems.sort((a, b) => {
          let aVal = a[this.allItemsSortBy];
          let bVal = b[this.allItemsSortBy];

          // Handle units as numbers
          if (this.allItemsSortBy === 'units') {
            aVal = parseInt(aVal) || 0;
            bVal = parseInt(bVal) || 0;
            return this.allItemsSortOrder === 'asc' ? aVal - bVal : bVal - aVal;
          }

          // Handle dates (expiry)
          if (this.allItemsSortBy === 'expiry') {
            aVal = aVal || '';
            bVal = bVal || '';
            return this.allItemsSortOrder === 'asc'
              ? aVal.localeCompare(bVal)
              : bVal.localeCompare(aVal);
          }

          // Handle text fields
          aVal = (aVal || '').toString().toLowerCase();
          bVal = (bVal || '').toString().toLowerCase();

          return this.allItemsSortOrder === 'asc'
            ? aVal.localeCompare(bVal)
            : bVal.localeCompare(aVal);
        });

        // Generate table rows
        allItemsTableBody.innerHTML = allItems
          .map(item => {
            const expiryDate = new Date(`${item.expiry}-01`);
            const now = new Date();
            const isExpired = expiryDate < now;
            const isExpiringSoon = !isExpired && expiryDate - now <= 30 * 24 * 60 * 60 * 1000; // 30 days

            let statusClass = '';
            if (isExpired) {
              statusClass = 'expired';
            } else if (isExpiringSoon) {
              statusClass = 'expiring-soon';
            }

            return `
            <tr class="${statusClass}" data-id="${escapeHtml(item.id)}">
              <td>${escapeHtml(item.desc)}</td>
              <td>${escapeHtml(item.sku || '-')}</td>
              <td>${escapeHtml(item.number || '-')}</td>
              <td>${escapeHtml(item.location)}</td>
              <td class="text-right">${escapeHtml(item.units)}</td>
              <td>${formatExpiryDisplay(item.expiry)}</td>
              <td class="actions-cell">
                <button class="btn small outline edit-btn" data-item-id="${escapeHtml(item.id)}" title="Edit Item" aria-label="Edit ${escapeHtml(item.desc)}">
                  <span class="icon" aria-hidden="true">✏️</span>
                </button>
                <button class="btn small danger delete-btn" data-item-id="${escapeHtml(item.id)}" title="Delete Item" aria-label="Delete ${escapeHtml(item.desc)}">
                  <span class="icon" aria-hidden="true">🗑️</span>
                </button>
              </td>
            </tr>
          `;
          })
          .join('');

        // Setup ALL ITEMS specific controls if not already done
        this.setupAllItemsControls();
      } catch (error) {
        console.error('❌ Error populating ALL ITEMS tab:', error);
      }
    }

    setupAllItemsControls() {
      // Setup sort headers for All Items table
      const allItemsTable = qs('#allItemsTable');
      const allItemsTableBody = qs('#allItemsTableBody');

      if (allItemsTable) {
        qsa('.sortable', allItemsTable).forEach(header => {
          if (!header.hasAttribute('data-sort-setup')) {
            // Add helpful tooltip
            const field = header.dataset.sort;
            const fieldName = header.textContent.trim();
            header.title = `Click to sort by ${fieldName}`;
            
            header.addEventListener('click', () => {
              // Update sort state
              if (this.allItemsSortBy === field) {
                this.allItemsSortOrder = this.allItemsSortOrder === 'asc' ? 'desc' : 'asc';
              } else {
                this.allItemsSortBy = field;
                this.allItemsSortOrder = 'asc';
              }

              // Update visual indicators
              qsa('.sortable', allItemsTable).forEach(h => {
                h.classList.remove('sorted-asc', 'sorted-desc');
                // Update tooltip based on current state
                const hField = h.dataset.sort;
                const hFieldName = h.textContent.trim().replace(/[↑↓]/g, '').trim();
                if (h === header) {
                  h.title = `Click to sort ${this.allItemsSortOrder === 'asc' ? 'descending' : 'ascending'}`;
                } else {
                  h.title = `Click to sort by ${hFieldName}`;
                }
              });
              header.classList.add(this.allItemsSortOrder === 'asc' ? 'sorted-asc' : 'sorted-desc');

              // Re-render the table with new sort
              this.populateAllItemsTab();
            });
            header.setAttribute('data-sort-setup', 'true');
          }
        });

        // Event delegation for edit/delete buttons in All Items table
        if (allItemsTableBody && !allItemsTableBody.hasAttribute('data-events-setup')) {
          allItemsTableBody.addEventListener('click', e => {
            const editBtn = e.target.closest('.edit-btn');
            const deleteBtn = e.target.closest('.delete-btn');

            if (editBtn) {
              const itemId = editBtn.dataset.itemId;
              if (window.editItem) {
                window.editItem(itemId);
              }
            }

            if (deleteBtn) {
              const itemId = deleteBtn.dataset.itemId;
              if (window.removeItem) {
                window.removeItem(itemId);
              }
            }
          });
          allItemsTableBody.setAttribute('data-events-setup', 'true');
        }
      }

      // Group by Location (All Items) - Toggle
      const groupByLocationAllBtn = qs('#groupByLocationAll');
      if (groupByLocationAllBtn && !groupByLocationAllBtn.hasAttribute('data-setup')) {
        groupByLocationAllBtn.addEventListener('click', () => {
          if (!this.allItemsGrouped) {
            DEBUG.log('🔄 Grouping ALL ITEMS by location');
            this.allItemsGrouped = true;
            this.groupAllItemsByLocation();
            groupByLocationAllBtn.classList.add('active');
            groupByLocationAllBtn.innerHTML = '📍 Ungroup';
          } else {
            DEBUG.log('🔄 Clearing ALL ITEMS grouping');
            this.allItemsGrouped = false;
            this.populateAllItemsTab();
            groupByLocationAllBtn.classList.remove('active');
            groupByLocationAllBtn.innerHTML = '📍 Group by Location';
          }
        });
        groupByLocationAllBtn.setAttribute('data-setup', 'true');
      }
    }

    groupAllItemsByLocation() {
      try {
        const allItemsTableBody = qs('#allItemsTableBody');
        if (!allItemsTableBody) {
          return;
        }

        const allItems = dataService.getAllItems();

        // Group items by location
        const groupedItems = {};
        allItems.forEach(item => {
          if (!groupedItems[item.location]) {
            groupedItems[item.location] = [];
          }
          groupedItems[item.location].push(item);
        });

        // Sort locations alphabetically
        const sortedLocations = Object.keys(groupedItems).sort();

        // Generate grouped table rows
        let html = '';
        sortedLocations.forEach(location => {
          // Location header
          html += `
            <tr class="group-header">
              <td colspan="7" style="background: var(--bg-secondary); font-weight: 600; padding: 12px 20px;">
                📍 ${location} (${groupedItems[location].length} items)
              </td>
            </tr>
          `;

          // Sort items in this location by expiry
          groupedItems[location].sort((a, b) => {
            const aDate = new Date(`${a.expiry}-01`);
            const bDate = new Date(`${b.expiry}-01`);
            return aDate - bDate;
          });

          // Items in this location
          groupedItems[location].forEach(item => {
            const expiryDate = new Date(`${item.expiry}-01`);
            const now = new Date();
            const isExpired = expiryDate < now;
            const isExpiringSoon = !isExpired && expiryDate - now <= 30 * 24 * 60 * 60 * 1000;

            let statusClass = '';
            if (isExpired) {
              statusClass = 'expired';
            } else if (isExpiringSoon) {
              statusClass = 'expiring-soon';
            }

            html += `
              <tr class="${statusClass}">
                <td>${item.desc}</td>
                <td>${item.sku || '-'}</td>
                <td>${item.number || '-'}</td>
                <td>${item.location}</td>
                <td class="text-right">${item.units}</td>
                <td>${item.expiry}</td>
                <td class="actions-cell">
                  <button class="btn small outline" onclick="window.editItem('${escapeHtml(item.id)}')" title="Edit Item">
                    <span class="icon">✏️</span>
                  </button>
                  <button class="btn small danger" onclick="window.removeItem('${escapeHtml(item.id)}')" title="Delete Item">
                    <span class="icon">🗑️</span>
                  </button>
                </td>
              </tr>
            `;
          });
        });

        allItemsTableBody.innerHTML = html;
      } catch (error) {
        console.error('❌ Error grouping ALL ITEMS by location:', error);
      }
    }

    setupEntryTabs() {
      const entryTabButtons = qsa('.entry-tab-btn');
      const entryTabContents = qsa('.entry-tab-content');

      entryTabButtons.forEach(button => {
        button.addEventListener('click', () => {
          const tabName = button.dataset.entryTab;

          // Update active button
          entryTabButtons.forEach(btn => btn.classList.remove('active'));
          button.classList.add('active');

          // Update active content
          entryTabContents.forEach(content => {
            if (content.id === `${tabName}-entry`) {
              content.style.display = 'block';
              content.classList.add('active');
            } else {
              content.style.display = 'none';
              content.classList.remove('active');
            }
          });
        });
      });

      DEBUG.log('✓ Entry tabs setup complete');
    }

    setupBulkEntry() {
      const bulkLocation = qs('#bulkLocation');
      const bulkExpiry = qs('#bulkExpiry');
      const bulkSkus = qs('#bulkSkus');
      const processBulkBtn = qs('#processBulkBtn');
      const clearBulkBtn = qs('#clearBulkBtn');
      const confirmBulkBtn = qs('#confirmBulkBtn');
      const cancelBulkBtn = qs('#cancelBulkBtn');
      const bulkPreview = qs('#bulkPreview');
      const bulkPreviewList = qs('#bulkPreviewList');
      const bulkHint = qs('#bulkHint');

      let previewedItems = [];

      // Populate bulk location select
      const bulkLocationSelect = qs('#bulkLocation');
      if (bulkLocationSelect && bulkLocationSelect.tagName === 'SELECT') {
        const dict = appState.dict;
        let locations = ['Warehouse A', 'Warehouse B', 'Store Front']; // default locations

        // Extract location names from dictionary stores if available
        if (dict.stores && Array.isArray(dict.stores)) {
          locations = dict.stores.map(s => s.name).filter(n => n);
        }

        // Clear and add placeholder
        bulkLocationSelect.innerHTML = '<option value="">Select Location...</option>';

        locations.forEach(loc => {
          const option = document.createElement('option');
          option.value = loc;
          option.textContent = loc;
          bulkLocationSelect.appendChild(option);
        });
      }

      // Auto-fill expiry to current month
      if (bulkExpiry && !bulkExpiry.value) {
        const { year, month } = getCurrentYearMonth();
        bulkExpiry.value = formatDateForInput(year, month);
      }

      // Process SKUs
      if (processBulkBtn) {
        processBulkBtn.addEventListener('click', () => {
          const location = bulkLocation.value;
          const expiry = bulkExpiry.value;
          const skusText = bulkSkus.value.trim();

          if (!location || !expiry || !skusText) {
            bulkHint.textContent = '⚠️ Please fill all fields';
            bulkHint.style.color = 'var(--warning)';
            return;
          }

          const skus = skusText
            .split('\n')
            .map(sku => sku.trim())
            .filter(sku => sku.length > 0);

          if (skus.length === 0) {
            bulkHint.textContent = '⚠️ No valid SKUs entered';
            bulkHint.style.color = 'var(--warning)';
            return;
          }

          // Get existing items to check for duplicates
          const existingItems = appState.data?.items || [];
          const duplicates = [];
          const uniqueSkus = [];
          const duplicateItems = [];

          // Filter out duplicates and track duplicate items
          skus.forEach(sku => {
            const existingItem = existingItems.find(
              item => item.sku === sku && item.location === location && item.expiry === expiry
            );

            if (existingItem) {
              duplicates.push(sku);
              duplicateItems.push(existingItem);
            } else {
              uniqueSkus.push(sku);
            }
          });

          // Look up item details from dictionary for each SKU
          const dict = appState.dict;
          previewedItems = uniqueSkus.map((sku) => {
            // Find matching item in dictionary
            let itemDetails = null;
            if (dict.items && Array.isArray(dict.items)) {
              itemDetails = dict.items.find(item => {
                if (Array.isArray(item.sku)) {
                  return item.sku.includes(sku);
                }
                return item.sku === sku;
              });
            }

            return {
              sku,
              desc: itemDetails ? itemDetails.desc : `Unknown Item`,
              number: itemDetails ? itemDetails.number : '',
              location,
              expiry,
              units: 1,
              id: null,
              verified: !!itemDetails,
            };
          });

          // Count verified vs unverified items
          const verifiedCount = previewedItems.filter(item => item.verified).length;
          const unverifiedCount = previewedItems.length - verifiedCount;

          // Display preview
          let previewHtml = '';

          // Warning for unverified SKUs
          if (unverifiedCount > 0) {
            const unverifiedSkus = previewedItems
              .filter(item => !item.verified)
              .map(item => item.sku);
            previewHtml += `
              <div style="padding: 12px; margin-bottom: 10px; background: var(--danger-06); border: 1px solid var(--danger-border); border-radius: 8px;">
                <strong style="color: var(--danger);">⚠️ ${unverifiedCount} SKU(s) not found in dictionary:</strong>
                <div style="margin-top: 8px; font-size: 13px; color: var(--text-secondary);">
                  ${unverifiedSkus.join(', ')}
                </div>
                <div style="margin-top: 4px; font-size: 12px; color: var(--text-muted);">
                  These SKUs will be added with generic descriptions. Please verify before adding.
                </div>
              </div>
            `;
          }

          if (duplicates.length > 0) {
            previewHtml += `
              <div style="padding: 12px; margin-bottom: 10px; background: var(--warning-06); border: 1px solid var(--warning-border); border-radius: 8px;">
                <strong style="color: var(--warning);">⚠️ ${duplicates.length} duplicate(s) found:</strong>
                <div style="margin-top: 8px; font-size: 13px; color: var(--text-secondary);">
                  ${duplicates.map(d => escapeHtml(d)).join(', ')}
                </div>
                <div style="margin-top: 8px; font-size: 12px; color: var(--text-muted);">
                  These SKUs already exist with the same location and expiry date.
                </div>
                <button class="btn small outline" id="updateDuplicatesBtn" style="margin-top: 10px;">
                  Update Quantities (+1 unit each)
                </button>
              </div>
            `;
          }

          // Verification summary
          if (verifiedCount > 0) {
            previewHtml += `
              <div style="padding: 12px; margin-bottom: 10px; background: var(--success-06); border: 1px solid var(--success-border); border-radius: 8px;">
                <strong style="color: var(--success);">✓ ${verifiedCount} SKU(s) verified from dictionary</strong>
              </div>
            `;
          }

          previewHtml +=
            '<div style="font-weight: 600; margin-bottom: 10px; padding: 8px; background: var(--bg-tertiary); border-radius: 6px;">Items to be added:</div>';

          previewHtml += previewedItems
            .map(
              (item, index) => `
            <div style="padding: 12px; border-bottom: 1px solid var(--border-light); background: ${item.verified ? 'transparent' : 'var(--warning-06)'};">
              <div style="display: flex; justify-content: space-between; align-items: start; margin-bottom: 6px;">
                <div style="flex: 1;">
                  <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 4px;">
                    <strong style="color: ${item.verified ? 'var(--success)' : 'var(--danger)'};">
                      ${item.verified ? '✓' : '⚠️'} SKU: ${escapeHtml(item.sku)}
                    </strong>
                  </div>
                  <div style="font-size: 14px; color: var(--text-primary); margin-bottom: 2px;">
                    <strong>Description:</strong> ${escapeHtml(item.desc)}
                  </div>
                  ${item.number ? `<div style="font-size: 13px; color: var(--text-secondary);"><strong>Item #:</strong> ${escapeHtml(item.number)}</div>` : ''}
                  <div style="font-size: 13px; color: var(--text-secondary); margin-top: 4px;">
                    <strong>Location:</strong> ${escapeHtml(item.location)} | <strong>Expiry:</strong> ${escapeHtml(item.expiry)} | <strong>Units:</strong> ${escapeHtml(item.units)}
                  </div>
                </div>
                <button class="btn small danger" onclick="window.removeBulkPreviewItem(${index})" style="margin-left: 10px;">✕</button>
              </div>
            </div>
          `
            )
            .join('');

          bulkPreviewList.innerHTML = previewHtml;
          bulkPreview.style.display = 'block';

          // Add event listener for update duplicates button
          const updateDuplicatesBtn = qs('#updateDuplicatesBtn');
          if (updateDuplicatesBtn) {
            updateDuplicatesBtn.addEventListener('click', async () => {
              let updatedCount = 0;
              for (const item of duplicateItems) {
                try {
                  const updatedItem = {
                    ...item,
                    units: parseInt(item.units, 10) + 1,
                  };
                  await appState.updateItem(updatedItem);
                  updatedCount++;
                } catch (error) {
                  console.error('Error updating item:', error);
                }
              }
              notificationService.success(
                `Updated ${updatedCount} duplicate item(s) (+1 unit each)`
              );
              updateDuplicatesBtn.disabled = true;
              updateDuplicatesBtn.textContent = '✓ Updated';
            });
          }

          if (previewedItems.length === 0 && duplicates.length === 0) {
            bulkHint.textContent = '⚠️ No items to process';
            bulkHint.style.color = 'var(--warning)';
          } else if (previewedItems.length === 0) {
            bulkHint.textContent = `⚠️ All SKUs are duplicates - ${duplicates.length} found`;
            bulkHint.style.color = 'var(--warning)';
          } else {
            bulkHint.textContent = `✓ ${previewedItems.length} unique item(s) ready to add${duplicates.length > 0 ? ` (${duplicates.length} duplicate(s) found)` : ''}`;
            bulkHint.style.color = 'var(--success)';
          }
        });
      }

      // Clear bulk form
      if (clearBulkBtn) {
        clearBulkBtn.addEventListener('click', () => {
          bulkLocation.value = '';
          bulkExpiry.value = '';
          bulkSkus.value = '';
          bulkPreview.style.display = 'none';
          bulkHint.textContent = '';
          previewedItems = [];
        });
      }

      // Confirm and add all items
      if (confirmBulkBtn) {
        confirmBulkBtn.addEventListener('click', async () => {
          if (previewedItems.length === 0) {
            return;
          }

          let addedCount = 0;
          for (const item of previewedItems) {
            try {
              await appState.addItem(item);
              addedCount++;
            } catch (error) {
              console.error('Error adding item:', error);
            }
          }

          notificationService.success(`Added ${addedCount} items successfully`);

          // Clear form
          bulkLocation.value = '';
          bulkExpiry.value = '';
          bulkSkus.value = '';
          bulkPreview.style.display = 'none';
          bulkHint.textContent = '';
          previewedItems = [];
        });
      }

      // Cancel bulk preview
      if (cancelBulkBtn) {
        cancelBulkBtn.addEventListener('click', () => {
          bulkPreview.style.display = 'none';
          previewedItems = [];
        });
      }

      // Global function to remove preview item
      window.removeBulkPreviewItem = index => {
        previewedItems.splice(index, 1);
        processBulkBtn.click(); // Re-render preview
      };

      DEBUG.log('✓ Bulk entry handlers setup complete');
    }

    async setupAutoSave() {
      // Check user preferences for auto-save
      let autoSaveEnabled = true;
      if (window.userPreferences) {
        const preferences = window.userPreferences.getPreferences();
        autoSaveEnabled = preferences.autoSave;
      }

      if (!autoSaveEnabled) {
        return;
      }

      const isElectron = storageService.isElectron;

      // Subscribe to data changes
      appState.subscribe('save:pending', () => {
        const autoSaveEnabled = localStorage.getItem('expirewise-autosave-enabled') !== 'false';
        if (autoSaveEnabled) {
          this.updateStorageStatus('Saving...', 'saving');
        }
      });

      appState.subscribe('save:completed', () => {
        const autoSaveEnabled = localStorage.getItem('expirewise-autosave-enabled') !== 'false';
        if (autoSaveEnabled) {
          this.updateStorageStatus('Saved', 'active');
          setTimeout(() => {
            this.updateStorageStatus('Auto-Save Active', 'active');
          }, 2000);
        }
      });

      // Setup status based on environment and user preferences
      // Update autoSaveEnabled from localStorage
      autoSaveEnabled = localStorage.getItem('expirewise-autosave-enabled') !== 'false';

      if (isElectron) {
        // In Electron, show file path on click
        const storageStatusEl = qs('#storageStatus');
        if (storageStatusEl) {
          storageStatusEl.style.cursor = 'pointer';
          storageStatusEl.title = autoSaveEnabled
            ? 'Auto-Save Active - Click to view location'
            : 'Auto-Save Disabled - Click to view location';

          // Set initial status
          if (autoSaveEnabled) {
            this.updateStorageStatus('Auto-Save Active', 'active');
          } else {
            this.updateStorageStatus('Auto-Save Disabled', 'disabled');
          }

          storageStatusEl.addEventListener('click', async () => {
            const path = await window.expireWise.getDataFilePath();
            notificationService.info(`Data location: ${path}`);
          });
        }
      } else {
        // Request file handle for auto-save (web version)
        const status = storageService.getStatus();
        if (status.supportsFileSystem) {
          const storageStatusEl = qs('#storageStatus');
          if (storageStatusEl) {
            storageStatusEl.style.cursor = 'pointer';

            if (autoSaveEnabled) {
              storageStatusEl.title = 'Click to enable auto-save to file';
              this.updateStorageStatus('LocalStorage', 'info');

              storageStatusEl.addEventListener('click', async () => {
                const handle = await storageService.requestFileHandle();
                if (handle) {
                  notificationService.success('Auto-save to file enabled');
                  this.updateStorageStatus('Auto-Save to File', 'active');
                  await dataService.saveData();
                }
              });
            } else {
              this.updateStorageStatus('Auto-Save Disabled', 'disabled');
            }
          }
        } else {
          // No file system API support
          if (autoSaveEnabled) {
            this.updateStorageStatus('LocalStorage', 'active');
          } else {
            this.updateStorageStatus('Auto-Save Disabled', 'disabled');
          }
        }
      }
    }

    updateStorageStatus(text, type = 'info') {
      const statusEl = qs('#storageStatus');
      if (statusEl) {
        statusEl.textContent = text;

        // Remove existing status classes
        statusEl.classList.remove('badge-success', 'badge-warning', 'badge-info', 'badge-saving');

        // Add appropriate class based on type
        switch(type) {
          case 'active':
            statusEl.classList.add('badge-success');
            statusEl.innerHTML = '✓ ' + text;
            break;
          case 'saving':
            statusEl.classList.add('badge-saving');
            statusEl.innerHTML = '⟳ ' + text;
            break;
          case 'disabled':
            statusEl.classList.add('badge-warning');
            statusEl.innerHTML = '⚠ ' + text;
            break;
          case 'info':
          default:
            statusEl.classList.add('badge-info');
            break;
        }
      }
    }

    updateActiveScope(year, month) {
      const scopeEl = qs('#activeScope');
      if (scopeEl) {
        const months = [
          'Jan',
          'Feb',
          'Mar',
          'Apr',
          'May',
          'Jun',
          'Jul',
          'Aug',
          'Sep',
          'Oct',
          'Nov',
          'Dec',
        ];
        scopeEl.textContent = `in ${months[month]} ${year}`;
      }
    }

    async handleExport() {
      DEBUG.log('📊 Handling export...');
      try {
        await excelService.exportToExcel();
      } catch (error) {
        console.error('Export failed:', error);
        notificationService.error(`Export failed: ${error.message}`);
      }
    }

    async handleImport(event) {
      DEBUG.log('📥 Handling import...');
      const file = event.target.files[0];
      if (!file) {
        return;
      }

      // Show import confirmation modal
      const importMode = await this.showImportConfirmationModal(file);
      if (!importMode) {
        // User cancelled
        event.target.value = '';
        return;
      }

      try {
        await excelService.importFromExcel(file, importMode);
        notificationService.success(`Import completed successfully (${importMode} mode)`);
      } catch (error) {
        console.error('Import failed:', error);
        notificationService.error(`Import failed: ${error.message}`);
      }

      // Clear file input
      event.target.value = '';
    }

    async showImportConfirmationModal(file) {
      return new Promise(resolve => {
        const currentItemCount = appState.data?.items?.length || 0;

        const modal = document.createElement('div');
        modal.className = 'alert-modal-overlay';
        modal.innerHTML = `
          <div class="alert-modal">
            <div class="alert-modal-header">
              <h3>📥 Import Excel File</h3>
              <button class="close-alert-modal">×</button>
            </div>
            <div class="alert-modal-body">
              <div class="import-info">
                <div class="file-info">
                  <h4>📄 File: ${escapeHtml(file.name)}</h4>
                  <p>Size: ${(file.size / 1024).toFixed(1)} KB</p>
                  <p>Modified: ${file.lastModified ? new Date(file.lastModified).toLocaleDateString() : 'Unknown'}</p>
                </div>
                
                <div class="current-data-info">
                  <h4>📊 Current Data</h4>
                  <p>You currently have <strong>${currentItemCount}</strong> items in the system.</p>
                </div>

                <div class="import-options">
                  <h4>🔄 Import Mode</h4>
                  <p>How would you like to handle the imported data?</p>
                  
                  <div class="option-group">
                    <label class="import-option">
                      <input type="radio" name="importMode" value="overwrite" ${currentItemCount === 0 ? 'checked' : ''}>
                      <div class="option-content">
                        <div class="option-title">🔄 Overwrite All Data</div>
                        <div class="option-description">Replace all current items with the imported data. Current data will be lost.</div>
                      </div>
                    </label>
                  </div>

                  <div class="option-group">
                    <label class="import-option">
                      <input type="radio" name="importMode" value="merge" ${currentItemCount > 0 ? 'checked' : ''}>
                      <div class="option-content">
                        <div class="option-title">➕ Merge with Current Data</div>
                        <div class="option-description">Add imported items to existing data. Duplicates will be added as separate items.</div>
                      </div>
                    </label>
                  </div>

                  <div class="option-group">
                    <label class="import-option">
                      <input type="radio" name="importMode" value="update">
                      <div class="option-content">
                        <div class="option-title">🔄 Smart Merge</div>
                        <div class="option-description">Merge data intelligently - update existing items (by SKU/Number) and add new ones.</div>
                      </div>
                    </label>
                  </div>
                </div>
              </div>
            </div>
            <div class="alert-modal-footer">
              <button class="btn outline" id="cancelImport">Cancel</button>
              <button class="btn primary" id="confirmImport">Continue Import</button>
            </div>
          </div>
        `;

        document.body.appendChild(modal);

        const confirmButton = modal.querySelector('#confirmImport');
        const cancelButton = modal.querySelector('#cancelImport');
        const closeButton = modal.querySelector('.close-alert-modal');

        confirmButton.onclick = function () {
          const selectedMode = modal.querySelector('input[name="importMode"]:checked')?.value;
          document.body.removeChild(modal);
          resolve(selectedMode || 'overwrite');
        };

        cancelButton.onclick = function () {
          document.body.removeChild(modal);
          resolve(null);
        };

        closeButton.onclick = function () {
          document.body.removeChild(modal);
          resolve(null);
        };
      });
    }

    async handleClearData() {
      DEBUG.log('🗑️ Handling clear data...');
      if (confirm('Are you sure you want to clear all data? This action cannot be undone.')) {
        try {
          await dataService.clearAllData();
          // UI will be updated automatically via data:cleared event emitted by appState.clearData()
          notificationService.success('All data cleared successfully');
        } catch (error) {
          console.error('Clear data failed:', error);
          notificationService.error(`Failed to clear data: ${error.message}`);
        }
      }
    }

    showNotificationPanel() {}
    getNotificationHistory() {
      return [];
    }
    getActiveAlerts() {
      return [];
    }
    renderNotificationAlert() {
      return '';
    }
    renderNotificationItem() {
      return '';
    }
    addNotificationToHistory() {}
    clearAllNotifications() {}
    markAllNotificationsRead() {}
    markNotificationsViewed() {}
    updateNotificationBadge() {}
    clearNotificationBadge() {}
    updateAllComponents() {
      DEBUG.log('🔄 Updating all UI components...');

      try {
        // Update month view (main calendar)
        if (this.populateMonthView) {
          this.populateMonthView();
          DEBUG.log('✓ Month view updated');
        }

        // Update all items table
        if (this.populateAllItemsTab) {
          this.populateAllItemsTab();
          DEBUG.log('✓ All items tab updated');
        }

        // Update items table component
        if (this.components?.table && typeof this.components.table.render === 'function') {
          this.components.table.render();
          DEBUG.log('✓ Items table component updated');
        }

        // Update analytics dashboard if it exists
        if (this.components?.analytics) {
          if (typeof this.components.analytics.updateQuickStats === 'function') {
            this.components.analytics.updateQuickStats();
            DEBUG.log('✓ Analytics stats updated');
          }

          // Re-render charts if analytics tab is currently active
          const currentTab = localStorage.getItem('activeTab');
          if (
            currentTab === 'analytics' &&
            typeof this.components.analytics.renderCharts === 'function'
          ) {
            this.components.analytics.renderCharts();
            DEBUG.log('✓ Analytics charts updated');
          }
        }

        // Update any other UI elements that display item counts
        this.updateItemCountDisplays();

        DEBUG.log('✅ All UI components updated successfully');
      } catch (error) {
        console.error('❌ Error updating UI components:', error);
      }
    }

    updateItemCountDisplays() {
      // Update any item count displays that might be scattered throughout the UI
      try {
        const totalItems = appState.data?.items?.length || 0;

        // Update any elements with data-item-count attribute
        document.querySelectorAll('[data-item-count]').forEach(el => {
          el.textContent = totalItems;
        });

        // Emit UI update event for any other components that need to know
        appState.emit('ui:updated', { totalItems });
      } catch (error) {
        console.error('❌ Error updating item count displays:', error);
      }
    }

    updateUI() {
      // Legacy method - redirect to comprehensive update
      this.updateAllComponents();
    }
  }

  // =======================================
  // GLOBAL FUNCTIONS
  // =======================================

  // Global function for removing items (called from table buttons)
  window.removeItem = function (id) {
    // Check user preferences for delete confirmation
    let confirmDeletes = true;
    if (window.userPreferences) {
      const preferences = window.userPreferences.getPreferences();
      confirmDeletes = preferences.confirmDeletes;
    }

    if (!confirmDeletes || confirm('Delete this item?')) {
      const success = appState.removeItem(id);
      if (success) {
        notificationService.success('Item deleted');
      }
    }
  };

  // Global function for editing items (called from table buttons)
  window.editItem = function (id) {
    const item = appState.getItem(id);
    if (!item) {
      notificationService.error('Item not found');
      return;
    }

    // Create a simple edit modal
    const modal = document.createElement('div');
    modal.className = 'alert-modal-overlay';
    modal.innerHTML = `
      <div class="alert-modal">
        <div class="alert-modal-header">
          <h3>✏️ Edit Item</h3>
          <button class="close-alert-modal" onclick="this.closest('.alert-modal-overlay').remove()">×</button>
        </div>
        <div class="alert-modal-body">
          <div class="form-group">
            <label>Description:</label>
            <input type="text" id="edit-desc" value="${escapeHtml(item.desc)}" class="form-control">
          </div>
          <div class="form-group">
            <label>SKU:</label>
            <input type="text" id="edit-sku" value="${escapeHtml(item.sku || '')}" class="form-control">
          </div>
          <div class="form-group">
            <label>Item Number:</label>
            <input type="text" id="edit-number" value="${escapeHtml(item.number || '')}" class="form-control">
          </div>
          <div class="form-group">
            <label>Location:</label>
            <input type="text" id="edit-location" value="${escapeHtml(item.location)}" class="form-control">
          </div>
          <div class="form-group">
            <label>Units:</label>
            <input type="number" id="edit-units" value="${escapeHtml(String(item.units))}" min="1" class="form-control">
          </div>
          <div class="form-group">
            <label>Expiry (YYYY-MM):</label>
            <input type="text" id="edit-expiry" value="${escapeHtml(item.expiry)}" pattern="[0-9]{4}-[0-9]{1,2}" class="form-control">
          </div>
        </div>
        <div class="alert-modal-footer">
          <button class="btn outline" onclick="this.closest('.alert-modal-overlay').remove()">Cancel</button>
          <button class="btn primary" onclick="window.saveItemEdit('${escapeHtml(id)}')">Save Changes</button>
        </div>
      </div>
    `;

    document.body.appendChild(modal);
  };

  // Global function for saving item edits
  window.saveItemEdit = function (id) {
    const item = appState.getItem(id);
    if (!item) {
      return;
    }

    // Get form values
    const desc = document.getElementById('edit-desc').value.trim();
    const sku = document.getElementById('edit-sku').value.trim();
    const number = document.getElementById('edit-number').value.trim();
    const location = document.getElementById('edit-location').value.trim();
    const units = parseInt(document.getElementById('edit-units').value, 10);
    const expiry = document.getElementById('edit-expiry').value.trim();

    // Validate required fields
    if (!desc || !location || !expiry || isNaN(units) || units < 1) {
      notificationService.error('Please fill in all required fields');
      return;
    }

    // Validate expiry format
    if (!expiry.match(/^\d{4}-\d{1,2}$/)) {
      notificationService.error('Please enter expiry in YYYY-MM format');
      return;
    }

    // Update item
    item.desc = desc;
    item.sku = sku;
    item.number = number;
    item.location = location;
    item.units = units;
    item.expiry = expiry;
    item.updatedAt = new Date().toISOString();

    // Emit update event
    appState.emit('item:updated');
    notificationService.success('Item updated successfully');

    // Close modal
    const modal = document.querySelector('.alert-modal-overlay');
    if (modal) {
      modal.remove();
    }
  };

  // Global backup functions for button handlers
  window.handleExport = function () {
    DEBUG.log('🌐 Global export handler called');
    if (window.app && window.app.handleExport) {
      window.app.handleExport();
    } else {
      alert('Export feature not available - app not initialized');
    }
  };

  window.handleImport = function () {
    DEBUG.log('🌐 Global import handler called');
    const fileInput = document.getElementById('dataPicker');
    if (fileInput) {
      DEBUG.log('📁 Triggering file picker...');
      fileInput.click();
    } else {
      console.error('❌ File input #dataPicker not found');
      alert('Import feature not available - file input not found');
    }
  };

  window.handleClearData = function () {
    DEBUG.log('🌐 Global clear handler called');
    if (window.app && window.app.handleClearData) {
      window.app.handleClearData();
    } else {
      alert('Clear feature not available - app not initialized');
    }
  };

  window.handleSettings = function () {
    DEBUG.log('🌐 Global settings handler called');
    if (window.userPreferences && window.userPreferences.showPreferencesDialog) {
      window.userPreferences.showPreferencesDialog();
    } else {
      alert('Settings feature not available - preferences not initialized');
    }
  };

  // Global function for handling notification View button clicks
  window.handleNotificationView = function (alertType) {
    DEBUG.log('🔔 Notification view clicked for:', alertType);

    // Clear the notification badge
    const badge = document.getElementById('notificationBadge');
    if (badge) {
      badge.style.display = 'none';
    }

    // Close the notification modal first
    const modal = document.querySelector('.notifications-panel');
    if (modal && modal.closest('.alert-modal-overlay')) {
      modal.closest('.alert-modal-overlay').remove();
    }

    // Handle the alert action
    if (notificationService && notificationService.handleAlertAction) {
      notificationService.handleAlertAction(alertType);
    } else {
      console.error('❌ NotificationService not available');
    }
  };

  // =======================================
  // APPLICATION INITIALIZATION
  // =======================================

  // Fix Firefox datalist double-click issue
  function fixFirefoxDatalistBehavior() {
    const datalistInputs = document.querySelectorAll('input[list]');
    datalistInputs.forEach(input => {
      input.addEventListener('focus', function () {
        // Trigger the dropdown by simulating a click
        if (navigator.userAgent.toLowerCase().indexOf('firefox') > -1) {
          this.click();
        }
      });
    });
  }

  // Initialize app when DOM is ready
  function initializeApp() {
    DEBUG.log('🚀 Starting ExpireWise initialization...');

    try {
      window.app = new ExpireWiseApp();
      DEBUG.log('✓ ExpireWiseApp instance created');

      window.app.init().catch(error => {
        console.error('❌ App initialization failed:', error);
        alert(`App initialization failed: ${error.message}`);
      });

      // Fix Firefox datalist behavior after app initializes
      fixFirefoxDatalistBehavior();

      // Initialize custom dropdowns
      initializeCustomDropdowns();

      // Check for first-time launch
      checkFirstTimeLaunch();
    } catch (error) {
      console.error('❌ Failed to create app instance:', error);
      alert(`Failed to create app instance: ${error.message}`);
    }
  }

  // First-time setup modal
  function checkFirstTimeLaunch() {
    const hasSeenSetup = localStorage.getItem('expirewise-setup-complete');

    if (!hasSeenSetup) {
      DEBUG.log('👋 First time launch detected - showing setup modal');
      showFirstTimeSetup();
    }
  }

  async function showFirstTimeSetup() {
    const modal = document.getElementById('firstTimeSetupModal');
    const completeBtn = document.getElementById('completeSetup');
    const autoSaveCheckbox = document.getElementById('setupAutoSave');
    const defaultStoragePathEl = document.getElementById('defaultStoragePath');
    const customStoragePathEl = document.getElementById('customStoragePath');
    const selectCustomBtn = document.getElementById('selectCustomLocationBtn');
    const storageRadios = document.querySelectorAll('input[name="storageLocation"]');

    if (!modal) {
      DEBUG.log('⚠️ First-time setup modal not found');
      return;
    }

    let customSavePath = null;

    // Show default storage path
    if (defaultStoragePathEl && storageService.dataFilePath) {
      const pathParts = storageService.dataFilePath.split(/[/\\]/);
      const displayPath = pathParts.slice(-3).join('/'); // Show last 3 parts
      defaultStoragePathEl.textContent = `.../${displayPath}`;
    }

    // Handle storage location radio changes
    storageRadios.forEach(radio => {
      radio.addEventListener('change', (e) => {
        if (selectCustomBtn) {
          selectCustomBtn.disabled = e.target.value !== 'custom';
        }
      });
    });

    // Handle custom location selection
    if (selectCustomBtn) {
      selectCustomBtn.addEventListener('click', async () => {
        try {
          const result = await window.electronAPI.selectFile({
            title: 'Select Save Location',
            properties: ['openDirectory', 'createDirectory']
          });

          if (result && !result.canceled && result.filePaths && result.filePaths[0]) {
            customSavePath = result.filePaths[0] + '/expirewise-data.json';
            customStoragePathEl.textContent = customSavePath;
            customStoragePathEl.style.display = 'block';
            DEBUG.log('✓ Custom save path selected:', customSavePath);
          }
        } catch (error) {
          DEBUG.error('Failed to select folder:', error);
          notificationService.error('Failed to select folder');
        }
      });
    }

    // Show modal
    modal.style.display = 'flex';

    // Complete setup handler (remove old listeners first)
    if (completeBtn) {
      const newCompleteBtn = completeBtn.cloneNode(true);
      completeBtn.parentNode.replaceChild(newCompleteBtn, completeBtn);

      newCompleteBtn.addEventListener('click', () => {
        // Get selected storage option
        const selectedStorage = document.querySelector('input[name="storageLocation"]:checked')?.value;

        // Save storage path preference
        if (selectedStorage === 'custom' && customSavePath) {
          localStorage.setItem('expirewise-custom-save-path', customSavePath);
          storageService.dataFilePath = customSavePath;
          DEBUG.log('✓ Custom save path set:', customSavePath);
        } else {
          localStorage.removeItem('expirewise-custom-save-path');
        }

        // Save auto-save preference
        const autoSaveEnabled = autoSaveCheckbox ? autoSaveCheckbox.checked : true;
        localStorage.setItem('expirewise-autosave-enabled', autoSaveEnabled);

        // Mark setup as complete
        localStorage.setItem('expirewise-setup-complete', 'true');

        // Hide modal
        modal.style.display = 'none';

        DEBUG.log('✓ First-time setup completed');
        notificationService.success('Setup complete! Your data will be saved to the selected location.');
      });
    }
  }

  // Add button handler for showing setup again from settings
  setTimeout(() => {
    const showSetupAgainBtn = document.getElementById('showSetupAgainBtn');
    if (showSetupAgainBtn) {
      showSetupAgainBtn.addEventListener('click', () => {
        DEBUG.log('📖 User requested to view setup guide again');

        // Close settings modal first
        const settingsModal = document.getElementById('settingsModal');
        if (settingsModal) {
          settingsModal.style.display = 'none';
        }

        // Show setup modal
        showFirstTimeSetup();
      });
    }

    // Add button handler for resetting first-time setup
    const resetSetupBtn = document.getElementById('resetSetupBtn');
    if (resetSetupBtn) {
      resetSetupBtn.addEventListener('click', () => {
        if (confirm('Reset first-time setup? The setup wizard will show again on next launch.')) {
          localStorage.removeItem('expirewise-setup-complete');
          DEBUG.log('🔄 First-time setup reset');
          alert('Setup reset! The wizard will show on next launch.');
        }
      });
    }

    // Sync autosave checkbox with saved preference
    const autoSaveCheckbox = document.getElementById('autoSaveEnabled');
    if (autoSaveCheckbox) {
      const autoSaveEnabled = localStorage.getItem('expirewise-autosave-enabled') !== 'false';
      autoSaveCheckbox.checked = autoSaveEnabled;

      // Update preference and status when checkbox changes
      autoSaveCheckbox.addEventListener('change', () => {
        const isEnabled = autoSaveCheckbox.checked;
        localStorage.setItem('expirewise-autosave-enabled', isEnabled);
        DEBUG.log('⚙️ Auto-save preference updated:', isEnabled);

        // Update the status indicator immediately
        if (window.app && window.app.updateStorageStatus) {
          if (isEnabled) {
            window.app.updateStorageStatus('Auto-Save Active', 'active');
          } else {
            window.app.updateStorageStatus('Auto-Save Disabled', 'disabled');
          }
        }
      });
    }

    // Settings panel - Storage location handlers
    const settingsDefaultRadio = document.querySelector('input[name="settingsStorageLocation"][value="default"]');
    const settingsCustomRadio = document.querySelector('input[name="settingsStorageLocation"][value="custom"]');
    const settingsSelectCustomBtn = document.getElementById('settingsSelectCustomLocationBtn');
    const settingsDefaultPathEl = document.getElementById('settingsDefaultStoragePath');
    const settingsCustomPathEl = document.getElementById('settingsCustomStoragePath');
    const saveStorageLocationBtn = document.getElementById('saveStorageLocationBtn');

    let settingsCustomSavePath = null;

    // Initialize storage path display when settings modal opens
    const settingsModal = document.getElementById('settingsModal');
    if (settingsModal) {
      // Add event listener to settings gear button to initialize paths when opened
      const settingsGear = document.querySelector('[onclick*="settingsModal"]');
      if (settingsGear) {
        settingsGear.addEventListener('click', async () => {
          // Load and display default path
          if (storageService.isElectron && settingsDefaultPathEl) {
            const customPath = localStorage.getItem('expirewise-custom-save-path');
            const userDataPath = await window.electronAPI.getAppPath('userData');
            const defaultPath = `${userDataPath}/expirewise-data.json`;
            settingsDefaultPathEl.textContent = defaultPath;

            // Check if we're currently using custom path
            if (customPath && storageService.dataFilePath === customPath) {
              settingsCustomRadio.checked = true;
              settingsCustomPathEl.textContent = customPath;
              settingsCustomPathEl.style.display = 'block';
              settingsSelectCustomBtn.disabled = false;
              settingsCustomSavePath = customPath;
            } else {
              settingsDefaultRadio.checked = true;
              settingsCustomPathEl.style.display = 'none';
              settingsSelectCustomBtn.disabled = true;
            }
          }
        });
      }
    }

    // Handle radio button changes
    if (settingsDefaultRadio) {
      settingsDefaultRadio.addEventListener('change', () => {
        if (settingsDefaultRadio.checked) {
          settingsSelectCustomBtn.disabled = true;
          settingsCustomPathEl.style.display = 'none';
        }
      });
    }

    if (settingsCustomRadio) {
      settingsCustomRadio.addEventListener('change', () => {
        if (settingsCustomRadio.checked) {
          settingsSelectCustomBtn.disabled = false;
          if (settingsCustomSavePath) {
            settingsCustomPathEl.style.display = 'block';
          }
        }
      });
    }

    // Handle custom location selection
    if (settingsSelectCustomBtn) {
      settingsSelectCustomBtn.addEventListener('click', async () => {
        try {
          const result = await window.electronAPI.selectFile({
            title: 'Select Save Location',
            properties: ['openDirectory', 'createDirectory']
          });

          if (result && !result.canceled && result.filePaths && result.filePaths[0]) {
            settingsCustomSavePath = result.filePaths[0] + '/expirewise-data.json';
            settingsCustomPathEl.textContent = settingsCustomSavePath;
            settingsCustomPathEl.style.display = 'block';
            DEBUG.log('📂 Custom save location selected:', settingsCustomSavePath);
          }
        } catch (error) {
          DEBUG.error('Failed to select custom location:', error);
          alert('Failed to select folder. Please try again.');
        }
      });
    }

    // Handle save button
    if (saveStorageLocationBtn) {
      saveStorageLocationBtn.addEventListener('click', async () => {
        try {
          const selectedStorage = document.querySelector('input[name="settingsStorageLocation"]:checked')?.value;

          if (selectedStorage === 'custom') {
            if (!settingsCustomSavePath) {
              alert('Please select a custom folder first.');
              return;
            }

            // Save custom path preference
            localStorage.setItem('expirewise-custom-save-path', settingsCustomSavePath);
            storageService.dataFilePath = settingsCustomSavePath;
            DEBUG.log('✓ Custom save path updated:', settingsCustomSavePath);

            // Save current data to new location
            if (window.app && window.app.inventory) {
              await storageService.save(window.app.inventory.getAllItems());
              notificationService.success('Save location updated! Your data has been saved to the new location.');
            } else {
              notificationService.success('Save location updated!');
            }
          } else {
            // Revert to default location
            localStorage.removeItem('expirewise-custom-save-path');
            const userDataPath = await window.electronAPI.getAppPath('userData');
            storageService.dataFilePath = `${userDataPath}/expirewise-data.json`;
            DEBUG.log('✓ Save path reverted to default:', storageService.dataFilePath);

            // Save current data to default location
            if (window.app && window.app.inventory) {
              await storageService.save(window.app.inventory.getAllItems());
              notificationService.success('Save location updated! Using default location.');
            } else {
              notificationService.success('Save location updated!');
            }
          }
        } catch (error) {
          DEBUG.error('Failed to save storage location:', error);
          alert('Failed to save location preference. Please try again.');
        }
      });
    }
  }, 1000);

  // Initialize custom dropdowns for autocomplete fields
  function initializeCustomDropdowns() {
    DEBUG.log('🔽 Initializing custom dropdowns...');

    if (typeof CustomDropdown === 'undefined') {
      DEBUG.log('⚠️ CustomDropdown not available, skipping dropdown initialization');
      return;
    }

    const dropdowns = {};

    // Helper to get dictionary data
    const getDict = () => window.DICT || {};

    // Helper to update dropdown options
    const updateDropdownOptions = () => {
      const dict = getDict();
      DEBUG.log('📝 Updating dropdown options with dictionary:', {
        hasDict: !!dict,
        itemsCount: dict.items?.length || 0,
        storesCount: dict.stores?.length || 0
      });

      if (dropdowns.number && dict.items) {
        const numbers = dict.items.map(item => item.number).filter(Boolean).sort();
        DEBUG.log('  - Number dropdown:', numbers.length, 'items');
        dropdowns.number.updateOptions({ items: numbers });
      }

      if (dropdowns.desc && dict.items) {
        const descriptions = dict.items.map(item => item.desc).filter(Boolean).sort();
        DEBUG.log('  - Description dropdown:', descriptions.length, 'items');
        dropdowns.desc.updateOptions({ items: descriptions });
      }

      // Location is now a native select element, populated separately
    };

    // Item Number dropdown
    const numberInput = document.getElementById('number');
    if (numberInput) {
      dropdowns.number = new CustomDropdown(numberInput, {
        items: [],
        onSelect: value => {
          const dict = getDict();
          const item = dict.items?.find(d => d.number === value);
          if (item) {
            document.getElementById('desc').value = item.desc || '';
            // SKU is an array in the dictionary, get the first element
            const sku = Array.isArray(item.sku) ? item.sku[0] : item.sku;
            document.getElementById('sku').value = sku || '';
          }
        },
      });
    }

    // Item Description dropdown
    const descInput = document.getElementById('desc');
    if (descInput) {
      dropdowns.desc = new CustomDropdown(descInput, {
        items: [],
        onSelect: value => {
          const dict = getDict();
          const item = dict.items?.find(d => d.desc === value);
          if (item) {
            document.getElementById('number').value = item.number || '';
            // SKU is an array in the dictionary, get the first element
            const sku = Array.isArray(item.sku) ? item.sku[0] : item.sku;
            document.getElementById('sku').value = sku || '';
          }
        },
      });
    }

    // Location is now a native select element, no need for CustomDropdown
    // It's populated by the form's populateLocations() method

    window.customDropdowns = dropdowns;
    window.updateDropdownOptions = updateDropdownOptions;

    // Initial update - try immediately and again after delay
    updateDropdownOptions();
    setTimeout(updateDropdownOptions, 1000);
    setTimeout(updateDropdownOptions, 2000);

    DEBUG.log('✓ Custom dropdowns initialized:', Object.keys(dropdowns));
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeApp);
  } else {
    initializeApp();
  }

  // Button test section removed - using actual handlers now

  DEBUG.log('✅ ExpireWise bundle loaded successfully');

  // Add global error handler to catch any issues
  window.addEventListener('error', event => {
    // Filter out null/undefined/empty errors
    if (!event.error || event.error === null || event.error === undefined) {
      console.debug('Filtered null/undefined error event');
      return;
    }

    console.error('❌ Global error:', event.error);
    // Only show alert for meaningful errors, not null/undefined
    if (event.error && event.error.message) {
      console.error('Error details:', {
        message: event.error.message,
        filename: event.filename,
        lineno: event.lineno,
        colno: event.colno,
        stack: event.error.stack,
      });
      // Don't show alert for now to avoid spam
      // alert('JavaScript error: ' + event.error.message);
    }
  });

  window.addEventListener('unhandledrejection', event => {
    console.error('❌ Unhandled promise rejection:', event.reason);
    if (event.reason && typeof event.reason === 'object' && event.reason.message) {
      console.error('Promise rejection details:', event.reason);
      // Don't show alert for now to avoid spam
      // alert('Promise rejection: ' + event.reason.message);
    }
  });

  // Service Worker removed - App runs fully offline without caching
  // All data is stored in localStorage
  DEBUG.log('✅ ExpireWise running in offline mode (no service worker)');

  // Additional file input backup handler - DISABLED to prevent duplicate handlers
  // setTimeout(() => {
  //   const fileInput = document.getElementById('dataPicker');
  //   if (fileInput && !fileInput._backupHandlerAdded) {
  //     fileInput._backupHandlerAdded = true;
  //     fileInput.addEventListener('change', function(e) {
  //       DEBUG.log('🔄 Backup file change handler triggered');
  //       if (e.target.files && e.target.files.length > 0) {
  //         if (window.app && typeof window.app.handleImport === 'function') {
  //           window.app.handleImport(e);
  //         } else {
  //           console.error('❌ App handleImport method not available');
  //         }
  //       }
  //     });
  //     DEBUG.log('✓ Backup file handler added');
  //   }
  // }, 1000);

  // ===================================
  // Custom Title Bar Controls
  // ===================================
  const setupTitleBarControls = () => {
    if (!window.electronAPI) {
      DEBUG.log('⚠️ electronAPI not available, title bar controls disabled');
      return;
    }

    const minimizeBtn = document.getElementById('minimizeBtn');
    const maximizeBtn = document.getElementById('maximizeBtn');
    const closeBtn = document.getElementById('closeBtn');

    if (minimizeBtn) {
      minimizeBtn.addEventListener('click', () => {
        window.electronAPI.windowMinimize();
      });
    }

    if (maximizeBtn) {
      maximizeBtn.addEventListener('click', async () => {
        const isMaximized = await window.electronAPI.windowMaximize();
        // Update icon based on state
        if (isMaximized) {
          maximizeBtn.innerHTML = `
            <svg width="12" height="12" viewBox="0 0 12 12">
              <rect x="2" y="0" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
              <rect x="0" y="2" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
            </svg>
          `;
          maximizeBtn.title = 'Restore';
        } else {
          maximizeBtn.innerHTML = `
            <svg width="12" height="12" viewBox="0 0 12 12">
              <rect x="1" y="1" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
            </svg>
          `;
          maximizeBtn.title = 'Maximize';
        }
      });

      // Check initial state
      window.electronAPI.windowIsMaximized().then(isMaximized => {
        if (isMaximized) {
          maximizeBtn.innerHTML = `
            <svg width="12" height="12" viewBox="0 0 12 12">
              <rect x="2" y="0" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
              <rect x="0" y="2" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
            </svg>
          `;
          maximizeBtn.title = 'Restore';
        }
      });
    }

    if (closeBtn) {
      closeBtn.addEventListener('click', () => {
        window.electronAPI.windowClose();
      });
    }

    DEBUG.log('✅ Custom title bar controls initialized');
  };

  // Initialize title bar controls
  setupTitleBarControls();
})();
