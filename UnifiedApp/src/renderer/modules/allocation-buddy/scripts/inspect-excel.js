// Script to inspect Excel file structure
const XLSX = require('xlsx');
const path = require('path');

const filePath = process.argv[2];

if (!filePath) {
  console.error('Usage: node inspect-excel.js <path-to-excel-file>');
  process.exit(1);
}

try {
  console.log(`Reading file: ${filePath}\n`);
  
  const workbook = XLSX.readFile(filePath);
  
  console.log('=== WORKBOOK INFO ===');
  console.log(`Sheet Names: ${workbook.SheetNames.join(', ')}`);
  console.log(`Number of Sheets: ${workbook.SheetNames.length}\n`);
  
  // Inspect first sheet
  const firstSheetName = workbook.SheetNames[0];
  const worksheet = workbook.Sheets[firstSheetName];
  
  console.log(`=== FIRST SHEET: "${firstSheetName}" ===\n`);
  
  // Convert to JSON to see structure
  const data = XLSX.utils.sheet_to_json(worksheet, { defval: '' });
  
  console.log(`Total Rows: ${data.length}\n`);
  
  if (data.length > 0) {
    console.log('=== COLUMNS (from first row) ===');
    const columns = Object.keys(data[0]);
    columns.forEach((col, idx) => {
      console.log(`${idx + 1}. "${col}"`);
    });
    
    console.log('\n=== FIRST 5 ROWS (sample data) ===');
    data.slice(0, 5).forEach((row, idx) => {
      console.log(`\nRow ${idx + 1}:`);
      columns.forEach(col => {
        const value = row[col];
        const type = typeof value;
        console.log(`  ${col}: ${JSON.stringify(value)} (${type})`);
      });
    });
    
    console.log('\n=== DATA TYPE ANALYSIS ===');
    columns.forEach(col => {
      const sample = data.slice(0, 10).map(row => row[col]);
      const types = sample.map(v => typeof v);
      const hasNumbers = sample.some(v => !isNaN(parseFloat(v)) && isFinite(v));
      const allNumbers = sample.every(v => !isNaN(parseFloat(v)) && isFinite(v));
      const uniqueValues = [...new Set(sample.filter(v => v !== ''))];
      
      console.log(`\n"${col}":`);
      console.log(`  Sample values: ${uniqueValues.slice(0, 3).map(v => JSON.stringify(v)).join(', ')}`);
      console.log(`  Has numeric: ${hasNumbers}`);
      console.log(`  All numeric: ${allNumbers}`);
      console.log(`  Unique (first 10): ${uniqueValues.length}`);
    });
  } else {
    console.log('WARNING: No data found in worksheet!');
  }
  
} catch (error) {
  console.error('Error reading file:', error.message);
  process.exit(1);
}
