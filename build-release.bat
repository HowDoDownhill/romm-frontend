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
powershell -Command "Compress-Archive -Path 'build\windows\*' -DestinationPath 'releases\romm-frontend-windows.zip' -Force"

echo.
echo Zipping Linux Release...
if exist releases\romm-frontend-linux.zip del releases\romm-frontend-linux.zip
powershell -Command "Compress-Archive -Path 'build\linux\*' -DestinationPath 'releases\romm-frontend-linux.zip' -Force"

echo.
echo Release packages created successfully in the 'releases' folder!
echo You can now upload 'romm-frontend-windows.zip' and 'romm-frontend-linux.zip' to GitHub Releases.
pause
