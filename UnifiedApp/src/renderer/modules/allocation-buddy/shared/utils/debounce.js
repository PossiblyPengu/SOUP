/**
 * Performance Utilities - Debounce and Throttle
 */

/**
 * Debounce function - delays execution until after wait time has elapsed
 * @param {Function} func - Function to debounce
 * @param {number} wait - Wait time in milliseconds
 * @returns {Function} Debounced function
 * @example
 * const search = debounce((term) => console.log(term), 300);
 * input.addEventListener('input', (e) => search(e.target.value));
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
 * Throttle function - limits execution to once per wait period
 * @param {Function} func - Function to throttle
 * @param {number} wait - Wait time in milliseconds
 * @returns {Function} Throttled function
 * @example
 * const handleScroll = throttle(() => console.log('scrolling'), 100);
 * window.addEventListener('scroll', handleScroll);
 */
function throttle(func, wait = 300) {
  let inThrottle;
  return function executedFunction(...args) {
    if (!inThrottle) {
      func.apply(this, args);
      inThrottle = true;
      setTimeout(() => inThrottle = false, wait);
    }
  };
}

/**
 * Request animation frame wrapper for smooth animations
 * @param {Function} func - Function to execute
 * @returns {Function} RAF-wrapped function
 */
function rafThrottle(func) {
  let rafId = null;
  return function executedFunction(...args) {
    if (rafId === null) {
      rafId = requestAnimationFrame(() => {
        func.apply(this, args);
        rafId = null;
      });
    }
  };
}

module.exports = {
  debounce,
  throttle,
  rafThrottle
};
