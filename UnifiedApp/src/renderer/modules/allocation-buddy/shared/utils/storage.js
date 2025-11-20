/**
 * Storage Manager with Backup and Recovery
 */

const { safeJsonParse, safeJsonStringify } = require('./sanitize');

class StorageManager {
  /**
   * Create storage manager
   * @param {string} storageKey - Main storage key
   * @param {number} version - Data version for migrations
   */
  constructor(storageKey, version = 1) {
    this.storageKey = storageKey;
    this.version = version;
    this.backupKey = `${storageKey}_backup`;
    this.versionKey = `${storageKey}_version`;
  }

  /**
   * Save data to localStorage with backup
   * @param {any} data - Data to save
   * @returns {boolean} Success status
   */
  save(data) {
    try {
      // Create backup before saving
      const existing = localStorage.getItem(this.storageKey);
      if (existing) {
        localStorage.setItem(this.backupKey, existing);
      }

      const payload = {
        version: this.version,
        timestamp: Date.now(),
        data: data
      };

      localStorage.setItem(this.storageKey, safeJsonStringify(payload));
      localStorage.setItem(this.versionKey, String(this.version));
      return true;
    } catch (error) {
      console.error('StorageManager: Save failed:', error);
      return false;
    }
  }

  /**
   * Load data from localStorage
   * @returns {any|null} Loaded data or null
   */
  load() {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) return null;

      const payload = safeJsonParse(raw);
      if (!payload) return this.restoreBackup();

      // Version migration
      if (payload.version < this.version) {
        return this.migrate(payload);
      }

      return payload.data;
    } catch (error) {
      console.error('StorageManager: Load failed, attempting backup restore:', error);
      return this.restoreBackup();
    }
  }

  /**
   * Restore from backup
   * @returns {any|null} Backup data or null
   */
  restoreBackup() {
    try {
      const backup = localStorage.getItem(this.backupKey);
      if (backup) {
        const payload = safeJsonParse(backup);
        if (payload && payload.data) {
          console.warn('StorageManager: Restored from backup');
          return payload.data;
        }
      }
    } catch (error) {
      console.error('StorageManager: Backup restore failed:', error);
    }
    return null;
  }

  /**
   * Migrate data from old version
   * @param {Object} oldPayload - Old data payload
   * @returns {any} Migrated data
   */
  migrate(oldPayload) {
    console.log(`StorageManager: Migrating from v${oldPayload.version} to v${this.version}`);
    // Override this method in subclasses for custom migrations
    return oldPayload.data;
  }

  /**
   * Clear all data
   * @param {boolean} includeBackup - Also clear backup
   */
  clear(includeBackup = false) {
    localStorage.removeItem(this.storageKey);
    localStorage.removeItem(this.versionKey);
    if (includeBackup) {
      localStorage.removeItem(this.backupKey);
    }
  }

  /**
   * Check if data exists
   * @returns {boolean} Has data
   */
  hasData() {
    return localStorage.getItem(this.storageKey) !== null;
  }

  /**
   * Get data size in bytes
   * @returns {number} Size in bytes
   */
  getSize() {
    const data = localStorage.getItem(this.storageKey);
    return data ? new Blob([data]).size : 0;
  }

  /**
   * Export data as downloadable file
   * @param {string} filename - Export filename
   */
  exportToFile(filename = 'export.json') {
    const data = this.load();
    if (!data) {
      console.warn('StorageManager: No data to export');
      return;
    }

    const blob = new Blob([safeJsonStringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  /**
   * Import data from file
   * @param {File} file - File to import
   * @returns {Promise<boolean>} Success status
   */
  async importFromFile(file) {
    try {
      const text = await file.text();
      const data = safeJsonParse(text);
      if (data) {
        return this.save(data);
      }
      return false;
    } catch (error) {
      console.error('StorageManager: Import failed:', error);
      return false;
    }
  }
}

/**
 * Session storage wrapper
 */
class SessionStorageManager extends StorageManager {
  save(data) {
    try {
      const payload = {
        version: this.version,
        timestamp: Date.now(),
        data: data
      };
      sessionStorage.setItem(this.storageKey, safeJsonStringify(payload));
      return true;
    } catch (error) {
      console.error('SessionStorageManager: Save failed:', error);
      return false;
    }
  }

  load() {
    try {
      const raw = sessionStorage.getItem(this.storageKey);
      if (!raw) return null;

      const payload = safeJsonParse(raw);
      return payload ? payload.data : null;
    } catch (error) {
      console.error('SessionStorageManager: Load failed:', error);
      return null;
    }
  }

  clear() {
    sessionStorage.removeItem(this.storageKey);
  }

  hasData() {
    return sessionStorage.getItem(this.storageKey) !== null;
  }
}

module.exports = {
  StorageManager,
  SessionStorageManager
};
