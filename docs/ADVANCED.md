# Advanced configuration

For players who want to edit files by hand, and for anyone adding an emulator. Everything
here can be left alone in normal use.

- [config.cfg](#configcfg)
- [Choosing emulators per system](#choosing-emulators-per-system)
- [Adding an emulator](#adding-an-emulator)
- [Emulator settings](#emulator-settings)
- [Controller bindings](#controller-bindings)
- [Custom themes](#custom-themes)

## config.cfg

Created in the app folder on first run. Close the app before editing it.

### `[Paths]`

| Key | Default | Contents |
|---|---|---|
| `RomsPath` | `roms/` | Downloaded games |
| `BiosPath` | `bios/` | BIOS and firmware |
| `EmulatorsPath` | `emulators/` | Installed emulators |
| `SavesPath` | `saves/` | All saves, one folder per emulator |

These four can point anywhere, for example another drive. Downloads, install scripts, tools and
cached art always stay in the app folder.

Each emulator's own save folder is a link into `SavesPath`, so the emulator writes where it
always does and the data lands in the store.

### `[RomM]`

`Host`, `Username`, `Password`, `ApiKey`, and `ValidLoginLastUsed`.

> [!WARNING]
> `Password` is stored in plain text.

### `[UI]`

| Key | Values |
|---|---|
| `GameListView` | `Carousel`, `Grid`, `List` |
| `AppTheme` | Any theme listed in the app, `Match System`, or one from `themes.json` |
| `AppBackground` | `Flow`, `Mesh`, `Silk`, `Horizon`, `Blobs`, `Waves`, `Aurora`, `Bokeh`, `Grid`, `Gradient`, `Solid` |
| `InterfaceSize` | `Auto`, `85%`, `100%`, `115%`, `130%`, `150%` |
| `HideGamesWithoutBoxArt` | `true` or `false` |
| `ShowAllSystems` | `true` shows systems with no games |

### `[Input]`

| Key | Meaning |
|---|---|
| `EmulatorCloseHotkeys` | Godot `JoyButton` numbers held together to close an emulator. Default `[4]`, the View button. |
| `EmulatorCloseHotkeyCount` | How many of them must be held |
| `EmulatorCloseHoldSeconds` | How long to hold them. Default `2.0`. |

## Choosing emulators per system

The easiest way is each system's page in **Settings**. That choice is stored under
`[PreferredEmulators]` in `config.cfg`.

`emulators/EmulatorMap.json` lists which emulators can run each system, first entry as the
default. It is created on first run:

| Systems | Default | Alternative |
|---|---|---|
| NES, SNES, Game Boy, GBC, GBA, DS, PS1, Genesis, Master System, Sega CD, 32X | RetroArch | the standalone emulator |
| Saturn, Arcade, Neo Geo, Atari, PC Engine, MSX, C64, Amiga, WonderSwan, 3DO and more | RetroArch | |
| GameCube, Wii | Dolphin | |
| N64 | gopher64 | RetroArch |
| 3DS | Azahar | |
| PS2 / PS3 / PS4 | PCSX2 / RPCS3 / shadPS4 | |
| PSP | PPSSPP | RetroArch |
| Dreamcast | Flycast | RetroArch |

Keys are RomM platform slugs and values are emulator folder names under `install_scripts/`:

```json
{
  "snes": ["retroarch", "snes9x"],
  "ps2": ["pcsx2"]
}
```

## Adding an emulator

Each emulator is a folder under `install_scripts/` with a `meta.json`, and optionally a
`default_config/` folder copied into the install on setup.

```json
{
  "name": "My Emulator",
  "executable_name": {
    "windows": "my_emulator.exe",
    "linux": "my_emulator-x86_64.AppImage"
  },
  "emulator_dir_name": { "windows": "my_emulator_win", "linux": "my_emulator_linux" },
  "emulator_bios_path": { "windows": "bios", "linux": "bios" },
  "relative_save_path": { "default": "saves", "ps2": "memcards" },
  "preserve_on_reinstall": ["config.ini"],
  "launch_args_with_game": "-fullscreen \"{rom_path}\"",
  "launch_args_without_game": "",
  "install_recipe": {
    "windows": {
      "type": "github_release",
      "repo": "developer/my_emulator",
      "asset_regex": ".*windows-x64\\.zip$",
      "extract": true
    },
    "linux": {
      "type": "direct_url",
      "url": "https://example.com/my_emulator.AppImage",
      "extract": false
    }
  }
}
```

### Fields

| Field | Meaning |
|---|---|
| `name` | Name shown in the app |
| `executable_name` | Executable per OS. Use `executable_regex` when the name contains a version. |
| `emulator_dir_name` | Install folder per OS, under `emulators/` |
| `emulator_bios_path` | Where the chosen BIOS is copied inside the install |
| `relative_save_path` | Save folder inside the install, per system slug or `default`. A string or an array; `{system_slug}` is replaced. |
| `preserve_on_reinstall` | Files kept when the emulator is reinstalled or updated |
| `launch_args_with_game` | Arguments with `{rom_path}`, `{bios_path}`, `{system}` and `{settings}` |
| `launch_args_without_game` | Arguments when opened from the start menu without a game |
| `launch_env` | Environment variables per OS; `{emulator_dir}` is replaced |
| `system_flags` | Text substituted for `{system}` per system slug, so one emulator can pick a core or machine per system. A missing slug removes `{system}`. |

### Install recipes

`install_recipe` is keyed by OS. Every type accepts `extract` and an optional
`extract_folder_regex` naming a folder inside the archive to take.

| Type | Fields | Use for |
|---|---|---|
| `github_release` | `repo`, `asset_regex` | Projects attaching builds to GitHub releases |
| `github_tags` | `repo`, `tag_regex`, `url_template` with `{version}` | Projects that tag releases without assets |
| `web_scrape` | `list_url`, then `version_regex` with `url_template`, or `link_regex` | Projects hosting builds on their own site |
| `direct_url` | `url` | A fixed link |

## Emulator settings

`settings_fields` in `meta.json` become controls on the system's settings page. Each change is
applied as a launch argument or written into the emulator's config file.

```json
{
  "id": "internal_res",
  "label": "Internal Resolution",
  "type": "dropdown",
  "default_value_string": "1",
  "config_file_relative_path": "config.ini",
  "config_section": "Graphics",
  "config_key": "ResolutionScale",
  "options": { "Native (1x)": "1", "2x": "2", "4x": "4" }
}
```

| Field | Meaning |
|---|---|
| `id`, `label` | Identifier and on-screen name |
| `type` | `boolean`, `dropdown` or `hidden` |
| `default_value_bool`, `default_value_string` | Defaults; strings accept macros such as `{game_id}` |
| `launch_arg_true`, `launch_arg_false` | Arguments for a boolean |
| `launch_arg_format` | Argument for a dropdown, such as `-res {value}` |
| `config_file_relative_path` | Config file inside the install; may be keyed by OS |
| `config_section`, `config_key` | Where to write; nest sections with `/` or `.` for JSON and BML |
| `operating_systems` | Only show the setting on these OSes |
| `apply_on_launch` | Rewrite the value before every launch, in case the emulator changed it |

Supported config formats: INI, CFG and TOML, JSON, and BML.

## Controller bindings

Most emulators ship a controller config in `install_scripts/<emulator>/default_config/`. With
Automatic Controller Mapping on (Windows), the app also points each emulator at its virtual
controller on every launch. Dolphin identifies pads by name: if yours shows as disconnected,
select it once in Dolphin's own controller settings.

## Custom themes

Add themes to `themes.json` in the app folder. Each one needs four colours: the base, two
accents, and the panel tint with its transparency:

```json
{
  "My Theme": { "bg": "#05070d", "primary": "#13215a", "secondary": "#0a4a6e", "panel": "#080c1a8c" }
}
```
