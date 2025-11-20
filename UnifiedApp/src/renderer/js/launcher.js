// ===================================
// Business Tools Suite - Launcher
// ===================================

// Module Navigation
const launcherContainer = document.querySelector('.launcher-container');
const moduleContainer = document.getElementById('module-container');
const moduleContent = document.getElementById('module-content');

// Simple debug logger - renderer can set window.DEBUG via electronAPI.isDev()
const log = (...args) => {
  try {
    if (window.DEBUG) {
      console.log(...args);
    }
  } catch (e) {
    // ignore
  }
};
// Module configurations
const modules = {
  'expirewise': {
    title: 'ExpireWise',
    path: 'modules/expirewise/index.html',
    icon: '📦'
  },
  'allocation-buddy': {
    title: 'Allocation Buddy',
    path: 'modules/allocation-buddy/index.html',
    icon: '📊'
  },
  'essentials-buddy': {
    title: 'Essentials Buddy',
    path: 'modules/essentials-buddy/index.html',
    icon: '📋'
  }
};

// Launch a module
window.launchModule = function(moduleName) {
  const module = modules[moduleName];

  if (!module) {
    console.error('Module not found:', moduleName);
    return;
  }

  // Hide launcher, show module container
  launcherContainer.style.display = 'none';
  moduleContainer.style.display = 'block';

  // Show back button and update titlebar with module title
  const backBtn = document.getElementById('backToLauncherBtn');
  const moduleTitle = document.getElementById('titlebar-module-title');
  
  if (backBtn) {
    backBtn.style.display = 'flex';
  }
  
  if (moduleTitle) {
    moduleTitle.textContent = `${module.icon} ${module.title}`;
  }

  // Check if module iframe already exists
  const existingIframe = moduleContent.querySelector('iframe[src*="' + moduleName + '"]');

  if (existingIframe) {
    // Module already loaded - hide all others and show this one
    console.log('Module already loaded, preserving state:', moduleName);

    // Hide all iframes
    const allIframes = moduleContent.querySelectorAll('iframe');
    allIframes.forEach(iframe => {
      iframe.style.display = 'none';
    });

    // Show the selected iframe
    existingIframe.style.display = 'block';
  } else {
    // Load module content for the first time
    loadModuleContent(module.path, moduleName);
  }
}

// Load module content via iframe or direct injection
function loadModuleContent(modulePath, moduleName) {
  // Hide all existing iframes
  const allIframes = moduleContent.querySelectorAll('iframe');
  allIframes.forEach(iframe => {
    iframe.style.display = 'none';
  });

  // Create an iframe to load the module
  const iframe = document.createElement('iframe');
  iframe.src = modulePath;
  iframe.style.width = '100%';
  iframe.style.height = 'calc(100vh - 48px)'; // Subtract titlebar height (48px)
  iframe.style.border = 'none';
  iframe.style.display = 'block';

  // Add data attribute to identify the module
  iframe.setAttribute('data-module', moduleName);

  // Add iframe without clearing existing ones
  moduleContent.appendChild(iframe);
}

// Return to launcher
window.returnToLauncher = function() {
  // Hide module container, show launcher
  moduleContainer.style.display = 'none';
  launcherContainer.style.display = 'flex';

  // Hide back button and clear module title
  const backBtn = document.getElementById('backToLauncherBtn');
  const moduleTitle = document.getElementById('titlebar-module-title');

  if (backBtn) {
    backBtn.style.display = 'none';
  }

  if (moduleTitle) {
    moduleTitle.textContent = '';
  }

  // Hide all module iframes (preserve state - don't clear)
  const allIframes = moduleContent.querySelectorAll('iframe');
  allIframes.forEach(iframe => {
    iframe.style.display = 'none';
  });
}

// Keyboard shortcuts
document.addEventListener('keydown', (e) => {
  // ESC to return to launcher
  if (e.key === 'Escape' && moduleContainer.style.display === 'block') {
    returnToLauncher();
  }

  // Alt+1, Alt+2, Alt+3 to launch modules
  if (e.altKey) {
    switch(e.key) {
      case '1':
        launchModule('expirewise');
        break;
      case '2':
        launchModule('allocation-buddy');
        break;
      case '3':
        launchModule('essentials-buddy');
        break;
    }
  }
});

// Log app initialization (gated by debug flag)
log('Business Tools Suite Launcher initialized');
log('Available modules:', Object.keys(modules));
log('Keyboard shortcuts:');
log('  - ESC: Return to launcher');
log('  - Alt+1: Launch ExpireWise');
log('  - Alt+2: Launch Allocation Buddy');
log('  - Alt+3: Launch Essentials Buddy');

// ===================================
// Custom Title Bar Controls
// ===================================
const setupTitleBarControls = () => {
  if (!window.electronAPI) {
    console.log('⚠️ electronAPI not available, title bar controls disabled');
    return;
  }

  const minimizeBtn = document.getElementById('minimizeBtn');
  const maximizeBtn = document.getElementById('maximizeBtn');
  const closeBtn = document.getElementById('closeBtn');
  const themeToggle = document.getElementById('themeToggle');
  const backToLauncherBtn = document.getElementById('backToLauncherBtn');

  // Back to launcher button
  if (backToLauncherBtn) {
    backToLauncherBtn.addEventListener('click', () => {
      window.returnToLauncher();
    });
  }

  // Theme toggle
  if (themeToggle) {
    const initTheme = () => {
      const currentTheme = localStorage.getItem('appTheme') || 'dark';
      document.documentElement.setAttribute('data-theme', currentTheme);
      themeToggle.querySelector('.theme-icon').textContent = currentTheme === 'dark' ? '☀️' : '🌙';
    };

    themeToggle.addEventListener('click', () => {
      const currentTheme = document.documentElement.getAttribute('data-theme');
      const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
      document.documentElement.setAttribute('data-theme', newTheme);
      localStorage.setItem('appTheme', newTheme);
      themeToggle.querySelector('.theme-icon').textContent = newTheme === 'dark' ? '☀️' : '🌙';

      // Notify main process so it can broadcast the theme change safely to all renderer windows
      if (window.electronAPI?.setTheme) {
        window.electronAPI.setTheme(newTheme);
      } else {
        // Fallback: postMessage to same-origin frames (best-effort)
        const iframes = document.querySelectorAll('iframe');
        iframes.forEach(iframe => {
          try {
            iframe.contentWindow?.postMessage({ type: 'themeChanged', theme: newTheme }, window.location.origin);
          } catch (err) {
            // ignore
          }
        });
      }

      log('🎨 Theme changed to:', newTheme);
    });

    initTheme();
  }

  if (minimizeBtn) {
    minimizeBtn.addEventListener('click', () => {
      window.electronAPI.windowMinimize();
    });
  }

  if (maximizeBtn) {
    maximizeBtn.addEventListener('click', async () => {
      const isMaximized = await window.electronAPI.windowMaximize();
      // Update icon based on state
      if (isMaximized) {
        maximizeBtn.innerHTML = `
          <svg width="12" height="12" viewBox="0 0 12 12">
            <rect x="2" y="0" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
            <rect x="0" y="2" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
          </svg>
        `;
        maximizeBtn.title = 'Restore';
      } else {
        maximizeBtn.innerHTML = `
          <svg width="12" height="12" viewBox="0 0 12 12">
            <rect x="1" y="1" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
          </svg>
        `;
        maximizeBtn.title = 'Maximize';
      }
    });

    // Check initial state
    window.electronAPI.windowIsMaximized().then(isMaximized => {
      if (isMaximized) {
        maximizeBtn.innerHTML = `
          <svg width="12" height="12" viewBox="0 0 12 12">
            <rect x="2" y="0" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
            <rect x="0" y="2" width="10" height="10" stroke="currentColor" stroke-width="1.5" fill="none"/>
          </svg>
        `;
        maximizeBtn.title = 'Restore';
      }
    });
  }

  if (closeBtn) {
    closeBtn.addEventListener('click', () => {
      window.electronAPI.windowClose();
    });
  }

  log('✅ Custom title bar controls initialized');
};

// Initialize title bar controls
setupTitleBarControls();

// Setup back to launcher button
const backToLauncherBtn = document.getElementById('backToLauncherBtn');
if (backToLauncherBtn) {
  backToLauncherBtn.addEventListener('click', () => {
    returnToLauncher();
  });
}

// Update title bar when module changes
const originalLaunchModule = launchModule;
window.launchModule = function(moduleName) {
  originalLaunchModule(moduleName);
  const module = modules[moduleName];
  if (module) {
    const titleElement = document.getElementById('titlebar-module-title');
    const backBtn = document.getElementById('backToLauncherBtn');
    if (titleElement) {
      titleElement.textContent = `${module.icon} ${module.title}`;
    }
    if (backBtn) {
      backBtn.style.display = 'flex';
    }
  }
};

const originalReturnToLauncher = returnToLauncher;
window.returnToLauncher = function() {
  originalReturnToLauncher();
  const titleElement = document.getElementById('titlebar-module-title');
  const backBtn = document.getElementById('backToLauncherBtn');
  if (titleElement) {
    titleElement.textContent = '';
  }
  if (backBtn) {
    backBtn.style.display = 'none';
  }
};

// Initialize debug flag and subscribe to theme changes from main
(function initRendererHelpers() {
  try {
    if (window.electronAPI?.isDev) {
      window.electronAPI.isDev().then(isDev => {
        window.DEBUG = !!isDev;
        log('Renderer dev mode:', window.DEBUG);
      }).catch(() => { window.DEBUG = false; });
    } else {
      window.DEBUG = false;
    }

    if (window.electronAPI?.onThemeChange) {
      window.electronAPI.onThemeChange((event, theme) => {
        try {
          if (theme) {
            document.documentElement.setAttribute('data-theme', theme);
            const themeToggle = document.getElementById('themeToggle');
            if (themeToggle) themeToggle.querySelector('.theme-icon').textContent = theme === 'dark' ? '☀️' : '🌙';
          }
        } catch (err) {
          // ignore
        }
      });
    }
  } catch (err) {
    // ignore
  }
})();
