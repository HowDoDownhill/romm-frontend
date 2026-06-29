# RomM Frontend User Guide

Welcome to the **RomM Frontend User Guide**! This guide covers the basics of getting started, including how to log in, map systems to emulators, and define custom emulators and their settings.

---

## 1. Login to the Frontend

Logging into the RomM Frontend requires connecting the application to your RomM backend instance. 

1. Launch the **RomM Frontend** application.
2. On the Login Screen, fill in the following details:
   - **RomM Host**: The URL/IP of your RomM backend server.
   - **RomM Username & Password**: Your standard RomM credentials.
   - **RomM API Key**: Get this from the RomM under "Client API Tokens"
3. Click **Login**. 

> [!NOTE]
> Upon successful authentication, your credentials will be saved locally. The application will attempt to auto-login on subsequent launches.

---

## 2. Add an Emulator to `EmulatorMap.json`

The `EmulatorMap.json` file is used to tell the frontend which emulator to use for a specific gaming system.

1. Navigate to your frontend's `emulators` directory (Created upon first launch).
2. Locate and open `EmulatorMap.json` in a text editor (if it doesn't exist, the application will generate a default one).
3. The file is a simple JSON dictionary mapping the **system slug** to the **emulator slug**. 
4. Add your new mapping in the following format:
   ```json
   {
     "snes": "snes9x",
     "nes": "mesen",
     "psx": "duckstation"
   }
   ```
5. Save the file. The frontend will now launch games from the specified system using the newly mapped emulator.

---

## 3. Add an Emulator via `meta.json`

Emulators in the Frontend are modular and defined using `meta.json` files. This allows the frontend to know how to install, locate, and launch the emulator across different operating systems.

To add a new emulator:
1. Go to the `install_scripts` directory.
2. Create a new folder with your emulator's slug (e.g., `install_scripts/my_emulator/`).
3. Inside this folder, create a file named `meta.json`.
4. Populate the `meta.json` with the necessary metadata.

Here is an example structure:
```json
{
  "name": "My Emulator",
  "executable_name": {
    "windows": "my_emulator.exe",
    "linux": "my_emulator",
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
  "launch_args_with_game": "-game \"{game_path}\" {bios_path}",
  "launch_args_without_game": "",
  "install_recipe": {
    "windows": {
      "type": "github_release",
      "repo": "author/my_emulator",
      "asset_regex": ".*win64.*\\.zip",
      "extract": true
    }
  }
}
```

> [!TIP]
> Use the `{game_path}` and `{bios_path}` placeholders in your launch arguments. The frontend will automatically replace them with the correct absolute paths when launching a game.

---

## 4. Add Emulator Settings via `meta.json`

You can define custom settings for your emulator directly in the `meta.json`. The frontend will present these options to the user, and automatically apply them as either **Command Line Arguments** or by modifying the emulator's **Configuration Files** (supports JSON, INI, CFG, and BML).

To add settings, add a `settings_fields` array to your emulator's `meta.json`:

```json
{
  "name": "My Emulator",
  "settings_fields": [
    {
      "id": "fullscreen",
      "label": "Enable Fullscreen",
      "type": "boolean",
      "default_value_bool": true,
      "launch_arg_true": "-fullscreen",
      "launch_arg_false": "-windowed"
    },
    {
      "id": "internal_resolution",
      "label": "Internal Resolution",
      "type": "dropdown",
      "options": {
        "1x": "1",
        "2x": "2",
        "4x": "4"
      },
      "default_value_string": "1x",
      "config_file_relative_path": "config.ini",
      "config_section": "Graphics",
      "config_key": "ResolutionScale"
    }
  ]
}
```

### Setting Properties:
- `id`: Unique identifier for the setting.
- `label`: The human-readable name shown in the UI.
- `type`: The type of input (`boolean`, `string`, `dropdown`).
- `launch_arg_true` / `launch_arg_false`: Arguments appended to the launch command if a boolean is toggled.
- `launch_arg_format`: For strings or dropdowns, the argument format (e.g., `-res {value}`).
- `config_file_relative_path`, `config_section`, `config_key`: If specified, the frontend will automatically parse the config file (INI, JSON, BML) and update the exact key in the given section.

> [!IMPORTANT]
> When a user changes a setting, the frontend saves their preference in `user_settings.json` within the emulator's directory, ensuring settings persist across sessions without overwriting the defaults.
