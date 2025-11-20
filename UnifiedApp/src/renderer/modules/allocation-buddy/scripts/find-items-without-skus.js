const fs = require('fs');
const path = require('path');

// Read and execute the dictionaries file
const dictPath = path.join(__dirname, '..', 'src', 'js', 'dictionaries.js');
const dictContent = fs.readFileSync(dictPath, 'utf8');

// Remove comments and extract the DICT object
let cleanContent = dictContent
  .replace(/\/\/.*/g, '') // Remove single-line comments
  .replace(/\/\*[\s\S]*?\*\//g, ''); // Remove multi-line comments

// Extract the JSON part
const match = cleanContent.match(/window\.DICT\s*=\s*({[\s\S]*});/);
if (!match) {
  console.error('Could not parse dictionary file');
  process.exit(1);
}

const DICT = JSON.parse(match[1]);

// Find items without SKUs
const noSkus = DICT.items.filter(item => !item.sku || item.sku.length === 0);
const withSkus = DICT.items.filter(item => item.sku && item.sku.length > 0);

console.log(`\nDictionary Analysis:`);
console.log(`===================`);
console.log(`Total Items: ${DICT.items.length}`);
console.log(`Items WITH SKUs: ${withSkus.length}`);
console.log(`Items WITHOUT SKUs: ${noSkus.length}`);
console.log(`\nFirst 50 items without SKUs:\n`);

noSkus.slice(0, 50).forEach((item, index) => {
  console.log(`${index + 1}. ${item.number.padEnd(15)} - ${item.desc}`);
});

if (noSkus.length > 50) {
  console.log(`\n... and ${noSkus.length - 50} more items without SKUs`);
}
