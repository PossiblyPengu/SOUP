/**
 * Toast Notification System
 */

const { escapeHtml } = require('./sanitize');

class ToastManager {
  constructor(options = {}) {
    this.options = {
      position: options.position || 'top-right', // top-right, top-left, bottom-right, bottom-left, top-center, bottom-center
      maxToasts: options.maxToasts || 5,
      defaultDuration: options.defaultDuration || 3000,
      ...options
    };
    this.toasts = [];
    this.container = null;
    this.init();
  }

  /**
   * Initialize toast container
   */
  init() {
    this.container = document.createElement('div');
    this.container.className = `toast-container toast-${this.options.position}`;
    this.container.setAttribute('aria-live', 'polite');
    this.container.setAttribute('aria-atomic', 'false');
    document.body.appendChild(this.container);

    // Inject styles
    if (!document.getElementById('toast-styles')) {
      this.injectStyles();
    }
  }

  /**
   * Show toast notification
   * @param {string} message - Toast message
   * @param {string} type - Toast type (success, error, warning, info)
   * @param {number} duration - Duration in ms (0 = no auto-dismiss)
   * @returns {HTMLElement} Toast element
   */
  show(message, type = 'info', duration = null) {
    // Limit number of toasts
    if (this.toasts.length >= this.options.maxToasts) {
      this.hide(this.toasts[0]);
    }

    const toast = this.createToast(message, type);
    this.container.appendChild(toast);
    this.toasts.push(toast);

    // Animate in
    requestAnimationFrame(() => {
      toast.classList.add('toast-show');
    });

    // Auto dismiss
    const dismissDuration = duration === null ? this.options.defaultDuration : duration;
    if (dismissDuration > 0) {
      toast.timeoutId = setTimeout(() => this.hide(toast), dismissDuration);
    }

    return toast;
  }

  /**
   * Create toast element
   * @param {string} message - Message text
   * @param {string} type - Toast type
   * @returns {HTMLElement} Toast element
   */
  createToast(message, type) {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.setAttribute('role', 'alert');

    const icon = this.getIcon(type);
    const closeBtn = document.createElement('button');
    closeBtn.className = 'toast-close';
    closeBtn.setAttribute('aria-label', 'Close notification');
    closeBtn.innerHTML = '&times;';
    closeBtn.addEventListener('click', () => this.hide(toast));

    toast.innerHTML = `
      <span class="toast-icon" aria-hidden="true">${icon}</span>
      <span class="toast-message">${escapeHtml(message)}</span>
    `;
    toast.appendChild(closeBtn);

    // Pause timer on hover
    toast.addEventListener('mouseenter', () => {
      if (toast.timeoutId) {
        clearTimeout(toast.timeoutId);
        toast.timeoutId = null;
      }
    });

    toast.addEventListener('mouseleave', () => {
      if (!toast.timeoutId) {
        toast.timeoutId = setTimeout(() => this.hide(toast), this.options.defaultDuration);
      }
    });

    return toast;
  }

  /**
   * Hide toast
   * @param {HTMLElement} toast - Toast element
   */
  hide(toast) {
    if (!toast || !toast.parentElement) return;

    if (toast.timeoutId) {
      clearTimeout(toast.timeoutId);
    }

    toast.classList.remove('toast-show');
    toast.classList.add('toast-hide');

    setTimeout(() => {
      if (toast.parentElement) {
        toast.remove();
      }
      this.toasts = this.toasts.filter(t => t !== toast);
    }, 300);
  }

  /**
   * Get icon for toast type
   * @param {string} type - Toast type
   * @returns {string} Icon HTML
   */
  getIcon(type) {
    const icons = {
      success: '✓',
      error: '✕',
      warning: '⚠',
      info: 'ℹ'
    };
    return icons[type] || icons.info;
  }

  /**
   * Show success toast
   * @param {string} message - Message
   * @param {number} duration - Duration
   */
  success(message, duration = null) {
    return this.show(message, 'success', duration);
  }

  /**
   * Show error toast
   * @param {string} message - Message
   * @param {number} duration - Duration
   */
  error(message, duration = null) {
    return this.show(message, 'error', duration || 5000);
  }

  /**
   * Show warning toast
   * @param {string} message - Message
   * @param {number} duration - Duration
   */
  warning(message, duration = null) {
    return this.show(message, 'warning', duration || 4000);
  }

  /**
   * Show info toast
   * @param {string} message - Message
   * @param {number} duration - Duration
   */
  info(message, duration = null) {
    return this.show(message, 'info', duration);
  }

  /**
   * Clear all toasts
   */
  clearAll() {
    this.toasts.forEach(toast => this.hide(toast));
  }

  /**
   * Inject toast styles
   */
  injectStyles() {
    const style = document.createElement('style');
    style.id = 'toast-styles';
    style.textContent = `
      .toast-container {
        position: fixed;
        z-index: 9999;
        pointer-events: none;
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .toast-top-right {
        top: 20px;
        right: 20px;
      }

      .toast-top-left {
        top: 20px;
        left: 20px;
      }

      .toast-bottom-right {
        bottom: 20px;
        right: 20px;
      }

      .toast-bottom-left {
        bottom: 20px;
        left: 20px;
      }

      .toast-top-center {
        top: 20px;
        left: 50%;
        transform: translateX(-50%);
      }

      .toast-bottom-center {
        bottom: 20px;
        left: 50%;
        transform: translateX(-50%);
      }

      .toast {
        pointer-events: all;
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 16px 20px;
        background: white;
        border-radius: 12px;
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
        min-width: 300px;
        max-width: 500px;
        opacity: 0;
        transform: translateX(100px);
        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
      }

      .toast-show {
        opacity: 1;
        transform: translateX(0);
      }

      .toast-hide {
        opacity: 0;
        transform: translateX(100px);
      }

      .toast-icon {
        flex-shrink: 0;
        width: 24px;
        height: 24px;
        display: flex;
        align-items: center;
        justify-content: center;
        border-radius: 50%;
        font-size: 16px;
        font-weight: bold;
      }

      .toast-message {
        flex: 1;
        color: var(--text-primary, #333);
        font-size: 14px;
        line-height: 1.4;
      }

      .toast-close {
        flex-shrink: 0;
        width: 24px;
        height: 24px;
        border: none;
        background: transparent;
        color: var(--text-secondary, #666);
        font-size: 24px;
        line-height: 1;
        cursor: pointer;
        border-radius: 50%;
        transition: all 0.2s ease;
        padding: 0;
        display: flex;
        align-items: center;
        justify-content: center;
      }

      .toast-close:hover {
        background: rgba(0, 0, 0, 0.05);
        color: var(--text-primary, #333);
      }

      /* Toast Types */
      .toast-success {
        border-left: 4px solid #28a745;
      }

      .toast-success .toast-icon {
        background: #d4edda;
        color: #28a745;
      }

      .toast-error {
        border-left: 4px solid #dc3545;
      }

      .toast-error .toast-icon {
        background: #f8d7da;
        color: #dc3545;
      }

      .toast-warning {
        border-left: 4px solid #ffc107;
      }

      .toast-warning .toast-icon {
        background: #fff3cd;
        color: #856404;
      }

      .toast-info {
        border-left: 4px solid #17a2b8;
      }

      .toast-info .toast-icon {
        background: #d1ecf1;
        color: #17a2b8;
      }

      /* Dark theme support */
      [data-theme="dark"] .toast {
        background: var(--bg-card, #2d2d2d);
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5);
      }

      [data-theme="dark"] .toast-message {
        color: var(--text-primary, #e0e0e0);
      }

      /* Responsive */
      @media (max-width: 640px) {
        .toast-container {
          left: 10px !important;
          right: 10px !important;
          top: 10px !important;
          transform: none !important;
        }

        .toast {
          min-width: auto;
          width: 100%;
        }
      }
    `;
    document.head.appendChild(style);
  }

  /**
   * Destroy toast manager
   */
  destroy() {
    this.clearAll();
    if (this.container && this.container.parentElement) {
      this.container.remove();
    }
  }
}

// Create singleton instance
let toastInstance = null;

/**
 * Get toast manager instance (singleton)
 * @param {Object} options - Configuration options
 * @returns {ToastManager} Toast manager instance
 */
function getToast(options = {}) {
  if (!toastInstance) {
    toastInstance = new ToastManager(options);
  }
  return toastInstance;
}

module.exports = {
  ToastManager,
  getToast
};
