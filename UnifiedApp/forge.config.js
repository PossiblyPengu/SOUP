module.exports = {
  packagerConfig: {
    asar: true,
    name: 'Business Tools Suite',
    executableName: 'business-tools-suite',
    icon: './build/icon'
  },
  rebuildConfig: {},
  makers: [
    {
      name: '@electron-forge/maker-squirrel',
      config: {
        name: 'business_tools_suite',
        setupIcon: './build/icon.ico',
        loadingGif: './build/loading.gif'
      },
    },
    {
      name: '@electron-forge/maker-zip',
      platforms: ['darwin', 'linux', 'win32'],
    },
    {
      name: '@electron-forge/maker-deb',
      config: {
        options: {
          maintainer: 'Business Tools Team',
          homepage: 'https://github.com/yourusername/business-tools-suite'
        }
      },
    },
    {
      name: '@electron-forge/maker-rpm',
      config: {},
    },
  ],
};
