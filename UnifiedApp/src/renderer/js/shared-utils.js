/**
 * Shared Utilities for Business Tools Suite
 * Common functions used across all modules
 */

// =====================================================
// TOAST NOTIFICATION SYSTEM
// =====================================================

/**
 * Show a toast notification
 * @param {string} message - The message to display
 * @param {string} type - 'success', 'error', 'warning', 'info'
 * @param {string} title - Optional title (defaults based on type)
 * @param {number} duration - Duration in ms (default: 4000)
 */
function showToast(message, type = 'info', title = null, duration = 4000) {
  let toastContainer = document.getElementById('toastContainer');

  // Create container if it doesn't exist
  if (!toastContainer) {
    toastContainer = document.createElement('div');
    toastContainer.id = 'toastContainer';
    toastContainer.className = 'toast-container';
    document.body.appendChild(toastContainer);
  }

  // Create toast element
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;

  // Set title based on type if not provided
  if (!title) {
    switch (type) {
      case 'success': title = 'Success'; break;
      case 'error': title = 'Error'; break;
      case 'warning': title = 'Warning'; break;
      case 'info': title = 'Information'; break;
      default: title = 'Notification'; break;
    }
  }

  // Set icon based on type
  let icon = '📢';
  switch (type) {
    case 'success': icon = '✅'; break;
    case 'error': icon = '❌'; break;
    case 'warning': icon = '⚠️'; break;
    case 'info': icon = 'ℹ️'; break;
  }

  toast.innerHTML = `
    <div class="toast-header">
      <span class="toast-icon">${icon}</span>
      <span>${escapeHtml(title)}</span>
      <button class="toast-close" onclick="closeToast(this)">&times;</button>
    </div>
    <div class="toast-body">${escapeHtml(message)}</div>
    <div class="toast-progress">
      <div class="toast-progress-bar"></div>
    </div>
  `;

  // Add to container
  toastContainer.appendChild(toast);

  // Show toast with animation
  setTimeout(() => {
    toast.classList.add('show');
  }, 100);

  // Auto remove after duration
  const timeoutId = setTimeout(() => {
    removeToast(toast);
  }, duration);

  // Store timeout ID for manual closing
  toast.dataset.timeoutId = timeoutId;

  return toast;
}

function closeToast(closeButton) {
  const toast = closeButton.closest('.toast');
  if (toast.dataset.timeoutId) {
    clearTimeout(parseInt(toast.dataset.timeoutId));
  }
  removeToast(toast);
}

function removeToast(toast) {
  if (toast && toast.parentNode) {
    toast.classList.remove('show');
    setTimeout(() => {
      if (toast.parentNode) {
        toast.parentNode.removeChild(toast);
      }
    }, 300);
  }
}

// =====================================================
// LOADING OVERLAY
// =====================================================

/**
 * Show loading overlay
 * @param {string} text - Main loading text
 * @param {string} subtext - Subtext description
 */
function showLoading(text = 'Processing...', subtext = 'Please wait') {
  let overlay = document.getElementById('loadingOverlay');

  // Create overlay if it doesn't exist
  if (!overlay) {
    overlay = document.createElement('div');
    overlay.id = 'loadingOverlay';
    overlay.className = 'loading-overlay';
    overlay.innerHTML = `
      <div class="loading-spinner">
        <div class="spinner"></div>
        <div class="loading-text" id="loadingText">Processing...</div>
        <div class="loading-subtext" id="loadingSubtext">Please wait</div>
      </div>
    `;
    document.body.appendChild(overlay);
  }

  const loadingText = document.getElementById('loadingText');
  const loadingSubtext = document.getElementById('loadingSubtext');

  if (loadingText) loadingText.textContent = text;
  if (loadingSubtext) loadingSubtext.textContent = subtext;

  overlay.classList.add('show');
}

/**
 * Hide loading overlay
 */
function hideLoading() {
  const overlay = document.getElementById('loadingOverlay');
  if (overlay) {
    overlay.classList.remove('show');
  }
}

// =====================================================
// KEYBOARD SHORTCUTS MANAGER
// =====================================================

class KeyboardShortcutsManager {
  constructor() {
    this.shortcuts = new Map();
    this.enabled = true;
    this.init();
  }

  init() {
    document.addEventListener('keydown', (e) => {
      if (!this.enabled) return;

      const key = this.getKeyString(e);
      const handler = this.shortcuts.get(key);

      if (handler) {
        // Check if shortcut should be prevented in input fields
        if (handler.preventInInputs && this.isInputField(e.target)) {
          return;
        }

        e.preventDefault();
        handler.callback(e);
      }
    });
  }

  getKeyString(e) {
    const parts = [];
    if (e.ctrlKey) parts.push('Ctrl');
    if (e.altKey) parts.push('Alt');
    if (e.shiftKey) parts.push('Shift');
    if (e.metaKey) parts.push('Meta');

    // Normalize key
    let key = e.key;
    if (key === ' ') key = 'Space';
    if (key === 'Escape') key = 'Esc';

    parts.push(key.toLowerCase());

    return parts.join('+');
  }

  isInputField(element) {
    return element.tagName === 'INPUT' ||
           element.tagName === 'TEXTAREA' ||
           element.isContentEditable;
  }

  register(keyCombo, callback, options = {}) {
    const key = keyCombo.toLowerCase();
    this.shortcuts.set(key, {
      callback,
      preventInInputs: options.preventInInputs !== false,
      description: options.description || ''
    });
  }

  unregister(keyCombo) {
    this.shortcuts.delete(keyCombo.toLowerCase());
  }

  enable() {
    this.enabled = true;
  }

  disable() {
    this.enabled = false;
  }

  getAll() {
    return Array.from(this.shortcuts.entries()).map(([key, data]) => ({
      key,
      description: data.description
    }));
  }
}

// Global instance
const keyboardShortcuts = new KeyboardShortcutsManager();

// =====================================================
// UTILITY FUNCTIONS
// =====================================================

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(text) {
  if (typeof text !== 'string') return text;

  const map = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  };
  return String(text).replace(/[&<>"']/g, m => map[m]);
}

/**
 * Debounce function to limit rate of execution
 */
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

/**
 * Format date for display
 */
function formatDate(date) {
  if (!(date instanceof Date)) {
    date = new Date(date);
  }
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
}

/**
 * Format date and time for display
 */
function formatDateTime(date) {
  if (!(date instanceof Date)) {
    date = new Date(date);
  }
  return date.toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

/**
 * Download file helper
 */
function downloadFile(content, filename, mimeType = 'text/plain') {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');

  link.setAttribute('href', url);
  link.setAttribute('download', filename);
  link.style.visibility = 'hidden';

  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);

  URL.revokeObjectURL(url);
}

/**
 * Copy text to clipboard
 */
async function copyToClipboard(text) {
  try {
    await navigator.clipboard.writeText(text);
    showToast('Copied to clipboard', 'success');
    return true;
  } catch (err) {
    console.error('Failed to copy:', err);
    showToast('Failed to copy to clipboard', 'error');
    return false;
  }
}

/**
 * Confirm dialog with custom styling
 */
function confirmDialog(message, title = 'Confirm') {
  return new Promise((resolve) => {
    const result = confirm(`${title}\n\n${message}`);
    resolve(result);
  });
}

/**
 * Generate timestamp for filenames
 */
function getTimestamp() {
  const now = new Date();
  return now.toISOString().replace(/[:.]/g, '-').split('T')[0];
}

/**
 * Normalize item number (uppercase, trim)
 */
function normalizeItemNo(value) {
  if (!value) return '';
  return String(value).trim().toUpperCase();
}

/**
 * Parse number safely
 */
function parseNumber(value, defaultValue = 0) {
  const parsed = parseFloat(value);
  return isNaN(parsed) ? defaultValue : parsed;
}

/**
 * Format number with commas
 */
function formatNumber(num) {
  return num.toLocaleString('en-US');
}

// =====================================================
// UNDO/REDO MANAGER (Generic)
// =====================================================

class UndoRedoManager {
  constructor(options = {}) {
    this.history = [];
    this.currentIndex = -1;
    this.maxHistory = options.maxHistory || 50;
    this.onStateChange = options.onStateChange || (() => {});
    this.onButtonUpdate = options.onButtonUpdate || (() => {});
  }

  /**
   * Push a new state to history
   */
  pushState(state, description = '') {
    // Remove any states after current index
    this.history = this.history.slice(0, this.currentIndex + 1);

    // Add new state
    this.history.push({
      state: JSON.parse(JSON.stringify(state)),
      description,
      timestamp: Date.now()
    });

    this.currentIndex++;

    // Trim old history
    if (this.history.length > this.maxHistory) {
      this.history.shift();
      this.currentIndex--;
    }

    this.updateButtons();
  }

  /**
   * Undo to previous state
   */
  undo() {
    if (!this.canUndo()) return null;

    this.currentIndex--;
    const entry = this.history[this.currentIndex];
    this.onStateChange(entry.state);
    this.updateButtons();

    return entry.description;
  }

  /**
   * Redo to next state
   */
  redo() {
    if (!this.canRedo()) return null;

    this.currentIndex++;
    const entry = this.history[this.currentIndex];
    this.onStateChange(entry.state);
    this.updateButtons();

    return entry.description;
  }

  canUndo() {
    return this.currentIndex > 0;
  }

  canRedo() {
    return this.currentIndex < this.history.length - 1;
  }

  clear() {
    this.history = [];
    this.currentIndex = -1;
    this.updateButtons();
  }

  updateButtons() {
    this.onButtonUpdate({
      canUndo: this.canUndo(),
      canRedo: this.canRedo(),
      undoDescription: this.canUndo() ? this.history[this.currentIndex - 1]?.description : '',
      redoDescription: this.canRedo() ? this.history[this.currentIndex + 1]?.description : ''
    });
  }
}

// =====================================================
// EXPORT UTILITIES
// =====================================================

/**
 * Check if XLSX library is available
 */
function isXLSXAvailable() {
  return typeof XLSX !== 'undefined';
}

/**
 * Check if Papa Parse is available
 */
function isPapaParseAvailable() {
  return typeof Papa !== 'undefined';
}

// =====================================================
// MAKE FUNCTIONS AVAILABLE GLOBALLY
// =====================================================

window.SharedUtils = {
  // Toast
  showToast,
  closeToast,
  removeToast,

  // Loading
  showLoading,
  hideLoading,

  // Keyboard
  keyboardShortcuts,
  KeyboardShortcutsManager,

  // Undo/Redo
  UndoRedoManager,

  // Utilities
  escapeHtml,
  debounce,
  formatDate,
  formatDateTime,
  downloadFile,
  copyToClipboard,
  confirmDialog,
  getTimestamp,
  normalizeItemNo,
  parseNumber,
  formatNumber,

  // Library checks
  isXLSXAvailable,
  isPapaParseAvailable
};

// =====================================================
// TAB SYSTEM
// =====================================================

// Generic tab switcher that works with data-tab attributes
function initTabSystem() {
  // Handle all tab clicks
  document.addEventListener('click', function(e) {
    const tab = e.target.closest('.tab[data-tab]');
    if (!tab) return;

    const tabName = tab.getAttribute('data-tab');
    const tabGroup = tab.closest('.tabs');
    
    if (!tabGroup) return;

    // Get all tabs in this group
    const allTabs = tabGroup.querySelectorAll('.tab[data-tab]');
    
    // Remove active class from all tabs
    allTabs.forEach(t => t.classList.remove('active'));
    
    // Add active class to clicked tab
    tab.classList.add('active');

    // Find the tab panels container
    let panelsContainer = tabGroup.nextElementSibling;
    
    // If next element is not a container, try different selectors
    if (!panelsContainer || (!panelsContainer.classList.contains('tab-panels') && 
                             !panelsContainer.classList.contains('settings-content') &&
                             !panelsContainer.classList.contains('settings-content-area'))) {
      panelsContainer = tabGroup.parentElement.querySelector('.tab-panels, .settings-content, .settings-content-area');
    }

    if (!panelsContainer) return;

    // Get all panels in this container
    const allPanels = panelsContainer.querySelectorAll('.tab-panel');
    
    // Hide all panels
    allPanels.forEach(panel => panel.classList.remove('active'));
    
    // Show the matching panel
    const targetPanel = panelsContainer.querySelector(`#${tabName}-tab, #settingsTab${capitalize(tabName)}, [data-panel="${tabName}"]`);
    
    if (targetPanel) {
      targetPanel.classList.add('active');
    }
  });
}

// Handle entry tabs (single/bulk entry)
function initEntryTabs() {
  document.addEventListener('click', function(e) {
    const entryTab = e.target.closest('.entry-tab-btn[data-entry-tab]');
    if (!entryTab) return;

    const tabName = entryTab.getAttribute('data-entry-tab');
    const tabGroup = entryTab.closest('.entry-tabs');
    
    if (!tabGroup) return;

    // Get all entry tabs in this group
    const allTabs = tabGroup.querySelectorAll('.entry-tab-btn');
    
    // Remove active class from all tabs
    allTabs.forEach(t => t.classList.remove('active'));
    
    // Add active class to clicked tab
    entryTab.classList.add('active');

    // Find the parent card body
    const cardBody = tabGroup.closest('.card-body');
    if (!cardBody) return;

    // Get all entry tab contents
    const allContents = cardBody.querySelectorAll('.entry-tab-content');
    
    // Hide all contents
    allContents.forEach(content => content.classList.remove('active'));
    
    // Show the matching content
    const targetContent = cardBody.querySelector(`#${tabName}-entry`);
    
    if (targetContent) {
      targetContent.classList.add('active');
    }
  });
}

// Handle settings modal tabs (for vertical tabs)
function switchTab(event, tabId) {
  event.preventDefault();
  
  // Get the tab that was clicked
  const clickedTab = event.target;
  const tabGroup = clickedTab.closest('.tabs');
  
  if (!tabGroup) return;
  
  // Remove active class from all tabs in this group
  const allTabs = tabGroup.querySelectorAll('.tab');
  allTabs.forEach(tab => tab.classList.remove('active'));
  
  // Add active class to clicked tab
  clickedTab.classList.add('active');
  
  // Find the content area
  const contentArea = tabGroup.parentElement.querySelector('.settings-content-area');
  if (!contentArea) return;
  
  // Hide all tab panels
  const allPanels = contentArea.querySelectorAll('.tab-panel');
  allPanels.forEach(panel => panel.classList.remove('active'));
  
  // Show the target panel
  const targetPanel = contentArea.querySelector(`#${tabId}`);
  if (targetPanel) {
    targetPanel.classList.add('active');
  }
}

// Helper function to capitalize first letter
function capitalize(str) {
  return str.charAt(0).toUpperCase() + str.slice(1);
}

// =====================================================
// UNIFIED THEME SYSTEM
// =====================================================

/**
 * Theme Manager - Unified theme handling across all apps
 */
const ThemeManager = {
  storageKey: 'unified-app-theme',
  
  /**
   * Initialize theme system
   */
  init() {
    // Load saved theme or detect system preference
    const savedTheme = this.getTheme();
    this.setTheme(savedTheme);
    
    // Listen for system theme changes
    if (window.matchMedia) {
      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (this.getTheme() === 'auto') {
          this.applyTheme(e.matches ? 'dark' : 'light');
        }
      });
    }
    
    // Set up theme toggle buttons
    this.setupThemeToggles();
  },
  
  /**
   * Get current theme from storage or detect system preference
   */
  getTheme() {
    try {
      const saved = localStorage.getItem(this.storageKey);
      if (saved) return saved;
      
      // Detect system preference
      if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        return 'dark';
      }
      return 'light';
    } catch (e) {
      return 'light';
    }
  },
  
  /**
   * Set and apply theme
   */
  setTheme(theme) {
    try {
      localStorage.setItem(this.storageKey, theme);
      const actualTheme = theme === 'auto' 
        ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
        : theme;
      this.applyTheme(actualTheme);
      
      // Notify Electron main process
      if (window.electronAPI && window.electronAPI.setTheme) {
        window.electronAPI.setTheme(theme);
      }
    } catch (e) {
      console.error('Failed to save theme:', e);
    }
  },
  
  /**
   * Apply theme to DOM
   */
  applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    
    // Update theme toggle buttons
    this.updateThemeToggles(theme);
    
    // Notify all iframes about theme change
    const iframes = document.querySelectorAll('iframe');
    iframes.forEach(iframe => {
      try {
        iframe.contentWindow.postMessage({
          type: 'themeChange',
          theme: theme
        }, '*');
      } catch (e) {
        console.log('Could not send theme to iframe:', e);
      }
    });
    
    // Dispatch event for components that need to react to theme changes
    window.dispatchEvent(new CustomEvent('themechange', { detail: { theme } }));
  },
  
  /**
   * Toggle between light and dark
   */
  toggle() {
    const current = document.documentElement.getAttribute('data-theme');
    const newTheme = current === 'dark' ? 'light' : 'dark';
    this.setTheme(newTheme);
  },
  
  /**
   * Set up theme toggle buttons
   */
  setupThemeToggles() {
    const toggles = document.querySelectorAll('#themeToggle, [data-theme-toggle]');
    toggles.forEach(toggle => {
      toggle.addEventListener('click', (e) => {
        e.preventDefault();
        this.toggle();
      });
    });
  },
  
  /**
   * Update theme toggle button states
   */
  updateThemeToggles(theme) {
    const toggles = document.querySelectorAll('#themeToggle, [data-theme-toggle]');
    toggles.forEach(toggle => {
      const icon = toggle.querySelector('.theme-icon');
      if (icon) {
        icon.textContent = theme === 'dark' ? '🌙' : '☀️';
      }
      toggle.setAttribute('title', `Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`);
    });
  }
};

// Initialize tab systems when DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', function() {
    initTabSystem();
    initEntryTabs();
    // Only init ThemeManager in iframes (modules), not in main window
    // Main window theme is handled by launcher.js
    if (window !== window.top) {
      ThemeManager.init();
    }
  });
} else {
  initTabSystem();
  initEntryTabs();
  // Only init ThemeManager in iframes (modules), not in main window
  if (window !== window.top) {
    ThemeManager.init();
  }
}

// Export for use in other scripts
window.initTabSystem = initTabSystem;
window.initEntryTabs = initEntryTabs;
window.switchTab = switchTab;
window.ThemeManager = ThemeManager;
