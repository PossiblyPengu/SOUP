/**
 * DOM Utility Functions
 * Shared across all applications
 */

/**
 * Query selector shorthand
 * @param {string} selector - CSS selector
 * @param {Element|Document} element - Parent element (defaults to document)
 * @returns {Element|null}
 */
const qs = (selector, element = document) => element.querySelector(selector);

/**
 * Query selector all shorthand
 * @param {string} selector - CSS selector
 * @param {Element|Document} element - Parent element (defaults to document)
 * @returns {NodeList}
 */
const qsa = (selector, element = document) => element.querySelectorAll(selector);

/**
 * Create element with attributes and children
 * @param {string} tag - HTML tag name
 * @param {Object} attrs - Attributes object
 * @param {Array|string} children - Child elements or text
 * @returns {Element}
 * @example
 * createElement('div', { class: 'card', id: 'myCard' }, [
 *   createElement('h2', {}, 'Title'),
 *   createElement('p', {}, 'Content')
 * ]);
 */
function createElement(tag, attrs = {}, children = []) {
  const element = document.createElement(tag);

  // Set attributes
  Object.entries(attrs).forEach(([key, value]) => {
    if (key === 'class') {
      element.className = value;
    } else if (key === 'dataset') {
      Object.entries(value).forEach(([dataKey, dataValue]) => {
        element.dataset[dataKey] = dataValue;
      });
    } else if (key.startsWith('on') && typeof value === 'function') {
      element.addEventListener(key.substring(2).toLowerCase(), value);
    } else {
      element.setAttribute(key, value);
    }
  });

  // Add children
  if (typeof children === 'string') {
    element.textContent = children;
  } else if (Array.isArray(children)) {
    children.forEach(child => {
      if (typeof child === 'string') {
        element.appendChild(document.createTextNode(child));
      } else if (child instanceof Element) {
        element.appendChild(child);
      }
    });
  }

  return element;
}

/**
 * Remove all children from element
 * @param {Element} element - Parent element
 */
function clearElement(element) {
  while (element.firstChild) {
    element.removeChild(element.firstChild);
  }
}

/**
 * Toggle element visibility
 * @param {Element} element - Element to toggle
 * @param {boolean} show - Force show/hide (optional)
 */
function toggleElement(element, show) {
  if (show === undefined) {
    element.style.display = element.style.display === 'none' ? '' : 'none';
  } else {
    element.style.display = show ? '' : 'none';
  }
}

/**
 * Add multiple event listeners
 * @param {Element} element - Target element
 * @param {string[]} events - Array of event names
 * @param {Function} handler - Event handler
 */
function addEventListeners(element, events, handler) {
  events.forEach(event => element.addEventListener(event, handler));
}

module.exports = {
  qs,
  qsa,
  createElement,
  clearElement,
  toggleElement,
  addEventListeners
};
