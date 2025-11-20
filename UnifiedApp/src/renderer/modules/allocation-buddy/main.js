const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');

// Disable GPU cache to avoid cache errors
app.commandLine.appendSwitch('disable-gpu-shader-disk-cache');
app.commandLine.appendSwitch('disable-http-cache');

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    webPreferences: {
      preload: path.join(__dirname, 'src/js/preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: false,  // Disabled for better library compatibility
      enableRemoteModule: false,
      allowRunningInsecureContent: false
    }
    // icon: path.join(__dirname, 'assets/icon.png')
  });

  mainWindow.loadFile('index.html');

  // Open DevTools in development mode
  if (process.argv.includes('--dev')) {
    mainWindow.webContents.openDevTools();
  }

  mainWindow.on('closed', function () {
    mainWindow = null;
  });
}

app.whenReady().then(createWindow);

app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', function () {
  if (mainWindow === null) {
    createWindow();
  }
});

// Handle file selection dialog
ipcMain.handle('select-file', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile'],
    filters: [
      { name: 'Spreadsheets', extensions: ['xlsx', 'xls', 'csv'] },
      { name: 'Excel Files', extensions: ['xlsx', 'xls'] },
      { name: 'CSV Files', extensions: ['csv'] },
      { name: 'All Files', extensions: ['*'] }
    ]
  });

  if (!result.canceled && result.filePaths.length > 0) {
    return result.filePaths[0];
  }
  return null;
});

// Handle path requests (sync)
ipcMain.on('get-user-data-path', (event) => {
  event.returnValue = app.getPath('userData');
});

ipcMain.on('get-temp-path', (event) => {
  event.returnValue = app.getPath('temp');
});

// Archive Management Handlers
const fs = require('fs');
const archivePath = path.join(app.getPath('userData'), 'archives');

// Ensure archives directory exists
if (!fs.existsSync(archivePath)) {
  fs.mkdirSync(archivePath, { recursive: true });
}

// Save archive
ipcMain.handle('save-archive', async (event, archiveName, archiveData) => {
  try {
    const filePath = path.join(archivePath, archiveName);
    fs.writeFileSync(filePath, JSON.stringify(archiveData, null, 2), 'utf8');
    return { success: true, path: filePath };
  } catch (error) {
    console.error('Error saving archive:', error);
    return { success: false, error: error.message };
  }
});

// Load all archives (metadata only)
ipcMain.handle('load-archives', async () => {
  try {
    const files = fs.readdirSync(archivePath);
    const archives = [];

    for (const file of files) {
      if (file.endsWith('.json')) {
        const filePath = path.join(archivePath, file);
        const stats = fs.statSync(filePath);
        const content = fs.readFileSync(filePath, 'utf8');
        const data = JSON.parse(content);

        archives.push({
          archiveName: file,
          filename: data.filename,
          timestamp: data.timestamp,
          metadata: data.metadata,
          size: stats.size
        });
      }
    }

    return { success: true, archives };
  } catch (error) {
    console.error('Error loading archives:', error);
    return { success: false, error: error.message, archives: [] };
  }
});

// Load specific archive
ipcMain.handle('load-archive', async (event, archiveName) => {
  try {
    const filePath = path.join(archivePath, archiveName);
    const content = fs.readFileSync(filePath, 'utf8');
    const archive = JSON.parse(content);
    return { success: true, archive };
  } catch (error) {
    console.error('Error loading archive:', error);
    return { success: false, error: error.message };
  }
});

// Delete archive
ipcMain.handle('delete-archive', async (event, archiveName) => {
  try {
    const filePath = path.join(archivePath, archiveName);
    fs.unlinkSync(filePath);
    return { success: true };
  } catch (error) {
    console.error('Error deleting archive:', error);
    return { success: false, error: error.message };
  }
});

// Export archives to Excel
ipcMain.handle('export-archives', async (event, archiveNames) => {
  try {
    // This will be implemented to create an Excel file with multiple sheets
    // For now, return success
    return { success: true, message: 'Export functionality will be implemented' };
  } catch (error) {
    console.error('Error exporting archives:', error);
    return { success: false, error: error.message };
  }
});

// Get archives directory path
ipcMain.handle('get-archives-path', async () => {
  return { success: true, path: archivePath };
});

// Save dictionary to file
ipcMain.handle('save-dictionary', async (event, dictionaryData) => {
  try {
    const dictPath = path.join(__dirname, 'src', 'js', 'dictionaries.js');
    
    // Create backup first
    const backupPath = path.join(__dirname, 'src', 'js', `dictionaries.backup.${Date.now()}.js`);
    if (fs.existsSync(dictPath)) {
      fs.copyFileSync(dictPath, backupPath);
    }
    
    // Format the dictionary content
    const content = `// Auto-generated dictionary file
// Last updated: ${new Date().toISOString()}

window.DICT = ${JSON.stringify(dictionaryData, null, 2)};
`;
    
    // Write to file
    fs.writeFileSync(dictPath, content, 'utf8');
    
    return { success: true, message: 'Dictionary saved successfully' };
  } catch (error) {
    console.error('Failed to save dictionary:', error);
    return { success: false, error: error.message };
  }
});

// Export dictionary to user-selected location
ipcMain.handle('export-dictionary', async (event, dictionaryData) => {
  try {
    const { dialog } = require('electron');
    const { filePath } = await dialog.showSaveDialog({
      title: 'Export Dictionary',
      defaultPath: `dictionary-export-${new Date().toISOString().split('T')[0]}.json`,
      filters: [
        { name: 'JSON Files', extensions: ['json'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });
    
    if (!filePath) {
      return { success: false, canceled: true };
    }
    
    fs.writeFileSync(filePath, JSON.stringify(dictionaryData, null, 2), 'utf8');
    
    return { success: true, filePath };
  } catch (error) {
    console.error('Failed to export dictionary:', error);
    return { success: false, error: error.message };
  }
});

// Import dictionary from user-selected file
ipcMain.handle('import-dictionary', async () => {
  try {
    const { dialog } = require('electron');
    const { filePaths } = await dialog.showOpenDialog({
      title: 'Import Dictionary',
      filters: [
        { name: 'JSON Files', extensions: ['json'] },
        { name: 'All Files', extensions: ['*'] }
      ],
      properties: ['openFile']
    });
    
    if (!filePaths || filePaths.length === 0) {
      return { success: false, canceled: true };
    }
    
    const content = fs.readFileSync(filePaths[0], 'utf8');
    const data = JSON.parse(content);
    
    return { success: true, data };
  } catch (error) {
    console.error('Failed to import dictionary:', error);
    return { success: false, error: error.message };
  }
});
