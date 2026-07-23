# RomM Frontend

<img width="1967" height="1117" alt="Screenshot 2026-07-18 115411" src="https://github.com/user-attachments/assets/06f85a3e-729c-488f-9f94-ba897407a7c7" />
<img width="2027" height="1163" alt="Screenshot 2026-07-18 115238" src="https://github.com/user-attachments/assets/e3988e20-7f8f-4a1c-8829-b09cdf0a320f" />
<img width="2153" height="1172" alt="Screenshot 2026-07-18 115223" src="https://github.com/user-attachments/assets/5ddc01d5-12b8-4366-95b2-19116cd8794e" />
<img width="2033" height="1178" alt="Screenshot 2026-07-18 115201" src="https://github.com/user-attachments/assets/56203c7a-509a-4d4c-840a-8542b7519f0b" />
<img width="2162" height="1251" alt="Screenshot 2026-07-18 115035" src="https://github.com/user-attachments/assets/304a7db6-79e4-458a-a00f-0d5341a98557" />

A native client for [RomM](https://github.com/rommapp/romm), built in Godot. Browse and
download your library from a RomM server, then play locally — emulators are installed and
configured for you.

## Contents

- [Requirements](#requirements)
- [Login](#login)
- [config.cfg](#configcfg)
- [BIOS and firmware](#bios-and-firmware)
- [Save sync](#save-sync)
- [Emulator mapping](#emulator-mapping)
- [Adding an emulator](#adding-an-emulator)
- [Emulator settings](#emulator-settings)
- [Controllers](#controllers)

## Requirements

- Windows, Linux, or macOS
- .NET 8 runtime
- A running RomM instance

## Login

On first launch you'll be asked for:

- **Host** — full URL of your backend, e.g. `https://romm.example.com` or `http://192.168.1.50:8080`
- **Username and password** — your RomM credentials
- **API key** — generate one in the RomM web panel under your profile → **Client API Tokens**

Credentials are saved to `config.cfg` and reused on later launches. Auth falls back in
order: Bearer token (API key) → OAuth2 → HTTP Basic.

## config.cfg

Lives in the application root. Created on first run.

### `[Paths]`

Directories are created on startup if missing.

| Key | Default | Contents |
|---|---|---|
| `RomsPath` | `roms/` | downloaded ROMs |
| `BiosPath` | `bios/` | firmware and BIOS files |
| `EmulatorsPath` | `emulators/` | installed emulators |
| `DownloadsPath` | `downloads/` | in-progress downloads |
| `InstallScriptsPath` | `install_scripts/` | emulator recipes |
| `ToolsPath` | `tools/` | bundled tools (7-Zip) |
| `AssetsPath` | `assets/` | cached boxart and screenshots |
| `SavesPath` | `saves/` | save-sync staging |

### `[RomM]`

`Host`, `Username`, `Password`, `ApiKey`, and `ValidLoginLastUsed` (whether the last login
succeeded).

> [!WARNING]
> `Password` is stored in plain text. Protect this file accordingly.

### `[UI]`

- `HideGamesWithoutBoxArt` — hide games with no boxart on the server
- `ShowAllSystems` — show systems that contain no games
- `AppTheme` — `Default`, `Rose-pine`, `Gruvbox`, `catppuccin`, `Solarized Dark`,
  `Solarized Light`, `monokai`, `Nord`, `Dracula`

### `[Input]`

- `EmulatorCloseHotkeyCount` — buttons required for the close-emulator combo (default `4`)
- `EmulatorCloseHotkeys` — array of Godot `JoyButton` integers. Default `[9, 10, 6, 4]` =
  **LB + RB + Select + Start** held together.

## BIOS and firmware

Some emulators (PCSX2, DuckStation) need official BIOS files.

1. **Download** — on login, firmware is fetched from RomM into `bios/<platform_slug>/`.
2. **Select** — pick a system or game, open the **Start Menu**, choose **Select BIOS**, and
   pick from the synced files. This sets the default for that system.
3. **Copy** — on launch, the chosen BIOS is copied into the emulator's own bios directory
   (`emulator_bios_path` in `meta.json`).

## Save sync

Saves sync bidirectionally with RomM.

**Before launch** — the frontend fetches saves for the game, downloads anything newer on the
server (folder saves arrive as `.folder.zip` and are extracted with 7-Zip), then snapshots
the local save directory.

**After exit** — the directory is compared against the snapshot; anything added or modified
is archived and uploaded.

The directory tracked is `relative_save_path` in each emulator's `meta.json`.

## Emulator mapping

`emulators/EmulatorMap.json` decides which emulator runs each system. If missing, a default
map is generated:

| Publisher | Systems |
|---|---|
| Nintendo | NGC, Wii (Dolphin) · SNES (snes9x) · N64 (gopher64) · GB, GBC, GBA (mGBA) · NDS (melonDS) · 3DS (Azahar) · NES (ares) |
| PlayStation | PSX (DuckStation) · PS2 (PCSX2) · PS3 (RPCS3) · PS4 (shadPS4) · PSP (PPSSPP) |
| Sega | 32X, Sega CD, SMS, Genesis (ares) · Dreamcast (Flycast) |

Edit the file to override. It maps system slugs to lists of emulator slugs:

```json
{
  "snes": ["snes9x"],
  "ps2": ["pcsx2"],
  "ngc": ["dolphin"]
}
```

The first entry in a list is launched by default. Per-system preferences can also be set in
the Settings menu, which writes to `[PreferredEmulators]` in `config.cfg`.

## Adding an emulator

Each emulator has a directory under `install_scripts/` containing `meta.json`, and
optionally a `default_config/` folder whose contents are copied into the install directory.

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
  "emulator_bios_path": { "windows": "bios", "linux": "bios", "macos": "bios" },
  "relative_save_path": { "default": "saves", "ps2": "memcards" },
  "preserve_on_reinstall": ["config.ini"],
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
  }
}
```

### Fields

- `name` — display name in the UI
- `executable_name` — per-OS executable filename. Use `executable_regex` instead when the
  name contains a version.
- `emulator_dir_name` — per-OS folder name under `emulators/`
- `emulator_bios_path` — where BIOS files are copied inside the emulator directory
- `relative_save_path` — maps system slugs (or `"default"`) to save directories inside the
  emulator folder. Accepts a string or an array. Supports the `{system_slug}` macro.
- `preserve_on_reinstall` — files kept when reinstalling or updating. Use for config the
  user shouldn't lose, such as their own controller mapping.
- `launch_args_with_game` — placeholders `{rom_path}`, `{bios_path}`, `{system}`, and
  `{settings}` (where per-emulator settings flags are injected)
- `launch_args_without_game` — used when launching without a game
- `launch_env` — per-OS environment variables, with the `{emulator_dir}` macro
- `system_flags` — maps system slugs to the fragment substituted for `{system}` in the
  launch arguments. Lets one multi-system emulator pick its core per platform:

  ```json
  "launch_args_with_game": "{system} --no-file-prompt \"{rom_path}\"",
  "system_flags": {
    "nes": "--system \"Famicom\"",
    "genesis": "--system \"Mega Drive\""
  }
  ```

  If a slug has no entry, `{system}` is removed from the arguments.

### Install recipes

`install_recipe` is keyed by OS. All four types accept `extract` and the optional
`extract_folder_regex` (a folder nested inside the archive to pull out).

**`github_release`** — latest release asset
- `repo` — e.g. `"mgba-emu/mgba"`
- `asset_regex` — matches the release asset filename

**`github_tags`** — for projects that tag releases but don't attach assets
- `repo`
- `tag_regex` — matches tag names, e.g. `"^([0-9]{4}[a-z]?)$"`
- `url_template` — download URL with `{version}` substituted from the tag

**`web_scrape`** — for projects hosting builds on their own site
- `list_url` — page to scrape
- `version_regex` — extracts a version from the page
- `url_template` — download URL with `{version}` substituted
- `link_regex` — alternative to the above; matches download links directly

**`direct_url`** — a fixed link
- `url`

## Emulator settings

`settings_fields` in `meta.json` renders as controls in the Settings UI. Changes are applied
either as launch flags or by editing the emulator's config file.

```json
{
  "id": "internal_res",
  "label": "Internal Resolution",
  "type": "dropdown",
  "default_value_string": "1",
  "config_file_relative_path": "config.ini",
  "config_section": "Graphics",
  "config_key": "ResolutionScale",
  "options": { "Native (1x)": "1", "2x Resolution": "2", "4x Resolution": "4" }
}
```

- `id` — unique identifier
- `label` — text shown in the UI
- `type` — `boolean`, `dropdown`, or `hidden`
- `default_value_bool` / `default_value_string` — string defaults support macros such as
  `{game_id}`

**As launch arguments**
- Boolean: `launch_arg_true` / `launch_arg_false`
- Dropdown: `launch_arg_format`, e.g. `-res {value}`

**As config file edits**
- `config_file_relative_path` — path relative to the emulator directory. Accepts an
  OS-keyed object when the path differs per platform.
- `config_section` — section name; nest with slashes or dots for JSON/BML (`"EmuCore/GS"`)
- `config_key` — key to write

Supported formats: **INI/CFG** (also `.toml`), **JSON**, and **BML**.

## Controllers

An Xbox-style pad works out of the box on every supported system. Most emulators get a
controller config from `install_scripts/<emulator>/default_config/`, copied into the
install directory on setup; Flycast and PPSSPP need none, as they map SDL gamepads
themselves.

Two exceptions:

- **Wii** uses real Wii Remotes over Bluetooth. Pair by pressing 1+2 or the red sync button
  while a game is running. An Xbox pad does nothing here.
- **Dolphin** identifies its pad by SDL device name. The shipped value matches an Xbox
  Series X controller; other models show as disconnected until selected once in Dolphin's
  own controller settings.

Non-Xbox controllers (DualShock, DualSense, Switch Pro) are not yet handled.

### `controller_config` (currently disabled)

`meta.json` also supports a `controller_config` block that rewrites an emulator's bindings
at launch from the connected controllers.

> [!IMPORTANT]
> This is **disabled** in current builds (`SuspendControllerMapping` in
> `EmulatorManager.cs`). Its `{controller_name}` macro resolves to Godot's name for a pad,
> which does not match what an emulator's SDL calls the same device. Shipped static configs
> are used instead.

- `max_controllers` — number of ports to configure
- `config_file_relative_path` — the emulator's controller config
- `format` — `ini`, `json`, or `bml`
- `platform_layout` — console buttons to canonical SDL inputs, e.g. `"A"` → `"FaceSouth"`
- `sdl_string_map` — canonical SDL inputs to the emulator's own syntax, e.g. `"FaceSouth"`
  → `` "`Button S`" ``
- `controller_sections` — for INI formats:
  - `section_template` — e.g. `"GCPad{port}"`
  - `port_start` — first port index
  - `device_key` / `device_template` / `device_disconnected` — the device line, e.g.
    `"SDL/{sdl_index}/{controller_name}"`
  - `type_key` / `type_connected` / `type_disconnected` — controller type per port
  - `mappings` — emulator input keys to `{Platform_<Button>}` macros
  - `static_values` — extra literal keys written to the section

For JSON formats: `assignment_key_path`, `assignment_template`, and `enabled_key_path` —
dot-separated paths for writing device assignment and per-port enabled flags.
