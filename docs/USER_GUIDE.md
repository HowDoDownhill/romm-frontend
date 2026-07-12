# RomM Frontend Complete User Guide

Welcome to the RomM Frontend! This application is a native companion client for [RomM (Rom Manager)](https://github.com/rommapp/romm). It is built using the Godot engine, allowing you to browse, search, and download your retro game library from your RomM backend and play games locally using automatically installed and configured emulators.

---

## Table of Contents
1. [Prerequisites](#1-prerequisites)
2. [Getting Started & Login](#2-getting-started--login)
3. [Global Configuration (`config.cfg`)](#3-global-configuration-configcfg)
4. [BIOS & Firmware Management](#4-bios--firmware-management)
5. [Save Game Synchronization](#5-save-game-synchronization)
6. [Emulator Mapping (`EmulatorMap.json`)](#6-emulator-mapping-emulatormapjson)
7. [Custom Emulator Metadata (`meta.json`)](#7-custom-emulator-metadata-metajson)
8. [Emulator Settings Configuration (`settings_fields`)](#8-emulator-settings-configuration-settings_fields)
9. [Controller Configuration (`controller_config`)](#9-controller-configuration-controller_config)

---

## 1. Prerequisites

Before running the RomM Frontend, ensure your local environment is ready:
- **Operating System**: Windows (D3D12/GL Compatibility), Linux, or macOS.
- **Runtime**: .NET 8 Runtime installed on your machine.
- **Backend**: A running instance of RomM.

---

## 2. Getting Started & Login

When you launch the frontend for the first time, you will be prompted to configure your connection to the RomM server:

1. **RomM Host**: Enter the full URL/IP of your backend instance (e.g., `https://romm.example.com` or `http://192.168.1.50:8080`).
2. **RomM Username & Password**: Your standard RomM credentials.
3. **RomM API Key**: Enter a client API key. 
   - **How to obtain the API key**:
     1. Log into your RomM backend web panel.
     2. Open your user profile or settings page in the top right.
     3. Scroll to the **Client API Tokens** or **Authentication** section.
     4. Generate a new API token, copy it, and paste it into the frontend's login screen.

> [!NOTE]
> Upon successful authentication, your credentials are saved to `config.cfg`. The application will automatically log in on subsequent launches. The frontend uses a cascading fallback mechanism: it attempts Bearer token auth (API key) first, then OAuth2 token auth, and finally HTTP Basic auth.

---

## 3. Global Configuration (`config.cfg`)

All global configuration parameters, paths, and user preferences are saved in `config.cfg` located in the application's root directory.

### Configuration Sections and Keys

#### `[Paths]`
Defines the directory layout. Directories are automatically created in the application root on startup if they do not exist:
* `RomsPath`: Directory where downloaded ROMs are stored (default: `roms/`).
* `BiosPath`: Directory where firmware and BIOS files are cached (default: `bios/`).
* `EmulatorsPath`: Directory where emulators are installed (default: `emulators/`).
* `DownloadsPath`: Temporary directory for in-progress downloads (default: `downloads/`).
* `InstallScriptsPath`: Location of emulator recipe metadata (default: `install_scripts/`).
* `ToolsPath`: Bundled external tools, such as the 7-Zip archiver (default: `tools/`).
* `AssetsPath`: Directory for cached game boxart and screenshots (default: `assets/`).
* `SavesPath`: Directory for temporary save-file synchronization operations (default: `saves/`).

#### `[RomM]`
Contains connection parameters:
* `Host`: The target RomM server URL.
* `Username`: Username used for authentication.
* `Password`: Password (stored in plain text; protect this file accordingly).
* `ApiKey`: The API Key generated from the RomM UI.
* `ValidLoginLastUsed`: Boolean indicating whether the last login attempt succeeded.

#### `[UI]`
Adjusts how items are rendered:
* `HideGamesWithoutBoxArt`: If `true`, hides games that don't have matching boxart on the backend server.
* `ShowAllSystems`: If `true`, displays systems even if they contain zero games.
* `AppTheme`: The active visual style (options: `Default`, `Rose-pine`, `Gruvbox`, `catppuccin`, `Solarized Dark`, `Solarized Light`, `monokai`, `Nord`, `Dracula`).

#### `[Input]`
Controls general controller hotkeys:
* `EmulatorCloseHotkeyCount`: The number of buttons required to trigger the hotkey combination that closes an emulator (default: `4`).
* `EmulatorCloseHotkeys`: An array of Godot `JoyButton` integers defining the shortcut. The default combination is `[9, 10, 6, 4]`, which translates to pressing **Left Shoulder + Right Shoulder + Select + Start** simultaneously.

---

## 4. BIOS & Firmware Management

Some emulators (such as PCSX2 for PS2 or DuckStation for PS1) require official BIOS/firmware to function. The frontend automates this synchronization:

1. **Auto-Download**: During the login loading screen, the frontend fetches all available system firmware files from the RomM API and stores them locally in `bios/<platform_slug>/`.
2. **Selecting Preferred BIOS**:
   - In the frontend interface, select the platform or game.
   - Open the **Start Menu** (using a controller or keyboard).
   - Navigate to **Select BIOS**.
   - A list of all synchronized BIOS/firmware files for that specific system will be shown.
   - Select the desired file to set it as the default for that gaming system.
3. **Auto-Copying**: When launching a game, the frontend automatically copies the preferred BIOS file into the emulator's local bios path (configured via `emulator_bios_path` in `meta.json`).

---

## 5. Save Game Synchronization

The frontend synchronizes your save files bidirectionally with the RomM backend:

1. **Pre-Launch Sync**:
   - Prior to launching an emulator, the frontend asks the RomM API for available save files associated with the game's ID.
   - It negotiates conflicts: newer save states on the server are automatically downloaded. If a save state represents a folder, it is downloaded as a `.folder.zip` archive and extracted to the local save directory using 7-Zip.
   - The frontend takes a file snapshot (recording names and modification timestamps) of the local emulator save folder.
2. **Post-Exit Sync**:
   - After the emulator process exits, the frontend compares the save directory state against the pre-launch snapshot.
   - Any files or folders that were added or modified during play are automatically archived and uploaded back to your RomM server, ensuring your progress is backed up and available on other devices.
3. **Save Directory Configuration**:
   - The directory containing the save files is configured per emulator using the `relative_save_path` parameter inside the emulator's `meta.json` file.

---

## 6. Emulator Mapping (`EmulatorMap.json`)

The frontend determines which emulator to launch for a system by referencing `emulators/EmulatorMap.json`.

If the file does not exist, the app generates a default mapping for 19 popular systems:
- **Nintendo**: NGC, Wii (Dolphin), SNES (Snes9x), N64 (Gopher64), NES (Nestopia), GB/GBA (mGBA), NDS (melonDS)
- **PlayStation**: PSX (DuckStation), PS2 (PCSX2), PS3 (RPCS3), PS4 (shadPS4), PSP (PPSSPP)
- **Sega**: Sega 32X, Sega CD, SMS, Genesis (Ares), Dreamcast (Flycast)

### Modifying the Mappings

You can override these assignments by opening `emulators/EmulatorMap.json` in a text editor. The file is structured as a dictionary of system slugs mapped to lists of compatible emulator slugs:

```json
{
  "snes": ["snes9x"],
  "ps2": ["pcsx2"],
  "ngc": ["dolphin"]
}
```

If multiple emulators are configured for a system, the first emulator in the list is launched by default. You can change your preferred emulator on a per-system basis from the Settings menu in the UI, which will write your preference to the `[PreferredEmulators]` section of `config.cfg`.

---

## 7. Custom Emulator Metadata (`meta.json`)

To add support for a custom emulator or modify how an existing emulator operates, you must edit its `meta.json` file. Every emulator has its own subdirectory in the `install_scripts/` folder (e.g., `install_scripts/my_emulator/meta.json`).

### File Structure

Here is a complete template of a custom emulator `meta.json`:

```json
{
  "name": "My Emulator",
  "executable_name": {
    "windows": "my_emulator.exe",
    "linux": "my_emulator-x86_64.AppImage",
    "macos": "my_emulator.app/Contents/MacOS/my_emulator"
  },
  "emulator_dir_name": {
    "windows": "my_emulator_win",
    "linux": "my_emulator_linux",
    "macos": "my_emulator_mac"
  },
  "emulator_bios_path": {
    "windows": "bios",
    "linux": "bios",
    "macos": "bios"
  },
  "relative_save_path": {
    "default": "saves",
    "ps2": "memcards"
  },
  "launch_args_with_game": "-batch -fullscreen -rom \"{rom_path}\" -bios \"{bios_path}\"",
  "launch_args_without_game": "-gui",
  "install_recipe": {
    "windows": {
      "type": "github_release",
      "repo": "developer/my_emulator",
      "asset_regex": ".*windows-x64\\.zip$",
      "extract": true,
      "extract_folder_regex": "my_emulator-*"
    },
    "linux": {
      "type": "direct_url",
      "url": "https://example.com/downloads/my_emulator.AppImage",
      "extract": false
    }
  },
  "settings_fields": [
    {
      "id": "internal_res",
      "label": "Internal Resolution",
      "type": "dropdown",
      "default_value_string": "1",
      "config_file_relative_path": "config.ini",
      "config_section": "Graphics",
      "config_key": "ResolutionScale",
      "options": {
        "Native (1x)": "1",
        "2x Resolution": "2",
        "4x Resolution": "4"
      }
    },
    {
      "id": "fullscreen",
      "label": "Enable Fullscreen",
      "type": "boolean",
      "default_value_bool": true,
      "launch_arg_true": "--fullscreen",
      "launch_arg_false": "--windowed"
    }
  ]
}
```

### Metadata Fields

#### General Definitions
- `name`: The user-friendly name displayed in the UI.
- `executable_name`: Key-value map of operating system keys (`windows`, `linux`, `macos`) to the filename of the emulator's executable.
- `emulator_dir_name`: Key-value map of operating systems to the folder name where the emulator is extracted and run.
- `emulator_bios_path`: Path inside the emulator directory where system BIOS files must be copied automatically when launching a game.
- `relative_save_path`: Maps gaming system slugs (or `"default"`) to local relative directories within the emulator folder. The frontend tracks these paths for save file syncing. Supports string values and arrays of strings. `{system_slug}` macro is supported to dynamically insert the system slug name.

#### Launch Arguments
- `launch_args_with_game`: Command-line parameters used when starting a game. Use `{rom_path}` (absolute path to the game ROM) and `{bios_path}` (absolute path to the system's preferred BIOS file) as placeholders.
- `launch_args_without_game`: Parameters used when launching the emulator standalone without loading a specific game.

#### Installation Recipes
The `install_recipe` section defines how the frontend downloads and installs the emulator for each platform (`windows`, `linux`, `macos`).
* **`type` = `"github_release"`**:
  - `repo`: GitHub repository path (e.g., `"mgba-emu/mgba"`).
  - `asset_regex`: Regular expression to match the filename of the release asset (e.g., `".*win64\\.7z$"`).
  - `extract`: If `true`, extracts the downloaded archive.
  - `extract_folder_regex`: (Optional) Regular expression matching a folder nested inside the archive to extract (e.g., `"mgba-*"`).
* **`type` = `"direct_url"`**:
  - `url`: Direct link to download the emulator executable or archive.
  - `extract`: If `true`, extracts the archive.

---

## 8. Emulator Settings Configuration (`settings_fields`)

You can declare custom settings fields in `meta.json` under `settings_fields`. The frontend parses these and renders them as interactive elements in the Settings UI. When user preferences change, the frontend automatically applies them: either by appending command-line flags or modifying configuration files.

### Configuration File Formats
If `config_file_relative_path` is specified, the frontend will read, update, and write the file. The frontend supports:
1. **INI/CFG Files** (`format = "ini"` or files ending in `.ini`/`.cfg`): Uses standard `[Section]` and `key = value` structures.
2. **JSON Files** (`format = "json"` or files ending in `.json`): Updates structured nested JSON blocks.
3. **BML Files** (files ending in `.bml`): Updates hierarchical spacing-based key-value files.

### Setting Field Parameters

* `id`: Unique identifier for the setting.
* `label`: Text shown in the frontend UI.
* `type`: The input widget style. Supported options: `"boolean"` (toggle switch), `"dropdown"` (menu list), or `"hidden"`.
* `default_value_bool` / `default_value_string`: Default settings values. String values support variables like `{game_id}` which translates dynamically to the RomM database ID of the launched game.
* **Command Line Arguments Injection**:
  - For booleans: `launch_arg_true` is appended to the launch arguments if enabled; `launch_arg_false` is appended if disabled.
  - For dropdowns: `launch_arg_format` specifies the string structure (e.g., `-res {value}` where `{value}` is replaced by the selected option's value).
* **Configuration File Injection**:
  - `config_file_relative_path`: The file path relative to the emulator installation directory.
  - `config_section`: The section group in the config file. For BML/JSON files, sections can be nested using slashes or dots (e.g., `"EmuCore/GS"`).
  - `config_key`: The target key name inside the section to modify.

---

## 9. Controller Configuration (`controller_config`)

The `controller_config` section within `meta.json` maps your physical joypads/controllers to the emulator on launch. It dynamically maps player ports, types, and controller button layouts based on the controllers currently connected to the system.

### Properties of `controller_config`

* `max_controllers`: The maximum number of controller ports to configure.
* `config_file_relative_path`: Path to the emulator's controller configuration file.
* `format`: The file format (`"ini"`, `"json"`, or `"bml"`).
* `platform_layout`: Maps abstract controller buttons (e.g., `"A"`, `"Dpad Up"`) to standardized SDL button actions (e.g., `"FaceSouth"`, `"DpadUp"`).
* `sdl_string_map`: Maps standard SDL actions to the emulator's internal naming syntax (e.g., `"FaceSouth"` maps to `"`Button S`"` in Dolphin).
* `controller_sections` (For INI configuration formats):
  - `section_template`: The template name of the controller section (e.g., `"GCPad{port}"` where `{port}` represents the port number).
  - `port_start`: The starting port index (usually `0` or `1`).
  - `device_key`: The config key where the physical controller device name is saved (e.g., `"Device"`).
  - `device_template`: The format string used to identify the controller (e.g., `"SDL/{sdl_index}/{controller_name}"`).
  - `device_disconnected`: Default value applied to the device key if no controller is plugged into the port.
  - `type_key`: Config key representing the controller type.
  - `type_connected` / `type_disconnected`: String values written to identify active or inactive controller ports.
  - `mappings`: Dict mapping emulator input actions to physical controls. Standard joypad buttons use macros like `{Platform_ButtonName}` which are dynamically replaced with the mapped SDL action.
  - `static_values`: Additional static keys and values written to the controller section.

### Example: Dolphin Controller Auto-Mapping

Dolphin uses INI configuration files. Below is an extract showing how Dolphin binds four GameCube controllers to SDL inputs automatically:

```json
  "controller_config": {
    "max_controllers": 4,
    "config_file_relative_path": "User/Config/GCPadNew.ini",
    "format": "ini",
    "platform_layout": {
      "A": "FaceSouth",
      "B": "FaceEast",
      "X": "FaceWest",
      "Y": "FaceNorth",
      "Start": "Start",
      "Dpad Up": "DpadUp",
      "Dpad Down": "DpadDown",
      "Dpad Left": "DpadLeft",
      "Dpad Right": "DpadRight"
    },
    "sdl_string_map": {
      "FaceSouth": "`Button S`",
      "FaceEast": "`Button E`",
      "FaceWest": "`Button W`",
      "FaceNorth": "`Button N`",
      "DpadUp": "`Pad N`",
      "DpadDown": "`Pad S`",
      "DpadLeft": "`Pad W`",
      "DpadRight": "`Pad E`",
      "Start": "Start"
    },
    "controller_sections": [
      {
        "section_template": "GCPad{port}",
        "port_start": 1,
        "device_key": "Device",
        "device_template": "SDL/{sdl_index}/{controller_name}",
        "device_disconnected": "DInput/0/Keyboard Mouse",
        "mappings": {
          "Buttons/A": "{Platform_A}",
          "Buttons/B": "{Platform_B}",
          "Buttons/X": "{Platform_X}",
          "Buttons/Y": "{Platform_Y}",
          "Buttons/Start": "{Platform_Start}",
          "D-Pad/Up": "{Platform_Dpad Up}",
          "D-Pad/Down": "{Platform_Dpad Down}",
          "D-Pad/Left": "{Platform_Dpad Left}",
          "D-Pad/Right": "{Platform_Dpad Right}"
        }
      }
    ]
  }
```

### JSON Format Controller Mappings

For emulators that store controller maps in JSON files, configure assignments using paths:
- `assignment_key_path`: Dot-separated path inside the JSON to write physical controller information (e.g. `"Input.Devices"`).
- `assignment_template`: Formatting template mapping `{sdl_index}` and `{controller_name}`.
- `enabled_key_path`: Dot-separated path inside the JSON to write active status booleans for each player port.
