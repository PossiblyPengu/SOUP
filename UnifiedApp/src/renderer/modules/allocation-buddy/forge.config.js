module.exports = {
  packagerConfig: {
    icon: './assets/icon',
    asar: true,
    executableName: 'StoreAllocationViewer'
  },
  rebuildConfig: {},
  makers: [
    {
      name: '@electron-forge/maker-squirrel',
      config: {
        name: 'StoreAllocationViewer',
        setupExe: 'StoreAllocationViewer-Setup.exe',
        authors: 'Store Allocation Team',
        iconUrl: 'file://' + require('path').resolve(__dirname, 'assets', 'icon.ico'),
        setupIcon: './assets/icon.ico'
      },
    },
    {
      name: '@electron-forge/maker-zip',
      platforms: ['darwin', 'linux', 'win32'],
    },
    {
      name: '@electron-forge/maker-deb',
      config: {},
    },
    {
      name: '@electron-forge/maker-rpm',
      config: {},
    },
  ],
  plugins: [],
};
