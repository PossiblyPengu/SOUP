// Global variables
let reportData = [];
let allData = [];
let masterList = [];
let thresholds = {};
let dataLoaded = false;
let currentSortColumn = null;
let currentSortDirection = 'asc';
let currentPage = 1;
const itemsPerPage = 100;

// LocalStorage keys
const STORAGE_KEYS = {
    MASTER_LIST: 'binContents_masterList',
    THRESHOLDS: 'binContents_thresholds',
    PREFERENCES: 'binContents_preferences'
};

// Default preferences
const DEFAULT_PREFERENCES = {
    itemsPerPage: 100,
    defaultSort: 'status',
    sortDirection: 'asc',
    rememberFilters: false,
    autoExpandFilters: false,
    exportFormat: 'excel',
    includeTimestamp: true
};

// User preferences
let userPreferences = { ...DEFAULT_PREFERENCES };

// DOM Elements - will be initialized after DOM loads
let welcomeScreen, dataView, filePathDisplay, clearBtn, status;
let reportSection, reportBody, statusFilter, searchInput;
let exportExcelBtn, exportCsvBtn;
let totalItems, noInventory, lowInventory, sufficient;

// Load master list and thresholds on page load
window.addEventListener('DOMContentLoaded', () => {
    // Initialize DOM element references
    welcomeScreen = document.getElementById('welcomeScreen');
    dataView = document.getElementById('dataView');
    filePathDisplay = document.getElementById('filePathDisplay');
    clearBtn = document.getElementById('clearBtn');
    status = document.getElementById('status');
    reportSection = document.getElementById('reportSection');
    reportBody = document.getElementById('reportBody');
    statusFilter = document.getElementById('statusFilter');
    searchInput = document.getElementById('searchInput');
    exportExcelBtn = document.getElementById('exportExcelBtn');
    exportCsvBtn = document.getElementById('exportCsvBtn');
    totalItems = document.getElementById('totalItems');
    noInventory = document.getElementById('noInventory');
    lowInventory = document.getElementById('lowInventory');
    sufficient = document.getElementById('sufficient');

    loadPreferences();
    loadMasterData();

    // Setup event listeners after DOM is loaded
    const selectFileWelcomeBtnEl = document.getElementById('selectFileWelcomeBtn');
    const selectFileBtnEl = document.getElementById('selectFileBtn');

    console.log('EB: Setting up event listeners');
    console.log('EB: selectFileWelcomeBtn:', selectFileWelcomeBtnEl);
    console.log('EB: selectFileBtn:', selectFileBtnEl);

    if (selectFileWelcomeBtnEl) {
        selectFileWelcomeBtnEl.addEventListener('click', () => {
            console.log('EB: Welcome button clicked!');
            selectFile();
        });
        console.log('EB: Welcome button listener attached');
    } else {
        console.error('EB: selectFileWelcomeBtn element not found!');
    }

    if (selectFileBtnEl) {
        selectFileBtnEl.addEventListener('click', () => {
            console.log('EB: Data view button clicked!');
            selectFile();
        });
        console.log('EB: Data view button listener attached');
    } else {
        console.error('EB: selectFileBtn element not found!');
    }
    if (clearBtn) {
        clearBtn.addEventListener('click', clearAll);
    }
    if (statusFilter) {
        statusFilter.addEventListener('change', applyFilters);
    }
    if (searchInput) {
        let searchTimeout;
        searchInput.addEventListener('input', () => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(applyFilters, 300);
        });
    }
    if (exportExcelBtn) {
        exportExcelBtn.addEventListener('click', exportToExcel);
    }
    if (exportCsvBtn) {
        exportCsvBtn.addEventListener('click', exportToCSV);
    }
});

// Keyboard shortcuts
document.addEventListener('keydown', (e) => {
    // Ctrl+F: Focus search input
    if (e.ctrlKey && e.key === 'f') {
        e.preventDefault();
        const searchInputEl = document.getElementById('searchInput');
        if (searchInputEl) {
            searchInputEl.focus();
            searchInputEl.select();
        }
    }

    // Ctrl+E: Export to Excel (if data available)
    if (e.ctrlKey && e.key === 'e') {
        e.preventDefault();
        if (reportData.length > 0) {
            exportToExcel();
        }
    }

    // Ctrl+S: Open settings
    if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        const settingsBtn = document.getElementById('settingsBtn');
        if (settingsBtn) settingsBtn.click();
    }

    // Escape: Close modal if open
    if (e.key === 'Escape') {
        const settingsModal = document.getElementById('settingsModal');
        if (settingsModal && settingsModal.style.display === 'flex') {
            settingsModal.style.display = 'none';
        }
    }
});

// Load master list and thresholds from localStorage or JSON files
async function loadMasterData() {
    console.log('EB: loadMasterData() called');
    showToast('Loading master data and thresholds...', 'info', 'System Startup');

    try {
        // Try to load from localStorage first
        const storedMasterList = localStorage.getItem(STORAGE_KEYS.MASTER_LIST);
        const storedThresholds = localStorage.getItem(STORAGE_KEYS.THRESHOLDS);
        console.log('EB: localStorage check -', {
            hasMasterList: !!storedMasterList,
            hasThresholds: !!storedThresholds,
            masterListLength: storedMasterList?.length,
            thresholdsLength: storedThresholds?.length
        });

        if (storedMasterList && storedThresholds) {
            // Validate and load from localStorage
            try {
                console.log('EB: Parsing localStorage data...');
                const parsedMasterList = JSON.parse(storedMasterList);
                const parsedThresholds = JSON.parse(storedThresholds);

                // Validate data integrity
                if (!Array.isArray(parsedMasterList)) {
                    throw new Error('Master list is not an array');
                }
                if (typeof parsedThresholds !== 'object') {
                    throw new Error('Thresholds is not an object');
                }

                masterList = parsedMasterList;
                thresholds = parsedThresholds;
                dataLoaded = true;
                console.log('EB: Data loaded from localStorage successfully!', {
                    masterListCount: masterList.length,
                    thresholdsCount: Object.keys(thresholds).length,
                    dataLoaded: dataLoaded
                });
                showToast(
                    `Successfully loaded ${masterList.length} items and ${Object.keys(thresholds).length} thresholds from local storage.`,
                    'success',
                    'Master Data Ready'
                );
                console.log(`Loaded ${masterList.length} items and ${Object.keys(thresholds).length} thresholds from localStorage`);
                return;
            } catch (parseError) {
                console.warn('EB: localStorage data corrupted, falling back to JSON files:', parseError);
                showToast('Local data corrupted. Loading from backup files...', 'warning', 'Data Recovery');
            }
        } else {
            console.log('EB: No data in localStorage, fetching from JSON files...');
        }

        // Fall back to JSON files
        console.log('EB: Fetching data/masterlist.json...');
        const masterResponse = await fetch('data/masterlist.json');
        console.log('EB: Master list response:', { ok: masterResponse.ok, status: masterResponse.status });
        if (!masterResponse.ok) throw new Error('Failed to load master list');
        masterList = await masterResponse.json();
        console.log('EB: Master list parsed, items:', masterList.length);

        console.log('EB: Fetching data/thresholds.json...');
        const thresholdResponse = await fetch('data/thresholds.json');
        console.log('EB: Thresholds response:', { ok: thresholdResponse.ok, status: thresholdResponse.status });
        if (!thresholdResponse.ok) throw new Error('Failed to load thresholds');
        thresholds = await thresholdResponse.json();
        console.log('EB: Thresholds parsed, count:', Object.keys(thresholds).length);

        // Save to localStorage for future use
        console.log('EB: Saving to localStorage...');
        localStorage.setItem(STORAGE_KEYS.MASTER_LIST, JSON.stringify(masterList));
        localStorage.setItem(STORAGE_KEYS.THRESHOLDS, JSON.stringify(thresholds));

        dataLoaded = true;
        console.log('EB: Data loaded from JSON files successfully!', {
            masterListCount: masterList.length,
            thresholdsCount: Object.keys(thresholds).length,
            dataLoaded: dataLoaded
        });
        showToast(
            `Successfully loaded ${masterList.length} items and ${Object.keys(thresholds).length} thresholds from JSON files.`,
            'success',
            'Master Data Imported'
        );
        console.log(`Loaded ${masterList.length} items and ${Object.keys(thresholds).length} thresholds from JSON files`);

    } catch (error) {
        console.error('EB: Error loading master data:', error);
        console.error('EB: dataLoaded is still:', dataLoaded);
        showToast(
            'Could not load master data. Please upload your master list and thresholds via Settings.',
            'error',
            'Loading Failed'
        );
    }
}

// Handle file selection
// Handle file selection
// Select file using Electron dialog
async function selectFile() {
    console.log('EB: selectFile() function called');

    try {
        // Access electronAPI from parent window (since module runs in iframe)
        const electronAPI = window.parent?.electronAPI || window.electronAPI;
        console.log('EB: electronAPI:', electronAPI);

        if (electronAPI && electronAPI.selectFile) {
            console.log('EB: Calling electronAPI.selectFile...');
            const filePath = await electronAPI.selectFile({
                filters: [
                    { name: 'Excel Files', extensions: ['xlsx', 'xls'] },
                    { name: 'All Files', extensions: ['*'] }
                ]
            });

            if (filePath) {
                console.log('EB: File selected:', filePath);
                console.log('EB: filePathDisplay:', filePathDisplay);
                console.log('EB: dataLoaded:', dataLoaded);

                if (filePathDisplay) {
                    filePathDisplay.value = filePath;
                }
                showStatus(`File selected: ${filePath.split(/[\\/]/).pop()}`, 'info');

                // Load and process the file
                if (dataLoaded) {
                    console.log('EB: Calling loadAndProcessFile...');
                    await loadAndProcessFile(filePath);
                } else {
                    console.error('EB: Master data not loaded yet!');
                    showStatus('Error: Master data not loaded yet. Please wait...', 'error');
                }
            } else {
                console.log('EB: No file selected (user cancelled)');
            }
        } else {
            showStatus('File selection not available', 'error');
        }
    } catch (error) {
        console.error('Error selecting file:', error);
        showStatus(`Error: ${error.message}`, 'error');
    }
}

// Load and process file
async function loadAndProcessFile(filePath) {
    console.log('EB: loadAndProcessFile called with:', filePath);

    try {
        showStatus('Processing bin contents...', 'info');
        console.log('EB: Calling showLoading...');
        showLoading('Processing bin contents...', 'Reading Excel file and analyzing data');

        // Access electronAPI from parent window (since module runs in iframe)
        const electronAPI = window.parent?.electronAPI || window.electronAPI;
        console.log('EB: electronAPI available:', !!electronAPI);

        // Read file using Electron API
        console.log('EB: Reading file...');
        const fileData = await electronAPI.readFile(filePath);
        console.log('EB: File read, size:', fileData?.byteLength || fileData?.length);

        const data = new Uint8Array(fileData);
        console.log('EB: Created Uint8Array, length:', data.length);

        console.log('EB: Parsing with XLSX...');
        const workbook = XLSX.read(data, { type: 'array' });
        console.log('EB: Workbook parsed, sheets:', workbook.SheetNames);

        // Try to read bin contents from first sheet or look for common sheet names
        let binContents = null;
        const possibleSheetNames = [
            'Sheet1',
            'Bin Contents',
            'BinContents',
            'Inventory',
            'Export',
            workbook.SheetNames[0] // Default to first sheet
        ];

        console.log('EB: Looking for bin contents in sheets:', possibleSheetNames);
        for (const sheetName of possibleSheetNames) {
            console.log('EB: Trying sheet:', sheetName);
            binContents = readSheet(workbook, sheetName);
            if (binContents && binContents.length > 0) {
                console.log(`EB: Found bin contents in sheet: ${sheetName}, rows:`, binContents.length);
                break;
            }
        }

        if (!binContents || binContents.length === 0) {
            console.error('EB: No data found in Excel file');
            hideLoading();
            showStatus('Error: No data found in Excel file', 'error');
            return;
        }

        // Process the data with pre-loaded master list and thresholds
        console.log('EB: Processing data...');
        processData(binContents);
        console.log('EB: Data processed, allData length:', allData.length);

        console.log('EB: Displaying report...');
        displayReport(allData);
        console.log('EB: Report displayed');

        console.log('EB: Updating statistics...');
        updateStatistics();
        console.log('EB: Statistics updated');

        console.log('EB: Showing report section...');
        reportSection.style.display = 'block';

        // Hide welcome screen, show data view
        console.log('EB: Switching views...');
        if (welcomeScreen) {
            welcomeScreen.style.display = 'none';
            console.log('EB: Welcome screen hidden');
        }
        if (dataView) {
            dataView.style.display = 'block';
            console.log('EB: Data view shown');
        }

        console.log('EB: Hiding loading...');
        hideLoading();
        showStatus('Report generated successfully!', 'success');
        showToast(`Successfully processed ${allData.length} items from bin contents.`, 'success', 'Report Generated');
        console.log('EB: loadAndProcessFile completed successfully!');

    } catch (error) {
        console.error('EB: Error in loadAndProcessFile:', error);
        console.error('EB: Error stack:', error.stack);
        hideLoading();
        showStatus(`Error processing file: ${error.message}`, 'error');
        showToast(`Error processing file: ${error.message}`, 'error', 'Processing Failed');
    }
}

// Legacy handler - keep for backward compatibility
function handleFileSelect(event) {
    const file = event.target.files[0];
    if (file) {
        filePathDisplay.value = file.name;
        showStatus(`File selected: ${file.name}`, 'info');
        
        // Auto-generate report on successful file import
        if (dataLoaded) {
            generateReportFromFile(file);
        }
    }
}

// Generate report from File object (fallback)
function generateReportFromFile(file) {
    if (!file) {
        showStatus('Please select a file first', 'error');
        return;
    }

    if (!dataLoaded) {
        showStatus('Error: Master data not loaded yet. Please wait...', 'error');
        return;
    }

    showStatus('Processing bin contents...', 'info');
    showLoading('Processing bin contents...', 'Reading Excel file and analyzing data');

    const reader = new FileReader();
    reader.onload = function(e) {
        try {
            const data = new Uint8Array(e.target.result);
            const workbook = XLSX.read(data, { type: 'array' });

            // Try to read bin contents from first sheet or look for common sheet names
            let binContents = null;
            const possibleSheetNames = [
                'Sheet1',
                'Bin Contents',
                'BinContents',
                'Inventory',
                'Export',
                workbook.SheetNames[0] // Default to first sheet
            ];

            for (const sheetName of possibleSheetNames) {
                binContents = readSheet(workbook, sheetName);
                if (binContents && binContents.length > 0) {
                    console.log(`Found bin contents in sheet: ${sheetName}`);
                    break;
                }
            }

            if (!binContents || binContents.length === 0) {
                showStatus('Error: No data found in Excel file', 'error');
                return;
            }

            // Process the data with pre-loaded master list and thresholds
            processData(binContents);
            displayReport(allData);
            updateStatistics();
            reportSection.style.display = 'block';
            hideLoading();
            showStatus('Report generated successfully!', 'success');
            showToast(`Successfully processed ${allData.length} items from bin contents.`, 'success', 'Report Generated');

        } catch (error) {
            console.error('Error:', error);
            hideLoading();
            showStatus(`Error processing file: ${error.message}`, 'error');
            showToast(`Error processing file: ${error.message}`, 'error', 'Processing Failed');
        }
    };

    reader.onerror = function() {
        hideLoading();
        showStatus('Error reading file', 'error');
        showToast('Error reading file. Please try again.', 'error', 'File Error');
    };

    reader.readAsArrayBuffer(file);
}

// Read sheet from workbook
function readSheet(workbook, sheetName) {
    if (!workbook.SheetNames.includes(sheetName)) {
        console.warn(`Sheet "${sheetName}" not found`);
        return null;
    }

    const worksheet = workbook.Sheets[sheetName];
    const data = XLSX.utils.sheet_to_json(worksheet, { defval: '' });

    // Log the columns found in the first row
    if (data && data.length > 0) {
        console.log('EB: Excel columns found:', Object.keys(data[0]));
        console.log('EB: First row sample:', data[0]);
    }

    return data;
}

// Process data
function processData(binContents) {
    // Build report from bin contents (ONLY 9-90* bins)
    // Sum quantities by item across all 9-90* bins
    const itemMap = new Map();

    console.log('EB: Processing', binContents.length, 'rows from bin contents');

    let processedCount = 0;
    let skippedCount = 0;

    binContents.forEach((row, index) => {
        const itemNo = normalizeItemNo(row['Item No.'] || row['Item No'] || row['ItemNo'] || '');
        const binCode = (row['Bin Code'] || row['BinCode'] || row['Bin'] || 'N/A').toString().trim();
        const qty = parseFloat(
            row['Available Qty. to Take'] ||
            row['Quantity Available to Take'] ||
            row['Qty Available to Take'] ||
            row['Available to Take'] ||
            row['Quantity'] ||
            row['Qty'] ||
            0
        );
        const rowDescription = (row['Description'] || row['Desc'] || row['ItemDescription'] || '').toString().trim();

        // Log first few rows for debugging
        if (index < 3) {
            console.log(`EB: Row ${index}:`, {
                itemNo,
                binCode,
                qty,
                rawQtyField1: row['Available Qty. to Take'],
                rawQtyField2: row['Quantity Available to Take'],
                rawQtyField3: row['Quantity'],
                allFields: Object.keys(row)
            });
        }

        if (!itemNo) {
            skippedCount++;
            return;
        }

        // FILTER: Only include bins starting with 9-90
        if (!binCode.toUpperCase().startsWith('9-90')) {
            skippedCount++;
            return; // Skip this bin
        }

        processedCount++;

        // Accumulate quantities by item
        if (itemMap.has(itemNo)) {
            // Item already exists, add to quantity
            itemMap.get(itemNo).quantity += qty;
        } else {
            // New item, create entry
            const masterItem = masterList.find(m => normalizeItemNo(m.itemNo) === itemNo);
            const description = masterItem ? masterItem.description : rowDescription;
            const threshold = thresholds[itemNo] || 100;

            itemMap.set(itemNo, {
                itemNo: itemNo,
                description: description,
                quantity: qty,
                threshold: threshold
            });
        }
    });

    console.log('EB: Processing complete:', {
        totalRows: binContents.length,
        processedRows: processedCount,
        skippedRows: skippedCount,
        uniqueItems: itemMap.size
    });

    // Convert map to array and determine status for each item
    allData = [];
    itemMap.forEach(item => {
        let status;
        if (item.quantity === 0) {
            status = 'No Inventory';
        } else if (item.quantity < item.threshold) {
            status = 'Low Inventory';
        } else {
            status = 'Sufficient';
        }

        allData.push({
            itemNo: item.itemNo,
            description: item.description,
            quantity: item.quantity,
            threshold: item.threshold,
            status: status
        });
    });

    console.log(`Processed ${allData.length} unique items (9-90* bins only, quantities summed)`);

    // Sort by status priority, then by item number
    const statusPriority = { 'No Inventory': 1, 'Low Inventory': 2, 'Sufficient': 3 };
    allData.sort((a, b) => {
        if (statusPriority[a.status] !== statusPriority[b.status]) {
            return statusPriority[a.status] - statusPriority[b.status];
        }
        return a.itemNo.localeCompare(b.itemNo);
    });

    reportData = [...allData];
}

// Display report in table
function displayReport(data) {
    reportBody.innerHTML = '';

    if (!data || data.length === 0) {
        reportBody.innerHTML = `
            <tr>
                <td colspan="5" style="text-align: center; padding: 40px; color: #999;">
                    No data to display
                </td>
            </tr>
        `;
        document.getElementById('pagination').innerHTML = '';
        return;
    }

    // Calculate pagination
    const totalPages = Math.ceil(data.length / itemsPerPage);
    const start = (currentPage - 1) * itemsPerPage;
    const end = start + itemsPerPage;
    const paginatedData = data.slice(start, end);

    // Display paginated data
    paginatedData.forEach(item => {
        const row = document.createElement('tr');

        // Determine row class and badge class
        let rowClass = '';
        let badgeClass = '';

        if (item.status === 'No Inventory') {
            rowClass = 'row-danger';
            badgeClass = 'badge-danger';
        } else if (item.status === 'Low Inventory') {
            rowClass = 'row-warning';
            badgeClass = 'badge-warning';
        } else {
            rowClass = 'row-success';
            badgeClass = 'badge-success';
        }

        row.className = rowClass;
        row.innerHTML = `
            <td>${escapeHtml(item.itemNo)}</td>
            <td>${escapeHtml(item.description)}</td>
            <td>${item.quantity}</td>
            <td>${item.threshold}</td>
            <td><span class="badge ${badgeClass}">${item.status}</span></td>
        `;

        reportBody.appendChild(row);
    });

    // Render pagination controls
    renderPagination(data.length, totalPages);
}

// Render pagination controls
function renderPagination(totalItems, totalPages) {
    const paginationDiv = document.getElementById('pagination');
    
    if (totalPages <= 1) {
        paginationDiv.innerHTML = '';
        return;
    }

    let html = '';
    
    // Previous button
    html += `<button class="pagination-btn" onclick="goToPage(${currentPage - 1})" ${currentPage === 1 ? 'disabled' : ''}>← Previous</button>`;
    
    // Page numbers
    const maxVisiblePages = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
    let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);
    
    if (endPage - startPage < maxVisiblePages - 1) {
        startPage = Math.max(1, endPage - maxVisiblePages + 1);
    }
    
    if (startPage > 1) {
        html += `<button class="pagination-btn" onclick="goToPage(1)">1</button>`;
        if (startPage > 2) {
            html += `<span class="pagination-info">...</span>`;
        }
    }
    
    for (let i = startPage; i <= endPage; i++) {
        html += `<button class="pagination-btn ${i === currentPage ? 'active' : ''}" onclick="goToPage(${i})">${i}</button>`;
    }
    
    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            html += `<span class="pagination-info">...</span>`;
        }
        html += `<button class="pagination-btn" onclick="goToPage(${totalPages})">${totalPages}</button>`;
    }
    
    // Next button
    html += `<button class="pagination-btn" onclick="goToPage(${currentPage + 1})" ${currentPage === totalPages ? 'disabled' : ''}>Next →</button>`;
    
    // Info text
    const start = (currentPage - 1) * itemsPerPage + 1;
    const end = Math.min(currentPage * itemsPerPage, totalItems);
    html += `<span class="pagination-info">Showing ${start}-${end} of ${totalItems}</span>`;
    
    paginationDiv.innerHTML = html;
}

// Go to specific page
function goToPage(page) {
    currentPage = page;
    applyFilters();
}

// Update statistics
function updateStatistics() {
    const stats = {
        total: 0,
        noInventory: 0,
        lowInventory: 0,
        sufficient: 0
    };

    allData.forEach(item => {
        stats.total++;
        if (item.status === 'No Inventory') {
            stats.noInventory++;
        } else if (item.status === 'Low Inventory') {
            stats.lowInventory++;
        } else if (item.status === 'Sufficient') {
            stats.sufficient++;
        }
    });

    totalItems.textContent = stats.total;
    noInventory.textContent = stats.noInventory;
    lowInventory.textContent = stats.lowInventory;
    sufficient.textContent = stats.sufficient;
}

// Apply filters
function applyFilters() {
    const statusValue = statusFilter.value;
    const searchValue = searchInput.value.toLowerCase().trim();

    // Get range filter values
    const qtyMin = parseFloat(document.getElementById('qtyMin')?.value) || null;
    const qtyMax = parseFloat(document.getElementById('qtyMax')?.value) || null;
    const thresholdMin = parseFloat(document.getElementById('thresholdMin')?.value) || null;
    const thresholdMax = parseFloat(document.getElementById('thresholdMax')?.value) || null;

    let filtered = [...allData];

    // Filter by status
    if (statusValue !== 'all') {
        filtered = filtered.filter(item => item.status === statusValue);
    }

    // Filter by search
    if (searchValue) {
        filtered = filtered.filter(item =>
            item.itemNo.toLowerCase().includes(searchValue) ||
            item.description.toLowerCase().includes(searchValue)
        );
    }

    // Filter by quantity range
    if (qtyMin !== null) {
        filtered = filtered.filter(item => item.quantity >= qtyMin);
    }
    if (qtyMax !== null) {
        filtered = filtered.filter(item => item.quantity <= qtyMax);
    }

    // Filter by threshold range
    if (thresholdMin !== null) {
        filtered = filtered.filter(item => item.threshold >= thresholdMin);
    }
    if (thresholdMax !== null) {
        filtered = filtered.filter(item => item.threshold <= thresholdMax);
    }

    // Apply current sorting
    if (currentSortColumn) {
        filtered = sortData(filtered, currentSortColumn, currentSortDirection);
    }

    currentPage = 1; // Reset to first page when filtering
    displayReport(filtered);
}

// Sort table by column
function sortTable(column) {
    // Toggle direction if same column
    if (currentSortColumn === column) {
        currentSortDirection = currentSortDirection === 'asc' ? 'desc' : 'asc';
    } else {
        currentSortColumn = column;
        currentSortDirection = 'asc';
    }

    // Update header classes
    document.querySelectorAll('th.sortable').forEach(th => {
        th.classList.remove('sort-asc', 'sort-desc');
    });
    
    const headerMap = {
        'itemNo': 0,
        'description': 1,
        'quantity': 2,
        'threshold': 3,
        'status': 4
    };
    
    const headers = document.querySelectorAll('thead tr:first-child th.sortable');
    if (headers[headerMap[column]]) {
        headers[headerMap[column]].classList.add(`sort-${currentSortDirection}`);
    }

    applyFilters();
}

// Sort data array
function sortData(data, column, direction) {
    return [...data].sort((a, b) => {
        let aVal = a[column];
        let bVal = b[column];

        // Special handling for status column - sort by severity
        if (column === 'status') {
            const statusOrder = {
                'No Inventory': 0,
                'Low Inventory': 1,
                'Sufficient': 2
            };
            aVal = statusOrder[aVal] ?? 999;
            bVal = statusOrder[bVal] ?? 999;
        }
        // Handle different data types
        else if (typeof aVal === 'string') {
            aVal = aVal.toLowerCase();
            bVal = bVal.toLowerCase();
        }

        if (aVal < bVal) return direction === 'asc' ? -1 : 1;
        if (aVal > bVal) return direction === 'asc' ? 1 : -1;
        return 0;
    });
}

// Apply filter presets
function applyPreset(preset) {
    const qtyMin = document.getElementById('qtyMin');
    const qtyMax = document.getElementById('qtyMax');
    const thresholdMin = document.getElementById('thresholdMin');
    const thresholdMax = document.getElementById('thresholdMax');
    const statusFilter = document.getElementById('statusFilter');
    const searchInput = document.getElementById('searchInput');

    switch(preset) {
        case 'critical':
            // Items with 0 quantity
            qtyMin.value = '0';
            qtyMax.value = '0';
            thresholdMin.value = '';
            thresholdMax.value = '';
            statusFilter.value = 'all';
            break;
        case 'urgent':
            // Items with quantity less than 5
            qtyMin.value = '';
            qtyMax.value = '4';
            thresholdMin.value = '';
            thresholdMax.value = '';
            statusFilter.value = 'all';
            break;
        case 'reorder':
            // Items below threshold (No Inventory + Low Inventory)
            qtyMin.value = '';
            qtyMax.value = '';
            thresholdMin.value = '';
            thresholdMax.value = '';
            statusFilter.value = 'all';
            searchInput.value = '';
            // Apply both No Inventory and Low Inventory filters
            statusFilter.value = 'Low Inventory';
            break;
        case 'reset':
            // Clear all filters
            qtyMin.value = '';
            qtyMax.value = '';
            thresholdMin.value = '';
            thresholdMax.value = '';
            statusFilter.value = 'all';
            searchInput.value = '';
            break;
    }
    
    applyFilters();
}

// Export to Excel
function exportToExcel() {
    if (!allData || allData.length === 0) {
        showStatus('No data to export', 'error');
        return;
    }

    const exportData = allData.map(item => ({
        'Item No.': item.itemNo,
        'Description': item.description,
        'Quantity': item.quantity,
        'Threshold': item.threshold,
        'Status': item.status
    }));

    const ws = XLSX.utils.json_to_sheet(exportData);

    // Set column widths
    ws['!cols'] = [
        { wch: 20 },  // Item No.
        { wch: 50 },  // Description
        { wch: 12 },  // Quantity
        { wch: 12 },  // Threshold
        { wch: 20 }   // Status
    ];

    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Inventory Report');

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').split('T')[0];
    const filename = `Essentials_Report_${timestamp}.xlsx`;

    // Use Electron file dialog if available
    if (window.electronAPI && window.electronAPI.saveFile) {
        window.electronAPI.saveFile({
            defaultPath: filename,
            filters: [
                { name: 'Excel Files', extensions: ['xlsx'] },
                { name: 'All Files', extensions: ['*'] }
            ]
        }).then(filePath => {
            if (filePath) {
                // Write workbook to buffer
                const wbout = XLSX.write(wb, { type: 'array', bookType: 'xlsx' });
                
                // Save using Electron API
                return window.electronAPI.writeFile(filePath, wbout);
            }
        }).then(() => {
            showStatus(`Exported to ${filename}`, 'success');
        }).catch(error => {
            console.error('Export failed:', error);
            showStatus(`Export failed: ${error.message}`, 'error');
        });
    } else {
        // Fallback for non-Electron environment
        XLSX.writeFile(wb, filename);
        showStatus(`Exported to ${filename}`, 'success');
    }
}

// Export to CSV
function exportToCSV() {
    if (!allData || allData.length === 0) {
        showStatus('No data to export', 'error');
        return;
    }

    const headers = ['Item No.', 'Description', 'Quantity', 'Threshold', 'Status'];
    const csvRows = [headers.join(',')];

    allData.forEach(item => {
        const row = [
            `"${item.itemNo}"`,
            `"${item.description.replace(/"/g, '""')}"`,
            item.quantity,
            item.threshold,
            `"${item.status}"`
        ];
        csvRows.push(row.join(','));
    });

    const csvContent = csvRows.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').split('T')[0];
    const filename = `Essentials_Report_${timestamp}.csv`;

    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    showStatus(`Exported to ${filename}`, 'success');
}

// Clear all data
function clearAll() {
    if (confirm('Are you sure you want to clear all data?')) {
        allData = [];
        reportData = [];
        filePathDisplay.value = '';
        reportSection.style.display = 'none';
        status.className = 'status';
        status.textContent = '';

        // Show welcome screen, hide data view
        if (dataView) dataView.style.display = 'none';
        if (welcomeScreen) welcomeScreen.style.display = 'flex';

        showStatus('All data cleared', 'info');
    }
}

// Utility function to clean strings (deprecated - use normalizeItemNo)
function cleanString(value) {
    if (!value) return '';
    return String(value).trim().toUpperCase();
}

// Centralized item number normalization
function normalizeItemNo(value) {
    if (!value) return '';
    return String(value).trim().toUpperCase();
}

// Loading spinner functions
function showLoading(text = 'Processing...', subtext = 'Please wait') {
    console.log('EB: showLoading called:', text);
    const overlay = document.getElementById('loadingOverlay');
    const loadingText = document.getElementById('loadingText');
    const loadingSubtext = document.getElementById('loadingSubtext');

    if (overlay) {
        if (loadingText) loadingText.textContent = text;
        if (loadingSubtext) loadingSubtext.textContent = subtext;
        overlay.classList.add('show');
        console.log('EB: Loading overlay shown');
    } else {
        console.warn('EB: Loading overlay element not found');
    }
}

function hideLoading() {
    console.log('EB: hideLoading called');
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.classList.remove('show');
        console.log('EB: Loading overlay hidden');
    } else {
        console.warn('EB: Loading overlay element not found');
    }
}

// Utility function to escape HTML
function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return String(text).replace(/[&<>"']/g, m => map[m]);
}

// Show status message
function showStatus(message, type) {
    status.textContent = message;
    status.className = `status ${type}`;

    if (type === 'info' || type === 'success') {
        setTimeout(() => {
            if (status.className.includes(type)) {
                status.className = 'status';
            }
        }, 5000);
    }
}

// Toast notification system
function showToast(message, type = 'info', title = null, duration = 4000) {
    let toastContainer = document.getElementById('toastContainer');

    // Create toast container if it doesn't exist
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = 'toastContainer';
        toastContainer.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 10000; display: flex; flex-direction: column; gap: 10px;';
        document.body.appendChild(toastContainer);
    }

    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    
    // Set title based on type if not provided
    if (!title) {
        switch (type) {
            case 'success': title = 'Success'; break;
            case 'error': title = 'Error'; break;
            case 'warning': title = 'Warning'; break;
            case 'info': title = 'Information'; break;
            default: title = 'Notification'; break;
        }
    }
    
    // Set icon based on type
    let icon = '📢';
    switch (type) {
        case 'success': icon = '✅'; break;
        case 'error': icon = '❌'; break;
        case 'warning': icon = '⚠️'; break;
        case 'info': icon = 'ℹ️'; break;
    }
    
    toast.innerHTML = `
        <div class="toast-header">
            <span class="toast-icon">${icon}</span>
            <span>${title}</span>
            <button class="toast-close" onclick="closeToast(this)">&times;</button>
        </div>
        <div class="toast-body">${message}</div>
        <div class="toast-progress">
            <div class="toast-progress-bar"></div>
        </div>
    `;
    
    // Add to container
    toastContainer.appendChild(toast);
    
    // Show toast with animation
    setTimeout(() => {
        toast.classList.add('show');
    }, 100);
    
    // Auto remove after duration
    setTimeout(() => {
        removeToast(toast);
    }, duration);
    
    return toast;
}

function closeToast(closeButton) {
    const toast = closeButton.closest('.toast');
    removeToast(toast);
}

function removeToast(toast) {
    if (toast && toast.parentNode) {
        toast.classList.remove('show');
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    }
}

// ==================== SETTINGS PANEL ====================

// Settings DOM elements
const settingsModal = document.getElementById('settingsModal');
const retailListFile = document.getElementById('retailListFile');
const retailListFileName = document.getElementById('retailListFileName');
const updateRetailListBtn = document.getElementById('updateRetailListBtn');
const exportDictionaryBtn = document.getElementById('exportDictionaryBtn');
const thresholdsFile = document.getElementById('thresholdsFile');
const thresholdsFileName = document.getElementById('thresholdsFileName');
const updateThresholdsBtn = document.getElementById('updateThresholdsBtn');
const exportThresholdsBtn = document.getElementById('exportThresholdsBtn');
const clearAllDataBtn = document.getElementById('clearAllDataBtn');
const reloadDataBtn = document.getElementById('reloadDataBtn');
const thresholdSearchInput = document.getElementById('thresholdSearchInput');
const thresholdEditContainer = document.getElementById('thresholdEditContainer');
const bulkSaveContainer = document.getElementById('bulkSaveContainer');
const bulkSaveBtn = document.getElementById('bulkSaveBtn');
const changedCount = document.getElementById('changedCount');

// Settings event listeners
retailListFile.addEventListener('change', handleRetailListFileSelect);
updateRetailListBtn.addEventListener('click', updateRetailList);
exportDictionaryBtn.addEventListener('click', exportDictionary);
thresholdsFile.addEventListener('change', handleThresholdsFileSelect);
updateThresholdsBtn.addEventListener('click', updateThresholds);
exportThresholdsBtn.addEventListener('click', exportThresholds);
clearAllDataBtn.addEventListener('click', clearStoredData);
reloadDataBtn.addEventListener('click', reloadFromFiles);
// Debounced threshold search
let thresholdSearchTimeout;
thresholdSearchInput.addEventListener('input', () => {
    clearTimeout(thresholdSearchTimeout);
    thresholdSearchTimeout = setTimeout(searchThresholdItems, 300);
});
bulkSaveBtn.addEventListener('click', saveAllThresholds);

// Open/Close settings - removed duplicate, using enhanced version below

function closeSettings() {
    settingsModal.style.display = 'none';
    
    // Clear threshold search
    thresholdSearchInput.value = '';
    thresholdEditContainer.innerHTML = `
        <div style="padding: 30px; text-align: center; color: #999;">
            <div style="font-size: 3em; margin-bottom: 10px;">🔍</div>
            <div>Start typing to search for items...</div>
            <div style="font-size: 0.85em; margin-top: 5px;">Minimum 2 characters required</div>
        </div>
    `;
    bulkSaveContainer.style.display = 'none';
    
    // Reset to first tab
    const tabs = document.querySelectorAll('.settings-tab');
    const panels = document.querySelectorAll('.settings-panel');
    
    tabs.forEach(tab => tab.classList.remove('active'));
    panels.forEach(panel => panel.classList.remove('active'));
    
    tabs[0].classList.add('active');
    panels[0].classList.add('active');
}

// Close modal when clicking outside
window.onclick = function(event) {
    if (event.target === settingsModal) {
        closeSettings();
    }
}

// Update settings info display
function updateSettingsInfo() {
    document.getElementById('masterListCount').textContent =
        `${masterList.length} items loaded`;
    document.getElementById('thresholdsCount').textContent =
        `${Object.keys(thresholds).length} custom thresholds loaded`;
}

// Handle retail list file selection
function handleRetailListFileSelect(event) {
    const file = event.target.files[0];
    if (file) {
        retailListFileName.textContent = file.name;
        updateRetailListBtn.disabled = false;
    } else {
        retailListFileName.textContent = 'No file selected';
        updateRetailListBtn.disabled = true;
    }
}

// Handle thresholds file selection
function handleThresholdsFileSelect(event) {
    const file = event.target.files[0];
    if (file) {
        thresholdsFileName.textContent = file.name;
        updateThresholdsBtn.disabled = false;
    } else {
        thresholdsFileName.textContent = 'No file selected';
        updateThresholdsBtn.disabled = true;
    }
}

// Update master list from Retail Export
function updateRetailList() {
    const file = retailListFile.files[0];
    if (!file) return;

    showStatus('Reading retail item list export...', 'info');
    showToast('Processing your master list file...', 'info', 'Import Started');
    showLoading('Importing Master List...', 'Reading and parsing Excel file');

    const reader = new FileReader();
    reader.onload = function(e) {
        try {
            const data = new Uint8Array(e.target.result);
            const workbook = XLSX.read(data, { type: 'array' });

            // Try to read from first sheet or common sheet names
            let itemSheet = null;
            const possibleSheetNames = ['Sheet1', 'Items', 'Item List', 'Products', 'Export', workbook.SheetNames[0]];

            for (const sheetName of possibleSheetNames) {
                itemSheet = readSheet(workbook, sheetName);
                if (itemSheet && itemSheet.length > 0) {
                    console.log(`Reading from sheet: ${sheetName}`);
                    break;
                }
            }

            if (!itemSheet || itemSheet.length === 0) {
                hideLoading();
                showStatus('Error: No data found in Excel file', 'error');
                return;
            }

            // Parse retail list - flexible column matching
            const newMasterList = [];
            itemSheet.forEach(row => {
                // Try various column name variations for Item Number
                const itemNo = normalizeItemNo(
                    row['No.'] ||
                    row['Item No.'] ||
                    row['Item No'] ||
                    row['Item Number'] ||
                    row['ItemNo'] ||
                    row['Code'] ||
                    row['SKU'] ||
                    Object.values(row)[0] ||
                    ''
                );

                // Try various column name variations for Description
                const description = (
                    row['Description'] ||
                    row['Item Description'] ||
                    row['Name'] ||
                    row['Item Name'] ||
                    row['Product Name'] ||
                    Object.values(row)[1] ||
                    ''
                ).toString().trim();

                if (itemNo) {
                    newMasterList.push({
                        itemNo: itemNo,
                        description: description
                    });
                }
            });

            if (newMasterList.length === 0) {
                hideLoading();
                showStatus('Error: No valid items found in file', 'error');
                showToast(
                    'No valid items found in the uploaded file. Please check the file format and column names.',
                    'error',
                    'Import Failed'
                );
                return;
            }

            // Save to localStorage
            localStorage.setItem(STORAGE_KEYS.MASTER_LIST, JSON.stringify(newMasterList));
            masterList = newMasterList;
            dataLoaded = true;

            hideLoading();
            showStatus(`Retail list imported! ${newMasterList.length} items loaded.`, 'success');
            showToast(
                `Successfully imported ${newMasterList.length} items from your Business Central export.`,
                'success',
                'Master List Updated'
            );
            updateSettingsInfo();

            // Reset file input
            retailListFile.value = '';
            retailListFileName.textContent = 'No file selected';
            updateRetailListBtn.disabled = true;

        } catch (error) {
            console.error('Error:', error);
            hideLoading();
            showStatus(`Error importing retail list: ${error.message}`, 'error');
        }
    };

    reader.onerror = function() {
        hideLoading();
        showStatus('Error reading file', 'error');
    };

    reader.readAsArrayBuffer(file);
}

// Update thresholds
function updateThresholds() {
    const file = thresholdsFile.files[0];
    if (!file) return;

    showStatus('Reading thresholds from file...', 'info');
    showToast('Processing Essentials Buddy file...', 'info', 'Import Started');
    showLoading('Importing Thresholds...', 'Reading Item Category sheet');

    const reader = new FileReader();
    reader.onload = function(e) {
        try {
            const data = new Uint8Array(e.target.result);
            const workbook = XLSX.read(data, { type: 'array' });

            const categorySheet = readSheet(workbook, 'Item Category');
            if (!categorySheet || categorySheet.length === 0) {
                hideLoading();
                showStatus('Error: "Item Category" sheet not found or empty', 'error');
                showToast(
                    'Could not find "Item Category" sheet in the uploaded file. Please check your Essentials Buddy file.',
                    'error',
                    'Import Failed'
                );
                return;
            }

            // Parse thresholds
            const newThresholds = {};
            categorySheet.forEach(row => {
                const values = Object.values(row);
                const itemNo = normalizeItemNo(values[0] || '');
                const threshold = parseInt(values[2]) || 100;

                if (itemNo) {
                    newThresholds[itemNo] = threshold;
                }
            });

            // Save to localStorage
            localStorage.setItem(STORAGE_KEYS.THRESHOLDS, JSON.stringify(newThresholds));
            thresholds = newThresholds;

            hideLoading();
            showStatus(`Thresholds updated! ${Object.keys(newThresholds).length} thresholds loaded.`, 'success');
            showToast(
                `Successfully imported ${Object.keys(newThresholds).length} threshold values from Essentials Buddy.`,
                'success',
                'Thresholds Updated'
            );
            updateSettingsInfo();

            // Reset file input
            thresholdsFile.value = '';
            thresholdsFileName.textContent = 'No file selected';
            updateThresholdsBtn.disabled = true;

        } catch (error) {
            console.error('Error:', error);
            hideLoading();
            showStatus(`Error updating thresholds: ${error.message}`, 'error');
            showToast(
                `Error processing Essentials Buddy file: ${error.message}`,
                'error',
                'Import Failed'
            );
        }
    };

    reader.onerror = function() {
        hideLoading();
        showStatus('Error reading file', 'error');
    };

    reader.readAsArrayBuffer(file);
}

// Clear stored data
function clearStoredData() {
    if (confirm('Are you sure you want to clear all stored master data? This will remove the master list and thresholds from storage.')) {
        localStorage.removeItem(STORAGE_KEYS.MASTER_LIST);
        localStorage.removeItem(STORAGE_KEYS.THRESHOLDS);
        masterList = [];
        thresholds = {};
        dataLoaded = false;
        showStatus('Stored data cleared. Please reload from files or upload new data.', 'info');
        updateSettingsInfo();
    }
}

// Reload from JSON files
function reloadFromFiles() {
    loadMasterData();
    showStatus('Reloading master data from files...', 'info');
}

// Export master list as dictionary file
function exportDictionary() {
    if (!masterList || masterList.length === 0) {
        showStatus('No master list to export', 'error');
        return;
    }

    // Create dictionary object with item numbers as keys
    const dictionary = {
        metadata: {
            exportDate: new Date().toISOString(),
            totalItems: masterList.length,
            appVersion: '1.0',
            description: 'Business Central Master List Dictionary'
        },
        items: {}
    };

    // Build dictionary with item numbers as keys
    masterList.forEach(item => {
        dictionary.items[item.itemNo] = {
            description: item.description,
            threshold: thresholds[normalizeItemNo(item.itemNo)] || 100
        };
    });

    // Convert to JSON
    const jsonString = JSON.stringify(dictionary, null, 2);
    const blob = new Blob([jsonString], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    const timestamp = new Date().toISOString().split('T')[0];
    const filename = `MasterList_Dictionary_${timestamp}.json`;

    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);

    showStatus(`Dictionary exported: ${filename} (${masterList.length} items)`, 'success');
}

// Export thresholds as JSON file
function exportThresholds() {
    if (!thresholds || Object.keys(thresholds).length === 0) {
        showStatus('No thresholds to export', 'error');
        return;
    }

    // Create thresholds object with metadata
    const thresholdsData = {
        metadata: {
            exportDate: new Date().toISOString(),
            totalThresholds: Object.keys(thresholds).length,
            appVersion: '1.0',
            description: 'Business Central Item Thresholds'
        },
        thresholds: thresholds
    };

    // Convert to JSON
    const jsonString = JSON.stringify(thresholdsData, null, 2);
    const blob = new Blob([jsonString], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    const timestamp = new Date().toISOString().split('T')[0];
    const filename = `Item_Thresholds_${timestamp}.json`;

    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);

    showStatus(`Thresholds exported: ${filename} (${Object.keys(thresholds).length} items)`, 'success');
}

// Search and display threshold items for editing
function searchThresholdItems() {
    const searchTerm = thresholdSearchInput.value.toLowerCase().trim();
    
    if (searchTerm.length < 2) {
        thresholdEditContainer.innerHTML = `
            <div style="padding: 30px; text-align: center; color: #999;">
                <div style="font-size: 3em; margin-bottom: 10px;">🔍</div>
                <div>Type at least 2 characters to search for items...</div>
            </div>
        `;
        return;
    }

    if (!masterList || masterList.length === 0) {
        thresholdEditContainer.innerHTML = `
            <div style="padding: 30px; text-align: center; color: #999;">
                <div style="font-size: 3em; margin-bottom: 10px;">📋</div>
                <div>No master list loaded</div>
                <div style="font-size: 0.85em; margin-top: 5px;">Please upload an item list first</div>
            </div>
        `;
        return;
    }

    // Filter items based on search term
    const filteredItems = masterList.filter(item => 
        item.itemNo.toLowerCase().includes(searchTerm) ||
        item.description.toLowerCase().includes(searchTerm)
    );

    if (filteredItems.length === 0) {
        thresholdEditContainer.innerHTML = `
            <div style="padding: 30px; text-align: center; color: #999;">
                <div style="font-size: 3em; margin-bottom: 10px;">❌</div>
                <div>No items found matching "${searchTerm}"</div>
                <div style="font-size: 0.85em; margin-top: 5px;">Try a different search term</div>
            </div>
        `;
        return;
    }

    // Limit results to prevent performance issues
    const displayItems = filteredItems.slice(0, 50);
    
    let html = '';
    displayItems.forEach(item => {
        const currentThreshold = thresholds[normalizeItemNo(item.itemNo)] || 100;
        html += `
            <div class="threshold-item" data-item="${escapeHtml(item.itemNo)}">
                <div class="threshold-item-info">
                    <div class="threshold-item-number">${escapeHtml(item.itemNo)}</div>
                    <div class="threshold-item-desc">${escapeHtml(item.description)}</div>
                </div>
                <input type="number" class="threshold-input" value="${currentThreshold}" 
                       min="0" max="9999" data-original="${currentThreshold}">
                <button class="threshold-save-btn" onclick="saveThreshold('${escapeHtml(item.itemNo)}', this)">Save</button>
            </div>
        `;
    });

    if (filteredItems.length > 50) {
        html += `
            <div style="padding: 15px; text-align: center; color: #666; background: #f8f9fa; font-size: 0.9em;">
                Showing first 50 results. Refine your search to see more specific items.
            </div>
        `;
    }

    thresholdEditContainer.innerHTML = html;

    // Add event listeners to threshold inputs
    const inputs = thresholdEditContainer.querySelectorAll('.threshold-input');
    inputs.forEach(input => {
        input.addEventListener('input', function() {
            const original = parseInt(this.dataset.original);
            const current = parseInt(this.value) || 0;
            const item = this.closest('.threshold-item');
            
            if (current !== original) {
                this.classList.add('changed');
                item.classList.add('changed');
            } else {
                this.classList.remove('changed');
                item.classList.remove('changed');
            }
            
            updateBulkSaveVisibility();
        });
    });
}

// Show all thresholds for editing
function showAllThresholds() {
    if (!masterList || masterList.length === 0) {
        thresholdEditContainer.innerHTML = `
            <div style="padding: 30px; text-align: center; color: #999;">
                <div style="font-size: 3em; margin-bottom: 10px;">📋</div>
                <div>No master list loaded</div>
                <div style="font-size: 0.85em; margin-top: 5px;">Please upload an item list first</div>
            </div>
        `;
        return;
    }

    // Clear search input
    thresholdSearchInput.value = '';

    // Show all items
    let html = '';
    masterList.forEach(item => {
        const currentThreshold = thresholds[normalizeItemNo(item.itemNo)] || 100;
        html += `
            <div class="threshold-item" data-item="${escapeHtml(item.itemNo)}">
                <div class="threshold-item-info">
                    <div class="threshold-item-number">${escapeHtml(item.itemNo)}</div>
                    <div class="threshold-item-desc">${escapeHtml(item.description)}</div>
                </div>
                <input type="number" class="threshold-input" value="${currentThreshold}" 
                       min="0" max="9999" data-original="${currentThreshold}">
                <button class="threshold-save-btn" onclick="saveThreshold('${escapeHtml(item.itemNo)}', this)">Save</button>
            </div>
        `;
    });

    if (masterList.length > 0) {
        html = `
            <div style="padding: 10px; text-align: center; color: #667eea; background: #f0f1f7; font-size: 0.9em; font-weight: 600; border-bottom: 2px solid #667eea;">
                Showing all ${masterList.length} items
            </div>
        ` + html;
    }

    thresholdEditContainer.innerHTML = html;

    // Add event listeners to threshold inputs
    const inputs = thresholdEditContainer.querySelectorAll('.threshold-input');
    inputs.forEach(input => {
        input.addEventListener('input', function() {
            const original = parseInt(this.dataset.original);
            const current = parseInt(this.value) || 0;
            const item = this.closest('.threshold-item');
            
            if (current !== original) {
                this.classList.add('changed');
                item.classList.add('changed');
            } else {
                this.classList.remove('changed');
                item.classList.remove('changed');
            }
            
            updateBulkSaveVisibility();
        });
    });
}

// Save individual threshold
function saveThreshold(itemNo, buttonElement) {
    const item = buttonElement.closest('.threshold-item');
    const input = item.querySelector('.threshold-input');
    const newThreshold = parseInt(input.value) || 100;
    
    // Update threshold (use normalized key for consistency)
    const cleanedItemNo = normalizeItemNo(itemNo);
    thresholds[cleanedItemNo] = newThreshold;
    
    // Save to localStorage
    localStorage.setItem(STORAGE_KEYS.THRESHOLDS, JSON.stringify(thresholds));
    
    // Update the original value and remove changed styling
    input.dataset.original = newThreshold;
    input.classList.remove('changed');
    item.classList.remove('changed');
    
    // Show success feedback
    showStatus(`Threshold updated for ${itemNo}: ${newThreshold}`, 'success');
    updateSettingsInfo();
    
    // Add visual feedback
    buttonElement.style.background = '#28a745';
    buttonElement.textContent = 'Saved!';
    setTimeout(() => {
        buttonElement.style.background = '#28a745';
        buttonElement.textContent = 'Save';
    }, 1500);
    
    updateBulkSaveVisibility();
}

// Update bulk save button visibility
function updateBulkSaveVisibility() {
    const changedItems = thresholdEditContainer.querySelectorAll('.threshold-item.changed');
    
    if (changedItems.length > 0) {
        bulkSaveContainer.style.display = 'block';
        changedCount.textContent = `${changedItems.length} item${changedItems.length > 1 ? 's' : ''} modified`;
    } else {
        bulkSaveContainer.style.display = 'none';
    }
}

// Save all changed thresholds
function saveAllThresholds() {
    const changedItems = thresholdEditContainer.querySelectorAll('.threshold-item.changed');
    let savedCount = 0;
    
    changedItems.forEach(item => {
        const itemNo = item.dataset.item;
        const input = item.querySelector('.threshold-input');
        const newThreshold = parseInt(input.value) || 100;
        
        // Update threshold (use normalized key for consistency)
        const cleanedItemNo = normalizeItemNo(itemNo);
        thresholds[cleanedItemNo] = newThreshold;
        
        // Update the original value and remove changed styling
        input.dataset.original = newThreshold;
        input.classList.remove('changed');
        item.classList.remove('changed');
        
        savedCount++;
    });
    
    // Save to localStorage
    localStorage.setItem(STORAGE_KEYS.THRESHOLDS, JSON.stringify(thresholds));
    
    // Update UI
    updateSettingsInfo();
    updateBulkSaveVisibility();
    
    // Show success message
    showStatus(`Successfully updated ${savedCount} threshold${savedCount > 1 ? 's' : ''}`, 'success');
}

// Switch between settings tabs
function switchTab(event, tabId) {
    // Remove active class from all tabs and panels
    const tabs = document.querySelectorAll('.settings-tab');
    const panels = document.querySelectorAll('.settings-panel');
    
    tabs.forEach(tab => tab.classList.remove('active'));
    panels.forEach(panel => panel.classList.remove('active'));
    
    // Add active class to clicked tab and corresponding panel
    event.target.classList.add('active');
    document.getElementById(tabId).classList.add('active');
}

// Enhanced file upload with drag and drop
function initializeFileUploads() {
    const fileAreas = document.querySelectorAll('.file-upload-area');
    
    fileAreas.forEach(area => {
        area.addEventListener('dragover', (e) => {
            e.preventDefault();
            area.classList.add('drag-over');
        });
        
        area.addEventListener('dragleave', (e) => {
            e.preventDefault();
            area.classList.remove('drag-over');
        });
        
        area.addEventListener('drop', (e) => {
            e.preventDefault();
            area.classList.remove('drag-over');
            
            const files = e.dataTransfer.files;
            if (files.length > 0) {
                const fileInput = area.querySelector('input[type="file"]');
                if (fileInput) {
                    fileInput.files = files;
                    fileInput.dispatchEvent(new Event('change'));
                }
            }
        });
    });
}

// Initialize enhanced file uploads when settings open
function openSettings() {
    settingsModal.style.display = 'block';
    updateSettingsInfo();
    initializeFileUploads();
    
    // Clear threshold search
    thresholdSearchInput.value = '';
    thresholdEditContainer.innerHTML = `
        <div style="padding: 30px; text-align: center; color: #999;">
            <div style="font-size: 3em; margin-bottom: 10px;">🔍</div>
            <div>Start typing to search for items...</div>
            <div style="font-size: 0.85em; margin-top: 5px;">Minimum 2 characters required</div>
        </div>
    `;
}

// ===== PREFERENCES SYSTEM =====

// Load user preferences
function loadPreferences() {
    try {
        const stored = localStorage.getItem(STORAGE_KEYS.PREFERENCES);
        if (stored) {
            userPreferences = { ...DEFAULT_PREFERENCES, ...JSON.parse(stored) };
        }
        
        // Apply preferences to UI
        applyPreferencesToUI();
        
        console.log('User preferences loaded:', userPreferences);
    } catch (error) {
        console.error('Error loading preferences:', error);
        userPreferences = { ...DEFAULT_PREFERENCES };
    }
}

// Apply preferences to UI elements
function applyPreferencesToUI() {
    // Set select values
    const itemsPerPageEl = document.getElementById('itemsPerPagePref');
    const defaultSortEl = document.getElementById('defaultSortPref');
    const sortDirectionEl = document.getElementById('sortDirectionPref');
    const exportFormatEl = document.getElementById('exportFormatPref');
    
    if (itemsPerPageEl) itemsPerPageEl.value = userPreferences.itemsPerPage;
    if (defaultSortEl) defaultSortEl.value = userPreferences.defaultSort;
    if (sortDirectionEl) sortDirectionEl.value = userPreferences.sortDirection;
    if (exportFormatEl) exportFormatEl.value = userPreferences.exportFormat;
    
    // Set checkbox values
    const rememberFiltersEl = document.getElementById('rememberFiltersPref');
    const autoExpandEl = document.getElementById('autoExpandFiltersPref');
    const timestampEl = document.getElementById('includeTimestampPref');
    
    if (rememberFiltersEl) rememberFiltersEl.checked = userPreferences.rememberFilters;
    if (autoExpandEl) autoExpandEl.checked = userPreferences.autoExpandFilters;
    if (timestampEl) timestampEl.checked = userPreferences.includeTimestamp;
    
    // Apply auto-expand filters
    if (userPreferences.autoExpandFilters) {
        const filterSection = document.querySelector('.filter-section');
        if (filterSection) filterSection.open = true;
    }
}

// Save a single preference
function savePreference(key, value) {
    userPreferences[key] = value;
    
    try {
        localStorage.setItem(STORAGE_KEYS.PREFERENCES, JSON.stringify(userPreferences));
        showToast(`Preference saved: ${key}`, 'success', 'Settings Updated');
        console.log('Preference saved:', key, value);
    } catch (error) {
        console.error('Error saving preference:', error);
        showToast('Failed to save preference', 'error', 'Settings Error');
    }
}

// Reset all preferences to defaults
function resetPreferences() {
    if (confirm('Are you sure you want to reset all preferences to defaults?')) {
        userPreferences = { ...DEFAULT_PREFERENCES };
        localStorage.removeItem(STORAGE_KEYS.PREFERENCES);
        applyPreferencesToUI();
        showToast('All preferences reset to defaults', 'success', 'Settings Reset');
    }
}

// Theme now managed by unified ThemeManager in shared-utils.js

// ==================== POST MESSAGE HANDLER ====================

/**
 * Listen for messages from parent window (unified settings)
 * Handles commands to open settings and switch tabs
 */
window.addEventListener('message', function(event) {
  if (!event.data || !event.data.type) return;

  try {
    switch (event.data.type) {
      case 'openSettings':
        // Open the settings modal
        if (typeof openSettings === 'function') {
          openSettings();

          // Switch to the specified tab if provided
          if (event.data.tab) {
            const tabId = event.data.tab;

            // Remove active class from all tabs and panels
            const tabs = document.querySelectorAll('.tab');
            const panels = document.querySelectorAll('.tab-panel');

            tabs.forEach(tab => tab.classList.remove('active'));
            panels.forEach(panel => panel.classList.remove('active'));

            // Find and activate the target tab
            const targetPanel = document.getElementById(tabId);
            if (targetPanel) {
              targetPanel.classList.add('active');

              // Also activate the corresponding tab button
              const tabButtons = document.querySelectorAll('.tab');
              tabButtons.forEach(tab => {
                if (tab.getAttribute('onclick') && tab.getAttribute('onclick').includes(tabId)) {
                  tab.classList.add('active');
                }
              });
            }
          }
        } else {
          console.warn('openSettings function not available');
        }
        break;

      default:
        // Unknown message type - ignore
        break;
    }
  } catch (error) {
    console.error('Error handling postMessage:', error);
  }
});

