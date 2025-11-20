const fs = require('fs');
const path = require('path');

/**
 * Simple utility to extract the object assigned to `window.DICT` in a bundled
 * `dictionaries.js` file and write it out as `dictionaries.json` for faster
 * lazy-loading in renderer processes.
 *
 * Usage: node scripts/extract-dictionaries.js <source-file> <out-json>
 */

async function run() {
  const src = process.argv[2] || path.join(__dirname, '..', 'src', 'renderer', 'modules', 'allocation-buddy', 'src', 'js', 'dictionaries.js');
  const out = process.argv[3] || path.join(__dirname, '..', 'src', 'renderer', 'modules', 'allocation-buddy', 'src', 'js', 'dictionaries.json');

  if (!fs.existsSync(src)) {
    console.error('Source file not found:', src);
    process.exit(1);
  }

  const content = fs.readFileSync(src, 'utf8');

  // Look for the first occurrence of `window.DICT =` and extract the following JS object/JSON
  const match = content.match(/window\.DICT\s*=\s*([\s\S]*);?\s*$/m);
  if (!match || !match[1]) {
    console.error('Could not find `window.DICT =` assignment in', src);
    process.exit(1);
  }

  let objText = match[1].trim();

  // If the object ends with a semicolon, strip it
  if (objText.endsWith(';')) objText = objText.slice(0, -1);

  try {
    // Attempt to evaluate the object safely by using Function constructor to return the object
    // This is intentionally minimal and assumes the dictionaries file is a plain JSON-like object.
    const obj = new Function('return (' + objText + ');')();

    fs.writeFileSync(out, JSON.stringify(obj, null, 2), 'utf8');
    console.log('Wrote JSON dictionary to', out);
  } catch (err) {
    console.error('Failed to parse dictionary object:', err.message);
    process.exit(1);
  }
}

run();
