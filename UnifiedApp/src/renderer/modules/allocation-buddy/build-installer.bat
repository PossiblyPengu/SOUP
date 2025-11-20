@echo off
echo Building Store Allocation Viewer Installer...
echo This will create a Windows installer (.exe)
echo.

node node_modules\@electron-forge\cli\dist\electron-forge-make.js

echo.
echo Build complete! 
echo.
echo Check these locations:
echo - Installer: out\make\squirrel.windows\x64\
echo - Portable: out\make\zip\win32\x64\
echo.
pause
