# Controller Test Matrix

The in-depth pass for controller handling, run on the hardware below whenever the input layer, an
emulator's `controller_config`, or an emulator version changes. `TESTING-CHECKLIST.md` section 11
holds the short release checks; this file holds the full grid and its results.

Each row is two steps. The **report** is automatic and shows whether every device reference the app
writes points at the right device. The **play check** is manual and shows whether the emulator
agrees. Today's bugs were all visible in the report before anyone pressed a button, but only the
play check proves the emulator reads what the report predicts.

## Hardware

| Pad | Connection | Shows up as |
|---|---|---|
| Xbox Series | Bluetooth | XInput `045E/0B13` |
| Xbox One | Xbox Wireless Adapter | XInput `045E/02DD` |
| Xbox One | USB | XInput `045E/02FF` |
| Switch Pro | Bluetooth | HIDAPI `057E/2009`, no XInput slot |

Hosts: Windows desktop, Linux laptop.

## Running the report

1. Close every running copy of the app.
2. Start the build under test with `--controller-report`: on the command line, or in a Steam
   shortcut's Launch Options for the Steam rows.
3. Leave it for about a minute. Virtual pads connect and disconnect; no emulator window opens.
4. Read `%APPDATA%\Godot\app_userdata\romm-frontend\logs\controller-report-<time>.md` (Linux:
   `~/.local/share/godot/app_userdata/romm-frontend/logs/`).

Every row in the report's checks table should read **OK**. **MISSING** means the reference names a
device the emulator cannot see; **WRONG DEVICE** means it names someone else's pad; **UNCHECKED**
means no SDL library was available to probe with (install PCSX2 or DuckStation, whose `SDL3.dll` the
probe borrows for statically linked emulators).

## Play check, per emulator

Launch one game per emulator and confirm, for each player:

- D-pad, face buttons, shoulders and triggers reach that player and no other.
- Both sticks move in the right direction.
- Start or Pause works.
- Quitting from the controller (Back + Start, or the emulator's hotkey) returns to the app.

Note which physical pad drove which player. Player order is connection order unless set in the app.

## Scenarios

| # | Host | Steam | Pads | Expectation |
|---|---|---|---|---|
| S1 | Windows | off | Series (BT) alone | passthrough, slot 0 |
| S2 | Windows | off | Xbox One (USB) alone | passthrough, slot 0 |
| S3 | Windows | off | Switch Pro alone | one virtual pad |
| S4 | Windows | off | Series + Xbox One USB | two passthrough |
| S5 | Windows | off | Switch Pro + Xbox One USB | one virtual, one passthrough |
| S6 | Windows | off | all four | three passthrough, one virtual, four slots used |
| S7 | Windows | shortcut, Steam Input **off** for Xbox | all four | as S6 |
| S8 | Windows | shortcut, Steam Input **on** | all four | four Steam pads (`28DE/11FF`), all passthrough |
| S9 | Windows | off | all four, ViGEmBus uninstalled | three Xbox players, Switch Pro left out with a log line |
| S10 | Linux | off | Series (BT) + Switch Pro | no layer; emulators use their own configs |
| S11 | Linux | Steam game mode | all four | Steam Input owns the pads |

## Results

Record date, build, and per-emulator result as **pass**, **fail: <what>**, or **report only**.

| Scenario | RetroArch | Dolphin | PCSX2 | DuckStation | Flycast | melonDS | Azahar | ares | snes9x | mGBA |
|---|---|---|---|---|---|---|---|---|---|---|
| S1 (2026-10-17, pre-passthrough) | pass | pass | | | | | | | | |
| S6 (2026-10-17) | | pass (SSBM, 4 players) | report OK | | | | | | | |
| S7 (2026-10-17, pre-passthrough) | | pass | | | | | | | | |
| S8 (2026-10-17, pre-passthrough) | | fail: no controller | fail: no controller | | | | | | | |
| S8 (2026-10-17) | | pass (SSBM) | | | | | | | | |

Unverified assumptions worth targeting first: the statically linked emulators (Flycast, melonDS,
Azahar, snes9x) are probed through a borrowed `SDL3.dll`, and S8 has been run for Dolphin only.
