// Essentials Buddy Module JavaScript
console.log('Essentials Buddy module loaded');

// Theme sync with parent
if (parent && parent.document) {
  const parentTheme = parent.document.documentElement.getAttribute('data-theme');
  if (parentTheme) {
    document.documentElement.setAttribute('data-theme', parentTheme);
  }
}

// Module initialization
document.addEventListener('DOMContentLoaded', () => {
  console.log('Essentials Buddy module initialized');
});
