# Release Testing Checklist

Run this before tagging a version. It is deliberately manual — almost everything that has broken in
this project broke at a boundary a unit test would not have crossed: a real emulator process, a real
filesystem, a real controller, a real OS difference.

**Rules**

- Run the whole list on the platform you developed on, and at minimum sections 0, 4, 5, 9 and 11 on
  the other one. Most defects here have been Windows-only or Linux-only.
- Test the **exported build**, not just the editor. `ApplicationRootDirectory` resolves differently
  in each (`ConfigManager.DetermineApplicationRootDirectory`), and `install_scripts/` and `tools/`
  are copied next to the executable only by `build.sh`.
- When a feature is added, add its row to the relevant section in the same commit. A feature with no
  row here is untested by definition.
- A failure means: record the emulator, the system slug, the platform, and the console output. Godot
  prints emulator resolution and installer errors via `GD.PrintErr` — read them before filing.

---

## 0. Build gates

- [ ] `dotnet build` succeeds with 0 warnings, 0 errors.
- [ ] `./build.sh` (or `build.bat`) exports **both** Windows and Linux without errors.
- [ ] `build/<platform>/` contains the binary, the `.pck`, the `data_romm-frontend_*` runtime folder,
      `install_scripts/` and `tools/`.
- [ ] Run `build.sh` a second time without deleting `build/` — no nested `install_scripts/install_scripts`.
- [ ] Exported binary launches and reaches the login screen.

## 1. First run on a clean profile

Delete (or move aside) `config.cfg`, `games.cache`, `systems.cache`, `emulators/EmulatorMap.json`.

- [ ] Login screen accepts host + credentials, and separately accepts an API key.
- [ ] Loading screen fetches systems, games and firmware without an unhandled exception.
- [ ] Required directories are created (`roms/`, `bios/`, `emulators/`, `saves/`, `downloads/`,
      `assets/*`) and each non-project directory gets a `.gdignore`.
- [ ] `config.cfg` is written and reloads correctly on second launch.
- [ ] Upgrade path: with an existing `EmulatorMap.json`, newly added default platforms appear and
      existing mappings are **not** reordered (`MergeMissingDefaultMappings`).

## 2. Library and browsing

- [ ] System carousel populates; details panel populates; covers load.
- [ ] Cover art, marquees and screenshots download in the background and appear without a restart.
- [ ] "Hide games without box art" and "Show all systems" both take effect.
- [ ] Fuzzy search filters; an empty buffer does not filter.
- [ ] Cache rebuild (delete cache files, re-fetch from the loading screen) works.

## 3. Downloading

- [ ] Download a ROM: progress bar advances, the downloads page lists it, cancel works.
- [ ] Downloaded ROM lands in `roms/<system_slug>/` and the installed icon appears.
- [ ] A multi-file / archived ROM extracts to the right place.
- [ ] Delete removes the local file and the button returns to "Download".

## 4. Emulator install

Test at least one **archive** emulator (RetroArch) and one **single-file** emulator (any AppImage or
`.exe` with `extract: false`) per platform.

- [ ] Action button reports the right step at each stage: `Install <emulator>` →
      `Installing <emulator>...` → `Download` / `Install Core` → `Play`.
- [ ] The release picker lists releases and installing one succeeds.
- [ ] **After install completes, the button leaves the install state.** If it re-opens the release
      picker, `IsEmulatorInstalled` is failing — check `executable_name` / `executable_regex` in
      `meta.json` against what actually landed in `emulators/<dir>/`.
- [ ] Installed files match the meta: the executable resolves, `emulator_dir_name` is respected.
- [ ] Reinstall over an existing install preserves `preserve_on_reinstall` paths and save data.
- [ ] Install is refused while that emulator is running.
- [ ] Uninstall removes the install and the button returns to `Install <emulator>`.
- [ ] RetroArch only: the per-system core downloads on first launch of that system, into
      `emulators/retroarch/cores/`, and switching core in settings fetches the new one next launch.

## 5. Launch and close

This is the section with the worst platform divergence. Do not skip it on either OS.

- [ ] Launch with a game: the emulator starts, loads the ROM, and BIOS is copied in where required.
- [ ] Launch without a game (start menu) works.
- [ ] Launch args are correct for the system — check `{system}`, `{settings}` and `{core_path}`
      substitution, and that the ROM path stays last where the emulator requires it.
- [ ] **Close hotkey**: hold the configured buttons for the configured hold time (default `Back` for
      2s) while the emulator has focus. The emulator exits. A quick press does nothing.
- [ ] The frontend does **not** freeze while closing. Any wait for process exit must be off the main
      thread.
- [ ] The emulator exits *gracefully* — not killed. Confirm by checking the game's save survived (see
      section 6). A hard kill loses in-memory SRAM.
- [ ] No orphaned emulator process is left behind. On Linux verify with
      `pgrep -af <emulator>` after closing; an AppImage runs the real binary in its own session, so
      the process the frontend started is only a wrapper.
- [ ] Changing the close buttons and hold time in settings works, persists to `config.cfg`, and the
      new hold closes the emulator.
- [ ] The frontend regains focus / input after the emulator exits.

## 6. Saves and sync

- [ ] The emulator's save directory is a link into `saves/<emulator>/...` after install and launch.
- [ ] Play, save in-game, exit via the close hotkey — the save file appears in the central store.
- [ ] Save sync to RomM runs after exit and uploads only real save data, never preserved config.
- [ ] The play session appears under `GET /api/play-sessions` with the right ROM and duration.
- [ ] Clear `DeviceId` in `config.cfg` and relaunch: RomM returns the **same** device for this
      machine (`GET /api/devices` gains no new row), and the device has a hostname, on both OSes.
- [ ] Switching a system to a different emulator warns that saves are not converted.
- [ ] Reinstalling the emulator does not destroy saves or save states.
- [ ] Save states land inside the install directory, not in a per-user location outside it.

## 7. Controller

- [ ] Controller is detected at startup and after hot-plug.
- [ ] Navigation, select, back, settings, downloads page, delete, cancel all respond.
- [ ] Controller icons render for the connected pad.
- [ ] Emulator controller config is written only when a controller is actually detected, and an
      existing device line is never overwritten.
- [ ] Face buttons map positionally (South/East/West/North), not by Xbox letter.

## 8. Settings and UI panels

- [ ] Every settings section opens, renders its fields, and persists changes to `config.cfg`.
- [ ] Per-emulator settings fields write `user_settings.json` and change the launch arguments.
- [ ] Preferred emulator and preferred core per system persist and take effect.
- [ ] BIOS/firmware selection per system persists.
- [ ] Panel open/close, section transitions, and the panel stack behave with both mouse and controller.
- [ ] Theme and background changes apply.

## 9. Cross-platform traps

These are the failure modes this project has actually shipped. Check them whenever you touch paths,
`meta.json`, or process handling.

- [ ] **Path casing.** Linux is case-sensitive. Every `res://` path must match the real name exactly.
      Sweep with a script that resolves each `res://` reference against the filesystem — a lowercase
      `scripts/autoloads/` silently killed four autoloads and emptied the carousel and details panel.
- [ ] **Executable resolution.** The same emulator can ship a directory tree on one OS and a single
      AppImage on the other. If a recipe extracts *and* declares a literal `executable_name`, verify
      the file it names actually exists after install.
- [ ] **Bundled tools over PATH tools.** Archive extraction must use `tools/7zip/<platform>/`, never
      a system `7z` — it will not be installed on a clean machine.
- [ ] **Executable bits.** Anything shipped or extracted that must run needs `chmod +x` on Unix; zip
      does not preserve the bit.
- [ ] **Windows-only .NET APIs.** `Process.CloseMainWindow()` returns `false` and does nothing on
      Unix. `Process.Kill()` does not reach reparented children. Registry, drive letters, `cmd.exe`
      and directory junctions are all Windows-only branches.
- [ ] **Editor vs exported paths.** Anything writing to `ApplicationRootDirectory` writes into the
      repository when run from the editor. Guard destructive startup work with `OS.HasFeature("editor")`.
- [ ] **Line endings and file modes** in git are clean after a full run of the app.

## 10. Netplay

Needs two machines. `--netplay-host` and `--netplay-join=<address>` (after a bare `--`) drive the
lobby from the command line, so one side can be the Linux test box (`docs/LINUX-TESTING.md`).

- [ ] Host is offered only when the play button would launch the game. A game that still needs a
      download or an emulator install says so instead.
- [ ] LAN: the joiner discovers the lobby, the roster shows both players, and readiness updates when
      the other player's ROM finishes downloading.
- [ ] Join code: copying it works, and the joiner connects with it.
- [ ] Internet: UPnP maps **both** the lobby port and the emulator's netplay port, and the code
      advertises the public address only when both mappings succeeded.
- [ ] Start launches both emulators connected. Test RetroArch and Flycast (GGPO) at minimum.
- [ ] Flycast: player two's inputs, including the analog stick, reach the host.
- [ ] After the session, a plain launch of any game carries no `--host` / `--connect` arguments.
- [ ] The joiner's save is **not** uploaded after a session. The host's is.
- [ ] Closing or cancelling the lobby returns to the previous browse view, with controller focus.

## 11. Input layer

- [ ] Linux: startup logs no `InputLayer` exception, and the controller settings report virtual
      controllers as unavailable on this platform.
- [ ] First launch asks once whether to use the controller layer. The answer persists.
- [ ] Windows: accepting installs ViGEmBus with a single UAC prompt, and the layer reports working
      afterwards. Declining UAC leaves the app usable with the layer off.
- [ ] With the layer on, each emulator sees the virtual pads in player order, whatever is physically
      connected. Check at least RetroArch, Dolphin, PCSX2, Flycast, ares and mGBA.
- [ ] Player order can be set by pressing a button on each pad, and the emulator follows it.
- [ ] Two physical pads give two players with no double input (PCSX2 player 2 is the known trap).
- [ ] A non-Xbox pad (PlayStation, 8BitDo) is normalized through the layer and maps positionally.
- [ ] The frontend UI ignores the controller while an emulator, or the layer's session, is active.
- [ ] Unplugging and re-plugging a pad mid-game does not reorder players.
- [ ] Layer off: emulators fall back to the physical pads and nothing in their config still points at
      a virtual pad.
