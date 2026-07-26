# Design Notes

Non-obvious behavior, external-system quirks, and fixed bugs that the code cannot express through
naming alone. The codebase itself carries no comments (see `CLAUDE.md`); this file is where the
"why" lives. Add to it whenever you fix something whose cause would not be obvious from reading the
resulting code.

---

## Emulator integration

### Azahar / Citra-lineage: `<key>\default` companion booleans
Azahar stores each setting in `qt-config.ini` alongside a companion `"<key>\default"` boolean. On
read, if `"<key>\default"` is true the stored value is **ignored** in favour of the built-in default
(see `QtConfig::ReadSetting` in azahar's `config.cpp`). Writing only `"<key>"` is therefore silently
discarded whenever the companion is true. `QtConfigurationUpdater` writes the value *and* sets
`"<key>\default = false"`, which is exactly what Azahar does when a user changes a setting.

### INI updater must stay last
`IniConfigurationUpdater` is a deliberate catch-all for INI-shaped formats: `.ini` (PCSX2,
DuckStation, mGBA), `.cfg` (flycast's `emu.cfg`) and `.toml` (melonDS), which all use
`[Section]` + `key = value`. It must remain last in the updater list, or it claims formats that have
a dedicated updater — notably ares' `settings.bml`, which is indentation-based and would be
corrupted by INI-style writes. The extension exclusion list keeps this correct regardless of
ordering, but the ordering is still load-bearing and there are two lists that must agree.

### A cached firmware path must be re-validated, not just null-checked
`GameSystem.PrefferedFirmware` is an absolute path, and `GameSystem` is serialized into
`systems.cache` next to the executable. The scan that fills it in only ran when the field was
**empty**, so a stored path was kept forever even once it pointed nowhere.

That breaks in two ways. Move the app to another drive or folder and firmware silently resolves to
the old location. Worse, carry the cache to another platform: a Windows value like
`E:/Projects/romm-frontend/bios/nes/disksys.rom` is **not rooted on Linux** — `:` has no meaning
there — so `Path.GetFullPath` resolves it against the working directory and yields
`<app dir>/E:/Projects/romm-frontend/bios/nes/disksys.rom`. Measured under WSL. A literal `E:`
directory then appears beside the executable and accumulates a duplicate of the whole tree; one
release build had grown a 316 MB copy of `bios` that way.

`IsUsableFirmwarePath` requires the path to be non-empty, rooted **and** to exist, and both the
consumer (`ResolveFirmwarePath`) and the producer (the firmware scan on the loading screen) go
through it, so a stale value is re-derived instead of followed. This mirrors how
`ResolveRelocatablePath` already treats stored directory overrides: honour them only if they still
exist, otherwise fall back to the derived default.

### Executable resolution falls back to regex
Emulator executables resolve by literal `executable_name` first, then by a per-OS
`executable_regex`. The fallback exists for version-stamped release filenames (AppImages in
particular), which change name with every release, so a literal name would break on upgrade.
`UniversalInstaller` also uses the regex to remove previously installed versions, so only one
remains after a reinstall.

### Per-OS config targets
`config_file_relative_path`, and the section/key fields alongside it, are each either a plain string
(same on every OS) or an object keyed by OS name:

```json
"config_file_relative_path": { "windows": "snes9x.conf", "linux": "config/snes9x/snes9x.conf" }
```

Some emulators use different config files or key names per platform. Always resolve these through
the `Resolve*` helpers rather than reading the fields directly.

### Environment variables are sometimes the only lever
Some emulators hardcode a user config location with no portable mode and no CLI override —
`snes9x-gtk` resolves `XDG_CONFIG_HOME` or `$HOME/.config`. Per-OS `environment_variables` in the
meta are the only way to keep their config and saves inside the install directory. Values support
the `{emulator_dir}` placeholder.

### `{system}` launch fragments
One emulator can serve several platforms from a single meta (ares). Auto-detection by file extension
is ambiguous for shared formats like `.bin`/`.cue` (Genesis vs. disc systems), so `system_flags` maps
a system slug to the launch fragment substituted for `{system}` — e.g. `--system "Mega Drive"`.

### Cores install on demand, not at emulator install time
RetroArch declares 19 cores across 14 systems; downloading all of them to play one game wastes about
30 MB. `EnsureCoreInstalled` runs at launch, after the save link and sync, and fetches only the core
the selected system actually needs. Switching core in settings therefore needs no reinstall — the new
one is fetched the next time that system launches.

`core_directory` plus a per-OS `core_file_name` are the single source of truth for a core's on-disk
path; `core_launch_arg` is a plain string using `{core_path}`, so the `.dll`/`.so` split lives in one
field rather than being duplicated into every system's launch fragment. `cores` is in
`preserve_on_reinstall` so an upgrade does not discard cores it would only re-download.

### Installing while the emulator runs corrupts the install
`UninstallEmulator` refused to run while an emulator was open but `InstallEmulator` did not, so
reinstalling RetroArch while it was running deleted what it could and then failed on the locked DLLs
(`avcodec-58.dll` is in use), leaving the install partly cleared — in practice an empty `cores`
directory and a working executable. `InstallEmulator` now carries the same `IsEmulatorRunning` guard.

### Default emulator per system favours RetroArch for pre-gen-6 and cartridge systems
`GetMappedEmulator` returns index `[0]`, so ordering in `BuildDefaultEmulatorMap` is the default.
RetroArch is first for generations 1-5 and for all cartridge systems (`nes`, `snes`, `n64`, `genesis`,
`sms`, `sega32`, `segacd`, `psx`, `gb`, `gbc`, `gba`, `nds`) because those are the systems RomM's
EmulatorJS browser player supports, so a save written by RetroArch is also playable in the web UI.
Gen-6-and-later disc systems keep their native emulator; 3DS is a cartridge system but has no usable
libretro core, so azahar stays.

`MergeMissingDefaultMappings` appends and never reorders, so this changes fresh installs only. That is
deliberate: reordering an existing user's map would switch their emulator, and since saves are not
converted their existing saves would appear to vanish.

### Core choice is per system, so it cannot be a `settings_field`
`settings_fields` are per-emulator, but RetroArch needs Mesen for NES *and* snes9x for SNES at the
same time. A single dropdown on the RetroArch entry would apply one core to every system.

`system_cores` maps a system slug to a list of core names, first entry being the default, and
`core_launch_arg` carries the per-OS template (`-L "cores/{core}_libretro.dll"` against `.so`). That
keeps the only per-OS difference — the shared-library extension — in one place instead of duplicating
a fragment per system, and it means `extra_downloads` can be generated from the union of declared
cores rather than hand-maintained alongside it.

The chosen core is stored in `[PreferredCores]` keyed by system slug alone, not by emulator. A value
left over from a different emulator simply is not in that emulator's list for the system, so
`ResolveSelectedCore` falls back to the default instead of producing a broken `-L` path. `{system}`
resolves through `system_cores` when the emulator declares them and falls back to `system_flags`
otherwise, which is what ares still uses.

### `{settings}` placeholder position matters
Settings-derived launch args are substituted at `{settings}` if the template contains it, and
appended otherwise. The placeholder exists for emulators that require the ROM path to be the **last**
argument (Azahar): those put `{settings}` before `{rom_path}` so appended settings cannot push the
ROM out of final position. When removing the placeholder, drop exactly one adjacent space — never
collapse other whitespace, since a ROM path may contain consecutive spaces.

### Save data lives inside the install directory
Every emulator keeps its saves inside its own install directory (memory cards, save folders), so
uninstall and reinstall must know which sub-paths to leave alone. Two separate lists exist and the
distinction is important:

- `relative_save_path` — game save data. Drives both preservation *and* save sync to RomM.
- `preserve_on_reinstall` — config the user shouldn't lose but which is not save data, e.g. ares'
  `settings.bml` holding controller mappings.

Save sync uses `GetSaveRelativePaths()` specifically so preserved config is never uploaded to RomM
as a game save.

### Prerelease filtering
GitHub release recipes prefer stable releases, but some repos (PCSX2) flag every rolling release as
a prerelease. When prereleases are all that exist, they are used.

### Platform mapping merges on upgrade
The on-disk `EmulatorMap.json` is written once at first run. Platforms added to the defaults in a
later version would never reach an existing install — and because systems with no mapping are
filtered out of the game list entirely, those platforms would silently disappear from the UI rather
than failing visibly. Missing default keys are unioned in on load; existing keys are never touched,
so user edits and `PreferredEmulators` overrides both survive.

---

## Controller handling

### Controller-config macros are disabled
The macro-driven controller-binding writer ships disabled (`static readonly`, not `const` — a const
would be folded at compile time and make every guarded branch an unreachable-code warning). Two
reasons:

1. The writer stamps bindings into the exact files that need to be read back clean after a manual
   in-emulator mapping pass.
2. Its correctness is in doubt. `{controller_name}` resolves to Godot's name for the pad (`"XInput
   Controller"`), which is **not** what the emulator's SDL calls the same device (`"Xbox Series X
   Controller"`), so Dolphin's `Device = SDL/{i}/{name}` line likely never matched. gopher64's
   `assignment_template` was outright wrong and was removed. Every emulator fixed so far was fixed by
   shipping static config instead.

See `docs/controller-followups.md`. Audit these macros before re-enabling.

### Never overwrite an existing device line
Following from the name mismatch above: Dolphin requires an exact device-name match and has no
fallback (verified — deleting the `Device` line entirely kills all input). A synthesised name reads
as disconnected and silently breaks every binding in the section. Once the emulator or the user has
written a real device there it is authoritative; only fill it in when missing.

### Do not rewrite config when no controllers are detected
Several install scripts ship static player-1 bindings (PCSX2, DuckStation on SDL-0). If no
controllers are detected, the writers would stamp port 1 as "disconnected" and wipe those bindings.
Leaving the config untouched preserves whatever was shipped.

### Face buttons are positional, not Xbox-lettered
Godot names the face buttons Xbox-style, but the canonical vocabulary here — `StandardSdlInputs` and
every emulator's `sdl_string_map` — is positional. `JoyButton.A/B/X/Y` are South/East/West/North
respectively. Returning `"A".."Y"` produced values no `sdl_string_map` contains, which resolved to
empty bindings and left the face buttons dead.

A stored per-platform mapping can also name an input a given emulator's `sdl_string_map` doesn't
know (`"A"` instead of `"FaceSouth"`). Falling through with an empty string writes a blank binding
that the emulator silently discards, so fall back to the `platform_layout` default, which is always
a valid key for that emulator.

### Emulator processes are tracked even without a game
`IsEmulatorRunning` gates the frontend's input handling. Without tracking a gameless emulator launch
the UI keeps reacting to the controller behind the emulator, which makes configuring an emulator's
own controller bindings nearly impossible. `activeGame` stays null so the process cleanup runs
without triggering a save sync.

---

## Rendering and theming

### Mica frosted glass needs the normal draw flow
Popups are deliberately **not** `TopLevel`. Staying in the normal draw flow (added last, so they
render on top) is what lets Godot auto-copy the back buffer for the mica shader. `TopLevel` plus a
manual `BackBufferCopy` did not feed the screen texture under the `gl_compatibility` renderer, so the
frost never blurred.

Where a mica material is used, the `StyleBoxFlat` only defines the rounded shape — the shader
replaces the fill with blurred screen + theme tint. Keep the stylebox opaque so the whole panel is
covered.

### Panel edges are drop shadows on a `ShowBehindParent` child
Panels are separated from the background by a shadow rather than an outline. Two constraints shape
how it is drawn:

- It cannot come from the mica panel's own stylebox. The shader forces `COLOR.a = 1.0` across every
  fragment the stylebox covers, so the shadow margin would render as an opaque rectangle instead of
  a soft falloff.
- It cannot be reconstructed inside the shader either. Anything derived from UV derivatives ends up
  ~1px asymmetric (top/left vs. bottom/right) from sub-pixel rasterization.

So `MicaShadow` attaches a child `Panel` carrying a shadow-only `StyleBoxFlat`, marked
`ShowBehindParent`. That flag is what makes it robust: the shadow draws behind its parent regardless
of sibling index, so it cannot be broken by content added later. An earlier border overlay relied on
being added *last* and silently ended up underneath the content when panel construction moved into a
base class.

The shadow draws before the panel, so Godot's back-buffer copy includes it and the frost pulls some
of that darkness inward at the edges. At the shipped shadow size this is not visible; a much larger
shadow combined with the wide `blur_radius` would read as a vignette.

### Frost has a luminance floor, not a brightness knob
Both inputs to the frost are dark — the blurred backdrop and the theme tint, which is deliberately
`GetDarkestColor` at ~0.55 alpha — so over dark content a panel collapses toward black and text on it
stops reading. `luminosity_floor` lifts the finished panel up to a minimum luminance.

It is a floor rather than a flat additive brightness so panels over bright content are left alone; a
flat lift washes those out to fix a problem they do not have. It is applied *after* the tint, which
makes it a guarantee about the final pixel instead of an input the tint can undo. Because the lift is
equal across RGB it desaturates slightly as it brightens, which is the same thing Acrylic's luminosity
layer does and is what makes text sit cleanly on the result.

`MainScene.ApplyTheme` pushes the value from an export, so themed panels follow the inspector. The
login and loading screens never call `ApplyTheme`, so they use whatever is stored in
`mica_panel.tres`.

### Only the selected card is frosted
Mica samples the back buffer and carousel cards overlap by design, so frosting all of them would
have them blurring each other's output instead of the background.

### Background styles are separate shaders
Each background style is its own shader sharing the uniform names declared in `bg_common.gdshaderinc`,
so applying a theme swaps the shader on the shared material and then re-applies one set of
parameters. Swap the shader *before* setting parameters, or the values land on a shader that isn't
going to render them.

### Palettes are hand-tuned, not derived
Palette entries are hand-tuned rather than produced by a blanket `.Darkened()` pass: the shader mixes
Primary and Secondary over Bg aggressively, and uniformly darkening every palette collapsed them all
into the same murky blue-purple. The stored values are the final rendered colors. Secondary doubles
as the UI accent (popup focus highlight), so it is kept the brighter and more saturated of the two
blobs while staying dark enough not to wash the flow out.

### "Match System" is a mode, not a palette
It derives colours from the selected platform's logo at runtime via `SystemPalette`, so it has no
fixed values and is deliberately absent from `Themes` and from `themes.json`. The settings list
offers it alongside the real palettes, and it must be re-applied whenever the selected platform
changes.

Only the *hue* is taken from the artwork; saturation and value are forced to the same targets the
hand-tuned palettes use, because logo colours are chosen to read on packaging at full brightness and
blow out the background if used unaltered. The plain platform mark is preferred over the "titles"
wordmark, since wordmarks are more often flat white lettering while the mark usually carries the
brand colour. Logos are downsampled before hue analysis and buckets are weighted by saturation and
value, so a small vivid mark outvotes a large washed one. When a logo yields no usable colour — a
great many are pure white or greyscale silhouettes — the caller falls back to a static palette,
because an all-grey background reads as broken rather than as a choice.

### Panels own their open/closed state
`UiPanel` is the single panel type — usable both as a node in a `.tscn` and constructed in code,
which is what collapsed the three separate construction idioms that preceded it (scene-authored,
per-popup class, and inline-in-`_Ready`).

The load-bearing part is that `PanelState { Closed, Opening, Open, Closing }` replaces `Visible` as
the source of truth. `Visible` cannot represent "on screen but closing", and everything that used to
compensate for that gap — a static side-table keyed by instance id, a parallel `hiding` set, a
per-popup `IsClosing` flag, and callers re-deriving "already open" vs "currently closing" — existed
only because the state was missing. Check `IsOpen`, never `Visible`: during a close animation
`Visible` is still true, and routing input to a panel that is fading out was a real bug.

- `Open`/`Close` are idempotent, because callers drive them from `_Process`. A non-idempotent version
  restarts its tween every frame: the animation is killed after one frame of progress and restarts
  from the current value, so it creeps and never arrives. `Open` on a closing panel correctly
  reverses it, which is what makes a panel dismissed and immediately re-triggered stay on screen.
- The scale target must **not** be the root for a panel with a dimmed backdrop — scaling the root
  scales the backdrop and pulls its edges away from the screen. `UiPanel` owns its backdrop, so it
  picks the inner panel itself rather than trusting callers to pass one.
- Content-sized panels (the fuzzy search box grows with the query) have not been laid out at their
  new size when open is called, so the scale pivot is set once from the current size and again after
  the layout pass. For the same reason, set the text *before* opening.
- A full-rect panel root would swallow every mouse event on screen, so the root and its centring
  container are `MouseFilter.Ignore`; only the backdrop, when present, deliberately blocks.

### Sections cannot crossfade while they share a container's layout
The three sections are `size_flags_vertical = 3` children of one `VBoxContainer`, so the VBox divides
its height between whichever of them are **visible**. An animated close keeps the outgoing section
visible for the duration of its fade, which means two expanding children exist at once and the VBox
gives each half the height — both squash and snap back, which reads as a hitch rather than a fade.

So the outgoing section is closed with `animate: false` and only the incoming one animates. This is
what the pre-`UiPanel` code did too, though by accident rather than intent: it hid every non-target
section up front and *then* tweened the outgoing's alpha, so that tween had nothing to render.

Footers are exempt and do crossfade, because they are anchored siblings inside a `Panel` — two
visible at once overlap instead of competing for space.

The general rule: `Close` with animation means "stays visible while fading", which is right for an
overlay and wrong for anything whose visibility feeds a parent container's layout. A real section
crossfade would require lifting the sections out of the VBox and anchoring them.

### The panel stack is pushed from `AboutToOpen`, not `Opened`
`UiPanelStack` decides which panel owns input, and `MainScene._Input` consults it in one place
instead of carrying a hardcoded branch per popup. It tracks membership from the `AboutToOpen` /
`AboutToClose` pair rather than `Opened` / `Closed`, because the latter fire at the *end* of their
animations: a panel would sit outside the stack for the whole ~0.18s open, and input meant for it
would leak through to the game list underneath. The about-to pair matches `IsOpen` exactly.

Mouse events are deliberately not routed through the stack — they must reach Godot's normal GUI pick
so buttons inside the panel stay clickable. `MainScene` returns early on them without marking them
handled, which is also why nothing further down `_Input` may act on a mouse event while a panel is
open.

Guards elsewhere in `_Input` that tested a specific popup's visibility are unreachable once the stack
returns first, so they were removed rather than left as reassuring no-ops.

### Section transitions
`CurrentSection` is the single source of truth for which top-level section is on screen. Section
state used to live implicitly in each container's `Visible` flag, with the settings toggle and the
list swap independently re-deriving which footer belonged to which section and duplicating the focus
restore.

This matters for input routing: during a crossfade the outgoing and incoming sections are *both*
visible, so `Visible` checks cannot say which one owns the input. Every input path gates on the
transition state instead. The logical state is flipped up front so anything asking "where am I?"
mid-transition — the header label especially — describes where we are going. Focus is applied only
once the incoming section has arrived, otherwise controller input lands on a list that is still
fading out. An interrupted transition must not strand a container at half alpha, so the running tween
is killed and every section snapped to a known state first.

Incoming sections start fractionally small and settle, scaled from the middle: animating position
instead would fight the layout, since containers overwrite child positions.

---

## Performance

### Asset queue is priority-ordered, not FIFO
Newly requested (on-screen) games are pushed to the **front** so the game the user is currently on is
fetched before games they merely scrolled past. A plain FIFO put the current selection behind
everything already queued — a priority inversion. Items are pushed reversed so the 3D cover ends up
frontmost.

Because only the on-screen window is ever queued (off-screen games are pruned), the queue can afford
more workers and no inter-download delay. Pruning on scroll-out means a fast scrub doesn't rack up a
backlog of art for games nobody is looking at; anything already handed to a worker finishes, and only
pending items are dropped.

A false result from an asset download is usually just a 404 (no art on the server), which is normal
and not worth logging. Genuine failures are already logged with URL/status detail inside
`DownloadAssetAsync`.

### Cover decodes are spread across frames
Cover art is decoded on the main thread at roughly 5ms per entry. The carousel reveals ~25 entries at
once when a list is built, which stalled the frame for ~130ms right between the fade-out and fade-in
of a system switch. Loads are budgeted **by time rather than by a fixed count** — a fixed count
blows the frame budget on slower machines, since decode time varies with image size and disk. One
load always runs so the queue cannot stall. Each entry keeps its placeholder until its turn.

The queue is drained highest-card-on-screen first rather than oldest-request-first, so the list fills
downward instead of outward from the selected game. That ordering has to happen at drain time, not
request time: the carousel queues entries in child-index order (unrelated to vertical position) and
assigns each child's `Position` *after* flipping `Visible`, so at request time the card has not been
placed yet.

### Cards reveal on attempt, not on success
Cards are built hidden and revealed once their cover load has been *attempted*. Plenty of games have
no art at all, and gating the reveal on success would leave those cards invisible forever.

### Fallback extensions are probed last
The fallback-extension `FileExists` probes run only after the 2d/3d covers have missed. They
previously ran for every entry, including the common case where a 2d cover exists and the result was
thrown away.

---

## Input and focus

### Settings panels are keyed by a sanitised node name
Each platform's settings panel is a child node named after the system's *display* name, and is later
found again by that same name. Godot silently strips characters it reserves in node names — `/`, `:`,
`@`, `"`, `%`, `.`, `$` — so assigning `Name = "SegaMegaDrive/Genesis"` stores
`SegaMegaDriveGenesis`, while `HasNode`/`GetNodeOrNull` given the unstripped string parse the `/` as
a **path separator** and look for a grandchild that does not exist. The panel was therefore built and
then never found: RomM reports Genesis as "Sega Mega Drive/Genesis" and Master System as "Sega Master
System/Mark III", and those two platforms had no reachable settings at all, while every slash-free
platform worked.

`BuildSectionNodeName` strips whitespace and every reserved character, and both the create and the
lookup path go through it. For names without reserved characters it returns exactly what the previous
`Replace(" ", "")` did, so existing panels are unaffected. Do not go back to trimming only spaces,
and do not key these panels on the display name without sanitising it — the system slug would be the
safer key if this ever needs revisiting.

### Footer shortcuts are controller-only
Footer-button actions are consumed only when driven by a controller. In keyboard/mouse mode the
footer buttons are clicked instead, which keeps their bound keys free for fuzzy search and normal
typing — Backspace for the downloads page, Enter for select.

### Manual `Pressed` emission bypasses the disabled gate
Emitting `Pressed` manually, and controller "Select" routing directly, both bypass Godot's
disabled-button gate. The disabled state has to be re-checked by hand at those call sites, e.g. while
an emulator install or download is running.

### Focus-follows-mouse
For keyboard/mouse users, whichever focusable widget the cursor moves over takes focus; the nearest
focusable *ancestor* is used, so hovering a settings entry's inner widget still focuses the entry.
The game carousel is handled at list level since it drives its own index-based selection rather than
per-entry focus.

While a modal popup owns the screen, only controls inside it may take mouse focus — otherwise
incidental mouse motion over the UI behind it steals focus from the popup entries and controller
input goes dead.

Disabled controls are skipped during d-pad navigation so focus never parks on something that can't be
actioned.

### Fuzzy search ignores an empty buffer
`Contains("")` matches everything, which would jump to the first game the moment the user backspaces
their query away.

---

## Configuration and paths

### Two kinds of path, two resolution rules
- **App-layout dirs** (downloads, install_scripts, tools, assets) are always derived from the
  executable's own folder and deliberately never persisted, so a build stays relocatable: move it to
  another PC or OS and it recomputes its own paths. Keys baked in by older versions are scrubbed on
  load.
- **User-relocatable dirs** (a user may point these at a separate or larger drive) honour a stored
  override *only if it still exists on disk*, otherwise falling back to the derived default. This
  makes stale absolute paths from a moved folder or a different machine self-heal instead of
  breaking. They are persisted only when they actually differ from the default.

### Runtime directories get a `.gdignore` written at startup
`emulators/`, `saves/`, `downloads/`, `roms/` and `bios/` sit inside the project folder but hold
downloaded data, not Godot resources. Without a `.gdignore` the editor imports all of it — a single
RetroArch install contributed 9,362 PNGs and roughly 8,600 `.slangp`/`.slang`/`.glsl`/`.glslp` shader
files, which Godot tries to parse as its own and reports as load errors. It had generated **10,451**
`.import` files under `emulators/` and a 1.6 GB import cache.

A committed `.gdignore` does not solve this, because those directories are in `.gitignore`: a fresh
clone recreates them on first run and the problem returns. `EnsureRequiredDirectoriesExist` therefore
writes a `.gdignore` into each one at startup, which is self-healing and survives a clone.

`assets/` is deliberately **not** in that list even though its cover and screenshot caches are — the
same directory holds `assets/shaders/*.gdshader`, which the project loads through `res://`. Ignoring
it wholesale would break every background shader. Only the cache subdirectories are ignored.

### Themes merge, they don't replace
`Themes` is rebuilt from the built-ins plus `themes.json`. The built-ins are always present, so a
JSON entry reusing a built-in name retunes that theme and removing the entry reverts it — this way
palettes added in a later version still reach users who already have the file. An unrecognised style
name in `config.cfg` (a hand-edit, or a style removed in a later version) falls back to the first
style so the background still renders. `themes.json` is seeded with the built-in palettes: they are
all redundant overrides at that point, but they document the format.

Anything reading `Themes` must run after `ConfigManager._Ready`, which is what populates it.

---

## Central save store

Every emulator's save directory is replaced by a filesystem link pointing at
`SavesPath/<emulator_slug>/<relative_save_path>`. The emulator is never reconfigured — it writes to
the path it always wrote to and the bytes land in the store.

### Why links instead of emulator configuration
Of the eleven recipes, only ares can be pointed at an arbitrary save directory
(`--setting Paths/Saves=`). flycast, ppsspp and snes9x expose only XDG environment variables, which
are Linux-only — on Windows they anchor to the install directory with no lever at all. The remaining
seven have no redirect mechanism. Links sidestep the question instead of asking ten emulators to
cooperate, and they make reinstall safety structural rather than dependent on `preserve_on_reinstall`
being correct.

### Junctions on Windows, not symlinks
`Directory.CreateSymbolicLink` needs `SeCreateSymbolicLinkPrivilege` — administrator rights or
Developer Mode. The app must stay unzip-and-run, so that is unacceptable. Measured on a stock
non-elevated Windows 11 account with Developer Mode off: `Directory.CreateSymbolicLink` **fails**,
while `mklink /J` **succeeds**. .NET has no junction API, hence shelling out to `cmd /c mklink /J`.
Junctions are directory-only and local-volume-only, which is all this needs. On Linux and macOS
`Directory.CreateSymbolicLink` works unprivileged and is used directly.

Do not "simplify" this back to `CreateSymbolicLink` on Windows. It will work on the developer's
machine (Developer Mode is usually on) and fail for users.

### Saves are pooled, so link at the directory level
Several systems have no per-game save file. PCSX2 and DuckStation use shared `memcards`, Dolphin uses
`User/GC/<region>/Card A`, Azahar uses `user/sdmc` plus `user/nand` — one file or one emulated
filesystem holds many games' saves. Any design that assumes one directory per game is wrong for
exactly the systems users care most about. The save directory is treated as opaque and linked whole;
card contents are never split, merged, or converted.

### ares and Genesis Plus GX disagree about Mega Drive SRAM
Measured on Sonic 3, same ROM, same save. ares writes `<rom basename>.ram`, 512 bytes, one SRAM byte
per byte. Genesis Plus GX writes `<rom basename>.srm`, 604 bytes, each SRAM byte stored in the low
half of a big-endian 16-bit word with `0xFF` in the high half — the 8-bit-SRAM-on-a-16-bit-bus
representation. All 302 of the core's low bytes match ares' first 302 exactly; ares' remaining 210
bytes are `0xFF` erased-state padding, and the core simply stops at the last meaningful byte.

A conversion would be lossless both ways — ares to libretro is trim trailing `0xFF` then emit `FF b`
per byte; libretro to ares is take the odd-indexed bytes then pad — and NES and SMS were measured as
plain renames needing no transform at all (Zelda: both 8192 bytes, exact alignment at offset 0;
Phantasy Star: two differing bytes across the 8188-byte overlap, ares padding the tail to 32768 with
`0xFF`).

**We deliberately do not convert.** Saves stay per-emulator and are tagged with the emulator on
upload, so both versions coexist on the server without contaminating each other. A user who wants a
save that also works in RomM's browser player picks the RetroArch entry for that system, which is one
dropdown and no magic. Conversion would have bought only the rare deliberate act of switching
emulator, in exchange for a permanent lossy-transform surface, a per-system format table, and an
unverifiable guess: the padded size when writing *into* ares is not derivable, since ares wrote 512
bytes for Sonic 3, 8192 for Zelda, but 32768 for Phantasy Star where the core writes 8188.

The measurements are recorded here so this can be revisited without redoing the work. The known cost
of not converting is that switching a system's emulator makes existing saves appear to vanish; the
netplay design already flags warning on that switch as the minimum mitigation.

### RetroArch must not compress save files
`save_file_compression` makes RetroArch write SRAM as a **zip archive** still named `.srm`. Anything
reading these files — save sync, or the conversion above — would be parsing a zip header as save
data. Both compression keys are pinned off in the shipped `retroarch.cfg`. `config_save_on_exit` is
pinned off for a different reason: RetroArch rewrites relative directory paths as absolute ones on
exit, which would break the portable install the first time it closed.

### A save directory's children must each be one game's save
`SyncAfterExit` treats every top-level entry of a save directory as a single game's save unit: files
upload directly, directories are zipped as `<name>.folder.zip`. That is correct for file-shaped saves
and for PPSSPP, where one directory genuinely is one game's PSP save.

It was wrong for ares, whose `relative_save_path` was `saves` — a directory whose children are
*per-system* folders holding many games each. Playing one Mega Drive game uploaded every Mega Drive
save under that game's `rom_id`, and the download path extracts over the whole directory, so a stale
bundle could overwrite other games' saves. `relative_save_path` is now keyed per system
(`saves/Famicom`, `saves/Mega Drive`, …) so the children are individual `.ram` files. When adding a
recipe, check what the save directory's immediate children actually are.

### `sync_include` is an allowlist, and preservation is separate from syncing
Some save directories hold far more than saves. flycast's `data` holds the Dreamcast and Naomi BIOS
images, a shader cache and a boxart cache alongside the VMU files — measured at 7.1 MB per sync of
which 6.6 MB (92%) was firmware and caches being uploaded to RomM as save data. `sync_include` is a
glob **allowlist** applied to each top-level item, deliberately not a denylist: a file the recipe
does not name is never uploaded, so a new file appearing in a future emulator release cannot silently
start syncing.

Omitting `sync_include` means "sync everything" and is the default; an empty list means sync nothing.

### `sync_save_path` narrows the sync scope without narrowing the link scope
`relative_save_path` answers two questions at once: what gets linked into the store and preserved
across reinstall, and what save-sync walks. Those are not always the same directory. azahar is the
case that forces them apart — 3DS game saves live at
`sdmc/Nintendo 3DS/<id0>/<id1>/title/00040000/<title id>/`, six levels down, while the directories
above it hold the emulated NAND (tickets, system config, Mii), an applet extdata tree and a photo
cache. Syncing from the top uploaded all of it under one game's `rom_id`.

`sync_save_path` is consulted by `SaveSyncManager` only, and supports `*`/`?` segments expanded
against the filesystem, so the two 32-hex emulated-SD ids do not have to be hardcoded — they are all
zeros on a default install but not guaranteed to be. `relative_save_path` is unchanged, so `user/nand`
and `user/sdmc` are still linked into the store and still survive a reinstall; only the *sync* scope
narrows. When the field is absent, `relative_save_path` is used exactly as before.

With the scope correct, the per-game unit falls out for free: the children of `title/00040000` are
per-title directories, so the existing modified-item detection uploads only the game just played, with
no need to map a ROM to its title id.

### Re-keying a save path splits an existing link
Narrowing `relative_save_path` from a parent to its children strands the old link: the new paths sit
*inside* it, so linking them would point a directory at itself, and `ClearDirectoryPreservingPaths`
would delete the parent link on reinstall because a link is checked before the
ancestor-of-preserved-path case. `ReplaceLinkedAncestorsWithRealDirectories` runs before linking and
converts any linked ancestor back into a real directory — safe because deleting a link never touches
the contents, which already live in the store.

### The store is keyed by emulator, not by system
`saves/<emulator_slug>/…` deliberately does **not** share saves between two emulators that run the
same system, because their save formats differ. This means switching emulator still does not carry
saves across; that has to be opted into per verified-compatible pair, not enabled blindly.

### Recursive delete must not be trusted around junctions
On Windows, `Directory.Delete(path, recursive: true)` on a tree containing a junction throws
`UnauthorizedAccessException` and leaves the tree in place. .NET calls `DeleteVolumeMountPoint` on
any `IO_REPARSE_TAG_MOUNT_POINT` entry; that fails for a plain directory junction and the recorded
error is thrown even though the junction itself is then removed successfully. It does not delete
through the junction, so saves are not destroyed — but the delete fails, which would have silently
broken uninstall and reinstall. `SaveStore.DeleteDirectoryTreeWithoutFollowingLinks` removes links
explicitly before recursing; `ClearDirectoryPreservingPaths` uses it and also short-circuits on any
link it meets before considering whether to recurse.

`Directory.GetDirectories(path, "*", SearchOption.AllDirectories)` *does* follow junctions —
`AttributesToSkip` only covers Hidden and System, and a junction is neither. `CopyDirectoryRecursively`
therefore walks one level at a time and skips links, otherwise a reinstall would copy the entire
store back into the emulator directory.

### Migration is refused rather than guessed
Linking a directory that already holds real saves moves it into the store first
(`Directory.Move`, falling back to copy-then-delete when the store is on another volume, which
`Directory.Move` rejects with `IOException`). If the store *already* holds data for that path and the
install directory has its own, the two are not merged — nothing is moved, the directory stays
unlinked, and the conflict is logged. Guessing which copy wins is how save data gets lost.

Failure anywhere in this path is non-fatal by design: the directory is left as it was, a line is
logged, and the emulator launches with saves in the old location.

---

## Image loading

Images are loaded by sniffing magic bytes (PNG/JPEG/WebP) rather than trusting the file extension, so
a mislabeled file (a PNG saved as `.jpg`) still decodes. A missing, unreadable, or undecodable file
returns null silently — callers treat "no image" as normal, not an error worth logging.

Card height is computed manually from the aspect ratio against a fixed width so the container knows
exactly how tall each image will be. `ICarouselItem` exists so the carousel can ask an item for its
artwork aspect; it previously detected this with `child is TextureRect`, which silently stopped
working the moment entries gained a wrapper node — every item then fell back to its existing height.
The reported aspect covers the whole card, not just the cover: the carousel sets the card's width and
derives height from it, so padding and the caption strip must be included or the cover gets
letterboxed by exactly the space they occupy.

---

## Downloads

### Downloads deliberately bypass Godot's `HttpRequest`

`HttpRequest` counts bytes in **32-bit ints** — `SafeNumeric<int> downloaded` and `int body_len` in
`scene/main/http_request.h`. The C# binding surfaces both as `long`, so the truncation is invisible
from this side and inspecting our own types proves nothing. Anything over 4 GiB is reported wrong: a
5,952,572,572-byte ROM arrived as `GetBodySize() == 1657605276`, exactly 2^32 short, and
`GetDownloadedBytes()` went negative past 2 GiB before wrapping back through zero.

The transfer itself still *completed*, because both counters wrap congruently and Godot's
`downloaded == body_len` equality holds again at the true end. Only the reported numbers lied — which
made a working three-minute download look like a bar pinned at 100% forever. Do not "simplify" this
back onto `HttpRequest`; the ceiling is in the engine and cannot be raised from C#.

`DownloadManager` therefore streams through `System.Net.Http.HttpClient`, where `Content-Length` is a
`long?` and the counters are `long`. Its `Timeout` **must** stay `InfiniteTimeSpan`: the default is
100 seconds and covers the whole response including the body, which would silently kill any download
longer than that.

### Progress crosses the thread boundary through `_Process`

The transfer runs on a task thread and publishes progress with `Interlocked.Exchange`; `_Process`
reads it back with `Interlocked.Read` and is the only place that emits `DownloadProgressUpdated` or
touches a node. Godot signals and UI must be driven from the main thread, so completion is a
`Volatile.Write` flag that `_Process` notices, never a callback invoked from the worker.

### Extraction runs off the main thread

7-Zip is spawned inside `Task.Run`, and the UI work that follows returns to the main thread via
`Callable.From(...).CallDeferred()`. It previously ran inline through `OS.Execute`, which is
synchronous: the whole app froze for the length of the extraction — imperceptible on a small ROM,
minutes on a multi-gigabyte one, where it looked like a download that reached the end and hung.
`System.Diagnostics.Process` is used rather than `OS.Execute` so the off-thread call is plainly safe.

### Active downloads are tracked by game id, not filename

The registry key is `destinationFilePath.GetFile()`, and the destination is
`game.Files[0].FileName + ".zip"` — so the key carries a `.zip` suffix the ROM's own filename does
not. `IsDownloading(game.Files[0].FileName)` therefore never matched, which left the details-panel
progress bar hidden and the action button never reading "Downloading...". Callers use
`IsDownloadingGame(id)`, which compares the id the download was registered under and so cannot drift
from the destination naming scheme.

### A non-positive download total must never reach a `ProgressBar`

`Content-Length` is absent on chunked responses, and `DownloadProgressDisplay` is the single place
that decides what to do about that. Feeding a non-positive total into a `ProgressBar` produces a bar
that reads **full**: `Range::set_max` clamps the new max up to the min (`MAX(p_max, min)`), so
`MaxValue = -1` silently becomes 0, and `get_as_ratio()` then hits its divide-by-zero guard and
returns `1.0`. No error, no warning, at either step. The helper switches the bar to `Indeterminate`
instead, and also treats `current > total` as unknown so that a bad total can never pin the bar.

Both progress consumers go through that helper. They used to clamp differently — the details panel
assigned `MaxValue`/`Value` raw, the downloads page used `total > 0 ? ... : 0` — and two UIs
disagreeing about a single download is far harder to diagnose than either one being wrong alone.

The preferred total is RomM's `fs_size_bytes`, passed into `DownloadFile` and trusted ahead of the
response header.
