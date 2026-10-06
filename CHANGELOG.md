RomM Frontend - Version 1.0.13 Changelog

✨ New Features

    Central Save Store: Every emulator's saves now live together in one `saves` folder instead of being scattered inside each emulator's install directory. Each emulator's save folder becomes a link into that store, so the emulator writes exactly where it always did. Reinstalling or uninstalling an emulator no longer risks your saves, and one folder is now the whole backup.
    RetroArch Support: RetroArch is now an installable emulator covering 41 systems, 27 of which had no emulator at all before, including Saturn, Arcade, Neo Geo, Atari 2600/5200/7800/Lynx/Jaguar, PC Engine, MSX, Commodore 64, Amiga, WonderSwan, 3DO, Vectrex, Intellivision, ColecoVision, Game Gear and SG-1000.
    Per-System Core Selection: Systems running under RetroArch gain a Core dropdown in their settings, so you can pick Mesen or Nestopia for NES, snes9x or bsnes for SNES, and so on, independently for each system.
    On-Demand Cores: Cores download the first time you launch a system rather than all at once, so installing RetroArch stays small and changing core never needs a reinstall.

💄 Quality of Life Improvements

    RetroArch by Default for Older Systems: Generations 1-5 and all cartridge systems now default to RetroArch, matching what RomM's in-browser player supports. Existing setups keep whichever emulator they already use.
    Save Warning When Switching Emulator: Each system's settings warn you when another emulator already holds saves for it, since saves stay with the emulator that wrote them.
    Clearer Install Prompts: Buttons now name the emulator ("Install RetroArch") and show "Install Core" when only a core is missing. The start menu no longer offers to launch an emulator that is not installed, and offers to install it instead.
    Newest Emulator Versions First: Emulator version lists that are scraped from a website are now sorted newest first rather than in page order.

🐛 Bug Fixes

    Unreachable Settings for Some Platforms: Genesis and Master System had no working settings page at all, because their names contain a slash and the panel could never be found again after being built.
    Saves Uploaded in Bulk: Playing one Genesis game uploaded every Mega Drive save you owned attached to that one game. Saves are now tracked per game.
    BIOS Uploaded as Save Data: Flycast was uploading 6.6 MB of Dreamcast and Naomi BIOS, a shader cache and cached boxart to the server on every sync. Only VMU and NVRAM files sync now.
    3DS System Files Uploaded: Azahar was uploading the entire emulated 3DS NAND and SD card under a single game. 3DS saves now sync per title.
    Saves Mixed Between Emulators: With two emulators available for one system, sync could pull one emulator's save into the other's folder. Saves are now tagged with the emulator that wrote them.
    Installing Over a Running Emulator: Reinstalling while an emulator was open deleted part of the install and then failed on files still in use. Installing is now blocked while an emulator is running.
    Editor Load Errors: The project no longer tries to import installed emulator files, which was producing thousands of shader and asset errors.
    Missing Source File: A source file was excluded by an over-broad ignore rule, so a fresh clone of the project did not compile.

---

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
