@echo off
echo Building Store Allocation Viewer...
echo.

node node_modules\@electron-forge\cli\dist\electron-forge-package.js

echo.
echo Build complete! Check the 'out' folder for the packaged app.
pause
