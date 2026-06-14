# Architecture Overview

## 1. Modular Backend System
The core design principle is the ability to swap backends seamlessly.
- `IBackend.cs`: An interface defining the contract for all data sources (Authentication, fetching Systems, fetching Games, getting download URLs).
- `RomMAPI.cs`: The concrete implementation of `IBackend` that communicates with the RomM FastAPI backend using standard C# `System.Net.Http.HttpClient`.

## 2. Autoloads (Singletons)
The application relies on several Godot Autoloads to manage global state and persistent services:
- **`RomMAPI`:** The global instance of the RomM API client. It is responsible for all HTTP communication with the RomM server, including handling authentication tokens and CSRF headers.
- **`BackendManager`:** Holds the `ActiveBackend` (an instance of `IBackend`). Other scenes reference this to fetch data without needing to know *which* specific backend (e.g., RomM, Local, SFTP) is currently active.
- **`ConfigManager`:** Handles reading and writing to `user://config.cfg`. It manages local paths (ROMs, Downloads), saved login credentials, and the user's last selected backend preference.
- **`DownloadManager`:** A global queue that manages Godot `HttpRequest` nodes for downloading files. It runs in the background and emits signals (`DownloadProgressUpdated`, `DownloadCompleted`) so that UI components, like the downloads page, can update independently without polling.
- **`DataBus`:** A simple singleton that holds the current in-memory database of `GameSystem` and `Game` objects to avoid redundant API calls and complex node passing.
- **`CacheManager`:** Responsible for serializing and deserializing the `DataBus` state to and from local JSON files (`user://systems.cache`, `user://games.cache`) to provide offline access and faster startup times.

## 3. Data Models (`DataTypes.cs`)
- Data classes (`Game`, `GameSystem`, `RomFile`) map directly to the backend JSON schemas using `[JsonPropertyName]`.
- The `GameSystem` class uses `igdb_slug` to map to local controller icon assets located in `res://assets/platforms/`.

## 4. UI Controllers
- **`LoginScreen.cs`:** Handles backend selection, authentication logic, auto-login via `ConfigManager`, and transitions to the main scene upon a successful login.
- **`LoadingScreen.cs`:** The entry point before the main UI. It attempts to load the system and game data from the `CacheManager` into the `DataBus`. If the cache is empty, it preloads all data from the API and creates the initial cache.
- **`MainScene.cs`:** The primary view. It handles:
  - Controller-based focus navigation using a custom `FocusState` enum.
  - Displaying the system header and updating the game list from the `DataBus` cache.
  - Managing a silent background refresh of the `DataBus` data from the API.
  - Toggling visibility between the game list and the download progress view.
  - Handling ROM downloads, organizing them into a `user://roms/{platform_slug}/` directory structure, and extracting them if they are zip files.
  - Identifying content-types of downloaded cover art to ensure correct image rendering.
- **`DownloadProgressUI.cs` & `DownloadEntryUI.cs`:** Components responsible for visually representing the progress of active file downloads managed by the `DownloadManager`.
