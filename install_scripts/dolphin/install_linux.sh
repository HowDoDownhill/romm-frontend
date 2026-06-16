SCRIPT_PATH="$1"
INSTALL_DIR="$2"
echo $SCRIPT_PATH
echo "Downloading Dolphin"
curl -L -o emulators/dolphin/dolphin.AppImage "https://github.com/pkgforge-dev/Dolphin-emu-AppImage/releases/download/2603a%402026-06-07_1780858798/Dolphin_Emulator-2603a-anylinux-x86_64.AppImage"
echo "Making Dolphin executable"
chmod +x emulators/dolphin/dolphin.AppImage

