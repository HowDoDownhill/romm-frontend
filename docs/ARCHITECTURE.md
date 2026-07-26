# RomM Frontend Architecture

## Global Singletons (AutoLoads)
The core logic of the application relies on Godot's AutoLoad (Singleton) system, making these services available globally. All singletons register themselves with `AppInstance` in their `_Ready()` method for centralized cross-referencing.

- **AppInstance**: The central service locator. Holds direct references to every other singleton (`configManager`, `rommApi`, `downloadManager`, `cacheManager`, `emulatorManager`, `dataBus`, `assetManager`). All singletons self-register via `appInstance.<field> = this` during `_Ready()`.
- **ConfigManager**: Manages reading and writing application configurations from `config.cfg` using Godot's `ConfigFile` API. Stores paths (`RomsPath`, `BiosPath`, `EmulatorsPath`, `DownloadsPath`, `InstallScriptsPath`, `ToolsPath`, `AssetsPath`), RomM server credentials (`Host`, `Username`, `Password`, `ApiKey`), UI preferences (`HideGamesWithoutBoxArt`), and input settings (`EmulatorCloseHotkeyCount`, `EmulatorCloseHotkeys`). Also ensures required subdirectories exist on startup and applies custom input maps for the emulator close hotkey combo.
- **RomMAPI**: The primary network service handling HTTP requests to the RomM backend via `System.Net.Http.HttpClient`. Supports three auth methods: API key (Bearer), OAuth2 token, and Basic auth (cascading fallback). Provides endpoints for: `GetSystemsAsync()`, `GetGamesAsync()` (with pagination), `GetFirmwareAsync()`, `GetFirmwareByIdAsync()`, `GetRomDownloadUrl()`, `GetFirmwareDownloadUrl()`, and `DownloadAssetAsync()`.
- **CacheManager**: Handles serializing and deserializing data to/from disk (`games.cache` / `systems.cache`) using `System.Text.Json`. Provides `SaveCache()`, `LoadCache()`, and `RebuildGameCache()` (which deletes cache files and redirects to the loading screen).
- **DataBus**: A shared in-memory data store (not an event bus). Holds the global `systems` list (`List<GameSystem>`) and `gameCache` dictionary (`Dictionary<int, List<Game>>`) that are populated during loading and consumed by the main UI.
- **DownloadManager**: Manages game ROM downloads via Godot's `HttpRequest` nodes. Supports multiple concurrent downloads with progress tracking (emits `DownloadProgressUpdated` signal each frame for active downloads) and completion notification (`DownloadCompleted` signal). Provides `DownloadFile()`, `IsDownloading()`, and `CancelDownload()` methods.
- **AssetManager**: Manages background queuing and concurrent downloading of game art assets (3D covers, 2D covers, marquees, screenshots) from the RomM server to local cache. Uses a `ConcurrentQueue` with 2 worker tasks. Deduplicates requests via a `HashSet<int>` of previously requested game IDs. Emits `AssetDownloaded` signal (deferred to main thread) when an asset completes.
- **EmulatorManager**: Responsible for launching emulators, managing the platform→emulator mapping (`EmulatorMap.json`), and installing missing emulators via `UniversalInstaller`. Key features:
  - Reads `meta.json` recipes from `install_scripts/` for each emulator, which define per-OS executable names, directory names, BIOS paths, launch arguments (with/without game), install recipes, and per-emulator settings fields.
  - Supports dynamic launch argument injection from per-emulator `user_settings.json` files (boolean toggles, dropdown/string options).
  - Handles BIOS path substitution in launch arguments and auto-copies BIOS files into emulator-specific directories.
  - Tracks active emulator process for close-on-hotkey functionality.

## File Hierarchy
- **`addons/`**: Third-party Godot addons and plugins (`SmoothScroll`, `controller_icons`, `tnowe_extra_controls`).
- **`assets/`**: Game art cache organized by type: `covers_3d/`, `covers_2d/`, `marquees/`, `screenshots/`, `covers_fallback/`, `platforms/` (system icons), and `background/`.
- **`bios/`**: Firmware/BIOS files organized by platform slug (e.g., `bios/psx/`, `bios/dc/`). Auto-downloaded from the RomM firmware API during loading.
- **`build/`**: Build output directory.
- **`downloads/`**: Temporary directory for in-progress downloads (emulator archives, etc.).
- **`emulators/`**: Installed emulator executables, per-emulator `user_settings.json` files, and mapping files (`EmulatorMap.json`, `ExecutableMap.json`).
- **`install_scripts/`**: Contains per-emulator directories (e.g., `dolphin/`, `snes9x/`, `mGBA/`, `pcsx2/`, `flycast/`, `gopher64/`) each with a `meta.json` recipe defining download URLs (direct or GitHub release), extraction settings, OS-specific executable names, launch arguments, BIOS paths, and settings fields.
- **`roms/`**: The default download path for game ROM files, organized by platform slug.
- **`tools/`**: Bundled utilities (e.g., `7zip/` for archive extraction during emulator installation).
- **`scenes/`**: Godot `.tscn` scene files grouped by functional UI area:
  - `login/` — Login screen and loading screen
  - `main_scene.tscn` — The primary game browsing UI
  - `games_list/` — Game listing components
  - `downloads_list/` — Download progress UI
  - `header/` — Top navigation bar
  - `footer/` — Bottom bar
  - `update/` — (Reserved for future update functionality)
- **`scripts/`**: The C# source code powering the application:
  - `scripts/autoloads/` — Global singleton logic (8 autoloads).
  - `scripts/data/` — Data models (`DataTypes.cs` defining `Game`, `GameSystem`, `RomFile`, `Firmware`, `GameResponse`, `User`).
  - `scripts/downloads/` — Download UI controllers (`DownloadEntryUI.cs`, `DownloadProgressUI.cs`).
  - `scripts/login/` — Login and loading screen controllers (`LoginScreen.cs`, `LoadingScreen.cs`). The `LoadingScreen` handles cache-or-fetch logic, firmware sync, and firmware-to-system assignment.
  - `scripts/main/` — Main scene controller (`MainScene.cs`) and game grid items (`GameGridItem.cs`).
  - `scripts/scenes/` — Scene-specific controllers (currently contains `update/` placeholder).
  - `scripts/ui/` — Custom UI components (`VerticalCarousel.cs` for 3D-like selection menus).
  - `scripts/utils/` — Utility classes (`UniversalInstaller.cs` for downloading and extracting emulators via GitHub API or direct URLs, with 7zip-based archive extraction).

## Core Data Models (`scripts/data/DataTypes.cs`)
- **`RomFile`**: Represents a single ROM file (`Id`, `FileName`, `FullPath`).
- **`GameSystem`**: Represents a gaming platform (`Id`, `Name`, `Slug`, `IgdbSlug`, `LogoUrl`, `RomCount`, `MappedEmulator`, `PrefferedFirmware`, `AvailableFirmwares`).
- **`Game`**: Represents a game ROM entry (`Id`, `Name`, `Path`, `Description`, `CoverArtUrl`, `PathCoverLarge`, `PathCoverSmall`, `PathCover3d`, `PlatformId`, `PlatformSlug`, `PlatformDisplayName`, `Files`, `LocalFilename`, `System` [navigation property]).
- **`Firmware`**: Represents a BIOS/firmware file (`Id`, `FileName`, `FileNameNoTags`, `FileNameNoExt`, `FileExtension`, `FilePath`, `FileSizeBytes`, `FullPath`, `IsVerified`, `CrcHash`, `Md5Hash`, `Sha1Hash`, `MissingFromFs`, `CreatedAt`, `UpdatedAt`).
- **`GameResponse`**: Paginated API response wrapper (`Games`, `Total`, `Limit`, `Offset`).
- **`User`**: Simple user model (`Username`, `Token`).

## Emulator Meta Configuration (`install_scripts/<emulator>/meta.json`)
Each emulator is described by an `EmulatorMeta` object:
- `name` — Display name
- `executable_name` — Per-OS map of relative executable paths
- `emulator_dir_name` — Per-OS map of install directory names
- `emulator_bios_path` — Per-OS map of relative BIOS directory paths within the emulator
- `launch_args_with_game` — Launch argument template with `{rom_path}` and `{bios_path}` placeholders
- `launch_args_without_game` — Launch argument template for standalone emulator launch
- `install_recipe` — Per-OS install recipes (`type`: `github_release` or `direct_url`, `url`, `repo`, `asset_regex`, `extract`, `extract_folder_regex`)
- `settings_fields` — List of per-emulator settings (boolean toggles, dropdowns) with `launch_arg_true`, `launch_arg_false`, `launch_arg_format` for dynamic argument injection

## Rendering and Inputs
The application renders at 1920×1080 in a borderless window with `canvas_items` stretch mode, using GL Compatibility renderer (D3D12 on Windows). The input map defines Joypad and Keyboard actions:
- **Navigation**: `MoveUp`, `MoveDown`, `CylceSystemUp`, `CycleSystemDown` (note: `CylceSystemUp` has a typo in the project)
- **Actions**: `Select`, `Back`, `ToggleSettings`, `ToggleDownloadsPage`, `DeleteGame`, `CancelDownload`, `ToggleInstalled`
- **Dynamic**: `CloseKey1`–`CloseKey4` (configurable emulator close hotkey combo, managed by `ConfigManager.ApplyInputMap()`)

Full controller support is provided via the `controller_icons` addon.
