@echo off
setlocal
cd /d "%~dp0"

:: Override with: set GODOT_BIN=C:\path\to\Godot_mono_console.exe before running.
:: Otherwise the first Godot found on PATH is used. Nothing is deleted until Godot is found.
if not defined GODOT_BIN (
    for %%N in (godot.exe godot-mono.exe godot4.exe Godot_v4.7.2-stable_mono_win64_console.exe Godot_v4.7.2-stable_mono_win64.exe) do (
        if not defined GODOT_BIN for /f "delims=" %%P in ('where %%N 2^>nul') do if not defined GODOT_BIN set "GODOT_BIN=%%P"
    )
)

if not defined GODOT_BIN (
    echo Could not find Godot. Set GODOT_BIN to the Godot 4.7.2 mono console executable and run again.
    goto :fail
)

set "GODOT_BIN=%GODOT_BIN:"=%"
if not exist "%GODOT_BIN%" (
    echo Godot not found at "%GODOT_BIN%". Set GODOT_BIN to the Godot 4.7.2 mono console executable.
    goto :fail
)

echo Using Godot: %GODOT_BIN%

:: The .NET runtime folder is regenerated on every export. Clearing it first stops stale assemblies,
:: and the *.dll~RF*.TMP files Windows leaves when replacing a locked DLL, from reaching a release zip.

echo Exporting app to Windows...
if not exist build\windows mkdir build\windows
for /d %%D in ("build\windows\data_romm-frontend_*") do rmdir /s /q "%%D"
"%GODOT_BIN%" --headless --export-release "Windows Desktop" "build\windows\romm-frontend.exe"
if errorlevel 1 (
    echo Windows export failed.
    goto :fail
)
if not exist "build\windows\data_romm-frontend_windows_x86_64\GodotSharp.dll" (
    echo Windows export produced no .NET assemblies.
    goto :fail
)

echo Copying install_scripts and tools to Windows build...
xcopy "install_scripts" "build\windows\install_scripts" /E /I /Y /Q
xcopy "tools" "build\windows\tools" /E /I /Y /Q

echo Exporting game to Linux...
if not exist build\linux mkdir build\linux
for /d %%D in ("build\linux\data_romm-frontend_*") do rmdir /s /q "%%D"
"%GODOT_BIN%" --headless --export-release "Linux Desktop" "build\linux\romm-frontend.x86_64"
if errorlevel 1 (
    echo Linux export failed.
    goto :fail
)
if not exist "build\linux\data_romm-frontend_linuxbsd_x86_64\GodotSharp.dll" (
    echo Linux export produced no .NET assemblies.
    goto :fail
)

echo Copying install_scripts and tools to Linux build...
xcopy "install_scripts" "build\linux\install_scripts" /E /I /Y /Q
xcopy "tools" "build\linux\tools" /E /I /Y /Q

echo Build complete!
if /i not "%~1"=="nopause" pause
exit /b 0

:fail
if /i not "%~1"=="nopause" pause
exit /b 1
