// ===================================
// Business Tools Suite - Main Process
// ===================================

const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs').promises;

// Handle Squirrel events for Windows installer
if (require('electron-squirrel-startup')) {
  app.quit();
}

let mainWindow;

// ===================================
// Window State Management
// ===================================

const windowStateFile = path.join(app.getPath('userData'), 'window-state.json');

async function loadWindowState() {
  try {
    const data = await fs.readFile(windowStateFile, 'utf8');
    return JSON.parse(data);
  } catch (error) {
    // Return defaults if file doesn't exist
    return {
      width: 1400,
      height: 900,
      x: undefined,
      y: undefined,
      isMaximized: false
    };
  }
}

async function saveWindowState() {
  try {
    const bounds = mainWindow.getBounds();
    const state = {
      width: bounds.width,
      height: bounds.height,
      x: bounds.x,
      y: bounds.y,
      isMaximized: mainWindow.isMaximized()
    };
    await fs.writeFile(windowStateFile, JSON.stringify(state, null, 2));
  } catch (error) {
    console.error('Failed to save window state:', error);
  }
}

async function createWindow() {
  // Load saved window state
  const windowState = await loadWindowState();

  mainWindow = new BrowserWindow({
    width: windowState.width,
    height: windowState.height,
    x: windowState.x,
    y: windowState.y,
    minWidth: 1000,
    minHeight: 700,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js'),
      // Disable webview tag by default for security. Enable only if a module explicitly requires it.
      webviewTag: false
    },
    icon: path.join(__dirname, '../../build/icon.ico'),
    backgroundColor: '#667eea',
    show: false, // Don't show until ready
    frame: false, // Remove default frame
    titleBarStyle: 'hidden',
    titleBarOverlay: false
  });

  // Load the launcher
  mainWindow.loadFile(path.join(__dirname, '../renderer/index.html'));

  // Show window when ready
  mainWindow.once('ready-to-show', () => {
    // Restore maximized state if needed
    if (windowState.isMaximized) {
      mainWindow.maximize();
    }
    mainWindow.show();
  });

  // Save window state on resize and move
  let saveStateTimeout;
  const debouncedSaveState = () => {
    clearTimeout(saveStateTimeout);
    saveStateTimeout = setTimeout(() => {
      if (!mainWindow.isMaximized() && !mainWindow.isMinimized() && !mainWindow.isFullScreen()) {
        saveWindowState();
      }
    }, 500);
  };

  mainWindow.on('resize', debouncedSaveState);
  mainWindow.on('move', debouncedSaveState);
  mainWindow.on('maximize', saveWindowState);
  mainWindow.on('unmaximize', saveWindowState);

  // Open DevTools in development
  if (process.env.NODE_ENV === 'development' || process.argv.includes('--dev')) {
    mainWindow.webContents.openDevTools();
  }

  // Register F12 and Ctrl+Shift+I to toggle DevTools
  mainWindow.webContents.on('before-input-event', (event, input) => {
    if (input.type === 'keyDown') {
      // F12 to toggle DevTools
      if (input.key === 'F12') {
        if (mainWindow.webContents.isDevToolsOpened()) {
          mainWindow.webContents.closeDevTools();
        } else {
          mainWindow.webContents.openDevTools();
        }
        event.preventDefault();
      }
      // Ctrl+Shift+I to toggle DevTools
      if (input.control && input.shift && input.key === 'I') {
        if (mainWindow.webContents.isDevToolsOpened()) {
          mainWindow.webContents.closeDevTools();
        } else {
          mainWindow.webContents.openDevTools();
        }
        event.preventDefault();
      }
    }
  });

  // Handle window close
  mainWindow.on('close', () => {
    saveWindowState();
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// App lifecycle
app.whenReady().then(() => {
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// IPC Handlers
ipcMain.handle('get-app-version', () => {
  return app.getVersion();
});

ipcMain.handle('get-app-path', (event, name) => {
  return app.getPath(name);
});

// Window controls for frameless window
ipcMain.handle('window-minimize', () => {
  if (mainWindow) {
    mainWindow.minimize();
  }
});

ipcMain.handle('window-maximize', () => {
  if (mainWindow) {
    if (mainWindow.isMaximized()) {
      mainWindow.unmaximize();
    } else {
      mainWindow.maximize();
    }
    return mainWindow.isMaximized();
  }
  return false;
});

ipcMain.handle('window-close', () => {
  if (mainWindow) {
    mainWindow.close();
  }
});

ipcMain.handle('window-is-maximized', () => {
  return mainWindow ? mainWindow.isMaximized() : false;
});

// File selection dialog
ipcMain.handle('select-file', async (event, options) => {
  try {
    const result = await dialog.showOpenDialog({
      properties: ['openFile'],
      filters: options?.filters || [
        { name: 'Excel Files', extensions: ['xlsx', 'xlsm', 'xls'] },
        { name: 'CSV Files', extensions: ['csv'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });
    
    if (result.canceled) {
      return null;
    }
    
    return result.filePaths[0];
  } catch (error) {
    console.error('Error selecting file:', error);
    throw error;
  }
});

// Save file dialog
ipcMain.handle('save-file', async (event, options) => {
  try {
    const result = await dialog.showSaveDialog({
      defaultPath: options?.defaultPath || 'export.xlsx',
      filters: options?.filters || [
        { name: 'Excel Files', extensions: ['xlsx'] },
        { name: 'CSV Files', extensions: ['csv'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });
    
    if (result.canceled) {
      return null;
    }
    
    return result.filePath;
  } catch (error) {
    console.error('Error saving file:', error);
    throw error;
  }
});

// Read file
ipcMain.handle('read-file', async (event, filePath) => {
  try {
    const data = await fs.readFile(filePath, 'utf8');
    return { success: true, data };
  } catch (error) {
    console.error('Error reading file:', error);
    return { success: false, error: error.message };
  }
});

// Write file
ipcMain.handle('write-file', async (event, filePath, data) => {
  try {
    // Convert ArrayBuffer to Buffer if needed
    const buffer = Buffer.isBuffer(data) ? data : Buffer.from(data);
    await fs.writeFile(filePath, buffer);
    return { success: true };
  } catch (error) {
    console.error('Error writing file:', error);
    return { success: false, error: error.message };
  }
});

// Get file stats
ipcMain.handle('get-file-stats', async (event, filePath) => {
  try {
    const stats = await fs.stat(filePath);
    return {
      success: true,
      size: stats.size,
      modified: stats.mtime,
      created: stats.birthtime,
      isFile: stats.isFile(),
      isDirectory: stats.isDirectory()
    };
  } catch (error) {
    console.error('Error getting file stats:', error);
    return { success: false, error: error.message };
  }
});

// ============================================
// Archive operations (for Allocation Buddy)
// ============================================

// Save archive to file system
ipcMain.handle('save-archive', async (event, archiveName, archiveData) => {
  try {
    const archivesDir = path.join(app.getPath('userData'), 'allocation-buddy-archives');

    // Create archives directory if it doesn't exist
    await fs.mkdir(archivesDir, { recursive: true });

    const archivePath = path.join(archivesDir, archiveName);
    await fs.writeFile(archivePath, JSON.stringify(archiveData, null, 2), 'utf8');

    return { success: true, path: archivePath };
  } catch (error) {
    console.error('Failed to save archive:', error);
    return { success: false, error: error.message };
  }
});

// Load all archives metadata
ipcMain.handle('load-archives', async () => {
  try {
    const archivesDir = path.join(app.getPath('userData'), 'allocation-buddy-archives');

    // Create directory if it doesn't exist
    try {
      await fs.mkdir(archivesDir, { recursive: true });
    } catch (err) {
      // Directory already exists
    }

    const files = await fs.readdir(archivesDir);
    const archives = [];

    for (const file of files) {
      if (file.endsWith('.json')) {
        try {
          const filePath = path.join(archivesDir, file);
          const content = await fs.readFile(filePath, 'utf8');
          const archiveData = JSON.parse(content);
          const stats = await fs.stat(filePath);

          archives.push({
            archiveName: file,
            filename: archiveData.filename,
            timestamp: archiveData.timestamp,
            metadata: archiveData.metadata,
            size: stats.size
          });
        } catch (err) {
          console.warn(`Failed to load archive ${file}:`, err);
        }
      }
    }

    return { success: true, archives };
  } catch (error) {
    console.error('Failed to load archives:', error);
    return { success: false, error: error.message, archives: [] };
  }
});

// Load single archive with full data
ipcMain.handle('load-archive', async (event, archiveName) => {
  try {
    const archivesDir = path.join(app.getPath('userData'), 'allocation-buddy-archives');
    const archivePath = path.join(archivesDir, archiveName);

    const content = await fs.readFile(archivePath, 'utf8');
    const archiveData = JSON.parse(content);

    return { success: true, archive: archiveData };
  } catch (error) {
    console.error('Failed to load archive:', error);
    return { success: false, error: error.message };
  }
});

// Delete archive
ipcMain.handle('delete-archive', async (event, archiveName) => {
  try {
    const archivesDir = path.join(app.getPath('userData'), 'allocation-buddy-archives');
    const archivePath = path.join(archivesDir, archiveName);

    await fs.unlink(archivePath);

    return { success: true };
  } catch (error) {
    console.error('Failed to delete archive:', error);
    return { success: false, error: error.message };
  }
});

// Export archives (placeholder - can be enhanced)
ipcMain.handle('export-archives', async (event, archiveNames) => {
  try {
    // For now, just return success
    // Can be enhanced to export to a specific location
    return { success: true };
  } catch (error) {
    return { success: false, error: error.message };
  }
});

// ============================================
// Dictionary operations (for Allocation Buddy)
// ============================================

// Save dictionary to file
ipcMain.handle('save-dictionary', async (event, dictData) => {
  try {
    // Do NOT write into application source. Save dictionaries to the user's data directory.
    const dictDir = path.join(app.getPath('userData'), 'dictionaries');
    await fs.mkdir(dictDir, { recursive: true });

    const dictPath = path.join(dictDir, 'allocation-buddy-dictionaries.json');

    // Write JSON data (portable across packaged apps)
    await fs.writeFile(dictPath, JSON.stringify(dictData, null, 2), 'utf8');

    return { success: true, path: dictPath };
  } catch (error) {
    console.error('Failed to save dictionary:', error);
    return { success: false, error: error.message };
  }
});

// Export dictionary to user-selected location
ipcMain.handle('export-dictionary', async (event, dictData) => {
  try {
    const result = await dialog.showSaveDialog({
      defaultPath: `dictionary-export-${new Date().toISOString().split('T')[0]}.json`,
      filters: [
        { name: 'JSON Files', extensions: ['json'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });

    if (result.canceled) {
      return { canceled: true };
    }

    await fs.writeFile(result.filePath, JSON.stringify(dictData, null, 2), 'utf8');

    return { success: true, filePath: result.filePath };
  } catch (error) {
    console.error('Failed to export dictionary:', error);
    return { success: false, error: error.message };
  }
});

// Import dictionary from user-selected file
ipcMain.handle('import-dictionary', async () => {
  try {
    const result = await dialog.showOpenDialog({
      properties: ['openFile'],
      filters: [
        { name: 'JSON Files', extensions: ['json'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });

    if (result.canceled) {
      return { canceled: true };
    }

    const filePath = result.filePaths[0];
    const content = await fs.readFile(filePath, 'utf8');
    const dictData = JSON.parse(content);

    return { success: true, data: dictData };
  } catch (error) {
    console.error('Failed to import dictionary:', error);
    return { success: false, error: error.message };
  }
});

// Load the user's saved dictionary (if present)
ipcMain.handle('load-user-dictionary', async () => {
  try {
    const dictPath = path.join(app.getPath('userData'), 'dictionaries', 'allocation-buddy-dictionaries.json');
    const content = await fs.readFile(dictPath, 'utf8');
    const data = JSON.parse(content);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err.message };
  }
});

// Theme handlers
ipcMain.handle('get-theme', () => {
  // Return stored theme or default
  return 'light';
});

ipcMain.on('set-theme', (event, theme) => {
  console.log('Theme changed to:', theme);
  // Broadcast theme change to all renderer windows/iframes via IPC instead of using postMessage('*')
  try {
    const allWins = BrowserWindow.getAllWindows();
    allWins.forEach(win => {
      try {
        win.webContents.send('theme-changed', theme);
      } catch (err) {
        console.warn('Failed to send theme-changed to a window:', err.message);
      }
    });
  } catch (err) {
    console.warn('Failed to broadcast theme change:', err.message);
  }
});

// Provide a small helper so renderer can detect development mode
ipcMain.handle('is-dev', () => {
  // app.isPackaged is false during local development
  return !app.isPackaged;
});

// Handle module navigation
ipcMain.on('navigate-to-module', (event, moduleName) => {
  console.log('Navigating to module:', moduleName);
});

ipcMain.on('return-to-launcher', () => {
  console.log('Returning to launcher');
});

// Log startup
console.log('Business Tools Suite starting...');
console.log('Version:', app.getVersion());
console.log('Electron:', process.versions.electron);
console.log('Node:', process.versions.node);
console.log('Chrome:', process.versions.chrome);
