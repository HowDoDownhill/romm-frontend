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
