RomM Frontend - Version 1.0.5 Changelog

🐛 Bug Fixes

    Portable Directory Support: Updated the AppUpdater to download files directly into the portable `downloads` folder next to the executable instead of the default Godot user directory.
    Linux Permissions: Added a startup check on Linux to recursively grant read, write, and execute permissions to the entire application directory, permanently resolving issues with emulators failing to launch downloaded games.
    Linux Updater Script: Fixed an issue where the `update.sh` script contained hidden carriage returns, preventing it from executing correctly on Linux.

---

RomM Frontend - Version 1.0.4 Changelog

✨ New Features

    Fuzzy Game Search: You can now type directly while focused on the game list to quickly jump to any game matching your search in the currently selected system.
    Random Game Selector: Added a new "Random Game" option in the start menu that instantly takes you to a randomly selected game from your active game list.

💄 Quality of Life Improvements

    Updater Interaction: The update changelog popup now cleanly hooks into standard footer buttons (A: Select, B: Close) for a smoother navigation experience.

🐛 Bug Fixes

    Linux Downloads: Addressed a permissions issue on Linux where downloaded games wouldn't be readable by emulators, explicitly granting read and write access post-download.
    Update Dialog Text Rendering: Removed raw formatting strings from the GitHub changelog payload that caused strange visual rendering artifacts.

---

RomM Frontend - Version 1.0.3 Changelog

💄 Quality of Life Improvements

    Carousel Focus Border: Added a clear focus border around the currently selected game in the carousel to easily identify your active selection.
    Cleaner Header Interface: The platform header text is now exclusively displayed on the Downloads and Settings pages, providing a cleaner, unobstructed view while browsing your games list.

🐛 Bug Fixes

    Carousel Centering: Fixed an issue where the game list would not properly center the active game upon switching systems until the carousel was manually moved.
    Update Popup Logic: Resolved a bug where the update changelog popup would display on startup even when you were already on the latest version.

---

RomM Frontend - Version 1.0.2 Changelog

✨ New Features

    Integrated In-App Popups: Replaced the standard OS dialog windows with a custom, sleek UI overlay for handling app updates and viewing changelogs natively.

💄 Quality of Life Improvements

    Refresh Games Progress Tracker: The "Refresh Current System" button now displays a dedicated progress overlay, letting you know exactly how many games have been discovered from RomM.
    Graceful Updater Restarts: The updater will now hold on the "Download complete" status for a few seconds before restarting, ensuring you aren't abruptly kicked out of the app without knowing why.

🐛 Bug Fixes

    Improved Logging Privacy: Removed the printing of sensitive configuration details (like your host, username, password, and API key) from the developer console during login.
    Cleaner Console Output: Silenced the 404 Not Found console errors that would unnecessarily print whenever a game lacked artwork on your RomM instance.
