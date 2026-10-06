#!/bin/bash
set -e

# Godot executable. Override with: GODOT_BIN=/path/to/godot ./build.sh
if [ -z "$GODOT_BIN" ]; then
    for candidate in godot godot-mono godot4 godot4-mono; do
        if command -v "$candidate" >/dev/null 2>&1; then
            GODOT_BIN="$candidate"
            break
        fi
    done
fi

if [ -z "$GODOT_BIN" ]; then
    echo "Could not find a Godot binary on PATH. Set GODOT_BIN=/path/to/godot and try again."
    exit 1
fi

echo "Using Godot: $GODOT_BIN"

# Mirror of xcopy "SRC" "DEST" /E /I /Y - create DEST, merge into it, overwrite.
copy_tree() {
    local source_directory="$1"
    local destination_directory="$2"
    mkdir -p "$destination_directory"
    cp -r "$source_directory/." "$destination_directory/"
}

# The .NET runtime folder is regenerated on every export. Clearing it first stops stale assemblies,
# and the *.dll~RF*.TMP files Windows leaves when replacing a locked DLL, from reaching a release zip.
echo "Exporting app to Windows..."
mkdir -p build/windows
rm -rf build/windows/data_romm-frontend_*
"$GODOT_BIN" --headless --export-release "Windows Desktop" "build/windows/romm-frontend.exe"

echo "Copying install_scripts and tools to Windows build..."
copy_tree "install_scripts" "build/windows/install_scripts"
copy_tree "tools" "build/windows/tools"

echo "Exporting game to Linux..."
mkdir -p build/linux
rm -rf build/linux/data_romm-frontend_*
"$GODOT_BIN" --headless --export-release "Linux Desktop" "build/linux/romm-frontend.x86_64"

echo "Copying install_scripts and tools to Linux build..."
copy_tree "install_scripts" "build/linux/install_scripts"
copy_tree "tools" "build/linux/tools"

chmod +x "build/linux/romm-frontend.x86_64" "build/linux/tools/7zip/linux/7zz"

echo "Build complete!"
