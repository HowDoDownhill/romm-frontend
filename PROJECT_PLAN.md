# Project Plan: RomM Frontend

## Phase 1: Core Foundation & Modular Backend (Completed)
1. **Define a Backend Interface (`IBackend.cs`):** Create a C# interface that defines a common contract for all backend modules (authentication, data fetching).
2. **Implement RomM Backend (`RommAPI.cs`):** Refactor `RommAPI` to implement the `IBackend` interface.
3. **Define Data Structures (`DataTypes.cs`):** Define shared C# classes for `User`, `Game`, `System`, `Library`, etc.

## Phase 2: User Authentication (Completed)
1. **Build Login Scene (`LoginScreen.cs`):** Design the UI with a backend selector (RomM, Local, SFTP). Implement logic to instantiate and use the selected backend.

## Phase 3: Main Application UI, State & Input (Completed)
1. **Data State Management:** Implement `DataBus` for in-memory storage and `CacheManager` for persistent offline caching of the entire library.
2. **Design Main Scene:** Create the main scene layout (System Header, Game list, Details panel).
3. **Implement UI Components:** Build the UI elements for displaying the data, loading local controller icons.
4. **Handle Multiple Controllers:** Implement robust input handling using a `FocusState` machine, allowing bumpers to swap systems and d-pad to navigate lists and buttons.

## Phase 4: Feature Implementation (RomM) (In Progress)
1. **Data Display:** Fetch and populate the UI with data from the `RommAPI` backend.
2. **Downloads:** Implement a background `DownloadManager`. Download zip files via API, extract them, and organize into local `platform_slug` directories. Implement a UI view for active downloads.
3. **Search and Filtering:** Add a search bar to filter the game list.

## Phase 5: Configuration & Settings
1. **Application Settings Menu:** Create a UI for configuring global application settings.
2. **Local Paths Configuration:** Implement a configuration file system (`ConfigManager.cs`) to store and retrieve user preferences, specifically the base paths for locally stored ROMs and Downloads.

## Phase 6: Future Backend Integration & Scraping
1. **Implement Local & SFTP Backends:** Create `LocalBackend.cs` and `SftpBackend.cs`, implementing the `IBackend` interface.
2. **Implement a Scraping Service:** Create a service to fetch metadata and artwork for local and SFTP ROMs.

## Phase 7: Emulator Management & Launching
1. **Automated Install Scripts:** Develop scripts to automate the download and installation of popular emulators.
2. **Emulator Configuration UI:** Allow users to associate emulators with specific systems.
3. **Configurable Launch Arguments:** Provide UI checkboxes and text fields to toggle and configure specific launch arguments for emulators.
4. **Seamless Launching (Hide until Fullscreen):** Implement process management to launch the emulator hidden and only show its window once it successfully enters fullscreen mode (using OS-specific window management APIs).
5. **In-Game Overlay / Exit Hook:** Create an overlay or global hotkey listener to easily close the currently open emulator and safely return to the frontend.

## Phase 8: Polishing and Deployment
1. **Error Handling & UX:** Implement global error handling, loading indicators, and overall UX polish.
2. **Testing & Build:** Test all features across target platforms and prepare for release.
