/**
 * Accessibility Utilities
 */

/**
 * Keyboard shortcuts manager
 */
class KeyboardShortcuts {
  constructor() {
    this.shortcuts = new Map();
    this.init();
  }

  /**
   * Initialize keyboard event listener
   */
  init() {
    document.addEventListener('keydown', this.handleKeyPress.bind(this));
  }

  /**
   * Register a keyboard shortcut
   * @param {string} keys - Key combination (e.g., 'Ctrl+K', 'Escape')
   * @param {Function} handler - Handler function
   * @param {string} description - Description for accessibility
   */
  register(keys, handler, description = '') {
    const normalizedKeys = this.normalizeKeys(keys);
    this.shortcuts.set(normalizedKeys, { handler, description });
  }

  /**
   * Unregister a keyboard shortcut
   * @param {string} keys - Key combination
   */
  unregister(keys) {
    const normalizedKeys = this.normalizeKeys(keys);
    this.shortcuts.delete(normalizedKeys);
  }

  /**
   * Handle key press event
   * @param {KeyboardEvent} event - Keyboard event
   */
  handleKeyPress(event) {
    const pressedKeys = this.getEventKeys(event);

    for (const [keys, { handler }] of this.shortcuts) {
      if (this.matchesKeys(pressedKeys, keys)) {
        event.preventDefault();
        handler(event);
        break;
      }
    }
  }

  /**
   * Normalize key string
   * @param {string} keys - Key combination
   * @returns {string} Normalized keys
   */
  normalizeKeys(keys) {
    return keys
      .toLowerCase()
      .split('+')
      .map(k => k.trim())
      .sort()
      .join('+');
  }

  /**
   * Get keys from keyboard event
   * @param {KeyboardEvent} event - Keyboard event
   * @returns {string} Normalized key combination
   */
  getEventKeys(event) {
    const keys = [];

    if (event.ctrlKey) keys.push('ctrl');
    if (event.shiftKey) keys.push('shift');
    if (event.altKey) keys.push('alt');
    if (event.metaKey) keys.push('meta');

    const key = event.key.toLowerCase();
    if (!['control', 'shift', 'alt', 'meta'].includes(key)) {
      keys.push(key);
    }

    return keys.sort().join('+');
  }

  /**
   * Check if event keys match registered keys
   * @param {string} eventKeys - Keys from event
   * @param {string} registeredKeys - Registered keys
   * @returns {boolean} Match status
   */
  matchesKeys(eventKeys, registeredKeys) {
    return eventKeys === registeredKeys;
  }

  /**
   * Get all registered shortcuts
   * @returns {Array} Shortcuts array
   */
  getShortcuts() {
    return Array.from(this.shortcuts.entries()).map(([keys, { description }]) => ({
      keys,
      description
    }));
  }
}

/**
 * Focus management utilities
 */
class FocusManager {
  /**
   * Trap focus within an element
   * @param {Element} element - Container element
   */
  static trapFocus(element) {
    const focusableElements = this.getFocusableElements(element);
    if (focusableElements.length === 0) return;

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];

    const handleKeyDown = (e) => {
      if (e.key !== 'Tab') return;

      if (e.shiftKey) {
        // Shift + Tab
        if (document.activeElement === firstElement) {
          e.preventDefault();
          lastElement.focus();
        }
      } else {
        // Tab
        if (document.activeElement === lastElement) {
          e.preventDefault();
          firstElement.focus();
        }
      }
    };

    element.addEventListener('keydown', handleKeyDown);

    // Focus first element
    firstElement.focus();

    // Return cleanup function
    return () => {
      element.removeEventListener('keydown', handleKeyDown);
    };
  }

  /**
   * Get all focusable elements within container
   * @param {Element} container - Container element
   * @returns {Array<Element>} Focusable elements
   */
  static getFocusableElements(container) {
    const selector = [
      'a[href]',
      'button:not([disabled])',
      'input:not([disabled])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])'
    ].join(',');

    return Array.from(container.querySelectorAll(selector));
  }

  /**
   * Save and restore focus
   * @returns {Function} Restore function
   */
  static saveFocus() {
    const activeElement = document.activeElement;
    return () => {
      if (activeElement && typeof activeElement.focus === 'function') {
        activeElement.focus();
      }
    };
  }
}

/**
 * ARIA live region announcer
 */
class LiveRegion {
  constructor() {
    this.region = this.createRegion();
  }

  /**
   * Create live region element
   * @returns {Element} Live region element
   */
  createRegion() {
    const region = document.createElement('div');
    region.setAttribute('role', 'status');
    region.setAttribute('aria-live', 'polite');
    region.setAttribute('aria-atomic', 'true');
    region.className = 'sr-only';
    region.style.cssText = `
      position: absolute;
      left: -10000px;
      width: 1px;
      height: 1px;
      overflow: hidden;
    `;
    document.body.appendChild(region);
    return region;
  }

  /**
   * Announce message to screen readers
   * @param {string} message - Message to announce
   * @param {string} priority - Priority ('polite' or 'assertive')
   */
  announce(message, priority = 'polite') {
    this.region.setAttribute('aria-live', priority);
    this.region.textContent = message;

    // Clear after announcement
    setTimeout(() => {
      this.region.textContent = '';
    }, 1000);
  }
}

/**
 * Skip link helper
 */
class SkipLinks {
  /**
   * Add skip link to page
   * @param {string} targetId - ID of target element
   * @param {string} text - Link text
   */
  static addSkipLink(targetId, text = 'Skip to main content') {
    const skipLink = document.createElement('a');
    skipLink.href = `#${targetId}`;
    skipLink.className = 'skip-link';
    skipLink.textContent = text;
    skipLink.addEventListener('click', (e) => {
      e.preventDefault();
      const target = document.getElementById(targetId);
      if (target) {
        target.tabIndex = -1;
        target.focus();
        target.addEventListener('blur', () => {
          target.removeAttribute('tabindex');
        }, { once: true });
      }
    });

    document.body.insertBefore(skipLink, document.body.firstChild);
    this.injectStyles();
  }

  /**
   * Inject skip link styles
   */
  static injectStyles() {
    if (document.getElementById('skip-link-styles')) return;

    const style = document.createElement('style');
    style.id = 'skip-link-styles';
    style.textContent = `
      .skip-link {
        position: absolute;
        top: -40px;
        left: 0;
        background: var(--accent, #667eea);
        color: white;
        padding: 8px 16px;
        text-decoration: none;
        border-radius: 0 0 4px 0;
        z-index: 10000;
        font-weight: 600;
      }

      .skip-link:focus {
        top: 0;
      }

      .sr-only {
        position: absolute;
        left: -10000px;
        width: 1px;
        height: 1px;
        overflow: hidden;
      }
    `;
    document.head.appendChild(style);
  }
}

// Create singleton instances
let keyboardShortcutsInstance = null;
let liveRegionInstance = null;

/**
 * Get keyboard shortcuts instance
 * @returns {KeyboardShortcuts} Instance
 */
function getKeyboardShortcuts() {
  if (!keyboardShortcutsInstance) {
    keyboardShortcutsInstance = new KeyboardShortcuts();
  }
  return keyboardShortcutsInstance;
}

/**
 * Get live region instance
 * @returns {LiveRegion} Instance
 */
function getLiveRegion() {
  if (!liveRegionInstance) {
    liveRegionInstance = new LiveRegion();
  }
  return liveRegionInstance;
}

module.exports = {
  KeyboardShortcuts,
  FocusManager,
  LiveRegion,
  SkipLinks,
  getKeyboardShortcuts,
  getLiveRegion
};
