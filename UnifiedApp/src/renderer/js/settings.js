// ===================================
// Unified Settings Manager
// ===================================

const settingsManager = {
  defaults: {
    // General
    theme: 'system',
    autoSave: true,
    confirmDelete: true,
    
    // ExpireWise
    expiryWarningDays: 30,
    criticalWarningDays: 7,
    showExpiredItems: true,
    defaultSortBy: 'expiry',
    
    // Essentials Buddy
    lowStockThreshold: 10,
    criticalStockThreshold: 3,
    
    // Analytics
    defaultChartType: 'bar',
    animateCharts: true,
    showChartLegends: true,
    analyticsPeriod: 30,
    forecastDays: 30,
    metricsLayout: 'grid',
    showPercentages: true,
    showTrends: true,
    includeExpiredInAnalytics: false,
    chartColorScheme: 'default',
    exportFormat: 'png',
    includeTimestampExport: true
  },

  load() {
    const saved = localStorage.getItem('unifiedAppSettings');
    return saved ? { ...this.defaults, ...JSON.parse(saved) } : { ...this.defaults };
  },

  save(settings) {
    localStorage.setItem('unifiedAppSettings', JSON.stringify(settings));
    this.notifyModules(settings);
  },

  reset() {
    localStorage.setItem('unifiedAppSettings', JSON.stringify(this.defaults));
    this.notifyModules(this.defaults);
    return { ...this.defaults };
  },

  notifyModules(settings) {
    // Dispatch event that modules can listen to
    window.dispatchEvent(new CustomEvent('settingsChanged', { detail: settings }));
    
    // Also post message to any iframes
    const iframe = document.querySelector('#module-content iframe');
    if (iframe) {
      iframe.contentWindow.postMessage({
        type: 'settingsChanged',
        settings: settings
      }, '*');
    }
  }
};

// Initialize settings modal
function initSettingsModal() {
  const modal = document.getElementById('settingsModal');
  const closeBtn = document.getElementById('closeSettingsModal');
  const cancelBtn = document.getElementById('cancelSettings');
  const saveBtn = document.getElementById('saveSettings');
  const resetBtn = document.getElementById('resetSettings');

  // Storage location state
  let unifiedCustomSavePath = null;

  // Load current settings
  async function loadSettings() {
    const settings = settingsManager.load();

    // General
    document.getElementById('themeSelect').value = settings.theme || 'system';
    document.getElementById('autoSave').checked = settings.autoSave !== false;
    document.getElementById('confirmDelete').checked = settings.confirmDelete !== false;

    // ExpireWise - Storage Location
    const unifiedDefaultRadio = document.querySelector('input[name="unifiedStorageLocation"][value="default"]');
    const unifiedCustomRadio = document.querySelector('input[name="unifiedStorageLocation"][value="custom"]');
    const unifiedDefaultPathEl = document.getElementById('unifiedDefaultStoragePath');
    const unifiedCustomPathEl = document.getElementById('unifiedCustomStoragePath');
    const unifiedSelectBtn = document.getElementById('unifiedSelectCustomLocationBtn');

    if (window.electronAPI && unifiedDefaultPathEl) {
      try {
        const customPath = localStorage.getItem('expirewise-custom-save-path');
        const userDataPath = await window.electronAPI.getAppPath('userData');
        const defaultPath = `${userDataPath}/expirewise-data.json`;
        unifiedDefaultPathEl.textContent = defaultPath;

        // Check if we're currently using custom path
        if (customPath) {
          unifiedCustomRadio.checked = true;
          unifiedCustomPathEl.textContent = customPath;
          unifiedCustomPathEl.style.display = 'block';
          unifiedSelectBtn.disabled = false;
          unifiedCustomSavePath = customPath;
        } else {
          unifiedDefaultRadio.checked = true;
          unifiedCustomPathEl.style.display = 'none';
          unifiedSelectBtn.disabled = true;
          unifiedCustomSavePath = null;
        }
      } catch (error) {
        console.error('Failed to load storage paths:', error);
        unifiedDefaultPathEl.textContent = 'Error loading path';
      }
    }

    // ExpireWise - Other settings
    document.getElementById('expiryWarningDays').value = settings.expiryWarningDays || 30;
    document.getElementById('criticalWarningDays').value = settings.criticalWarningDays || 7;
    document.getElementById('showExpiredItems').checked = settings.showExpiredItems !== false;
    document.getElementById('defaultSortBy').value = settings.defaultSortBy || 'expiry';

    // Essentials Buddy - settings managed in module's own settings modal

    // Analytics
    document.getElementById('defaultChartType').value = settings.defaultChartType || 'bar';
    document.getElementById('animateCharts').checked = settings.animateCharts !== false;
    document.getElementById('showChartLegends').checked = settings.showChartLegends !== false;
    document.getElementById('analyticsPeriod').value = settings.analyticsPeriod || 30;
    document.getElementById('forecastDays').value = settings.forecastDays || 30;
    document.getElementById('metricsLayout').value = settings.metricsLayout || 'grid';
    document.getElementById('showPercentages').checked = settings.showPercentages !== false;
    document.getElementById('showTrends').checked = settings.showTrends !== false;
    document.getElementById('includeExpiredInAnalytics').checked = settings.includeExpiredInAnalytics === true;
    document.getElementById('chartColorScheme').value = settings.chartColorScheme || 'default';
    document.getElementById('exportFormat').value = settings.exportFormat || 'png';
    document.getElementById('includeTimestampExport').checked = settings.includeTimestampExport !== false;
  }

  // Save settings
  async function saveSettings() {
    const currentSettings = settingsManager.load();

    // Handle ExpireWise storage location
    const selectedStorage = document.querySelector('input[name="unifiedStorageLocation"]:checked')?.value;

    if (window.electronAPI) {
      try {
        if (selectedStorage === 'custom') {
          if (!unifiedCustomSavePath) {
            alert('Please select a custom folder first.');
            return;
          }

          // Save custom path preference
          localStorage.setItem('expirewise-custom-save-path', unifiedCustomSavePath);

          // Notify ExpireWise module to update its storage path
          const iframe = document.querySelector('iframe[src*="expirewise"]');
          if (iframe && iframe.contentWindow) {
            iframe.contentWindow.postMessage({
              type: 'updateStoragePath',
              path: unifiedCustomSavePath
            }, '*');
          }

          console.log('✓ Custom save path updated:', unifiedCustomSavePath);
        } else {
          // Revert to default location
          localStorage.removeItem('expirewise-custom-save-path');

          // Notify ExpireWise module to use default path
          const iframe = document.querySelector('iframe[src*="expirewise"]');
          if (iframe && iframe.contentWindow) {
            const userDataPath = await window.electronAPI.getAppPath('userData');
            const defaultPath = `${userDataPath}/expirewise-data.json`;
            iframe.contentWindow.postMessage({
              type: 'updateStoragePath',
              path: defaultPath
            }, '*');
          }

          console.log('✓ Save path reverted to default');
        }
      } catch (error) {
        console.error('Failed to save storage location:', error);
        alert('Failed to save location preference. Please try again.');
        return;
      }
    }

    const settings = {
      // General
      theme: document.getElementById('themeSelect').value,
      autoSave: document.getElementById('autoSave').checked,
      confirmDelete: document.getElementById('confirmDelete').checked,

      // ExpireWise
      expiryWarningDays: parseInt(document.getElementById('expiryWarningDays').value),
      criticalWarningDays: parseInt(document.getElementById('criticalWarningDays').value),
      showExpiredItems: document.getElementById('showExpiredItems').checked,
      defaultSortBy: document.getElementById('defaultSortBy').value,

      // Essentials Buddy - settings managed in module's own settings modal
      lowStockThreshold: currentSettings.lowStockThreshold || 10,
      criticalStockThreshold: currentSettings.criticalStockThreshold || 3,

      // Analytics
      defaultChartType: document.getElementById('defaultChartType').value,
      animateCharts: document.getElementById('animateCharts').checked,
      showChartLegends: document.getElementById('showChartLegends').checked,
      analyticsPeriod: parseInt(document.getElementById('analyticsPeriod').value),
      forecastDays: parseInt(document.getElementById('forecastDays').value),
      metricsLayout: document.getElementById('metricsLayout').value,
      showPercentages: document.getElementById('showPercentages').checked,
      showTrends: document.getElementById('showTrends').checked,
      includeExpiredInAnalytics: document.getElementById('includeExpiredInAnalytics').checked,
      chartColorScheme: document.getElementById('chartColorScheme').value,
      exportFormat: document.getElementById('exportFormat').value,
      includeTimestampExport: document.getElementById('includeTimestampExport').checked
    };

    settingsManager.save(settings);
    closeModal();

    // Show success message
    showNotification('Settings saved successfully!');
  }

  // Show settings modal
  window.showSettings = function(tab = 'general') {
    loadSettings();
    modal.style.display = 'flex';
    
    // Switch to specified tab if provided
    if (tab) {
      switchSettingsTab(tab);
    }
  };

  // Close modal
  function closeModal() {
    modal.style.display = 'none';
  }

  // Event listeners
  closeBtn.addEventListener('click', closeModal);
  cancelBtn.addEventListener('click', closeModal);
  saveBtn.addEventListener('click', saveSettings);
  
  resetBtn.addEventListener('click', () => {
    if (confirm('Are you sure you want to reset all settings to defaults?')) {
      const defaults = settingsManager.reset();
      loadSettings();
      showNotification('Settings reset to defaults');
    }
  });

  // Tab switching
  const tabs = document.querySelectorAll('.settings-tab');
  const panels = document.querySelectorAll('.settings-panel');

  function switchSettingsTab(tabName) {
    tabs.forEach(tab => {
      tab.classList.toggle('active', tab.dataset.tab === tabName);
    });
    panels.forEach(panel => {
      panel.classList.toggle('active', panel.dataset.panel === tabName);
    });
  }

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      switchSettingsTab(tab.dataset.tab);
    });
  });

  // Storage location radio handlers
  const unifiedDefaultRadio = document.querySelector('input[name="unifiedStorageLocation"][value="default"]');
  const unifiedCustomRadio = document.querySelector('input[name="unifiedStorageLocation"][value="custom"]');
  const unifiedSelectBtn = document.getElementById('unifiedSelectCustomLocationBtn');
  const unifiedCustomPathEl = document.getElementById('unifiedCustomStoragePath');

  if (unifiedDefaultRadio) {
    unifiedDefaultRadio.addEventListener('change', () => {
      if (unifiedDefaultRadio.checked) {
        unifiedSelectBtn.disabled = true;
        unifiedCustomPathEl.style.display = 'none';
      }
    });
  }

  if (unifiedCustomRadio) {
    unifiedCustomRadio.addEventListener('change', () => {
      if (unifiedCustomRadio.checked) {
        unifiedSelectBtn.disabled = false;
        if (unifiedCustomSavePath) {
          unifiedCustomPathEl.style.display = 'block';
        }
      }
    });
  }

  // Handle custom location selection
  if (unifiedSelectBtn) {
    unifiedSelectBtn.addEventListener('click', async () => {
      if (!window.electronAPI) return;

      try {
        const result = await window.electronAPI.selectFile({
          title: 'Select Save Location',
          properties: ['openDirectory', 'createDirectory']
        });

        if (result && !result.canceled && result.filePaths && result.filePaths[0]) {
          unifiedCustomSavePath = result.filePaths[0] + '/expirewise-data.json';
          unifiedCustomPathEl.textContent = unifiedCustomSavePath;
          unifiedCustomPathEl.style.display = 'block';
          console.log('📂 Custom save location selected:', unifiedCustomSavePath);
        }
      } catch (error) {
        console.error('Failed to select custom location:', error);
        alert('Failed to select folder. Please try again.');
      }
    });
  }

  // Close modal on outside click
  modal.addEventListener('click', (e) => {
    if (e.target === modal) {
      closeModal();
    }
  });
}

// Show notification
function showNotification(message) {
  // Create simple notification
  const notification = document.createElement('div');
  notification.className = 'notification';
  notification.textContent = message;
  notification.style.cssText = `
    position: fixed;
    top: 20px;
    right: 20px;
    background: var(--success);
    color: white;
    padding: 1rem 1.5rem;
    border-radius: 8px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.2);
    z-index: 100000;
    animation: slideIn 0.3s ease-out;
  `;
  
  document.body.appendChild(notification);
  
  setTimeout(() => {
    notification.style.animation = 'slideOut 0.3s ease-out';
    setTimeout(() => notification.remove(), 300);
  }, 3000);
}

// Listen for messages from iframes to open settings
window.addEventListener('message', (event) => {
  if (event.data.type === 'openSettings') {
    window.showSettings(event.data.tab);
  }
});

// Initialize when DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initSettingsModal);
} else {
  initSettingsModal();
}

// Titlebar settings button handler
document.addEventListener('DOMContentLoaded', () => {
  const titlebarSettingsBtn = document.getElementById('titlebarSettingsBtn');
  if (titlebarSettingsBtn) {
    titlebarSettingsBtn.addEventListener('click', () => {
      window.showSettings('general');
    });
  }
});

// Export for module access
window.settingsManager = settingsManager;

// Allocation Buddy integration functions
window.openAllocationBuddyDictionary = function(tab = 'items') {
  // Close the unified settings modal first
  const modal = document.getElementById('settingsModal');
  if (modal) {
    modal.style.display = 'none';
  }

  // Launch allocation buddy (will preserve existing iframe if already loaded)
  if (window.launchModule) {
    window.launchModule('allocation-buddy');
  }

  // Wait a moment for UI to settle, then send message
  setTimeout(() => {
    const iframe = document.querySelector('iframe[src*="allocation-buddy"]');
    if (iframe && iframe.contentWindow) {
      iframe.contentWindow.postMessage({
        type: 'openDictionary',
        tab: tab
      }, '*');
    } else {
      console.warn('Allocation Buddy iframe not found');
    }
  }, 200);
};

window.openAllocationBuddyRankings = function() {
  // Close the unified settings modal first
  const modal = document.getElementById('settingsModal');
  if (modal) {
    modal.style.display = 'none';
  }

  // Launch allocation buddy (will preserve existing iframe if already loaded)
  if (window.launchModule) {
    window.launchModule('allocation-buddy');
  }

  // Wait a moment for UI to settle, then send message
  setTimeout(() => {
    const iframe = document.querySelector('iframe[src*="allocation-buddy"]');
    if (iframe && iframe.contentWindow) {
      iframe.contentWindow.postMessage({
        type: 'openRankings'
      }, '*');
    } else {
      console.warn('Allocation Buddy iframe not found');
    }
  }, 200);
};

window.openAllocationBuddyArchives = function() {
  // Close the unified settings modal first
  const modal = document.getElementById('settingsModal');
  if (modal) {
    modal.style.display = 'none';
  }

  // Launch allocation buddy (will preserve existing iframe if already loaded)
  if (window.launchModule) {
    window.launchModule('allocation-buddy');
  }

  // Wait a moment for UI to settle, then send message
  setTimeout(() => {
    const iframe = document.querySelector('iframe[src*="allocation-buddy"]');
    if (iframe && iframe.contentWindow) {
      iframe.contentWindow.postMessage({
        type: 'openArchives'
      }, '*');
    } else {
      console.warn('Allocation Buddy iframe not found');
    }
  }, 200);
};

// Essentials Buddy integration function
window.openEssentialsBuddySettings = function(tab = 'masterListTab') {
  // Close the unified settings modal first
  const modal = document.getElementById('settingsModal');
  if (modal) {
    modal.style.display = 'none';
  }

  // Launch essentials buddy (will preserve existing iframe if already loaded)
  if (window.launchModule) {
    window.launchModule('essentials-buddy');
  }

  // Wait a moment for UI to settle, then send message to open settings
  setTimeout(() => {
    const iframe = document.querySelector('iframe[src*="essentials-buddy"]');
    if (iframe && iframe.contentWindow) {
      iframe.contentWindow.postMessage({
        type: 'openSettings',
        tab: tab
      }, '*');
    } else {
      console.warn('Essentials Buddy iframe not found');
    }
  }, 200);
};
