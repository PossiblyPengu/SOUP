const fs = require('fs');
const path = require('path');
const https = require('https');

// Create assets directory
const assetsDir = path.join(__dirname, '..', 'assets');
if (!fs.existsSync(assetsDir)) {
  fs.mkdirSync(assetsDir, { recursive: true });
}

// Create a simple SVG icon with the box emoji
const svgContent = `<?xml version="1.0" encoding="UTF-8"?>
<svg width="256" height="256" xmlns="http://www.w3.org/2000/svg">
  <rect width="256" height="256" fill="#2C3E50"/>
  <text x="128" y="200" font-size="180" text-anchor="middle" fill="white">📦</text>
</svg>`;

fs.writeFileSync(path.join(assetsDir, 'icon.svg'), svgContent);

console.log('✓ Created SVG icon in assets/icon.svg');
console.log('📦 Box emoji icon ready!');
console.log('');

// Try to download a high-quality emoji PNG
const emojiUrl = 'https://em-content.zobj.net/source/microsoft-teams/363/package_1f4e6.png';
const iconPath = path.join(assetsDir, 'icon.png');

console.log('📥 Attempting to download high-quality emoji PNG...');

https.get(emojiUrl, (response) => {
  if (response.statusCode === 200) {
    const fileStream = fs.createWriteStream(iconPath);
    response.pipe(fileStream);
    fileStream.on('finish', () => {
      fileStream.close();
      console.log('✓ Downloaded icon.png');
      console.log('');
      console.log('Next steps:');
      console.log('  1. Use online converter: https://icoconvert.com/');
      console.log('  2. Upload assets/icon.png');
      console.log('  3. Download as icon.ico and save to assets/');
      console.log('  OR');
      console.log('  Use ImageMagick: magick convert assets/icon.png -define icon:auto-resize=256,128,64,48,32,16 assets/icon.ico');
    });
  } else {
    console.log('⚠ Could not download emoji. Use manual method:');
    console.log('  1. Visit: https://emojipedia.org/package');
    console.log('  2. Download a high-quality PNG');
    console.log('  3. Convert to .ico using https://icoconvert.com/');
  }
}).on('error', (err) => {
  console.log('⚠ Could not download emoji:', err.message);
  console.log('  Use manual method or the SVG file created above.');
});
