// ExpireWise Module JavaScript
console.log('ExpireWise module loaded');

// Theme sync with parent
if (parent && parent.document) {
  const parentTheme = parent.document.documentElement.getAttribute('data-theme');
  if (parentTheme) {
    document.documentElement.setAttribute('data-theme', parentTheme);
  }
}

// Module initialization
document.addEventListener('DOMContentLoaded', () => {
  console.log('ExpireWise module initialized');
});
