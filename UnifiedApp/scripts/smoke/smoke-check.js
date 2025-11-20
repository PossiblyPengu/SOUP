const fs = require('fs');
const path = require('path');

function ok(message) { console.log('\x1b[32m%s\x1b[0m', message); }
function fail(message) { console.error('\x1b[31m%s\x1b[0m', message); process.exitCode = 2; }

const root = path.join(__dirname, '..', '..');

// Basic checks
const checks = [
  { path: 'src/main/preload.js', contains: 'exposeInMainWorld' },
  { path: 'src/main/main.js', contains: 'webviewTag: false' },
  { path: 'src/renderer/js/launcher.js', contains: 'window.electronAPI' }
];

let failed = false;
for (const c of checks) {
  const p = path.join(root, c.path);
  if (!fs.existsSync(p)) {
    fail('Missing file: ' + c.path);
    failed = true;
    continue;
  }
  if (c.contains) {
    const txt = fs.readFileSync(p, 'utf8');
    if (!txt.includes(c.contains)) {
      fail(`Check failed for ${c.path}: missing '${c.contains}'`);
      failed = true;
    } else {
      ok(`OK: ${c.path} contains '${c.contains}'`);
    }
  } else {
    ok(`OK: ${c.path} exists`);
  }
}

// Verify the extract utility exists
if (!fs.existsSync(path.join(root, 'scripts', 'extract-dictionaries.js'))) {
  fail('Missing extract-dictionaries.js script');
  failed = true;
} else {
  ok('OK: extract-dictionaries.js exists');
}

if (failed) process.exit(2);
console.log('\nSmoke checks passed.');
