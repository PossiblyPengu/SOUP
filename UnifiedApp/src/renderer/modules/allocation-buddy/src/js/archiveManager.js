// Archive Manager Module
// Handles saving, loading, and managing allocation archives

class ArchiveManager {
  constructor() {
    this.archivesLoaded = false;
    this.archives = [];
    this.retentionDays = this.loadRetentionSettings();
  }

  /**
   * Load retention settings from localStorage
   */
  loadRetentionSettings() {
    const saved = localStorage.getItem('archiveRetentionDays');
    return saved ? parseInt(saved) : 90; // Default 90 days
  }

  /**
   * Save retention settings
   */
  saveRetentionSettings(days) {
    this.retentionDays = days;
    localStorage.setItem('archiveRetentionDays', days.toString());
  }

  /**
   * Save current allocation data to archive
   */
  async saveArchive(data, filename) {
    try {
      const timestamp = new Date().toISOString();
      const archiveName = this.generateArchiveName(filename);

      const archiveData = {
        timestamp,
        filename,
        archiveName,
        data: {
          organizedData: data.organizedData,
          excludedStores: Array.from(data.excludedStores || []),
          redistributedItems: Array.from(data.redistributedItems || [])
        },
        metadata: {
          totalStores: Object.keys(data.organizedData.byStore || {}).length,
          totalItems: Object.keys(data.organizedData.byItem || {}).length,
          totalQuantity: this.calculateTotalQuantity(data.organizedData)
        }
      };

      // Save to file system via IPC
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const result = await electronAPI.saveArchive(archiveName, archiveData);

      if (result.success) {
        console.log('Archive saved:', archiveName);
        return { success: true, archiveName };
      } else {
        throw new Error(result.error);
      }
    } catch (error) {
      console.error('Failed to save archive:', error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Generate archive filename with timestamp
   */
  generateArchiveName(originalFilename) {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');

    const baseName = originalFilename ? originalFilename.replace(/\.[^/.]+$/, '') : 'allocation';
    return `${year}-${month}-${day}_${hours}${minutes}${seconds}_${baseName}.json`;
  }

  /**
   * Calculate total quantity from organized data
   */
  calculateTotalQuantity(organizedData) {
    let total = 0;
    if (organizedData && organizedData.byStore) {
      Object.values(organizedData.byStore).forEach(items => {
        items.forEach(item => {
          total += item.quantity || 0;
        });
      });
    }
    return total;
  }

  /**
   * Load all archives from file system
   */
  async loadArchives() {
    try {
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const result = await electronAPI.loadArchives();

      if (result.success) {
        this.archives = result.archives.sort((a, b) =>
          new Date(b.timestamp) - new Date(a.timestamp)
        );
        this.archivesLoaded = true;
        return this.archives;
      } else {
        throw new Error(result.error);
      }
    } catch (error) {
      console.error('Failed to load archives:', error);
      return [];
    }
  }

  /**
   * Load a specific archive by name
   */
  async loadArchive(archiveName) {
    try {
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const result = await electronAPI.loadArchive(archiveName);

      if (result.success) {
        return result.archive;
      } else {
        throw new Error(result.error);
      }
    } catch (error) {
      console.error('Failed to load archive:', error);
      return null;
    }
  }

  /**
   * Delete an archive
   */
  async deleteArchive(archiveName) {
    try {
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const result = await electronAPI.deleteArchive(archiveName);

      if (result.success) {
        // Remove from local cache
        this.archives = this.archives.filter(a => a.archiveName !== archiveName);
        return { success: true };
      } else {
        throw new Error(result.error);
      }
    } catch (error) {
      console.error('Failed to delete archive:', error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Clean up old archives based on retention policy
   */
  async cleanupOldArchives() {
    try {
      const cutoffDate = new Date();
      cutoffDate.setDate(cutoffDate.getDate() - this.retentionDays);

      const archivesToDelete = this.archives.filter(archive => {
        const archiveDate = new Date(archive.timestamp);
        return archiveDate < cutoffDate;
      });

      let deletedCount = 0;
      for (const archive of archivesToDelete) {
        const result = await this.deleteArchive(archive.archiveName);
        if (result.success) {
          deletedCount++;
        }
      }

      console.log(`Cleaned up ${deletedCount} old archives`);
      return { success: true, deletedCount };
    } catch (error) {
      console.error('Failed to cleanup archives:', error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Search archives by criteria
   */
  searchArchives(searchTerm) {
    if (!searchTerm) return this.archives;

    const term = searchTerm.toLowerCase();
    return this.archives.filter(archive => {
      return (
        archive.filename.toLowerCase().includes(term) ||
        archive.archiveName.toLowerCase().includes(term) ||
        new Date(archive.timestamp).toLocaleDateString().includes(term)
      );
    });
  }

  /**
   * Export archives to Excel
   */
  async exportArchivesToExcel(archiveNames) {
    try {
      const electronAPI = window.parent?.electronAPI || window.electronAPI;
      const result = await electronAPI.exportArchives(archiveNames);
      return result;
    } catch (error) {
      console.error('Failed to export archives:', error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Get archives grouped by month
   */
  getArchivesByMonth() {
    const grouped = {};

    this.archives.forEach(archive => {
      const date = new Date(archive.timestamp);
      const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;

      if (!grouped[monthKey]) {
        grouped[monthKey] = [];
      }
      grouped[monthKey].push(archive);
    });

    return grouped;
  }

  /**
   * Get statistics about archives
   */
  getArchiveStats() {
    if (this.archives.length === 0) {
      return {
        total: 0,
        oldestDate: null,
        newestDate: null,
        totalSize: 0
      };
    }

    const dates = this.archives.map(a => new Date(a.timestamp));

    return {
      total: this.archives.length,
      oldestDate: new Date(Math.min(...dates)),
      newestDate: new Date(Math.max(...dates)),
      monthsSpanned: this.getUniqueMonths().length
    };
  }

  /**
   * Get unique months that have archives
   */
  getUniqueMonths() {
    const months = new Set();
    this.archives.forEach(archive => {
      const date = new Date(archive.timestamp);
      months.add(`${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`);
    });
    return Array.from(months).sort();
  }
}

// Export singleton instance
window.archiveManager = window.archiveManager || new ArchiveManager();
