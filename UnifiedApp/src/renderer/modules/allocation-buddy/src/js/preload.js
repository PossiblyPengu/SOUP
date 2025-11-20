/**
 * Preload Script
 * Securely exposes limited Node.js APIs to the renderer process via contextBridge
 * This ensures proper isolation between the main and renderer processes
 */

const { contextBridge, ipcRenderer } = require('electron');
const fs = require('fs');
const path = require('path');
const XLSX = require('xlsx');
const Papa = require('papaparse');

/**
 * Expose secure API to renderer process
 */
contextBridge.exposeInMainWorld('electronAPI', {
  /**
   * Open file selection dialog
   * @returns {Promise<string|null>} Selected file path or null if canceled
   */
  selectFile: () => ipcRenderer.invoke('select-file'),

  /**
   * Read file contents securely
   * @param {string} filePath - Absolute path to file
   * @returns {Promise<{success: boolean, data?: any, error?: string}>}
   */
  readFile: async (filePath) => {
    try {
      // Validate file path
      if (!filePath || typeof filePath !== 'string') {
        throw new Error('Invalid file path');
      }

      // Check file exists
      if (!fs.existsSync(filePath)) {
        throw new Error('File does not exist');
      }

      // Get file extension
      const ext = path.extname(filePath).toLowerCase();

      // Validate file extension
      const allowedExtensions = ['.csv', '.xlsx', '.xls'];
      if (!allowedExtensions.includes(ext)) {
        throw new Error('Unsupported file format');
      }

      // Check file size (max 50MB)
      const stats = fs.statSync(filePath);
      if (stats.size > 50 * 1024 * 1024) {
        throw new Error('File too large (max 50MB)');
      }

      // Read file based on extension
      if (ext === '.csv') {
        const content = fs.readFileSync(filePath, 'utf8');
        return { success: true, data: content, type: 'csv' };
      } else {
        // For Excel files, return the path for XLSX library to handle
        return { success: true, data: filePath, type: 'excel' };
      }
    } catch (error) {
      return { success: false, error: error.message };
    }
  },

  /**
   * Get file stats
   * @param {string} filePath - Absolute path to file
   * @returns {Promise<{success: boolean, stats?: object, error?: string}>}
   */
  getFileStats: async (filePath) => {
    try {
      if (!fs.existsSync(filePath)) {
        throw new Error('File does not exist');
      }
      const stats = fs.statSync(filePath);
      return {
        success: true,
        stats: {
          size: stats.size,
          modified: stats.mtime,
          created: stats.birthtime
        }
      };
    } catch (error) {
      return { success: false, error: error.message };
    }
  },

  /**
   * Get application paths
   * @returns {object} Object containing userData and temp paths
   */
  getPaths: () => {
    return {
      userData: ipcRenderer.sendSync('get-user-data-path'),
      temp: ipcRenderer.sendSync('get-temp-path')
    };
  },

  /**
   * Archive Management API
   */
  saveArchive: (archiveName, archiveData) => ipcRenderer.invoke('save-archive', archiveName, archiveData),
  loadArchives: () => ipcRenderer.invoke('load-archives'),
  loadArchive: (archiveName) => ipcRenderer.invoke('load-archive', archiveName),
  deleteArchive: (archiveName) => ipcRenderer.invoke('delete-archive', archiveName),
  exportArchives: (archiveNames) => ipcRenderer.invoke('export-archives', archiveNames),
  getArchivesPath: () => ipcRenderer.invoke('get-archives-path'),

  /**
   * Dictionary Management API
   */
  saveDictionary: (dictionaryData) => ipcRenderer.invoke('save-dictionary', dictionaryData),
  exportDictionary: (dictionaryData) => ipcRenderer.invoke('export-dictionary', dictionaryData),
  importDictionary: () => ipcRenderer.invoke('import-dictionary')
});

// Expose XLSX and Papa libraries to renderer process
contextBridge.exposeInMainWorld('XLSX', XLSX);
contextBridge.exposeInMainWorld('Papa', Papa);
