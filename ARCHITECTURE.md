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

## 3. Data Models (`DataTypes.cs`)
- Data classes (`Game`, `GameSystem`, `RomFile`) map directly to the backend JSON schemas using `[JsonPropertyName]`.
- The `GameSystem` class uses `igdb_slug` to map to local controller icon assets located in `res://assets/platforms/`.

## 4. UI Controllers
- **`LoginScreen.cs`:** Handles backend selection, authentication logic, auto-login via `ConfigManager`, and transitions to the main scene upon a successful login.
- **`MainScene.cs`:** The primary view. It handles:
  - Chunked, paginated loading of games using `CancellationToken`s to prevent UI freezes.
  - Controller-based focus navigation using a custom `FocusState` enum.
  - Dynamic population of the system carousel using `TextureButton` nodes and local assets.
  - Toggling visibility between the game list and the download progress view.
