# RomM Frontend

<img width="1969" height="1182" alt="image" src="https://github.com/user-attachments/assets/d52d23c1-fb2a-48d0-acf7-80fa05132720" />
<img width="1983" height="1188" alt="image" src="https://github.com/user-attachments/assets/acdc1791-f39a-4e2c-9122-c8dd63f907d7" />
<img width="1992" height="1210" alt="image" src="https://github.com/user-attachments/assets/2dd19953-6748-4a97-837f-0bb601603922" />

Welcome to the **RomM Frontend**! This application is a native client designed to connect to your [RomM (Rom Manager)](https://github.com/rommapp/romm) backend instance. It allows you to browse, search, and download your retro game library from your RomM backend and play games locally using automatically installed and configured emulators.

---

## 📖 Complete Documentation

For the full detailed user guide, including deep dives into advanced configuration options, custom emulator metadata configurations, automated controller mapping setups, and save game synchronization, see the:

👉 **[RomM Frontend Complete User Guide](file:///e:/Projects/romm-frontend/docs/USER_GUIDE.md)**

---

## Quick Start Guide

### 1. Connecting & Logging In
Logging into the Frontend requires connecting the application to your RomM backend instance.

1. Launch the **RomM Frontend**.
2. On the Login Screen, fill in the following details:
   - **RomM Host**: The URL/IP of your RomM backend server (e.g., `https://romm.example.com`).
   - **RomM Username & Password**: Your standard RomM credentials.
   - **RomM API Key**: Get this from your RomM profile page under "Client API Tokens".
3. Click **Login**.

> [!NOTE]
> Upon successful authentication, your credentials will be saved locally in `config.cfg`. The application will attempt to auto-login on subsequent launches.

### 2. Default System-to-Emulator Mappings
The frontend maps gaming systems to emulators using a local JSON map. When launching a game for the first time, it will automatically download, install, and configure the associated emulator if it isn't already installed.

To change which emulator launches for a specific system:
1. Navigate to the `emulators/` directory in the application root folder (created after first launch).
2. Locate and open `EmulatorMap.json` in a text editor.
3. Modify the mapped emulator slugs:
   ```json
   {
     "snes": ["snes9x"],
     "nes": ["mesen"],
     "psx": ["duckstation"]
   }
   ```
4. Save the file. The frontend will now launch games using the newly mapped emulator.

### 3. Modifying Settings & Controller Profiles
Custom emulator behaviors, resolution scales, and button layouts can be fully customized using `meta.json` configurations.
For details on how to write custom recipes and configure joypads/controllers, refer to the **[Complete User Guide](file:///e:/Projects/romm-frontend/docs/USER_GUIDE.md)**.
