This "user" directory is Azahar's portable-mode marker. Do not delete it.

Verified in src/common/file_util.cpp, SetUserPath():

  Windows  user_path = GetExeDirectory() + "/user/", used only if that directory
           already exists; otherwise Azahar falls back to %APPDATA%/Azahar.

  Linux    if the process working directory contains "user/", that is used;
           otherwise Azahar falls back to $XDG_DATA_HOME/azahar-emu.
           The frontend sets the working directory to the emulator install
           directory, so this file being here is what keeps Azahar portable.

Because the marker is a directory rather than a file, git will not track it
unless it contains something — hence this README.

Azahar populates the rest itself on first run:
  user/config/   qt-config.ini
  user/sdmc/     SD card contents, where game saves live
  user/nand/     emulated NAND, system titles and system saves
  user/sysdata/  AES keys / shared system files, if provided

Only sdmc and nand are listed in relative_save_path, so a reinstall preserves
saves without also sweeping config and cache into RomM as save data.
