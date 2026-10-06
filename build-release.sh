#!/bin/bash
set -e

echo "Building Release Packages..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

./build.sh

echo
echo "Preparing release folder..."
mkdir -p releases

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) SEVEN_ZIP="$SCRIPT_DIR/tools/7zip/windows/7za.exe" ;;
    Darwin*)              SEVEN_ZIP="$SCRIPT_DIR/tools/7zip/macOS/7zz" ;;
    *)                    SEVEN_ZIP="$SCRIPT_DIR/tools/7zip/linux/7zz" ;;
esac
chmod +x "$SEVEN_ZIP" 2>/dev/null || true

# Only the files listed below are packaged. The build folders also accumulate
# runtime data whenever the app is run in place for testing - config.cfg with
# live credentials, plus roms, bios, downloads, saves and caches - and zipping
# the folder wholesale shipped all of it. Keep this list in step with
# build-release.bat, and add new shipped files here rather than using a wildcard.

echo
echo "Zipping Windows Release..."
rm -f releases/romm-frontend-windows.zip
(cd build/windows && "$SEVEN_ZIP" a -tzip "$SCRIPT_DIR/releases/romm-frontend-windows.zip" \
    romm-frontend.exe romm-frontend.console.exe romm-frontend.pck data_romm-frontend_* install_scripts tools)

echo
echo "Zipping Linux Release..."
rm -f releases/romm-frontend-linux.zip
(cd build/linux && "$SEVEN_ZIP" a -tzip "$SCRIPT_DIR/releases/romm-frontend-linux.zip" \
    romm-frontend.x86_64 romm-frontend.sh romm-frontend.pck data_romm-frontend_* install_scripts tools)

echo
echo "Release packages created successfully in the 'releases' folder!"
echo "You can now upload 'romm-frontend-windows.zip' and 'romm-frontend-linux.zip' to GitHub Releases."
