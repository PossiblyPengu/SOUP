/**
 * Initialize Shared Utilities for Store Allocation Viewer (NewAB)
 * Disabled in unified app context - utilities handled by main app
 */

/**
 * Initialize all shared utilities (disabled in unified app)
 */
function initSharedUtilities() {
  console.info('Shared utilities initialization skipped in unified app context');
  return null;
}

// Auto-initialize (no-op in unified app)
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initSharedUtilities);
} else {
  initSharedUtilities();
}

// Export for compatibility (no-op)
if (typeof module !== 'undefined' && module.exports) {
  module.exports = { initSharedUtilities };
}
