@echo off
echo Building Release Packages...

:: Run the existing build script
call build.bat

echo.
echo Preparing release folder...
if not exist releases mkdir releases

echo.
echo Zipping Windows Release...
if exist releases\romm-frontend-windows.zip del releases\romm-frontend-windows.zip
tools\7zip\windows\7za.exe a -tzip "releases\romm-frontend-windows.zip" ".\build\windows\*"

echo.
echo Zipping Linux Release...
if exist releases\romm-frontend-linux.zip del releases\romm-frontend-linux.zip
tools\7zip\windows\7za.exe a -tzip "releases\romm-frontend-linux.zip" ".\build\linux\*"

echo.
echo Release packages created successfully in the 'releases' folder!
echo You can now upload 'romm-frontend-windows.zip' and 'romm-frontend-linux.zip' to GitHub Releases.
pause
