/**
 * Shared Utilities Index
 * Central export point for all utility modules
 */

const dom = require('./dom');
const { debounce, throttle, rafThrottle } = require('./debounce');
const { escapeHtml, Validator, safeJsonParse, safeJsonStringify } = require('./sanitize');
const date = require('./date');
const { StorageManager, SessionStorageManager } = require('./storage');
const { ErrorBoundary, initErrorBoundary, getErrorBoundary } = require('./error-boundary');
const { ToastManager, getToast } = require('./toast');
const accessibility = require('./accessibility');

module.exports = {
  // DOM utilities
  ...dom,

  // Performance utilities
  debounce,
  throttle,
  rafThrottle,

  // Security utilities
  escapeHtml,
  Validator,
  safeJsonParse,
  safeJsonStringify,

  // Date utilities
  ...date,

  // Storage
  StorageManager,
  SessionStorageManager,

  // Error handling
  ErrorBoundary,
  initErrorBoundary,
  getErrorBoundary,

  // Toast notifications
  ToastManager,
  getToast,

  // Accessibility
  ...accessibility
};
