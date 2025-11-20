/**
 * Security and Sanitization Utilities
 */

/**
 * Escape HTML to prevent XSS attacks
 * @param {string} unsafe - Unsafe string
 * @returns {string} Escaped string
 */
function escapeHtml(unsafe) {
  if (typeof unsafe !== 'string') return unsafe;
  return unsafe
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

/**
 * Validator class for input validation
 */
class Validator {
  /**
   * Sanitize string input
   * @param {string} str - Input string
   * @param {number} maxLength - Maximum length
   * @returns {string} Sanitized string
   */
  static sanitizeString(str, maxLength = 500) {
    if (typeof str !== 'string') return '';
    return str
      .trim()
      .slice(0, maxLength)
      .replace(/[<>]/g, ''); // Basic XSS prevention
  }

  /**
   * Validate date range
   * @param {number} year - Year value
   * @param {number} month - Month value (0-11)
   * @returns {boolean} Is valid
   */
  static isValidDate(year, month) {
    return (
      Number.isInteger(year) &&
      Number.isInteger(month) &&
      year >= 2000 && year <= 2100 &&
      month >= 0 && month <= 11
    );
  }

  /**
   * Sanitize number input
   * @param {any} value - Input value
   * @param {number} min - Minimum value
   * @param {number} max - Maximum value
   * @returns {number} Sanitized number
   */
  static sanitizeNumber(value, min = 0, max = 1000000) {
    const num = parseFloat(value);
    if (isNaN(num)) return min;
    return Math.max(min, Math.min(max, num));
  }

  /**
   * Validate email format
   * @param {string} email - Email string
   * @returns {boolean} Is valid email
   */
  static isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

  /**
   * Sanitize filename
   * @param {string} filename - Filename string
   * @returns {string} Sanitized filename
   */
  static sanitizeFilename(filename) {
    return filename
      .replace(/[^a-z0-9_\-\.]/gi, '_')
      .replace(/_{2,}/g, '_')
      .toLowerCase();
  }

  /**
   * Check if string is empty or whitespace
   * @param {string} str - Input string
   * @returns {boolean} Is empty
   */
  static isEmpty(str) {
    return !str || str.trim().length === 0;
  }

  /**
   * Validate URL format
   * @param {string} url - URL string
   * @returns {boolean} Is valid URL
   */
  static isValidUrl(url) {
    try {
      new URL(url);
      return true;
    } catch {
      return false;
    }
  }
}

/**
 * Safe JSON parse wrapper
 * @param {string} str - JSON string
 * @param {any} fallback - Fallback value
 * @returns {any} Parsed object or fallback
 */
function safeJsonParse(str, fallback = null) {
  try {
    return JSON.parse(str);
  } catch (error) {
    console.error('JSON parse error:', error);
    return fallback;
  }
}

/**
 * Safe JSON stringify wrapper
 * @param {any} obj - Object to stringify
 * @param {string} fallback - Fallback string
 * @returns {string} JSON string or fallback
 */
function safeJsonStringify(obj, fallback = '{}') {
  try {
    return JSON.stringify(obj);
  } catch (error) {
    console.error('JSON stringify error:', error);
    return fallback;
  }
}

module.exports = {
  escapeHtml,
  Validator,
  safeJsonParse,
  safeJsonStringify
};
