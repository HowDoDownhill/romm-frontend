#!/bin/bash

# Default Godot executable name (can be overridden via environment variable)
# Example usage: GODOT_BIN=/path/to/godot ./build.sh
GODOT_BIN=${GODOT_BIN:-godot}

echo "Exporting app to Windows..."
mkdir -p build/windows
$GODOT_BIN --headless --export-release "Windows Desktop" "build/windows/romm-frontend.exe"

echo "Copying install_scripts and tools to Windows build..."
cp -r install_scripts build/windows/
cp -r tools build/windows/

echo "Exporting game to Linux..."
mkdir -p build/linux
$GODOT_BIN --headless --export-release "Linux Desktop" "build/linux/romm-frontend.x86_64"

echo "Copying install_scripts and tools to Linux build..."
cp -r install_scripts build/linux/
cp -r tools build/linux/

echo "Build complete!"
