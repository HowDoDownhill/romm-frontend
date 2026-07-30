# RomM Frontend Context

## Overview
This repository contains the source code for a frontend application interacting with the RomM (Rom Manager) API. It is designed to run locally, providing a user interface to browse, fetch, and download games from a RomM server, and launch them via emulators. The application features full controller and keyboard support.

## Technology Stack
- **Game Engine**: Godot 4.6
- **Language**: C# (.NET 8)
- **Renderer**: GL Compatibility (D3D12 driver on Windows)
- **Physics Engine**: Jolt Physics

## Key Functionality
1. **API Integration**: Fetches game lists, platform metadata, game data, and firmware/BIOS files from a RomM backend server. Supports both OAuth2 token-based and Basic HTTP authentication, as well as API key (Bearer token) authentication.
2. **Download Management**: Downloads game ROM files and firmware/BIOS files via Godot's `HttpRequest` nodes with progress tracking and cancellation support. A dedicated Downloads UI shows per-file progress bars, status labels, and supports controller-driven selection and cancellation.
3. **Firmware/BIOS Sync**: Automatically fetches and downloads firmware (BIOS) files per platform from the RomM API during the loading phase. Firmware files are stored in `bios/<platform_slug>/` and can be selected per-system for emulators that require them. BIOS files are auto-copied into emulator-specific directories when launching.
4. **Emulator Integration**: Configures and launches emulators natively on Windows/Linux/macOS to play downloaded games. Emulators can be automatically downloaded and installed via cross-platform recipes defined in `install_scripts/<emulator>/meta.json`. Supports per-emulator user settings (boolean toggles, dropdowns) that inject dynamic launch arguments. The emulator close hotkey combo is fully configurable (count and button mapping).
5. **Data Caching**: Caches game data and images locally (`games.cache` and `systems.cache`) to reduce API calls and improve load times. Cache can be rebuilt by deleting cache files and re-fetching from the loading screen.
6. **Asset Management**: Background-downloads game art assets (3D box covers, 2D covers, marquees, screenshots) via a concurrent worker queue (2 workers). Assets are stored under `assets/covers_3d/`, `assets/covers_2d/`, `assets/marquees/`, `assets/screenshots/`, and `assets/covers_fallback/`. The `AssetManager` emits signals when downloads complete so the UI can reactively update textures.
7. **UI**: Features a 3D-like vertical carousel for system selection, a game grid with focusable items and cover art, a settings overlay, and a downloads list — all navigable via controller or keyboard. The main scene is a 1920×1080 borderless window with canvas-item stretching.

## Default Emulator Platform Mappings
The application ships with default mappings for 19 platforms:
- **Nintendo**: NGC, Wii (Dolphin), SNES (Snes9x), N64 (Gopher64), NES (Nestopia), GB/GBA (mGBA), NDS (melonDS)
- **PlayStation**: PSX (DuckStation), PS2 (PCSX2), PS3 (RPCS3), PS4 (shadPS4), PSP (PPSSPP)
- **Sega**: Sega 32X, Sega CD, SMS, Genesis (Ares), Dreamcast (Flycast)

## Workflow Rules for AI Agents
- **Review Architecture**: Always review `docs/ARCHITECTURE.md` to understand the Singleton pattern and script structure before proposing architectural changes.
- **Maintain Context**: If a feature changes the core behavior, data models, or external dependencies of the project, this `CONTEXT.md` and `ARCHITECTURE.md` file MUST be updated.
- **Testing on Linux**: Do not reason about Linux behavior from the Windows build. `docs/LINUX-TESTING.md` describes a push-to-real-hardware harness (`tools/linux-test/`) that exports on Windows, deploys over SSH to an Arch machine, launches on its desktop session and screenshots back. Use it for anything platform-specific or GPU-dependent.
