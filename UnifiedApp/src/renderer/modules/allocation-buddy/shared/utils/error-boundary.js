/**
 * Error Boundary - Global Error Handling
 */

const { escapeHtml } = require('./sanitize');

class ErrorBoundary {
  constructor(options = {}) {
    this.options = {
      containerId: options.containerId || 'error-fallback',
      onError: options.onError || null,
      showDetails: options.showDetails !== false,
      ...options
    };
    this.errorCount = 0;
    this.maxErrors = 10;
  }

  /**
   * Initialize error boundary
   */
  init() {
    // Uncaught errors
    window.addEventListener('error', (event) => {
      console.error('Uncaught error:', event.error);
      this.handleError(event.error, 'Uncaught Error');
      event.preventDefault();
    });

    // Unhandled promise rejections
    window.addEventListener('unhandledrejection', (event) => {
      console.error('Unhandled promise rejection:', event.reason);
      this.handleError(event.reason, 'Unhandled Promise Rejection');
      event.preventDefault();
    });

    // Create error fallback container if it doesn't exist
    if (!document.getElementById(this.options.containerId)) {
      const container = document.createElement('div');
      container.id = this.options.containerId;
      container.style.display = 'none';
      document.body.appendChild(container);
    }
  }

  /**
   * Handle error
   * @param {Error} error - Error object
   * @param {string} type - Error type
   */
  handleError(error, type = 'Error') {
    this.errorCount++;

    // Prevent infinite error loops
    if (this.errorCount > this.maxErrors) {
      console.error('Too many errors, stopping error handling');
      return;
    }

    // Call custom error handler if provided
    if (this.options.onError) {
      try {
        this.options.onError(error, type);
      } catch (handlerError) {
        console.error('Error in custom error handler:', handlerError);
      }
    }

    // Show error UI
    this.showErrorUI(error, type);
  }

  /**
   * Show error UI
   * @param {Error} error - Error object
   * @param {string} type - Error type
   */
  showErrorUI(error, type) {
    const container = document.getElementById(this.options.containerId);
    if (!container) return;

    const errorMessage = error.message || 'An unknown error occurred';
    const errorStack = error.stack || '';

    container.innerHTML = `
      <div class="error-boundary-overlay">
        <div class="error-boundary-content">
          <div class="error-boundary-header">
            <h2>⚠️ ${escapeHtml(type)}</h2>
          </div>
          <div class="error-boundary-body">
            <p class="error-boundary-message">${escapeHtml(errorMessage)}</p>
            <p class="error-boundary-description">
              We encountered an error, but your data is safe. You can try reloading the app.
            </p>
            <div class="error-boundary-actions">
              <button onclick="location.reload()" class="btn btn-primary">
                🔄 Reload App
              </button>
              <button onclick="this.closest('.error-boundary-overlay').style.display='none'" class="btn btn-secondary">
                ✕ Dismiss
              </button>
            </div>
            ${this.options.showDetails ? `
              <details class="error-boundary-details">
                <summary>Technical Details</summary>
                <pre class="error-boundary-stack">${escapeHtml(errorStack)}</pre>
              </details>
            ` : ''}
          </div>
        </div>
      </div>
    `;

    container.style.display = 'block';

    // Add styles if not already present
    if (!document.getElementById('error-boundary-styles')) {
      this.injectStyles();
    }
  }

  /**
   * Inject error boundary styles
   */
  injectStyles() {
    const style = document.createElement('style');
    style.id = 'error-boundary-styles';
    style.textContent = `
      .error-boundary-overlay {
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        background: rgba(0, 0, 0, 0.8);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
        padding: 20px;
      }

      .error-boundary-content {
        background: var(--bg-card, white);
        border-radius: 16px;
        max-width: 600px;
        width: 100%;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
        overflow: hidden;
      }

      .error-boundary-header {
        background: linear-gradient(135deg, #dc3545 0%, #c82333 100%);
        color: white;
        padding: 24px;
        text-align: center;
      }

      .error-boundary-header h2 {
        margin: 0;
        font-size: 24px;
        font-weight: 700;
      }

      .error-boundary-body {
        padding: 24px;
      }

      .error-boundary-message {
        font-size: 18px;
        font-weight: 600;
        color: var(--text-primary, #333);
        margin-bottom: 12px;
      }

      .error-boundary-description {
        color: var(--text-secondary, #666);
        margin-bottom: 24px;
      }

      .error-boundary-actions {
        display: flex;
        gap: 12px;
        margin-bottom: 16px;
      }

      .error-boundary-actions .btn {
        flex: 1;
        padding: 12px 24px;
        border: none;
        border-radius: 8px;
        font-size: 16px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.2s ease;
      }

      .error-boundary-actions .btn-primary {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
      }

      .error-boundary-actions .btn-primary:hover {
        transform: translateY(-2px);
        box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
      }

      .error-boundary-actions .btn-secondary {
        background: var(--bg-secondary, #f8f9fa);
        color: var(--text-primary, #333);
      }

      .error-boundary-actions .btn-secondary:hover {
        background: var(--bg-tertiary, #e9ecef);
      }

      .error-boundary-details {
        margin-top: 16px;
        border-top: 1px solid var(--border-light, #e9ecef);
        padding-top: 16px;
      }

      .error-boundary-details summary {
        cursor: pointer;
        font-weight: 600;
        color: var(--text-secondary, #666);
        padding: 8px;
        user-select: none;
      }

      .error-boundary-details summary:hover {
        color: var(--text-primary, #333);
      }

      .error-boundary-stack {
        background: var(--bg-secondary, #f8f9fa);
        border: 1px solid var(--border-light, #e9ecef);
        border-radius: 8px;
        padding: 16px;
        overflow-x: auto;
        font-size: 12px;
        color: var(--text-primary, #333);
        margin-top: 12px;
        max-height: 300px;
        overflow-y: auto;
      }
    `;
    document.head.appendChild(style);
  }

  /**
   * Clear error UI
   */
  clearError() {
    const container = document.getElementById(this.options.containerId);
    if (container) {
      container.style.display = 'none';
      container.innerHTML = '';
    }
  }

  /**
   * Reset error count
   */
  reset() {
    this.errorCount = 0;
    this.clearError();
  }
}

// Create singleton instance
let errorBoundaryInstance = null;

/**
 * Initialize error boundary (singleton)
 * @param {Object} options - Configuration options
 * @returns {ErrorBoundary} Error boundary instance
 */
function initErrorBoundary(options = {}) {
  if (!errorBoundaryInstance) {
    errorBoundaryInstance = new ErrorBoundary(options);
    errorBoundaryInstance.init();
  }
  return errorBoundaryInstance;
}

/**
 * Get error boundary instance
 * @returns {ErrorBoundary|null} Error boundary instance
 */
function getErrorBoundary() {
  return errorBoundaryInstance;
}

module.exports = {
  ErrorBoundary,
  initErrorBoundary,
  getErrorBoundary
};
