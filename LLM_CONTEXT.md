# LLM Context for RomM Frontend

## Project Overview
This project is a Godot 4 (C#) application acting as a frontend for managing and launching video game ROMs. It is designed to be modular and currently supports the [RomM](https://github.com/rommapp/romm) backend via its REST API, with plans to support Local and SFTP directories in the future.

## Current State
- **Phase 1 & 2 Completed:** Core data structures, modular backend interface (`IBackend`), and User Authentication are implemented. The app successfully logs into a RomM server using an API Key (Bearer token) or Username/Password (OAuth2) and saves credentials to a local config file.
- **Phase 3 (In Progress):** Main application UI is built. It successfully fetches and displays platforms (systems) in a carousel using local controller icons (matched via `igdb_slug`), and displays games for the selected platform in a list.
- **Downloads:** A `DownloadManager` is partially implemented using Godot's `HttpRequest` to download ROMs, with a decoupled `DownloadProgressUI` listening to signals.

## Important Notes for AI Assistants
- **Read `CODING_GUIDELINES.md`:** This project has strict coding rules, particularly regarding comments and UI node references.
- **Read `ARCHITECTURE.md`:** Familiarize yourself with the singleton managers and the `IBackend` abstraction layer before proposing architectural changes.
- **RomM API specifics:** 
  - Login uses `/api/token` (OAuth2 password grant) or direct API key validation via `/api/platforms`. 
  - Platforms use `/api/platforms`.
  - Games use `/api/roms?platform_id={id}`.
