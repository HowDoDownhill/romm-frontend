@echo off
echo Exporting app to Windows...
:: Run Godot in headless mode to export the project
"E:\Godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "build\windows\romm-frontend.exe"

echo Copying install_scripts...
:: Copy the directory into the build folder
xcopy "install_scripts" "build\windows\install_scripts" /E /I /Y
xcopy "downloads" "build\windows\roms" /E /I /Y
xcopy "roms" "build\windows\roms" /E /I /Y
xcopy "emulators" "build\windows\emulators" /E /I /Y
xcopy "bios" "build\windows\bios" /E /I /Y
xcopy "tools" "build\windows\tools" /E /I /Y

echo Exporting game to Linux...
:: Run Godot in headless mode to export the project
"E:\Godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe" --headless --export-release "Linux Desktop" "build\linux\romm-frontend.x86_64"

echo Copying install_scripts...
:: Copy the directory into the build folder
xcopy "install_scripts" "build\linux\install_scripts" /E /I /Y
xcopy "downloads" "build\linux\roms" /E /I /Y
xcopy "roms" "build\linux\roms" /E /I /Y
xcopy "emulators" "build\linux\emulators" /E /I /Y
xcopy "bios" "build\linux\bios" /E /I /Y
xcopy "tools" "build\linux\tools" /E /I /Y

echo Build complete!
pause