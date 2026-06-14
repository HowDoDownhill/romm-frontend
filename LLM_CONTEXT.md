# LLM Context for RomM Frontend

## Project Overview
This project is a Godot 4 (C#) application acting as a frontend for managing and launching video game ROMs. It is designed to be modular and currently supports the [RomM](https://github.com/rommapp/romm) backend via its REST API, with plans to support Local and SFTP directories in the future.

## Current State
- **Phase 1 & 2 Completed:** Core data structures, modular backend interface (`IBackend`), and User Authentication are implemented. The app successfully logs into a RomM server using an API Key (Bearer token) or Username/Password (OAuth2) and saves credentials to a local config file.
- **Phase 3 (In Progress):** Main application UI is built. It successfully fetches and displays platforms (systems) using local controller icons (matched via `igdb_slug`), and displays games for the selected platform in a list. The UI uses controller-friendly focus states (Bumpers to change system, D-pad to navigate lists).
- **Data Management (New):** A global `DataBus` singleton holds all games and systems in memory. A `CacheManager` persists this data to local JSON files (`user://systems.cache`, `user://games.cache`) for immediate offline loading. A `LoadingScreen` handles pre-fetching data from the API into the cache before reaching the main scene. The `MainScene` periodically performs a silent background refresh of the cache.
- **Downloads:** A `DownloadManager` singleton uses Godot's `HttpRequest` to handle file downloads. It saves files to a user-configurable temporary `DownloadsPath` (via `ConfigManager`), extracts zip files using `System.IO.Compression.ZipFile`, and moves the resulting ROMs to `LocalRomsPath/{platform_slug}/`. A `DownloadProgressUI` visually tracks these background downloads.

## Important Notes for AI Assistants
- **Read `CODING_GUIDELINES.md`:** This project has strict coding rules. Most notably, **NO COMMENTS** are allowed in C# code.
- **Read `ARCHITECTURE.md`:** Familiarize yourself with the singleton managers (`DataBus`, `CacheManager`, `ConfigManager`, `DownloadManager`, `BackendManager`) before modifying logic.
- **RomM API specifics:** 
  - Platforms use `/api/platforms`.
  - Games use `/api/roms?platform_ids={id}`.
  - Downloading uses `/api/roms/download?rom_ids={id}` (returns a .zip file).
