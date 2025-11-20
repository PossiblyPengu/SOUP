/**
 * Date Utility Functions
 */

/**
 * Month names
 */
const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
];

const MONTHS_SHORT = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
];

/**
 * Get current year and month
 * @returns {{year: number, month: number}}
 */
function getCurrentYearMonth() {
  const now = new Date();
  return {
    year: now.getFullYear(),
    month: now.getMonth()
  };
}

/**
 * Format date for input field (YYYY-MM)
 * @param {number} year - Year
 * @param {number} month - Month (0-11)
 * @returns {string} Formatted date
 */
function formatDateForInput(year, month) {
  return `${year}-${String(month + 1).padStart(2, '0')}`;
}

/**
 * Parse date from input field (YYYY-MM)
 * @param {string} value - Input value
 * @returns {{year: number, month: number}}
 */
function parseDateFromInput(value) {
  const [yearStr, monthStr] = value.split('-');
  return {
    year: parseInt(yearStr, 10),
    month: parseInt(monthStr, 10) - 1
  };
}

/**
 * Format month and year (e.g., "January 2024")
 * @param {number} year - Year
 * @param {number} month - Month (0-11)
 * @param {boolean} short - Use short month name
 * @returns {string} Formatted date
 */
function formatMonthYear(year, month, short = false) {
  const monthNames = short ? MONTHS_SHORT : MONTHS;
  return `${monthNames[month]} ${year}`;
}

/**
 * Format date as YYYY-MM-DD
 * @param {Date} date - Date object
 * @returns {string} Formatted date
 */
function formatDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Calculate days between two dates
 * @param {Date} date1 - First date
 * @param {Date} date2 - Second date
 * @returns {number} Days difference
 */
function daysBetween(date1, date2) {
  const diff = date2 - date1;
  return Math.ceil(diff / (1000 * 60 * 60 * 24));
}

/**
 * Calculate days until expiration
 * @param {Date} expiryDate - Expiry date
 * @returns {number} Days until expiry (negative if expired)
 */
function daysUntilExpiry(expiryDate) {
  const now = new Date();
  now.setHours(0, 0, 0, 0);
  return daysBetween(now, expiryDate);
}

/**
 * Check if date is expired
 * @param {Date} expiryDate - Expiry date
 * @returns {boolean} Is expired
 */
function isExpired(expiryDate) {
  return daysUntilExpiry(expiryDate) < 0;
}

/**
 * Check if date is expiring soon
 * @param {Date} expiryDate - Expiry date
 * @param {number} days - Days threshold
 * @returns {boolean} Is expiring soon
 */
function isExpiringSoon(expiryDate, days = 30) {
  const daysLeft = daysUntilExpiry(expiryDate);
  return daysLeft >= 0 && daysLeft <= days;
}

/**
 * Add days to date
 * @param {Date} date - Base date
 * @param {number} days - Days to add
 * @returns {Date} New date
 */
function addDays(date, days) {
  const result = new Date(date);
  result.setDate(result.getDate() + days);
  return result;
}

/**
 * Get start of month
 * @param {Date} date - Date
 * @returns {Date} Start of month
 */
function startOfMonth(date) {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

/**
 * Get end of month
 * @param {Date} date - Date
 * @returns {Date} End of month
 */
function endOfMonth(date) {
  return new Date(date.getFullYear(), date.getMonth() + 1, 0);
}

/**
 * Format relative time (e.g., "2 days ago", "in 3 weeks")
 * @param {Date} date - Date to compare
 * @returns {string} Relative time string
 */
function formatRelativeTime(date) {
  const now = new Date();
  const diff = date - now;
  const absDiff = Math.abs(diff);
  const seconds = Math.floor(absDiff / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const days = Math.floor(hours / 24);
  const weeks = Math.floor(days / 7);
  const months = Math.floor(days / 30);
  const years = Math.floor(days / 365);

  const past = diff < 0;
  const suffix = past ? 'ago' : 'from now';

  if (years > 0) return `${years} year${years > 1 ? 's' : ''} ${suffix}`;
  if (months > 0) return `${months} month${months > 1 ? 's' : ''} ${suffix}`;
  if (weeks > 0) return `${weeks} week${weeks > 1 ? 's' : ''} ${suffix}`;
  if (days > 0) return `${days} day${days > 1 ? 's' : ''} ${suffix}`;
  if (hours > 0) return `${hours} hour${hours > 1 ? 's' : ''} ${suffix}`;
  if (minutes > 0) return `${minutes} minute${minutes > 1 ? 's' : ''} ${suffix}`;
  return 'just now';
}

module.exports = {
  MONTHS,
  MONTHS_SHORT,
  getCurrentYearMonth,
  formatDateForInput,
  parseDateFromInput,
  formatMonthYear,
  formatDate,
  daysBetween,
  daysUntilExpiry,
  isExpired,
  isExpiringSoon,
  addDays,
  startOfMonth,
  endOfMonth,
  formatRelativeTime
};
