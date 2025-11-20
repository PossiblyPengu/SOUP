const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const assetsDir = path.join(__dirname, '..', 'assets');
const pngPath = path.join(assetsDir, 'icon.png');
const icoPath = path.join(assetsDir, 'icon.ico');

// Try using ImageMagick if available
exec('magick --version', (error) => {
  if (!error) {
    console.log('✓ ImageMagick found, converting PNG to ICO...');
    const command = `magick convert "${pngPath}" -define icon:auto-resize=256,128,64,48,32,16 "${icoPath}"`;
    exec(command, (err) => {
      if (err) {
        console.error('❌ Conversion failed:', err.message);
        fallbackMethod();
      } else {
        console.log('✓ Created icon.ico successfully!');
        updateConfig();
      }
    });
  } else {
    console.log('⚠ ImageMagick not found, trying alternative method...');
    fallbackMethod();
  }
});

function fallbackMethod() {
  // For Windows, we can use a simple ICO format
  // ICO files can embed PNG data directly for modern Windows
  console.log('📦 Creating simple ICO file (Windows 7+)...');
  
  try {
    const pngData = fs.readFileSync(pngPath);
    
    // Simple ICO header: ICONDIR structure
    const header = Buffer.alloc(6);
    header.writeUInt16LE(0, 0); // Reserved, must be 0
    header.writeUInt16LE(1, 2); // Image type: 1 = ICO
    header.writeUInt16LE(1, 4); // Number of images in file
    
    // ICONDIRENTRY structure (16 bytes per image)
    const dirEntry = Buffer.alloc(16);
    dirEntry.writeUInt8(0, 0);  // Width (0 = 256)
    dirEntry.writeUInt8(0, 1);  // Height (0 = 256)
    dirEntry.writeUInt8(0, 2);  // Color palette (0 = no palette)
    dirEntry.writeUInt8(0, 3);  // Reserved
    dirEntry.writeUInt16LE(1, 4); // Color planes
    dirEntry.writeUInt16LE(32, 6); // Bits per pixel
    dirEntry.writeUInt32LE(pngData.length, 8); // Size of image data
    dirEntry.writeUInt32LE(22, 12); // Offset to image data (6 + 16)
    
    // Combine all parts
    const icoData = Buffer.concat([header, dirEntry, pngData]);
    fs.writeFileSync(icoPath, icoData);
    
    console.log('✓ Created icon.ico successfully!');
    updateConfig();
  } catch (err) {
    console.error('❌ Failed to create ICO:', err.message);
    console.log('');
    console.log('Please manually convert using:');
    console.log('  https://icoconvert.com/');
    console.log('  Or install ImageMagick from: https://imagemagick.org/');
  }
}

function updateConfig() {
  console.log('');
  console.log('📝 Icon created! Now updating forge.config.js...');
  
  const configPath = path.join(__dirname, '..', 'forge.config.js');
  let configContent = fs.readFileSync(configPath, 'utf8');
  
  // Add icon to packagerConfig
  if (!configContent.includes('icon:')) {
    configContent = configContent.replace(
      'packagerConfig: {',
      `packagerConfig: {\n    icon: './assets/icon',`
    );
    
    fs.writeFileSync(configPath, configContent);
    console.log('✓ Updated forge.config.js with icon path');
  } else {
    console.log('✓ Icon path already in config');
  }
  
  console.log('');
  console.log('🎉 All done! Your app will now use the 📦 box emoji icon!');
  console.log('');
  console.log('To rebuild with the new icon, run: .\\build-installer.bat');
}
