# RomM Frontend User Guide

Welcome to the RomM Frontend! This guide provides instructions on how to use various features of the application, including configuration and extending support for new emulators.

## How to get your API Key from RomM

To connect this frontend to your RomM server using an API key, you need to generate one from the RomM web interface:

1. Open your web browser and navigate to your RomM server's web interface.
2. Log in with your RomM account.
3. Go to your user profile or account settings.
4. Look for the **API Keys** or **Authentication** section.
5. Generate a new API key and copy it.
6. In the RomM Frontend, enter this API key on the login screen to authenticate.

## How to add support for a new emulator

The frontend uses a script-based system to download, install, and configure emulators. To add support for a new emulator, you need to create a metadata file for it.

1. Navigate to the `install_scripts` directory in your application's root folder.
2. Create a new folder with the name of the emulator (e.g., `install_scripts/my_emulator/`).
3. Inside this folder, create a `meta.json` file.
4. Populate `meta.json` with the emulator's details. Here is an example of what it should look like:

```json
{
  "name": "my_emulator",
  "executable_name": {
    "windows": "emulator.exe",
    "macos": "emulator.app/Contents/MacOS/emulator"
  },
  "emulator_dir_name": {
    "windows": "my_emulator",
    "macos": "my_emulator"
  },
  "install_recipe": {
    "windows": {
      "type": "github_release",
      "repo": "developer/my_emulator",
      "asset_regex": ".*windows-x64\\.zip$",
      "extract": true,
      "extract_folder_regex": "my_emulator-*"
    }
  },
  "launch_args_with_game": "\"{rom_path}\"",
  "launch_args_without_game": "",
  "settings_fields": []
}
```

This JSON file tells the frontend how to download the emulator, what the executable is named on different operating systems, and how to launch games with it.

## How to add settings for a specific emulator

You can define custom settings (such as toggling fullscreen mode) that will automatically appear in the frontend's UI and inject specific launch arguments when starting the emulator.

To add settings, modify the `settings_fields` array in the emulator's `meta.json` file.

Example of a fullscreen toggle setting:

```json
  "settings_fields": [
    {
      "id": "fullscreen",
      "label": "Start in Fullscreen",
      "type": "boolean",
      "default_value_bool": true,
      "launch_arg_true": "--fullscreen",
      "launch_arg_false": "--windowed"
    }
  ]
```

- `id`: A unique identifier for the setting.
- `label`: The text that will be displayed in the frontend's settings menu.
- `type`: The data type of the setting (e.g., `"boolean"`).
- `default_value_bool`: The default state of the setting.
- `launch_arg_true`: The command-line argument appended when the setting is enabled.
- `launch_arg_false`: The command-line argument appended when the setting is disabled.

When users toggle this setting in the UI, the frontend will automatically include the corresponding launch argument when launching a game.

## How to select a BIOS

The frontend automatically fetches required BIOS and firmware files from your RomM server during the loading screen and saves them to the local `bios/` directory.

To select a specific BIOS for a system:

1. In the frontend, navigate to the game or system you want to configure.
2. Open the **Start Menu** (usually by pressing Start on your controller or the corresponding keyboard key).
3. Select the **Select BIOS** option.
4. A list of available BIOS/firmware files for that specific system will appear.
5. Highlight and select the desired file. This file will now be set as the preferred firmware for that system.
