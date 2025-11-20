// ===================================
// Business Tools Suite - Preload Script
// Secure bridge between main and renderer
// ===================================

const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods to renderer
contextBridge.exposeInMainWorld('electronAPI', {
  // App info
  getAppVersion: () => ipcRenderer.invoke('get-app-version'),
  getAppPath: (name) => ipcRenderer.invoke('get-app-path', name),

  // Window controls
  windowMinimize: () => ipcRenderer.invoke('window-minimize'),
  windowMaximize: () => ipcRenderer.invoke('window-maximize'),
  windowClose: () => ipcRenderer.invoke('window-close'),
  windowIsMaximized: () => ipcRenderer.invoke('window-is-maximized'),

  // Module navigation
  navigateToModule: (moduleName) => ipcRenderer.send('navigate-to-module', moduleName),
  returnToLauncher: () => ipcRenderer.send('return-to-launcher'),

  // File system operations (for module use)
  readFile: (filePath) => ipcRenderer.invoke('read-file', filePath),
  writeFile: (filePath, data) => ipcRenderer.invoke('write-file', filePath, data),
  selectFile: (options) => ipcRenderer.invoke('select-file', options),
  saveFile: (options) => ipcRenderer.invoke('save-file', options),
  getFileStats: (filePath) => ipcRenderer.invoke('get-file-stats', filePath),

  // Archive operations (for Allocation Buddy)
  saveArchive: (archiveName, archiveData) => ipcRenderer.invoke('save-archive', archiveName, archiveData),
  loadArchives: () => ipcRenderer.invoke('load-archives'),
  loadArchive: (archiveName) => ipcRenderer.invoke('load-archive', archiveName),
  deleteArchive: (archiveName) => ipcRenderer.invoke('delete-archive', archiveName),
  exportArchives: (archiveNames) => ipcRenderer.invoke('export-archives', archiveNames),

  // Dictionary operations (for Allocation Buddy)
  saveDictionary: (dictData) => ipcRenderer.invoke('save-dictionary', dictData),
  exportDictionary: (dictData) => ipcRenderer.invoke('export-dictionary', dictData),
  importDictionary: () => ipcRenderer.invoke('import-dictionary'),
  loadUserDictionary: () => ipcRenderer.invoke('load-user-dictionary'),

  // Theme
  getTheme: () => ipcRenderer.invoke('get-theme'),
  setTheme: (theme) => ipcRenderer.send('set-theme', theme),

  // Dev detection
  isDev: () => ipcRenderer.invoke('is-dev'),

  // Listeners
  onThemeChange: (callback) => ipcRenderer.on('theme-changed', callback)
});

// Log preload initialization
console.log('Preload script initialized - electronAPI exposed');
