# Task: Build a Steam-Input-style input layer for the RomM frontend

You are working in `E:\Projects\romm-frontend`, a Godot 4.6 .NET (C#, net8.0) game-launcher
frontend for RomM that installs and launches external emulators on Windows and Linux. Your job
is to implement a virtual-controller input layer — the same architecture Steam Input uses — so
that every emulator sees identical, correctly-ordered virtual Xbox 360 pads regardless of what
physical controllers are connected.

Read `CLAUDE.md` first and follow it strictly. The rules that bite hardest here: **no comments
of any kind in `scripts/`** (rationale goes in `docs/DESIGN-NOTES.md`), naming conventions, do
not touch `addons/`, and read `docs/LINUX-TESTING.md` before any Linux work.

## Why this feature

The user wants all three of Steam Input's benefits:

1. **A single remapping UI** in the frontend, instead of configuring N emulators.
2. **Guaranteed player order** — Player 1 in the frontend is always Player 1 in the emulator.
3. **Normalization** — mixed controller brands all appear to emulators as the same device, so
   emulator default bindings just work.

None of these can be achieved reliably by rewriting emulator config files. The project already
tried that approach and suspended it (see below).

## Existing code you must understand and build on

- `scripts/autoloads/ControllerManager.cs` — tracks connected pads via Godot `Input`
  (device id, name, SDL GUID, connection order). This is the device inventory.
- `scripts/autoloads/EmulatorManager.cs` — the launch pipeline. Key points:
  - `LaunchGame` / `LaunchEmulatorWithoutGame` build args and call
    `BuildAndStartEmulatorProcess` (~line 1991), which applies per-emulator environment
    variables from meta.json `launch_env` via `ApplyLaunchEnvironment` (~line 2011).
  - Emulator exit is detected in `_Process` (~line 999) with a liveness grace system; save
    sync and netplay teardown already hang off that path. Session end hooks go here.
  - `CloseEmulator` (~line 2232) gracefully closes the emulator — the future hotkey target.
  - `ApplyControllerMappings` (~line 2478) is a **suspended** declarative config-file-rewriting
    system (`SuspendControllerMapping = true` at ~line 2476). It reads `controller_config`
    from each emulator's meta.json (INI/JSON section templates, `{sdl_index}` macros,
    per-platform layouts). Do not delete it: it becomes the fallback tier.
  - `IniConfigurationUpdater` / `JsonConfigurationUpdater` etc. — reusable config writers.
- `scripts/main/handlers/MainSceneInputHandler.cs:96` —
  `GeneratePlatformControllerMappingsUI` builds a per-platform, per-player button-mapping UI
  from meta.json `platform_layout`. `ConfigManager.PlatformInputMappings`
  (system → player → button → SDL input) stores user remaps. This is the seed of the layer's
  mapping model and UI.
- Emulator metadata lives in per-emulator `meta.json` files under the install-scripts
  directory (`ConfigManager.InstallScriptsPath`); `EmulatorMeta` in EmulatorManager.cs is the
  schema. `UniversalInstaller` handles downloading/installing emulators — reuse its patterns
  for driver onboarding.

## Environment facts already verified (do not re-derive)

- Dev machine is Windows 11. **ViGEmBus is already installed and running** (service
  `ViGEmBus`, device "Nefarius Virtual Gamepad Emulation Bus").
- **HidHide is NOT installed.** Latest release is on GitHub `nefarius/HidHide`. Installing it
  requires an elevated installer — ask the user before downloading, and let them click UAC.
- **No physical pad is currently in gamepad mode** on the dev machine. A Flydigi device
  (VID_320F PID_5044) is present but enumerates as keyboard/mouse/vendor HID. Ask the user to
  connect or mode-switch a controller before pump testing.
- `romm-frontend.csproj` uses `Godot.NET.Sdk/4.6.3`, `net8.0`, and currently has **no**
  `PackageReference` items (one giant `Content` ItemGroup of assets). Add NuGet packages via
  `dotnet add package`.
- The app runs GL Compatibility renderer by design. Debug launch:
  `--windowed --resolution 1920x1080`.
- Linux testing happens on a real Arch machine over SSH via
  `powershell -File tools\linux-test\deploy.ps1` (see `docs/LINUX-TESTING.md`;
  `tools/linux-test/config.json` is gitignored — if missing, run `tools\linux-test\setup.ps1`).

## Architecture

One new autoload `InputLayer` (register it in `project.godot` like the other autoloads) that
owns a session-scoped pipeline:

```
physical pads → reader → mapping tables → virtual X360 pads → emulator
                  │            └─ per-platform + per-player remaps (reuse PlatformInputMappings)
                  └─ chord detection (Guide+Start → EmulatorManager.CloseEmulator)
```

Three backend seams so Windows and Linux stay symmetrical:

- **`IPhysicalPadReader`** — first attempt: poll Godot `Input` each frame. If the Phase 0
  spike shows focus-throttling or insufficient cadence, replace with a background thread
  P/Invoking a bundled SDL2 at ~250 Hz. Linux likely wants the evdev/SDL path from day one
  because grabbing needs the device fd anyway.
- **`IVirtualPadBackend`** — Windows: ViGEm via the `Nefarius.ViGEm.Client` NuGet package
  (plain C#, no P/Invoke needed). Linux: `uinput` via P/Invoke — open `/dev/uinput`, register
  an X360-shaped device, write input events.
- **`IDeviceHider`** — Windows: HidHide (ship/download `HidHideCLI.exe`; hide physical pads,
  allowlist the frontend executable so the frontend itself can still read them). Linux:
  `EVIOCGRAB` on the physical evdev device — grabbing is hiding, no extra component.

### Session flow

1. In `LaunchGame` (and `LaunchEmulatorWithoutGame`), before the process starts:
   `InputLayer.BeginSession(players)` — create virtual pads **in player order**, hide physical
   devices, start the pump.
2. The existing exit path in `EmulatorManager._Process` calls `InputLayer.EndSession()` —
   stop pump, destroy virtual pads, unhide.
3. **Crash safety:** HidHide rules persist across process death. A frontend crash mid-session
   would leave the user's controllers invisible system-wide. Always clear this app's HidHide
   rules during frontend startup, and route every emulator-teardown path through EndSession.

### Known traps (each of these is real; handle them)

- **Feedback loop:** the frontend will see its own ViGEm virtual pads appear as new Godot
  joypad devices. `ControllerManager` must recognize and exclude them (ViGEm X360 pads have a
  recognizable name/VID+PID), or the layer will read its own output.
- **XInput slot order:** hide physical pads *first*, then create virtual pads in player
  order, so virtual pads land in XInput slots 0..3 deterministically.
- **Steam double-remapping:** if the frontend runs under Steam with Steam Input active,
  physical pads may already be virtual. Detect Steam-virtual devices and warn/skip.
- **Godot low-processor mode / unfocused throttling:** verify Godot keeps polling joypads at
  full rate while the emulator window has focus. This is a Phase 0 question, not an
  assumption.
- **Driver-less fallback chain:** when ViGEmBus/HidHide are missing or the user declines:
  (1) inject SDL controller remaps via the `SDL_GAMECONTROLLERCONFIG` environment variable
  through the existing `launch_env` mechanism (covers SDL-based emulators: PCSX2, DuckStation,
  Flycast, melonDS, Dolphin, shadPS4, ares, …; XInput-native readers ignore it),
  (2) un-suspend the existing config-file writer, (3) raw passthrough.
- **Per-emulator override:** add an `input_layer` field to meta.json
  (`virtual_pad` default | `sdl_env` | `config_file` | `none`) so a misbehaving emulator can
  be pinned to a lower tier without code changes.
- **Netplay and save sync must be untouched** beyond the named hooks. The Flycast GGPO
  netplay work on this branch just sees a normal pad.

## Phased plan — work in order, get user sign-off between phases

### Phase 0 — risk-retirement spike (throwaway code)

Goal: answer the unknowns before real architecture. On the dev machine:

1. Ask the user to install HidHide (you download the MSI from `nefarius/HidHide` releases
   after asking; they click UAC) and to connect a controller in gamepad (XInput/DInput) mode.
2. Add `Nefarius.ViGEm.Client` to the csproj.
3. Write a scratch autoload (`scripts/autoloads/InputLayerSpike.cs`, follows repo conventions
   even though throwaway) that: connects to ViGEmBus, creates one virtual X360 pad, and each
   frame copies Godot `Input` joypad state (buttons, sticks, triggers) onto it. Log effective
   pump rate and physical-device detection.
4. Validate with a real emulator (RetroArch is installed via the app's own emulator system):
   - Emulator sees the virtual X360 pad and responds to physical input relayed through it.
   - Godot still receives physical input while the emulator window has focus (watch the pump
     logs) and while the frontend is unfocused/minimized.
   - With HidHide active (hide physical pad, allowlist the frontend exe): frontend still
     reads the physical pad; emulator sees only the virtual pad; no double input.
   - Measure pump cadence; judge whether frame-rate pumping is adequate or an SDL thread is
     needed.

Record every finding in `docs/DESIGN-NOTES.md` under a new "Input layer" section. These
findings decide Phase 1's reader implementation. Delete or clearly quarantine spike code
before Phase 1 lands.

### Phase 1 — `InputLayer` autoload on Windows

- `scripts/autoloads/InputLayer.cs` + backend interfaces (`scripts/input/` is a reasonable
  home for `IPhysicalPadReader`, `IVirtualPadBackend`, `ViGEmPadBackend`, etc.).
- Mapping model: physical input → logical control → virtual pad element, sourced from
  `PlatformInputMappings` (per-platform, per-player).
- `ControllerManager` filtering of virtual pads.
- `BeginSession`/`EndSession` wired into `EmulatorManager` launch and exit paths.
- Ship initially **without** hiding (double input is tolerable for dev) so this can land and
  be tested before Phase 2.

### Phase 2 — hiding + driver onboarding

- HidHide integration with the startup-cleanup crash-safety net.
- Driver install flow using `UniversalInstaller` patterns: download ViGEmBus + HidHide
  installers from their official GitHub releases, run elevated, detect installed state
  (service presence), settings toggle in the existing settings UI.
- Implement the fallback chain (SDL env var → config writer → passthrough) and the meta.json
  `input_layer` override.

### Phase 3 — Linux backend

- uinput virtual pad + `EVIOCGRAB` reader. Match Godot's SDL GUID to the evdev node by
  VID/PID from `EVIOCGID`.
- udev rule for `/dev/uinput` access folded into `tools/linux-test/setup.ps1`.
- Validate on the Arch test machine via `deploy.ps1` per `docs/LINUX-TESTING.md` — do not
  guess at Linux behavior.

### Phase 4 — payoff features

- Player-assignment screen ("press A for Player 1") feeding virtual-pad creation order.
- Guide+Start chord → `EmulatorManager.CloseEmulator()` (save sync already rides that exit
  path).
- Rumble passthrough: ViGEm feedback callback → vibration on the matching physical pad.
- Evolve `GeneratePlatformControllerMappingsUI` into the unified remap UI backed by the
  layer's mapping model instead of config-file writing.

## Working agreements

- The user must approve: any file download (state name/source/size), any elevated installer,
  and anything destructive. UAC prompts are theirs to click.
- Commit in coherent steps with clear messages; do not commit spike code to main history
  without flagging it.
- Anything non-obvious you learn (driver quirks, Godot input behavior, HidHide semantics)
  goes in `docs/DESIGN-NOTES.md`, never in code comments.
- If a finding invalidates part of this plan, say so explicitly and propose the adjustment
  before proceeding.
