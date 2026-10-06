@echo off
echo Building Release Packages...

:: Run the existing build script
call build.bat

echo.
echo Preparing release folder...
if not exist releases mkdir releases

:: Only the files listed below are packaged. The build folders also accumulate
:: runtime data whenever the app is run in place for testing - config.cfg with
:: live credentials, plus roms, bios, downloads, saves and caches - and zipping
:: the folder wholesale shipped all of it. Add new shipped files here rather
:: than switching back to a wildcard.

echo.
echo Zipping Windows Release...
if exist releases\romm-frontend-windows.zip del releases\romm-frontend-windows.zip
pushd build\windows
..\..\tools\7zip\windows\7za.exe a -tzip "..\..\releases\romm-frontend-windows.zip" romm-frontend.exe romm-frontend.console.exe romm-frontend.pck data_romm-frontend_* install_scripts tools
popd

echo.
echo Zipping Linux Release...
if exist releases\romm-frontend-linux.zip del releases\romm-frontend-linux.zip
pushd build\linux
..\..\tools\7zip\windows\7za.exe a -tzip "..\..\releases\romm-frontend-linux.zip" romm-frontend.x86_64 romm-frontend.sh romm-frontend.pck data_romm-frontend_* install_scripts tools
popd

echo.
echo Release packages created successfully in the 'releases' folder!
echo You can now upload 'romm-frontend-windows.zip' and 'romm-frontend-linux.zip' to GitHub Releases.
pause
