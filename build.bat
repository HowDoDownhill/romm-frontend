@echo off
:: Override with: set GODOT_BIN=C:\path\to\Godot_mono.exe before running
if not defined GODOT_BIN set GODOT_BIN="E:\Godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe"

:: The .NET runtime folder is regenerated on every export. Clearing it first stops stale assemblies,
:: and the *.dll~RF*.TMP files Windows leaves when replacing a locked DLL, from reaching a release zip.

echo Exporting app to Windows...
:: Run Godot in headless mode to export the project
for /d %%D in ("build\windows\data_romm-frontend_*") do rmdir /s /q "%%D"
%GODOT_BIN% --headless --export-release "Windows Desktop" "build\windows\romm-frontend.exe"

echo Copying install_scripts and tools to Windows build...
:: Copy the directory into the build folder
xcopy "install_scripts" "build\windows\install_scripts" /E /I /Y
xcopy "tools" "build\windows\tools" /E /I /Y

echo Exporting game to Linux...
:: Run Godot in headless mode to export the project
for /d %%D in ("build\linux\data_romm-frontend_*") do rmdir /s /q "%%D"
%GODOT_BIN% --headless --export-release "Linux Desktop" "build\linux\romm-frontend.x86_64"

echo Copying install_scripts and tools to Linux build...
:: Copy the directory into the build folder
xcopy "install_scripts" "build\linux\install_scripts" /E /I /Y
xcopy "tools" "build\linux\tools" /E /I /Y

echo Build complete!
pause
