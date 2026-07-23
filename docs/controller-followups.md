# Controller Mapping — Handoff

Status as of the end of the emulator-install-scripts session. Everything here was
derived by reading emulator source and verified against real Windows testing unless
explicitly marked as unverified.

---

## TL;DR

A root cause was found and fixed in code. **It has not yet been confirmed working
in-game.** The first job is to verify it, then re-test the other affected emulators.

Three things landed:

1. `ConvertInputEventToStandardString` returned the wrong canonical names for the four
   face buttons. **Fixed.**
2. `ResolvePlatformMacros` silently produced an empty binding when a stored mapping was
   unrecognised. **Fixed** with a fallback + log line. Confirmed firing in the user's log.
3. Dolphin's `-u ./` launch arg pointed its user directory at the wrong place. **Fixed**
   and confirmed — Dolphin now reads/writes the config we generate.

---

## How controller mapping actually works

Understanding this pipeline is necessary before changing anything.

### Detection

`scripts/autoloads/ControllerManager.cs`

- `GetConnectedControllers()` returns controllers discovered via Godot's
  `Input.GetConnectedJoypads()`.
- Each gets `ConnectionOrder` from a monotonic counter (`nextConnectionOrder++`), which
  is what `{sdl_index}` resolves to.
- **Verified working**: user's log showed `Applying controller mappings: 1 of 4 max controllers`.
  Detection is NOT the problem.
- Known weakness: the counter never resets, so a disconnect/reconnect can push the first
  physical pad to index 1+, which would then not match the emulator's SDL index 0.

### Application

`scripts/autoloads/EmulatorManager.cs`

- `ApplyControllerMappings()` runs at launch, dispatches on `controller_config.format`:
  - `"ini"` → `ApplyIniControllerMappings()`
  - `"json"` → `ApplyJsonControllerMappings()`
- Guard added: if `availableControllerCount == 0` it now **returns without writing**.
  Previously it stamped port 1 as disconnected, wiping the static player-1 bindings that
  PCSX2/DuckStation ship.

### Value resolution — `ResolvePlatformMacros()`

This is where the bug was. For a mapping like `"Buttons/A": "{Platform_A}"`:

1. `defaultSdlInput` = `controller_config.platform_layout["A"]` → e.g. `"FaceSouth"`
2. If `config.cfg [PlatformInputMappings][slug][playerIndex]["A"]` exists, it **overrides**
   the default.
3. That value is looked up in `controller_config.sdl_string_map` to get the
   emulator-specific string → e.g. `` `Button S` ``.
4. **Previously**: a miss at step 3 left an empty string, writing `Buttons/A = ` with no
   value, which emulators silently discard.
   **Now**: falls back to `sdl_string_map[defaultSdlInput]` and logs.

### The canonical vocabulary

`MainSceneInputHandler.StandardSdlInputs` is the single source of truth:

```
FaceSouth, FaceEast, FaceWest, FaceNorth
LeftShoulder, RightShoulder, LeftTrigger, RightTrigger
DpadUp, DpadDown, DpadLeft, DpadRight
Back, Start, Guide, LeftStick, RightStick
LeftStick_Up/Down/Left/Right, RightStick_Up/Down/Left/Right
```

Every emulator's `sdl_string_map` keys must come from this list.

---

## The root cause (fixed, needs in-game confirmation)

`scripts/main/handlers/MainSceneInputHandler.cs` → `ConvertInputEventToStandardString()`

It returned Godot's Xbox-style face button names instead of the canonical positional ones:

```csharp
case JoyButton.A: return "A";   // WRONG — not in StandardSdlInputs
case JoyButton.B: return "B";
case JoyButton.X: return "X";
case JoyButton.Y: return "Y";
case JoyButton.LeftShoulder: return "LeftShoulder";  // correct
case JoyButton.DpadUp:       return "DpadUp";        // correct
```

Only the four face buttons were wrong; every other case already returned a canonical name.

**Consequence:** any control remapped through the frontend UI wrote `"A"/"B"/"X"/"Y"` into
`config.cfg [PlatformInputMappings]`. Those match no `sdl_string_map` key, so they resolved
to `""` and were written as empty bindings, which the emulator dropped. Result: exactly the
four face buttons dead, every other button working.

Fixed to return `FaceSouth` / `FaceEast` / `FaceWest` / `FaceNorth`
(Godot's `JoyButton.A/B/X/Y` are positionally South/East/West/North).

**This is not Dolphin-specific.** It affects every emulator using `controller_config` —
Dolphin, PCSX2, DuckStation, gopher64.

### Existing bad data self-heals

Users who already remapped have stale `"A"/"B"/"X"/"Y"` in `config.cfg`. The
`ResolvePlatformMacros` fallback handles them. Confirmed in the user's log:

```
Controller mapping 'A' -> 'A' is not valid for this emulator; using default 'FaceSouth'.
Controller mapping 'B' -> 'B' is not valid for this emulator; using default 'FaceEast'.
Controller mapping 'X' -> 'X' is not valid for this emulator; using default 'FaceWest'.
Controller mapping 'Y' -> 'Y' is not valid for this emulator; using default 'FaceNorth'.
```

No user action required — but see the optional migration below to stop the log noise and
correct the value shown in the mapping UI.

---

## ⚠️ Do not re-litigate: Dolphin's `Button S/E/W/N` are CORRECT

This was already investigated, got wrong once, and reverted. Do not "fix" it again.

`Source/Core/InputCommon/ControllerInterface/SDL/SDLGamepad.h` → `s_sdl_button_names`:

```cpp
"Button S",  // SDL_GAMEPAD_BUTTON_SOUTH
"Button E",  // SDL_GAMEPAD_BUTTON_EAST
"Button W",  // SDL_GAMEPAD_BUTTON_WEST
"Button N",  // SDL_GAMEPAD_BUTTON_NORTH
```

These are the **primary** names and what Dolphin writes on save. `SDLGamepad.cpp` →
`Gamepad::Button::IsMatchingName()` additionally accepts `Button A/B/X/Y` as *positional
aliases* for XInput compatibility. Both forms match; the primary names are what belong in
`sdl_string_map`.

The rest of Dolphin's names, all confirmed valid by surviving Dolphin's own config rewrite:
`Shoulder L/R`, `Trigger L/R`, `Pad N/S/W/E`, `Left X±`, `Left Y±`, `Right X±`, `Right Y±`,
`Start`, `Back`, `Guide`, `Thumb L/R`.

---

## ⚠️ Dolphin's device name — `{controller_name}` DISPROVEN

The earlier note that *"the device name matched fine"* is **wrong**. Measured against a
hand-mapped Dolphin config, one **Xbox Series X controller** is called three different
things:

| Source | Name |
|---|---|
| Godot (`Input.GetJoyName`) | `XInput Controller` |
| ares (SDL3) | `XInput Controller` (name CRC `fa67`) |
| **Dolphin (SDL)** | **`Xbox One Controller`** |
| What we shipped | `Xbox Series X Controller` ❌ |

Symptom: Dolphin reported the controller our shipped config named as **disconnected**, and
the user had to pick it by hand. `{controller_name}` would have written
`SDL/0/XInput Controller` — wrong in a different way.

**Dolphin has no fallback.** Verified directly: deleting the `Device` line from `[GCPad1]`
entirely kills all input. Partial qualifiers don't help either — Dolphin's
`DeviceQualifier` compares source, id *and* name. So Dolphin is the one emulator where the
device-agnostic trick that solved Azahar, melonDS and gopher64 does not exist.

### What IS verified correct

Every binding string. A hand-mapped `GCPadNew.ini` diffed against our shipped one is
byte-identical apart from `Device` and `Calibration` — all of `` `Button S` ``,
`` `Shoulder R` ``, `` `Pad N` ``, `` `Left Y+` `` etc. match. `sdl_string_map` and
`platform_layout` in `dolphin/meta.json` are **confirmed good**, and the "do not
re-litigate `Button S/E/W/N`" section above stands.

Calibration is deliberately **not** shipped from a hand-mapped file: the measured 32-value
form encodes one specific controller's stick range and wear. The idealised 8-value default
stays.

### Mitigations applied

1. Shipped `Device` corrected to `SDL/0/Xbox One Controller` — right for an Xbox Series X
   pad, which is the common case. Still a guess for any other controller.
2. `preserve_on_reinstall` added for `GCPadNew.ini`, so a user's correction is no longer
   wiped by a reinstall or update. Dolphin previously had none.

   Deliberately **not** `WiimoteNew.ini` — it contains only `Source` lines (verified: zero
   lines that aren't a section header or `Source`, and real Wii Remote pairing lives in the
   OS Bluetooth stack, not here). Preserving it would have no user data to protect while
   blocking the shipped real-Wiimote policy from re-asserting on reinstall.
3. **The writer no longer overwrites an existing `Device` line** — it only fills it in when
   absent (`IniConfigurationUpdater.ReadValue`). Once the emulator or the user has set a
   real device, it is authoritative.

### Still open

First run on a non-Series-X pad still shows disconnected until the user picks the device.
Options considered:

- Ship no `[GCPad1]` section, let Dolphin create it with the correct device, then inject
  bindings on a later launch while keeping Dolphin's `Device`. Automatic from launch two.
- Ship a VID/PID → SDL-name table. **Not recommended** — it revives the "predict the
  emulator's device identity" approach that has now failed three times (Azahar GUID, ares
  GUID, Dolphin name).

## Dolphin user directory (fixed, confirmed)

`install_scripts/dolphin/meta.json` had `-u ./` in both launch arg strings.

Per Dolphin `Source/Core/UICommon/UICommon.cpp` → `SetUserDirectory()`:
- a `-u` path **overrides everything**
- with no `-u`, `portable.txt` next to the exe → user dir = `<exe>/User/`

`-u ./` therefore set the user dir to the emulator **root**, while everything we ship and
expect lives under `User/` (`User/Config/GCPadNew.ini`, `relative_save_path` of
`User/GC/...` and `User/Wii/...`). Dolphin created its own `Config/`, `GC/`, `Cache/` at the
root and never saw our files.

`-u ./` removed; `portable.txt` (already shipped) now governs. This fixed three things at
once: controller config being read, GC/Wii save alignment, and our `Dolphin.ini` (analytics
off, `WiimoteContinuousScanning`) finally loading.

Confirmed: after the change, `User/Config/GCPadNew.ini` showed `Device = SDL/0/XInput Controller`
(Godot's name, written by us) and Dolphin had rewritten the file — proving it now reads and
writes there. The device name matched fine; non-face-button bindings survived intact.

---

## Emulator tiers

### Auto-mapping — already work, no action

**PPSSPP verified.** `memstick/PSP/SYSTEM/controls.ini` ships PPSSPP's own defaults binding
every input for both device classes — `20-` (XInput) and `10-` (generic pad) — alongside
keyboard (`1-`). No mapping pass needed.

Portable mode also confirmed working: PPSSPP creates `memstick/` next to the exe by itself
on Windows (no `Documents/PPSSPP` is created), and `memstick/PSP/SAVEDATA` matches
`relative_save_path[0]`. An earlier concern that PPSSPP would fall back to the Documents
folder and break save sync was **unfounded**.



**Flycast**, **PPSSPP**. They detect and map SDL gamepads themselves via SDL's built-in
gamepad DB, independent of this frontend.

### Tier 1 — have `controller_config`, frontend writes bindings

**Dolphin**, **PCSX2**, **DuckStation**, **gopher64**. Current state:

| Emulator | State |
|---|---|
| DuckStation | ✅ **verified working in-game.** Shipped config was already correct. |
| PCSX2 | ✅ **verified working on a fresh install.** Shipped config untouched by the emulator. |
| gopher64 | ✅ Solved — self-assigns, #18 closed. |
| Dolphin | ⚠️ Bindings verified correct; device name was wrong and is now fixed. |

The face-button bug that broke these lived in the **writer**, not in the shipped configs —
which is why DuckStation's config turned out to be right all along. The writer is now
suspended, so tier 1 runs entirely on shipped static config.

DuckStation was separately blocked by its setup wizard (fixed via
`SetupWizardIncomplete = false`); the wizard may also have been resetting input.

#### DuckStation — verified, no action needed

A hand-mapped config came out **byte-identical** to what we ship (line endings aside), and
the controller works in-game (Metal Gear Solid: stick and d-pad both drive menus).

It is also the best-designed input config of any emulator here, and a useful reference:

```ini
Cross = SDL-0/FaceSouth
L2    = SDL-0/+LeftTrigger
LUp   = SDL-0/-LeftY
```

- **`SDL-0/` is index-based** — no GUID, no device name. Device-agnostic *by design*, so
  none of the identity problems that hit Azahar, ares and Dolphin apply.
- **Button names are semantic** and line up almost exactly with our own canonical
  `StandardSdlInputs` vocabulary (`FaceSouth`, `LeftShoulder`, `Back`, `Guide`…). Note
  `DPadUp` capitalises the P where our canonical form uses `DpadUp` — matters if a
  `sdl_string_map` is ever written for it.
- PlayStation uses shapes, so the face-button convention question does not arise.

⚠️ `Analog = SDL-0/Guide` binds the DualShock analog-mode toggle to the Xbox Guide button,
which Windows' Game Bar or the Steam overlay often swallow first. Since `Type =
AnalogController`, games needing analog sticks depend on this toggle. Alternatives if it
misbehaves: a chord (`Back & FaceNorth`) or a stick click. **PCSX2 has the identical
binding** and the same caveat.

#### PCSX2 — verified, no action needed

Works on a fresh install with no intervention. A diff of shipped vs. post-run config shows
**zero input differences** — the emulator left `[InputSources]`, `[Pad1]`, `[Pad2]` and
`[Hotkeys]` exactly as shipped. The only writes were window geometry, a created memory card
and game-list column state.

PCSX2 and DuckStation share their input layer (same upstream author), which is why both use
the identical `SDL-0/FaceSouth` vocabulary and the same `SDL-<n>/` index-based device
addressing. Neither has any device-identity problem.

**PCSX2 ships a working player 2** (`[Pad2]` on `SDL-1/`, full bindings). DuckStation ships
`[Pad2] Type = None`. Nothing is wrong with either, but it is an inconsistency: a second
controller works out of the box on PS2 and not on PS1. Copying PCSX2's `[Pad2]` block into
DuckStation's config with `SDL-1/` would close the gap — untested, and it would need a
second pad to verify.

### Tier 2 — no `controller_config`, no auto-map (task #22)

**ares**, **snes9x**, **melonDS**, **Azahar**. All store bindings keyed by device
GUID / SDL device path, which cannot be shipped generically because they differ per
physical controller.

| Emulator | Binding storage | Notes |
|---|---|---|
| ~~ares~~ | — | **Solved** — device-agnostic bindings shipped. See below. |
| ~~snes9x~~ | — | **Solved on Windows** — see below. Linux still unmapped. |
| ~~melonDS~~ | — | **Solved** — see below. Device-agnostic, no GUID involved. |
| ~~Azahar~~ | — | **Solved** — see below. The GUID turned out to be optional. |

---

## ⚠️ Godot's controller identity does NOT match the emulator's

**This is the single most important finding of the mapping work. Do not build anything
that assumes otherwise.**

Same physical pad, same machine, read two ways:

```
                bus   nameCRC  vendor  ----  product ----  ----  drv
Azahar / SDL:   0300   938d    5e04    0000   120b   0000  0000  7200
Godot:          0300   fa67    5e04    0000   120b   0000  0000  7801
                 ✓      ✗       ✓       ✓      ✓      ✓     ✓     ✗
```

- **Name CRC differs** because the *names* differ. Godot reports `XInput Controller`;
  the emulator's SDL reports `Xbox Series X Controller`. SDL 2.0.18+ embeds a CRC-16 of
  the name in GUID bytes 2–3.
- **Driver byte differs**: `0x72` = `'r'` (RawInput) vs `0x78` = `'x'` (XInput). Godot
  and the emulator are using different backends against the same device.
- Only vendor (`5e04` = Microsoft `0x045E`) and product (`120b`) agree.

### Consequences

1. **A `{controller_guid}` macro is not viable.** It was scoped and abandoned. Producing a
   matching GUID would mean predicting which SDL backend the emulator picks on each user's
   machine *and* the exact name string it produces, then CRC-ing it. Fails silently when
   wrong — the same trap as gopher64 (#18).
2. **`{controller_name}` is equally suspect.** Dolphin's
   `device_template: "SDL/{sdl_index}/{controller_name}"` writes Godot's
   `XInput Controller` into a file Dolphin's SDL reads while calling the pad
   `Xbox Series X Controller`. The earlier note in this doc that "the device name matched
   fine" does **not** establish that the name matched — only that Dolphin rewrote the file
   and non-face-button bindings survived. Dolphin may be matching on `SDL/0/` and ignoring
   the name. **Unverified; worth checking deliberately.**
3. `ControllerManager` now logs the GUID on connect, for exactly this kind of comparison.

### The workaround that actually works

Prefer **device-agnostic bindings** — port/index-based, no GUID, no name. Verified for
Azahar (below). Try this first for melonDS, snes9x and ares before assuming a device key
is mandatory.

---

### Azahar — solved with device-agnostic bindings

Azahar has no auto-map (confirmed: hand-mapping was required), and every binding it wrote
embedded the GUID — which per the section above we cannot reproduce.

**But the GUID is optional.** Tested by hand-editing a live config to three variants and
launching:

| Binding | Form | Result |
|---|---|---|
| `button_a` | full `guid:0300938d...` | works (control) |
| `button_x` | `guid:` present but empty | works |
| `button_y` | `guid:` key absent entirely | works |

Azahar matches on `port:` when no GUID is present. Decisively, on exit Azahar **rewrote
the config and kept the GUID-less form**, normalizing the empty `guid:,` away rather than
restoring a GUID — so this is a first-class supported form, not incidental tolerance.

Shipped as static bindings in `default_config/user/config/qt-config.ini`. No
`controller_config`, no new macro, no `qt` writer dispatch needed.

#### Value formats

```
button   button:N,engine:sdl,port:0
hat      direction:up,engine:sdl,hat:0,port:0
axis     axis:4,direction:+,engine:sdl,port:0,threshold:0.5
stick    axis_x:0,axis_y:1,deadzone:0.100000,engine:sdl,port:0
```

Params are comma-separated in **alphabetical order**, values quoted, and every key needs a
`<key>\default=false` companion or Azahar ignores it and restores its own default.

Button indices are raw SDL joystick / XInput ordering — the **same scheme mGBA uses**
(`0=A 1=B 2=X 3=Y 4=LB 5=RB 6=Back 7=Start`), independently confirmed by two emulators.

`preserve_on_reinstall: ["user/config/qt-config.ini"]` added so a user's own remapping is
not wiped by a reinstall or update.

### melonDS — solved with static device-agnostic bindings

Config is **TOML**, not INI as previously assumed, and the binding section is
`[Instance0.Joystick]` (not per-button `Joy_*` keys). Nothing in it is device-specific —
no GUID, no controller name — so the bindings ship verbatim for every user.
`JoystickID = 0` under `[Instance0]` is a joystick *index*, not a device identifier.

Shipped in `default_config/portable/melonDS.toml`. No `controller_config`, no code.

#### Value encoding

Two encodings share the section:

| Kind | Encoding |
|---|---|
| Buttons | raw SDL joystick button index |
| D-pad | `0x100 \| (hat_index << 4) \| SDL_HAT_direction` |

Hat direction bits are `UP=1 RIGHT=2 DOWN=4 LEFT=8`, so hat 0 gives
`257/258/260/264` for up/right/down/left.

Button indices are the **raw SDL joystick** scheme —
`0=A 1=B 2=X 3=Y 4=LB 5=RB 6=Back 7=Start` — now confirmed by a third emulator
(mGBA, Azahar, melonDS). gopher64 remains the outlier using the SDL *GameController*
enum instead. **Always derive the scheme from a real config; never assume.**

`HK_*` hotkey entries are omitted from the shipped file — melonDS writes them as `-1`
(unbound), which is already its default.

**melonDS preserves comments** through its config rewrite (verified: our 22 comment lines
survived), so the shipped TOML is documented inline. This is *not* true of mGBA, whose
parser was never verified to tolerate comments.

⚠️ **Left stick is unbound.** The DS has no analog stick, so a hardware-faithful mapping
uses the hat only — but many users expect the left stick to work as a d-pad. Whether
melonDS reads axis 0/1 for the d-pad independently of these bindings was not determined.
Worth testing.

### snes9x — Windows solved; the config we shipped was unreachable

**Root cause: we shipped no config the Windows build reads.** `default_config` contained
only `config/snes9x/snes9x.conf`, which is the **GTK/Linux** XDG path (placed there via
`launch_env` → `XDG_CONFIG_HOME`). Its own header says the Windows build does not read it.
The Windows build is portable and reads `snes9x.conf` **next to the exe**
(`S9xGetDirectory` in `win32/win32.cpp`, via `GetModuleFileName`).

That file also contained **no controller bindings on either platform** — only `[Files]`
and `[Display]`. So snes9x had never had a shipped mapping at all.

Fixed by adding `default_config/snes9x.conf`. The two files coexist: Windows reads the
root one, Linux reads the `config/` one, and each ignores the other.

#### Windows binding format

```
Joypad1:A     = (J1)Button 1
Joypad1:Up    = (J1)POV Up
Joypad1:Left+Down = (J1)POV Dn Left
```

| Element | Form |
|---|---|
| Device | `(J1)` — **ONE-indexed**, unlike every other emulator here |
| Buttons | `Button N`, raw SDL index (`0=A 1=B 2=X 3=Y 4=LB 5=RB 6=Back 7=Start`) |
| D-pad | `POV Up/Down/Left/Right`; diagonals abbreviate down as **`Dn`** (`POV Dn Right`) |

The `(J1)` one-indexing is a genuine trap — every other emulator in this project indexes
joysticks from 0.

Shipped as a partial config: snes9x registers defaults for unset keys and rewrites the file
in full on exit. Comments with `#` are supported (the emulator's own output uses them).

`preserve_on_reinstall` added for **both** config paths, so a user's remapping survives on
either platform. snes9x previously had none.

#### ⚠️ Linux is still unmapped

The GTK build uses a different config format and a different joypad syntax, and no Linux
mapping pass has been done. `config/snes9x/snes9x.conf` still carries only `[Files]` and
`[Display]`. Needs a mapping pass on Linux to fill in — do not guess the syntax from the
Windows form.

### ares — decoded, but shippability UNRESOLVED

**Confirmed: `VirtualPad1` is the single source of truth.** All 24 GUID-bearing bindings
live there, and every per-core section (`Famicom`, `SuperFamicom`, `MegaDrive`,
`Nintendo64`, `GameBoy`, …) is entirely unmapped (`;;`). ares maps VirtualPad → core
internally, so **one mapping covers all five systems** we use ares for.

#### Format

```
<GUID>/<device>/<group>/<input>[/<qualifier>]      trailing ";;" = 3 binding slots
```

| Group | Meaning | Indices observed |
|---|---|---|
| `0` | Axis | `0/1` left stick, `2` LT, `3/4` right stick, `5` RT |
| `1` | Hat | `0` = X axis, `1` = Y axis, with `Lo`/`Hi` |
| `3` | Button | `0=A 1=B 2=X 3=Y 4=LB 5=RB 6=Back 7=Start 8=LS 9=RS` |

Button indices are the **raw SDL joystick** scheme — a *fourth* independent confirmation
(mGBA, Azahar, melonDS, ares). gopher64 remains the sole outlier on the GameController enum.

#### A hardcoded GUID is not viable (measured, not assumed)

Two different XInput pads — an Xbox Series X controller and a Hyperkin Duchess (OG Xbox
Controller S replica, Xbox One internals) — were mapped in ares and compared:

```
          bus  nameCRC vendor pad  product ver  ---- drv
Series X: 0300  fa67   5e04  0000   ff02  0000 0000 7801
Hyperkin: 0300  ba66   5e04  0000   ff02  0000 0000 7801
                 ^^^^ ONLY difference
```

- **Product `0x02FF` is SDL's generic XInput fallback**, shared by both pads regardless of
  their real USB product IDs. So the product field is *not* the obstacle.
- **The name CRC varies per controller model.** There is no constant to ship: a hardcoded
  GUID works for the baked-in pad and silently fails for every other one.

#### Lead: ares' GUID is derivable from Godot's

```
ares  (Series X): 0300 fa67 5e04 0000 ff02 0000 0000 7801
Godot (Series X): 0300 fa67 5e04 0000 120b 0000 0000 7801
```

Name CRC, vendor and driver byte all **match** — unlike Azahar, where the name CRC and
driver both differed. Only the product field differs, and ares' value is the fixed generic
`ff02`. So `ares_guid = godot_guid` with the product field overwritten by `ff02`.

Not built, and not recommended without more evidence: it revives the retired
`{controller_guid}` approach, needs the suspended writer, and almost certainly breaks on
Linux and for non-XInput pads. Recorded because it is a real lead if ares needs one.

#### ✅ RESOLVED: ares accepts device-agnostic bindings

Tested by staging `Start` with the GUID field omitted (`/0/3/7`) against a real-GUID
control, then launching a NES game: **Start worked and the game was playable.** ares
matches on the `/0/` device index when no GUID is present, exactly like Azahar.

So the GUID is optional and the whole mapping ships for every controller. This makes the
"derive ares' GUID from Godot's by substituting `ff02`" lead **unnecessary** — recorded
above only as a historical finding.

Shipped as `install_scripts/ares/default_config/settings.bml`, a partial seed containing
`Input/Driver` and `VirtualPad1` only; ares fills in every other section and rewrites the
file in full on exit. `settings.bml` was already in `preserve_on_reinstall`.

No comments in the shipped file — BML comment syntax was not verified, and the same
assumption was avoided for mGBA.

**One mapping covers all five ares systems** (NES, Genesis, Sega CD, Master System, 32X),
since the per-core sections stay empty and ares maps VirtualPad → core internally.

#### The A/B flip setting cannot reach ares

ares' virtual pad is already positional (`A..South`, `B..East`, `X..West`, `Y..North`) and
the per-core sections that would let us override are empty — the VirtualPad→core mapping is
ares' internal business. The face-button convention setting therefore has no effect on any
of ares' five systems.

---

## Face-button convention: POSITIONAL (decided)

Nintendo consoles put **A on the right and B on the bottom** — the opposite of Xbox. Every
Nintendo-layout platform therefore needs a convention, and it must be applied consistently:

- **Positional** *(chosen)* — a console button keeps its physical spot on the pad.
  3DS A is on the right, so it maps to Xbox B. A game prompting "press A" means pressing
  the *right* button.
- **Label** *(rejected)* — console A → Xbox A. The prompt matches the button, but the
  physical layout rotates.

Applies to: `snes` `nes` `gb` `gbc` `gba` `nds` `new-nintendo-3ds` `n64`.

**Not** `ngc` — the GameCube A is already the big bottom button, so Dolphin's existing
`"A": "FaceSouth"` is correct under both conventions and must not be swapped. PlayStation
and Sega use shapes/ABC and are unaffected.

Shipped this way for Azahar, mGBA and gopher64. Note this is currently **hardcoded into
each shipped config**; the user-facing toggle is still outstanding (see Next steps).

Hand-mapping produced *inconsistent* conventions across three emulators (mGBA positional,
Azahar and gopher64 label-matched) — which is precisely why it belongs in a setting rather
than in per-file judgement calls.

---

### mGBA — solved by shipping static bindings

Root cause of "only d-pad + left stick": mGBA's out-of-box SDL defaults bind the hat and
left stick but leave every button at `-1` (unbound). Confirmed by a clean-baseline diff —
a virgin `config.ini` has only `[gba.input.QT_K]` (keyboard), and after first launch mGBA
writes a `[gba.input.SDLB]` where `keyA`/`keyB`/`keyL`/`keyR`/`keyStart`/`keySelect` are
all `-1`.

Fixed by shipping button bindings in `default_config/config.ini`. **No `controller_config`
and no code changes were needed** — see "Why no controller_config" below.

#### Config format

Three different conventions live in the same section:

| Kind | Shape | Example |
|---|---|---|
| Buttons | `key<Name>` = raw SDL joystick button index | `keyA=1` |
| Hat | `hat0<Dir>` = **mGBA key ID**, not a direction | `hat0Up=6` |
| Axes | direction in the *key name*, signed axis index in the value | `axisLeftAxis=-0` |

Button indices are **raw SDL joystick**, not the SDL GameController enum. For XInput pads:

```
0=A  1=B  2=X  3=Y  4=LB  5=RB  6=Back  7=Start  8=LStick  9=RStick
```

Verified by a real mapping pass: `keyL=4`, `keyR=5`, `keySelect=6`, `keyStart=7` only make
sense under this scheme — under the GameController enum those indices would be
Back/Guide/Start/LeftStick. The hat corroborates it: raw joystick exposes the d-pad as
hat 0, whereas GameController would place it on buttons 11–14.

mGBA key IDs (the values of `hat0*`):

```
A=0  B=1  Select=2  Start=3  Right=4  Left=5  Up=6  Down=7  R=8  L=9
```

#### Why no `controller_config`

`[<platform>.input.SDLB]` is mGBA's **global** SDL binding section — it is not keyed to a
device. The per-controller `[<platform>.input-profile.<name>]` and
`[<platform>.input-profile.<guid>]` sections override it, but only come into existence
once mGBA has seen that specific pad. On a fresh install none exist, so SDLB governs and
mGBA derives the profile from it.

Since XInput button ordering is identical across effectively all Xbox-style pads, the
bindings are static text. That avoids needing a `{controller_guid}` macro, section-name
macro support, or any new writer dispatch.

**Never ship:** `device0=<guid>` (records a physical pad's GUID) or any `input-profile`
section (keyed to a pad's name). These are the machine-specific artifacts that were
stripped from `config.ini` once already.

**Tradeoff:** as with the auto-mapping emulators, the frontend's `[PlatformInputMappings]`
remap UI does not reach mGBA.

#### Sections shipped

`gba.input.SDLB` and `gb.input.SDLB`. mGBA namespaces bindings per emulated platform, and
GB/GBC both read `gb.*`. The `gb.` block omits `keyL`/`keyR` — the hardware has none.

⚠️ The `gb.` block is **extrapolated** from the `gba.` one, not captured from a real GB
mapping pass. The `key*` entries are safe (the key name carries the meaning, the value is
just a button index), but the `hat0*` values assume mGBA's `GBKey` enum orders identically
to `GBAKey`. Worth confirming with a GB/GBC game that the d-pad works.

No comments in the shipped `config.ini` — mGBA's INI parser has not been verified to
tolerate them, and the file previously contained none.

---

## Open tasks

### #18 — CLOSED AS OBSOLETE (gopher64 self-assigns)

The analysis below was written against an older gopher64. It no longer holds.

**Observed on a clean install, with the frontend's mapping writer suspended and
`assignment_key_path` absent from `meta.json`** — so nothing we wrote could have caused it:

```json
"controller_assignment": ["XInput#0", null, null, null],
"controller_enabled":    [true, false, false, false]
```

gopher64 wrote `XInput#0` **itself** and enabled port 1 on its own. Confirmed working
in-game. The claim below that `XInput#0` *"can never match"* is false for current
versions, and the frontend does not need to write controller assignment at all.

**Also: the config schema changed.** The per-input-type arrays (`keys`,
`controller_buttons`, `controller_axis`, `joystick_*`) collapsed into a single `inputs`
list of 19 slots, each holding all bindings for that N64 input together:

```json
[ {"Key": {"id": 26}}, {"ControllerButton": {"id": 11}} ]   ← W + DPAD_UP
```

Slot order: `DpadR DpadL DpadD DpadU Start Z B A CRight CLeft CDown CUp R L AnaR AnaL
AnaD AnaU <slot18>`. `Key` ids are USB HID usage codes.

gopher64 migrated our stale config successfully, but `default_config` has been regenerated
in the new schema so we no longer depend on that migration path.

**gopher64 uses the SDL3 gamepad enum**, *not* the raw joystick indices mGBA and Azahar
use — `9`/`10` are the shoulders here, where in mGBA/Azahar `4`/`5` are. Bindings are
device-agnostic (no GUID, no device name), so the profile ships as-is for every user.

<details>
<summary>Original (superseded) analysis</summary>

#### gopher64 controller assignment needs SDL device paths

gopher64 matches controllers by **exact string equality** against
`SDL_GetJoystickPathForID` (`src/ui/input.rs:577`: `path == *controller_assignment`) — a raw
device path like `\\?\HID#VID_045E&PID_02FF...` on Windows or `/dev/input/event*` on Linux.

The meta previously used `assignment_template: "XInput#{sdl_index}"`, producing `XInput#0`,
which can never match. There is no fallback: unmatched → `joystick_id` stays 0 → no gamepad
opened. Hot-plug handling only *reconnects* controllers whose GUID was already set, so it
doesn't rescue this either.

Current state (honest, not silently broken): `assignment_key_path`/`assignment_template`
removed from `meta.json`, and `controller_assignment` ships as `[null, null, null, null]`.
Users assign in gopher64's own GUI.

Proper fix: gopher64 exposes `--list-controllers` and `--assign-controller <N> --port <P>`
(`src/lib.rs`), where `assign_controller()` resolves the SDL path itself from an index. This
needs a post-install / pre-launch hook — probably a general `post_install_commands` concept
in `meta.json` that other emulators could reuse. **Requires physical controller testing**;
a wrong index→port mapping fails confusingly rather than obviously.

</details>

### #21 — systemic controller mapping (this document)

### Frontend stayed input-live behind a configure-only launch (FIXED)

`LaunchEmulatorWithoutGame` never assigned `activeEmulatorProcess`, so `IsEmulatorRunning`
stayed false and the input gate in `MainScene._Input` never engaged. The frontend kept
navigating its own UI while an emulator had focus.

Only the *configure* path was affected — the game-launch path always set it — so the bug
was invisible during normal play and appeared exactly when configuring an emulator's
controller bindings. It was actively corrupting saves: gopher64 wrote a dangling
`input_profile_binding` referencing a profile whose body never persisted, and the profile
saved correctly on the first attempt after the fix.

`activeGame` is deliberately left null so `_Process` cleanup runs without triggering a
save sync for a gameless launch.

⚠️ Side effect: the gate is also what enables the emulator-close hotkey, which defaults to
**LB + RB + Back + Start** held together — all buttons a user presses while mapping a
controller. Simultaneous presses make an accidental trigger unlikely, but it is now
possible where it previously was not.

### #22 — tier-2 emulators (see table above)

---

## The launch-time writer ships DISABLED as of v1.0.10

`EmulatorManager.SuspendControllerMapping = true`. This is a deliberate release decision,
not a leftover debug flag.

The writer's correctness was never actually established. What this session found:

- **`{controller_name}` is probably broken.** It resolves to Godot's name for the pad
  (`XInput Controller`); the emulator's SDL calls the same device
  `Xbox Series X Controller`. Dolphin's `Device = SDL/{sdl_index}/{controller_name}` is
  therefore suspect. The earlier "device name matched fine" note does not establish it.
- **`{controller_guid}` was scoped and abandoned** — see the GUID section above.
- **gopher64's `assignment_template` was outright wrong** and removed; the emulator
  self-assigns anyway.
- **Every emulator solved so far — mGBA, Azahar, gopher64 — was solved by shipping static
  device-agnostic config, bypassing the writer entirely.**

The only writer change that was genuinely verified is the `ConvertInputEventToStandardString`
face-button fix.

**Before re-enabling, audit `{controller_name}` and `{sdl_index}` against a real emulator
config** the same way the Azahar GUID question was settled — by hand-editing a live file
and observing what the emulator does with it.

## ✅ Inventory COMPLETE (as of v1.0.11)

Every emulator now maps an **Xbox** controller out of the box. 16 of 18 systems work with
no setup; Wii uses real Wii Remotes by design, and PS3/PS4 have no emulator shipped at all
(`rpcs3`/`shadPS4` are in `BuildDefaultEmulatorMap` but absent from `install_scripts/`).

**None of it uses the launch-time writer** — every fix is shipped static config.

---

## ⏭️ NEXT SESSION: non-Xbox controllers (Switch Pro, DualShock/DualSense)

The single biggest open item. Everything shipped assumes **XInput raw-joystick button
ordering** (`0=A 1=B 2=X 3=Y 4=LB 5=RB 6=Back 7=Start`).

### Safe — SDL normalises these, no work expected

| Emulator | Systems | Why |
|---|---|---|
| DuckStation, PCSX2 | PS1, PS2 | semantic names (`SDL-0/FaceSouth`) |
| Dolphin | GameCube | SDL gamepad names (`` `Button S` ``) |
| gopher64 | N64 | SDL3 **gamepad** enum |
| Flycast, PPSSPP | Dreamcast, PSP | auto-map from SDL's controller DB |

### At risk — raw joystick indices, **11 of 18 systems**

| Emulator | Systems | Form |
|---|---|---|
| ares | NES, Genesis, Sega CD, SMS, 32X | `/0/3/0` |
| mGBA | GB, GBC, GBA | `keyA = 1` |
| melonDS | DS | `A = 1` |
| Azahar | 3DS | `button:1` |
| snes9x | SNES | `(J1)Button 0` |

**Predictions (unverified — measure, don't assume):**

- **DualShock 4 / DualSense** likely breaks. Raw HID order is `0=Square 1=Cross 2=Circle
  3=Triangle` = west, south, east, north, against Xbox's south, east, west, north. Face
  buttons scramble; L2/R2 also differ (button *and* axis on PlayStation pads).
- **Switch Pro** may work by luck. SDL's raw order `0=B 1=A 2=Y 3=X` lands on south, east,
  west, north — the same positional sequence as Xbox, because Nintendo's B is the bottom
  button.

### Cheapest first step

Plug in a DS4 or Pro pad, map **mGBA** (smallest config), diff the indices against the Xbox
ones. That one test tells us whether SDL is normalising underneath and sizes the whole job.

### If they differ, the work is

1. A mapping pass per controller type per at-risk emulator (5 × 2 = 10 passes).
2. Per-controller-type variants in `default_config` — a new concept.
3. Detection in the frontend. The GUID is already logged; vendor ID gives the family
   (`045E` Microsoft, `054C` Sony, `057E` Nintendo). This is coarse-grained — controller
   *family*, not identity — so it avoids the trap that killed the GUID approach.

Worth checking first whether any at-risk emulator can be switched to a **normalised binding
mode**; for some that would turn this into a config change instead of a variant matrix.

### Breaks regardless of the above

**Dolphin's `Device` line.** A DS4 enumerates as `SDL/0/PS4 Controller`, a Pro as
`SDL/0/Nintendo Switch Pro Controller` — neither matches the shipped `Xbox One Controller`.
Bindings are fine; the device must be picked once in Dolphin's UI. Already true for any
non-Series-X pad, including a Hyperkin Duchess.

---

## Other open items, smaller

1. **Verify on clean installs.** ares and snes9x shipped their `default_config` seeds but
   have only been exercised as live configs. Both are *partial* files relying on the
   emulator to fill in the rest — that assumption is untested. Azahar and melonDS likewise
   unverified from a fresh install.
2. **Confirm mGBA's GB/GBC d-pad** — the `gb.*` hat values are extrapolated from the `gba.*`
   block, not captured.
3. **melonDS left stick** is unbound (the DS has no analog stick). Unclear whether melonDS
   reads axis 0/1 for the d-pad independently.
4. **snes9x on Linux is unmapped** — GTK build, different config format and joypad syntax.
   Do not guess it from the Windows form.
5. **DuckStation `[Pad2]`** is `Type = None` while PCSX2 ships a working player 2 on
   `SDL-1/`. Arbitrary inconsistency; copying PCSX2's block would close it (needs a second
   pad to verify).
6. **Decide the writer's fate.** Its `Device` handling is fixed and `{controller_guid}` is
   retired, but `{sdl_index}` was never audited — and tier 1 demonstrably does not need it.
   Retiring it entirely is now a serious option.
7. **The face-button swap setting** was never built. Positional is hardcoded per shipped
   config. Note it **cannot reach ares** (its VirtualPad→core mapping is internal), so the
   setting can never be fully universal.
8. **Harden `ConnectionOrder`** if multi-controller or reconnect scenarios misbehave.
9. **`docs/` is untracked** — this file is not in git.

---

## Files touched

| File | Change |
|---|---|
| `scripts/main/handlers/MainSceneInputHandler.cs` | `ConvertInputEventToStandardString` face buttons → canonical names |
| `scripts/autoloads/EmulatorManager.cs` | `ResolvePlatformMacros` fallback + log; `ApplyControllerMappings` zero-controller guard; launch command logging |
| `install_scripts/dolphin/meta.json` | removed `-u ./` from both launch arg strings |
| `install_scripts/gopher64/meta.json` | removed bogus `assignment_template`/`assignment_key_path` |
| `install_scripts/gopher64/default_config/.../config.json` | `controller_assignment` → `[null x4]` |

### v1.0.10 session

| File | Change |
|---|---|
| `scripts/autoloads/AppUpdater.cs` | version `v1.0.9` → `v1.0.10` |
| `scripts/autoloads/EmulatorManager.cs` | `SuspendControllerMapping` flag (ships **true**); `LaunchEmulatorWithoutGame` now sets `activeEmulatorProcess` so the input gate engages |
| `scripts/autoloads/ControllerManager.cs` | log the GUID on connect |
| `install_scripts/mGBA/default_config/config.ini` | static `[gba.input.SDLB]` + `[gb.input.SDLB]` gamepad bindings |
| `install_scripts/azahar/default_config/.../qt-config.ini` | full device-agnostic `[Controls]` block |
| `install_scripts/azahar/meta.json` | `preserve_on_reinstall` for `qt-config.ini` |
| `install_scripts/gopher64/default_config/.../config.json` | regenerated in the new `inputs` schema; N64 Z → right trigger |
| `install_scripts/melonDS/default_config/portable/melonDS.toml` | static `[Instance0.Joystick]` bindings + `JoystickID` |
| `install_scripts/dolphin/default_config/.../GCPadNew.ini` | `Device` → `SDL/0/Xbox One Controller` (SDL's name for a Series X pad) |
| `install_scripts/dolphin/meta.json` | `preserve_on_reinstall` for `GCPadNew.ini` |
| `scripts/autoloads/EmulatorManager.cs` | `IniConfigurationUpdater.ReadValue`; writer no longer overwrites an existing `Device` line |
| `install_scripts/snes9x/default_config/snes9x.conf` | **new** — Windows `[Controls\Win]` Joypad1 bindings (the shipped Linux config was unreachable on Windows) |
| `install_scripts/snes9x/meta.json` | `preserve_on_reinstall` for both config paths |
| `install_scripts/ares/default_config/settings.bml` | **new** — device-agnostic `VirtualPad1` bindings (covers all 5 ares systems) |

---

## Also open (not controller-related)

- **Wii games not downloading** — never investigated. ROM transfer path, not emulation;
  Wii images run ~4.7 GB. Need the symptom (error / stall at % / partial file).
- **Unverified saves**: melonDS and PPSSPP.
- **Awaiting re-test**: Flycast per-game VMU (`PerGameVmu=yes`), Sega CD `--system "Mega CD"`,
  Dolphin GC/Wii save alignment.
