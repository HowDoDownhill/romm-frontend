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

### Cores ship with the install, because only the bundle is versioned
Cores are installed with RetroArch as one `RetroArch_cores.7z` from the same `{version}` the install
recipe resolved, not fetched per core at launch. This replaces an earlier on-demand scheme that pulled
each core individually from `buildbot.libretro.com/nightly/.../latest/`.

The reason is netplay parity. RetroArch refuses a netplay connection unless both peers run the same
RetroArch version *and* the same core version, and `latest/` is unpinnable — two users installing days
apart get different core builds with no way to ask for a specific one. Probed on the buildbot: there is
no per-core stable download and no dated per-core archive. `stable/<version>/<os>/x86_64/cores/`,
`.../latest/` and a bare `.../<core>_libretro.dll.zip` all 404. The only versioned artifact is the
whole-bundle `RetroArch_cores.7z`.

Extracting one core from the remote archive is not an option either: the bundle reports `Solid = +`
with `Blocks = 1`, so a single file cannot be decompressed without the entire archive. The cost is
therefore fixed at one 227 MB download per RetroArch install, against roughly 30 MB before, and
on-demand buys nothing once it has been paid. All 44 declared cores across 41 systems were verified
present in the stable bundle, so no system needs a fallback core.

`{version}` is substituted into `extra_downloads` URLs from the resolved `ReleaseOption.VersionLabel`,
which is the bare version for `web_scrape` recipes, so cores can never drift from the RetroArch build
they shipped with. `extract_folder_regex` on an extra download lifts the contents of the archive's
wrapper directory into the destination — the bundle nests everything under `RetroArch-Win64/cores/`,
so without it the cores land one level too deep.

`core_directory` plus a per-OS `core_file_name` are the single source of truth for a core's on-disk
path; `core_launch_arg` is a plain string using `{core_path}`, so the `.dll`/`.so` split lives in one
field rather than being duplicated into every system's launch fragment. `cores` is in
`preserve_on_reinstall` so an upgrade does not discard them.

`core_download_url` and the per-core path in `EnsureCoreInstalled` are deliberately left in place for
any future emulator that has a versioned per-core source. RetroArch no longer declares the field, so a
core missing from a RetroArch install is reported as needing a reinstall rather than silently pulling
an unpinned nightly.

### Installing while the emulator runs corrupts the install
`UninstallEmulator` refused to run while an emulator was open but `InstallEmulator` did not, so
reinstalling RetroArch while it was running deleted what it could and then failed on the locked DLLs
(`avcodec-58.dll` is in use), leaving the install partly cleared — in practice an empty `cores`
directory and a working executable. `InstallEmulator` now carries the same `IsEmulatorRunning` guard.

### Default emulator per system favours RetroArch for pre-gen-6 and cartridge systems
`GetMappedEmulator` returns index `[0]`, so ordering in `BuildDefaultEmulatorMap` is the default.
RetroArch is first for generations 1-5 and for most cartridge systems (`nes`, `snes`, `genesis`,
`sms`, `sega32`, `segacd`, `psx`, `gb`, `gbc`, `gba`, `nds`) because those are the systems RomM's
EmulatorJS browser player supports, so a save written by RetroArch is also playable in the web UI.
Gen-6-and-later disc systems keep their native emulator; 3DS is a cartridge system but has no usable
libretro core, so azahar stays.

`n64` is the exception to the cartridge rule and defaults to gopher64. Neither `mupen64plus_next`
nor `parallel_n64` supports RetroArch netplay (measured — see `docs/NETPLAY-DESIGN.md`), while
gopher64 has P2P netplay with TURN fallback built in. Browser-player save compatibility is not worth
giving up multiplayer on the console whose library is most worth playing together.

For the same reason `psx` keeps RetroArch but its default core is `mednafen_psx_hw`, not
`swanstation`: swanstation cannot netplay at all, and beetle is the only PS1 path that can. psx is
the one system where the netplay-capable choice and the browser-save-compatible choice coincide, so
it stays on RetroArch rather than moving to DuckStation, which has no netplay whatsoever.

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

### The device id must be persisted, or every sync registers a new device
`GetOrCreateDeviceAsync` is called by `SaveSyncManager` twice per game session — once before launch
and once after exit — and it unconditionally `POST`ed `/api/devices`, discarding the returned id.
Nothing was ever reused, so each sync created a row. Measured against a live server: **403 devices,
all named `romm-frontend`**, with `last_seen` spread across weeks.

`allow_existing` did not save it, most likely because the payload carried nothing RomM could match an
existing row on — no `hostname`, no `mac_address`. The id is now stored in `config.cfg` under
`[RomM] DeviceId` and revalidated with `GET /api/devices/{id}` before reuse, so a server-side
deletion re-registers rather than failing forever. `hostname` is also sent now, giving RomM a chance
to dedupe on its own.

This matters beyond tidiness: `device_id` is what save sync attributes uploads to, so hundreds of
one-shot devices make the server's own sync history meaningless, and any future presence built on the
device registry would be unusable.

### A netplay session must not outlive the emulator
`NetplayManager` holds the session as autoload state, and `{netplay}` resolves from it on every
launch. If a session were left active after the emulator closed, the *next* ordinary launch would
silently carry `--host` or `--connect` and sit waiting for a peer that never arrives — a failure with
no visible cause.

`EndSession` is therefore called from the same `EmulatorManager._Process` branch that already detects
process exit and triggers save sync, not from the UI. Cancelling from the menu also ends it, but the
process-exit path is the one that must never be missed, because it covers the emulator being closed
by the hotkey, by its own quit, or by crashing.

### Hosting is gated on the game being launchable, not just supported
The host button asks `ResolveGameAction(game).Kind == LaunchGame` rather than testing the emulator
and download state itself. That reuses the single state machine the play button already derives from,
so the menu can never offer to host a game that the play button would refuse to start — and a game
that still needs downloading says so instead of failing at launch.

### The netplay view is a third start-menu view, and Back cancels rather than closes
`ShowNetplayView` follows `ShowBiosView`: the menu list hides and a sibling container shows. Two
things had to change beyond adding nodes. `HandleInput` picked its focus-cycling list with a boolean
`IsBiosViewOpen ? biosView : optionsList`, which would have cycled the hidden menu while the netplay
view was up; and Back closed the whole panel, which would have left a session armed with no visible
way to cancel it. Back now emits `NetplayCancelRequested`, which ends the session and returns to the
menu.

### `{netplay}` is a denylist, unlike `sync_include`
`netplay.unsupported_systems` names the systems that *cannot* netplay, so an unlisted system is
allowed to try. That is the opposite of `sync_include`, which is an allowlist, and the difference is
deliberate.

The failure modes are not symmetric. A save-sync allowlist that misses an extension silently stops a
real save from being uploaded, and nobody notices until the save is needed. A netplay denylist that
misses a bad core costs nothing: RetroArch refuses the connection outright with
`Core does not support netplay`, which is loud, immediate and harmless. Guessing wrong is safe in one
direction and destructive in the other, so the schemas point opposite ways on purpose.

Only 13 of RetroArch's 41 systems were measured. The unmeasured ones are allowed to attempt netplay
because the worst case is a clear refusal.

`{netplay}` sits before `{rom_path}` in the launch template for the same reason `{settings}` does —
some emulators require the ROM path last — and is removed with the same single-adjacent-space rule
when no session is active.

### Join codes encode an address, not a session
A join code is the host's IPv4 address and port packed into 6 bytes and rendered as 10 characters of
Crockford base32. The alphabet omits `I`, `L`, `O` and `U`, and parsing folds `I`/`L` to `1` and `O`
to `0`, because these are typed on a gamepad's on-screen keyboard where those glyphs are easy to
confuse. Hyphens and whitespace are ignored so a code can be written in groups.

The code carries no session identity, so it cannot survive the host changing address. That is
acceptable while codes are LAN-scoped; a relay-backed session id (RetroArch's `--mitm-session`) is
the eventual replacement and is deliberately a different field rather than an overload of this one.
`BuildJoinCode` returns null for anything that is not IPv4, rather than inventing an encoding that
would then need supporting.

### The host has no Ready button, so its readiness is its preparedness
`AllMembersReady` asks every member — including the host — for `IsReady`, but the host's action
button is Start, not Ready, so nothing ever set the host's `isLocallyReady`. The host therefore
reported itself unready forever, `AllMembersReady` could never be true, and Start stayed disabled
reading `Waiting For Players` no matter how many clients readied up.

Readiness is now reported as `isPrepared && (isLocallyReady || IsHosting)`. Pressing Start *is* the
host's readiness declaration, so deriving it from preparedness rather than adding a Ready toggle
keeps one button on the host and avoids a state where the host is ready but has not started.

### A rom cannot be selected until the system transition has finished
`TransitionToSystem` is `async void`: it awaits a 0.2 s fade-out *before* calling
`DoSelectSystemByIndex`, which is what actually rebuilds `currentlyShownGames`. Switching system and
then immediately calling `SelectGameById` therefore searched the **previous** system's list and
silently found nothing, while the carousel header — updated synchronously by
`SetSelectionSilently` — had already moved. A client following the host's pick showed the new
system's name above the old system's games.

`SelectGameOnceSystemSettles` records the wanted rom id instead, and `OnSystemSelected` consumes it
through `ResolveInitialGameIndex` in place of its usual `OnGameSelected(0)`. The selection happens
once, on the list that actually contains the game, rather than being retried against a list that is
not there yet.

### The host's browsing is broadcast separately from its pick, and is debounced
Clients mirror the host's carousel so everyone is looking at the same game before it is committed.
That is a different message from `ReceiveGameSelection`: browsing moves the client's carousel and
nothing else, while a committed selection also resets readiness, publishes the host's emulator and
version, and drives the download prompt.

Broadcasting on every highlight was rejected once already because it puts a network message behind
every frame of scrolling. `PushHostBrowsingGame` therefore coalesces: it records the newest rom id
and schedules one broadcast `BrowsingBroadcastSettleSeconds` later, so a scroll through fifty games
sends one packet rather than fifty. `BroadcastBrowsingGame` also returns early when the host is
alone in the lobby.

The client's handler ignores the message when it is itself hosting, which is what stops
`OnGameSelected` → broadcast → `OnGameSelected` from looping back.

### The port mapping belongs to the lobby, not to the emulator session
`EmulatorManager`'s exit branch released the UPnP mappings along with ending the netplay session. That
is right when the lobby is closing too, and wrong while the lobby is still open: the *second* game in a
lobby then had no port forwarding at all, so an internet peer could reach the lobby but not the
emulator. Measured over a phone tether — the second session connected to the right address and simply
was not forwarded, which presents as "netplay launched but the players are not connected".

`ReleasePorts` on emulator exit is now conditional on the lobby being gone. The lobby's own paths —
leaving, cancelling, tree exit — still release, which is where that responsibility belonged.

### The advertised host address must come from the routing table, not the first private address
`ResolveLocalHostAddress` walked `IP.GetLocalAddresses()` and returned the first RFC1918 address it
found. On a machine with WSL or Hyper-V installed that is a **virtual adapter**: it advertised
`172.31.16.1`, an address no peer can reach. It also poisons the LAN join code, which encodes the same
value.

It now asks the OS which local address would be used to reach the internet, by connecting a UDP socket
to a public address and reading the local endpoint back. UDP connect sends no packets — it only
resolves the route — so this costs nothing and cannot fail on a firewall. The old scan remains as a
fallback for a machine with no route out.

Clients were insulated from this by the earlier "connect to the address you reached the lobby on" rule,
which is the second time that rule has covered for a bad advertised address.

### Readiness is cleared when a session ends
Nothing reset the ready flags when an emulator exited, so a lobby returning from a game showed everyone
still Ready. The host could then start the next game before anyone had confirmed they wanted it or
finished downloading it. Ending a session now clears readiness on the host and locally on each client.

### The readiness signature suppresses the message, never the panel refresh
Reporting readiness is idempotent so that recomputing on `MembersChanged` cannot loop, and the first
version of that returned early on an unchanged signature — skipping `RefreshLobbyPanel` along with the
RPC. The guard exists to stop redundant *network messages*; a local repaint is free and there is never
a reason to skip it. The refresh now always runs and only the RPC is gated.

Preparedness transitions are logged when the signature changes, which is naturally low-volume — one
line per real state change — and is what confirms whether a stuck panel is a missed refresh or a
preparedness ladder returning the wrong answer.

This guard was initially blamed for a lobby that did not notice a *download* finishing. It was not the
cause — see below.

### A finished download is not a finished ROM; preparedness must wait for extraction
`DownloadManager.HandleDownloadFinished` invokes the completion callback, which starts extraction on a
background thread, and then emits `DownloadCompleted` synchronously — so the signal always arrives
*before* the ROM exists at `roms/<slug>/<file>`. Preparedness is derived from `CheckIfGameIsDownloaded`,
which tests exactly that path, so recomputing on `DownloadCompleted` reads the file as still missing.

The lobby therefore settled on `Needs game` the moment a download completed, and nothing recomputed
again once extraction landed. This is why the symptom survived making `RefreshLobbyPanel` unconditional:
the repaint was running, it was just painting a correct answer to a question asked too early.

The same ordering silently skipped the ROM hash check. `VerifyLocalRomAgainstHost` returns early when
the file does not exist, so a freshly downloaded ROM was never hashed against the host's and
`isLocalRomMatchingHost` kept its optimistic default — the match check failed open rather than running.

`HandleExtractionFinished` now notifies netplay via `OnLocalRomLibraryChanged`, which is the first
moment the file is real. The `DownloadCompleted` subscription is kept because a *failed* download never
reaches extraction and still has to repaint.

Emulator installs do not have this problem: `InstallEmulator` awaits the installer to completion before
emitting, so that signal already fires when the state it describes is true.

Confirmed on the two-machine rig. A client joining with `--netplay-auto-download` logs the whole ladder,
and the middle line is the bug itself:

```
[Netplay] preparedness now "Downloading..."
Download complete. Starting extraction for: ...
[Netplay] preparedness now "Needs game"
Successfully extracted ... to roms/nes
[Netplay] preparedness now "Ready"
```

`Needs game` after `Download complete` is the recompute reading a file that is not there yet. Before the
fix that was the terminal state.

### The published ROM hash is absent from any cache written before the hashing work
`RequiredRomHash` comes only from `game.Files[0].Md5Hash`, which is populated from the API's `md5_hash`.
That field was added to `RomFile` alongside netplay, so a `games.cache` written before then contains no
hash at all — not even a null. `VerifyLocalRomAgainstHost` returns early on an empty expected hash, so
on a stale cache the whole ROM-matching feature is inert and every client silently passes.

It fails open rather than blocking play, which is why it is easy to miss. A cache rebuild is what turns
it back on; there is no error to notice in the meantime.

### The host publishes a hash it computed, not one the server reported
Depending on the server's `md5_hash` made the check hostage to cache freshness, and it was the wrong
question anyway. Netplay needs both sides running *identical bytes*; "matches the host" is the real
requirement and "matches the server's canonical dump" only approximates it. The host therefore hashes
its own file and publishes that.

Hashing is too slow to do inline — `CommitHostGameSelection` would block on a multi-GB image — so the
selection goes out immediately and the hash follows on `ReceiveRomHash`. Clients treat an empty
`RequiredRomHash` as *not yet verified*: `OnRequiredRomHashChanged` resets verification and re-runs it,
which drops a client back out of Ready until it has actually compared. That closes the window where
everyone looks ready before anyone has checked.

While the host hashes it sets `isVerifyingLocalRom`, so it reports `Checking game file` through the
existing `LobbyMember.Status` and `LocalPlayerHasGame` returns false — a hashing host cannot press
Start. No new status field was needed; the host simply never used to participate in the one that
already existed.

`romhashes.cache` sits beside `games.cache` and is keyed by path with a size and mtime stamp. The stamp
is what makes a re-download or a swapped dump invalidate cleanly rather than trusting a stale entry.
All three callers — the download-time warm, the host publish and the client verify — funnel through
`ResolveRomHashAsync`, which collapses concurrent requests for the same path so a file is never hashed
twice at once.

Verified on the two machines: identical ROMs produce `Checking game file` then `Ready`, and a client
whose ROM has four bytes appended reports the real computed hash against the host's and settles on
`Game file does not match`, refusing to ready up.

### A client must not have its game list filtered out from under the host's choice
`showOnlyInstalledGames` hides anything not downloaded, which is right when browsing your own library
and wrong in a lobby: the host picks from *their* library, and the whole point of a client's lobby is
to see that game and download it. With the filter on, the host's pick was simply absent from
`currentlyShownGames`, so `SelectGameById` found nothing and the client's carousel sat still while the
host browsed.

The filter is therefore suppressed while following a lobby — a non-hosting member of a lobby — and left
alone for the host, who is choosing from what they have. It composes with
`FilterSystemsForNetplayHost`, which narrows *systems* for both roles.

### Closing a lobby has to restore the browse source, not just hide the panel
`LeaveLobby` re-ran `GetCache` so the carousel returned to the full system list, but `OnLobbyClosed` —
the path taken when the *host* quits or the connection drops — only hid the panel. A client left after
the host quit therefore stayed narrowed to netplay-capable systems and could not see any other
platform until something else forced a rebuild.

`OnLobbyClosed` now performs the same teardown as leaving deliberately: refresh the browse source,
return focus to the game list, release ports, and clear the cached lobby address, readiness signature
and browsing flag. The two paths differ in *why* the lobby ended, not in what has to be undone.

### Readiness never starts a session; only the host pressing Start does
Readiness says "I am able to play this", not "go". The host decides when a session begins, and nothing
else may decide for it — otherwise a player finishing a download is enough to drag everyone into a
game.

That was never true of the product, but it *was* true of the test flags: `--netplay-auto-ready`
originally readied clients **and** started the game on the host, so selecting a game appeared to launch
it instantly. The two behaviours are now separate arguments — `--netplay-auto-ready` readies up and
stops there, `--netplay-auto-start` is the host-only flag that presses Start. Only the second one can
begin a session, and neither exists unless explicitly passed.

Auto-start is also one-shot per selection, reset when the selection changes. Without that, clearing
readiness at the end of a session immediately re-readies everyone and relaunches the same game forever.

### `lobbyCopyCodeButton` was never bound, so the button did nothing at all
The node existed in `main_scene.tscn` and the `[Export]` existed in `MainScene`, but the field was
absent from `node_paths` and had no assignment line — exactly the failure mode this document and
`CLAUDE.md` warn about. The field was null, so the button was invisible to focus navigation *and* its
`Pressed` signal was never connected: it had never copied anything.

Verifying by eye is what let it through. The check that catches it is mechanical — parse the scene for
declared `node_paths`, assignment lines and real node paths, parse the C# for `[Export]` fields, and
diff all four sets. Doing that surfaced a second one immediately: `lobbySelectGameButton` has a C#
export and **no node in the scene at all**, so the Select/Change Game button has never existed and
browsing is reachable only via Back.

### A netplay client must not upload its save, because the progress is the host's
Lockstep netplay has exactly one game state and it is the host's file — RetroArch syncs it to each
client on connect. So whatever a client's emulator writes when it exits is the *host's* progress
sitting in the client's save directory under the client's name.

`SyncAfterExit` ran on every emulator exit with no netplay distinction, which meant joining a session
overwrote the joiner's own cloud save for that game with the host's progress. Silently, and for a game
they may have had their own long-running save of. The upload is now skipped when
`NetplayManager.Role` is `Join`. The role is still set at that point — `EndSession` is called further
down the same branch — which is the only reason this can be decided there at all.

Only the upload is suppressed. `SyncBeforeLaunch` still runs for a client, because it costs nothing
and keeps the pre-session state coherent.

The client's *local* file turns out to be safe without any help from us. Measured with both machines
holding the same Emerald save and the host saving in-game mid-session: the host's `.srm` changed and
uploaded, while the client's was **not written at all** — same hash, same mtime as before the session
— even though lockstep means the client's in-memory SRAM was identical to the host's at the moment of
saving. RetroArch does not persist SRAM while it is a netplay client. The `.netplay` directory it
creates under the save directory stayed empty too.

So no backup-and-restore is needed, and the upload gate is defence in depth rather than the only thing
standing between a joined session and someone's overwritten save. That distinction matters for phase
4: this is a RetroArch behaviour, not a guarantee, and Flycast, PPSSPP and gopher64 have not been
measured. The gate protects the cloud save whatever they do locally.

### Readiness depends on who else is in the lobby, so it has to be recomputed when they change
Once the version target became "the newest anyone has" rather than the host's, readiness stopped
being a purely local property: a peer joining with a newer build can invalidate everyone else's. The
first cut recomputed only on download, install and game-selection events, so a host that readied
while alone stayed ready when a newer client arrived, and started the game it was no longer eligible
for.

Recomputing on `MembersChanged` fixes that but closes a loop — reporting readiness broadcasts the
roster, which raises `MembersChanged`, which reports readiness. Reporting is therefore idempotent: the
computed values are hashed into a signature and an unchanged signature sends nothing. The loop
terminates on the first repeat.

Handler order on that signal matters too, and it is subtle. `AutoStartHostedGameIfRequested` was
subscribed before `ReportPreparednessIfInLobby`, so it saw the *stale* roster — the host still marked
ready from before the client joined — and started the game before the recompute could clear the flag.
Preparedness is now reported first. Auto-start also asks `LocalPlayerHasGame` directly rather than
trusting the roster, because a host should never start a session it is not itself eligible for,
whatever order the signals arrive in.

A peer's own installed version has to seed the target as well, not just the versions in the roster.
A client learns its own version through the roster only *after* it reports, so a client on the newest
build would compare itself against the host's older one, decide it needed to change, and never report
at all — a deadlock where nobody is ready because nobody has spoken.

### The host waits until the emulator is actually listening, rather than guessing
Clients used to sleep `HostStartupGraceSeconds` — five seconds — after the start message and then
connect blind. `--connect` against a host that is not yet listening fails outright with no retry, so
a core that took longer than the guess lost the session, and every session paid the five seconds
whether it needed them or not.

The host now polls `GetActiveTcpListeners` for its netplay port and only sends `ReleaseMembersToStart`
once something is listening; clients connect immediately on receipt. RetroArch binds that port when
netplay initialises, which is after the core and content have loaded — exactly the delay the guess
was covering.

Reading the OS listener table is deliberate over connecting to the port to test it. A probe
connection would arrive at RetroArch as a peer that immediately disappears, which is at best noise in
the very handshake this is trying to protect. Binding the port to see if it is taken is not reliable
either: address-reuse semantics differ across platforms, so a successful bind does not prove nobody
is listening. The listener table is passive and means exactly what it says.

There is still a timeout, and it releases the other players anyway when it expires — a session that
might work beats one that certainly will not.

#### The session port must be re-resolved per launch, not read from the previous session
`EndSession` sets `Port` to zero when an emulator exits, and `BeginHosting` is what applies the
fallback. `StartHostedGame` read `Port` *before* calling `BeginHosting`, so the first game in a lobby
worked — the lobby's own `BeginHosting` had already set it — and **every game after it launched with
port 0**.

The damage was almost invisible, which is why it survived: the client's `BeginJoining` applies the
same fallback independently, so it still connected to the right port. What broke was the host's
listening check, which waited for something to listen on port 0 and burned the entire 60-second
timeout before releasing the other players. Measured as `nothing started listening on 0` followed by a
client that launched a minute-plus late, into a session the host had by then finished with.

The port is now resolved through `ResolveDefaultPort` on every launch, matching how the lobby resolved
the port it asked UPnP to map, and the listening poll refuses a non-positive port outright rather than
waiting out the timeout on a value that can never match.

Worth noting how this escaped: every automated test launched exactly **one** game per lobby. The bug
only exists from the second launch onward, so a scripted run could never see it.

### ROM parity is checked against the file on disk, not against the metadata
Comparing the host's ROM metadata with the client's would prove nothing: both resolve the same
`rom_id` against the same RomM server, so the hashes are equal by construction. The failure this
guards against is a **local file** that does not match what RomM says it should be — a partial
download, a hand-replaced file, a different regional dump — which RetroArch reports as a cryptic
connection failure.

The host publishes RomM's `md5_hash` with the game selection, and each peer hashes its own file and
compares. MD5 rather than the CRC32 RetroArch itself logs, because .NET has MD5 built in and CRC32
would need a package for no benefit — the check is "is this the file RomM describes", and any hash
answers that.

Hashing runs off the main thread and is cached per file path, so it happens once per selection rather
than once per panel refresh. A mismatch blocks readiness and turns the action button into
`Redownload <game>`, so the fix is one press. A hash that cannot be computed counts as a match: the
frontend's own failure to read a file must not be what stops a session starting.

### A client connects to the address it reached the lobby on, not the one the host advertises
`StartHostedGame` publishes the host's UPnP external address when there is one, which is right for a
peer coming in over the internet and wrong for everyone else. Measured on two machines on one LAN:
the host launched `--host --port 55435` while the client launched `--connect 50.34.50.204` — its own
router's public address. That only works if the router supports NAT hairpinning, and consumer
routers commonly do not, so a purely local session would fail with a connect error that names a
public address and gives no hint that the LAN address was available.

The host cannot tell which of its addresses a given peer can reach, but the peer already knows: it
is talking to the lobby right now, over an address that demonstrably arrives. `MainSceneNetplayHandler`
remembers that address from `JoinLobby` and prefers it, falling back to the advertised one only if
it has none. This needs no subnet comparison and stays correct for internet peers, whose lobby
address *is* the external one.

### Internet play needs the lobby port forwarded too, not just the netplay port
Hosting mapped `hostPort` — the emulator's netplay port — and nothing else, so the log read
`Mapped ports 55435 via UPnP` and the join code advertised the public address as reachable. But a
joiner connects the **lobby** first, and `NetplayLobby` listens on its own ENet port (55440), which
was never forwarded. Over the internet the client's connection was dropped at the host's NAT, so the
lobby opened with an empty roster and sat there until ENet gave up — the failure looked like an
empty lobby rather than a blocked port. On a LAN nothing forwards anything, which is why this only
appeared once a code was used across the internet.

Both ports are mapped now, and *both* must succeed before the code advertises the public address:
`MapPorts` returns true if **any** port mapped, so checking its return value alone would still call
a host reachable when only the emulator's port got through. `IsPortMapped` is asked about each one.
When either fails the code carries the LAN address and the note names both ports to forward by hand.

A client cannot tell "connected, nobody here yet" from "never connected" either, since both are an
empty roster, so a non-hosting lobby with no members reports `Connecting to the host...` rather than
the waiting-for-a-game text.

### Closing the emulator is three different things on Linux
`CloseEmulator` was written for Windows and silently did nothing on Linux. Measured: the host quit,
the client logged that it had been told to close, and RetroArch stayed on screen.

Three separate faults:

- `Process.CloseMainWindow` only works on Windows. On Unix .NET has no window handle to send to, so
  it returns false and nothing happens.
- `Process.Kill()` without `entireProcessTree` kills only the tracked process. RetroArch on Linux is
  an AppImage, so the tracked pid is the AppImage runtime and the emulator is its child — killing the
  parent leaves the emulator running.
- `WaitForExit(5000)` ran on the main thread, so the whole frontend froze for five seconds while
  waiting for a process that was never asked to exit.

The request is now per-platform — `CloseMainWindow` on Windows, `SIGTERM` elsewhere — and the wait
happens off the main thread. `SIGTERM` rather than `Kill` first matters: `Process.Kill` sends
`SIGKILL` on Unix, which would stop RetroArch before it flushes SRAM, and save sync reads those files
off disk once the process exits.

#### An AppImage's launcher outlives the emulator, so exit is detected by scanning `/proc`
The same double-fork breaks *detecting* the exit, not just causing it, and that is the more damaging
half. Measured: the emulator was quit, and one minute later

```
27935      1  romm-frontend.x
28366  27935  AppRun            <- still alive, emulator long gone
```

`activeEmulatorProcess` is the launcher, so `HasExited` stayed false indefinitely. Everything hanging
off that check silently never ran: **save sync never fired on exit**, `EndSession` never cleared the
netplay role — so the next ordinary launch would have carried `--host` or `--connect` — the port
mapping was never released, and `IsEmulatorRunning` stayed true, which refuses every later launch
with "an emulator is already starting or running". None of it produced an error; the frontend simply
believed a game was still running forever.

On Windows `HasExited` is the whole answer and is still used unchanged. On Linux the emulator is
identified by scanning `/proc` for a process whose `comm` is the executable's file name — truncated
to the 15 characters the kernel keeps — *and* whose `cmdline` contains the executable path.

A process counts as the emulator when its `cmdline` contains the executable path **and** either its
`comm` is that expected name, or its `comm` is not a known launcher. Both tests are kept because
their failure modes are wildly asymmetric, and only the union is safe in both directions:

- **Name match alone** breaks badly when an AppImage's `AppRun` execs a binary under a different
  name — plausible for PCSX2 or Dolphin, whose real process may not be named after the AppImage. No
  process would match, the session would be declared over about fifteen seconds in, and save sync
  would fire *while the game was still being played*.
- **Excluding launchers alone** breaks gently: an unrecognised launcher keeps the frontend thinking a
  game is running until that launcher exits. That is the old bug's behaviour, no worse.

So a severe failure now needs both rules to be wrong at once. The launcher list has to include the
FUSE mount daemons some AppImages leave behind — `dwarfs` processes carrying the AppImage path in
their own `cmdline`, observed still running ten hours after their emulator closed — or those alone
would keep the session alive forever.

Verified against RetroArch, whose `comm` is `RetroArch-Linux`, exactly the first 15 characters of
`RetroArch-Linux-x86_64.AppImage`, with `AppRun` as its launcher. The other AppImage emulators are
covered by the launcher-exclusion half and are untested.

The scan is throttled to `EmulatorLivenessCheckSeconds` rather than run every frame, and suppressed
for `EmulatorLivenessGraceSeconds` after launch: an AppImage has to mount itself before the emulator
exists, and a check that ran in that window would see no match and declare the session over
immediately. Detecting the exit a second late costs nothing.

The lingering launcher is killed once the exit is detected, or it accumulates one orphan per game.

#### Signalling the process tree does not reach an AppImage's emulator
Killing by process handle cannot work for these emulators, and measuring the tree shows why:

```
14145  ppid=13842  AppRun                    <- what the frontend tracks
14149  ppid=1      RetroArch-Linux-x86_64    <- re-parented to init
```

`AppRun` double-forks the real emulator, so it is orphaned to init and is **not a descendant** of
anything the frontend holds. `pkill -P`, `Kill(entireProcessTree: true)` and every other tree walk
find no children to signal. Worse, `AppRun` ignores `SIGTERM` outright — sending it one changes
nothing — while the same signal to the real process shuts it down cleanly.

Shutdown is therefore addressed by **executable path** (`pkill -f` on `StartInfo.FileName`), which
matches the orphan as well as the wrapper, with the tracked pid signalled too so non-AppImage
emulators still take the ordinary path. The force stage repeats it with `-KILL` before falling back
to `Kill(true)`, because a lingering `AppRun` keeps `IsEmulatorRunning` true and would leave the
frontend believing a game is still running.

### Selecting a game by id has to scroll the carousel, not just mark the card
`OnGameSelected` sets `currentlySelectedGame`, fills the details panel and flips each card's
`Selected` flag — but the carousel's own position lives in `VerticalCarousel.SelectedIndex`, and
nothing in that path touches it. Calling `SelectGameById` therefore lit the right card up while the
list stayed exactly where it was, so a client following the host's pick highlighted a game that was
somewhere off screen.

Every other caller that moves the selection programmatically already sets `SelectedIndex` and calls
`UpdateLayout` alongside `OnGameSelected`; `SelectGameById` was the one that did not.
`ScrollGameListTo` is that pair, and `OnSystemSelected` uses it too so the first card of a new
system is actually scrolled to rather than assumed to be in view.

### The lobby panel needs explicit d-pad navigation, and must not steal focus on refresh
Two separate faults made the lobby unnavigable on a controller.

Godot drops focus from a control the moment it is disabled, and `RefreshLobbyPanel` reassigns
`Disabled` on the action and select-game buttons every time it runs. It runs on every roster
broadcast, which arrives repeatedly, so the focused button was routinely disabled out from under the
user and the panel then re-focused its *first* enabled button. Focus snapped back to the top faster
than it could be moved. The refresh now remembers what was focused and restores it when it is still
usable, falling back to the first button only when it is not.

Focus navigation between the buttons could not be left to Godot either. `MainScene` sits in front of
the GUI for directional input, so `HandleLobbyNavigation` cycles the enabled, visible lobby buttons
directly, in the same place `Select` and `Back` are already routed for the lobby. It is a wrap-around
cycle over a filtered list rather than a geometric neighbour search, which means a disabled button is
skipped instead of becoming a dead end.

The cycle order is read from the panel's own child order, not from a list in code. It was a hardcoded
array first, and it silently disagreed with the scene: the panel laid the buttons out as
Action → Leave → Copy Code while the array said Action → Copy Code → Leave, so pressing down from the
action button jumped to the bottom entry and then back up to the middle one. It reads as the d-pad
running backwards. Walking `lobbyPanel` for `Button` descendants in tree order cannot drift from the
layout, and it picks up buttons nested inside their `MarginContainer` wrappers without needing to know
they are there.

The consequence is that `LobbyPanel` is a `VBoxContainer`, so the order of the `MarginContainer` blocks
in `main_scene.tscn` is the only thing that sets *both* the visual order and the d-pad order — there is
no second place to keep them in sync, and no code change can reorder them. They now read
Action → Copy Code → Leave so that leaving the lobby is last rather than sitting between the two
buttons you actually use.

`lobbySelectGameButton` used to be a fourth entry here. It had a C# `[Export]` but no node and no
binding in the scene, so it was permanently null: invisible to this walk and its `Pressed` handler never
connected. Browsing for a game is reached from Back and from the internal call sites instead, so the
export was removed rather than given a node.

### Flycast's GGPO port is fixed at 19713; `network:LocalPort` does not move it
`network:LocalPort` looks like the GGPO port and is not — it belongs to the Naomi/BBA networking
path. Launching the host with `-config network:LocalPort=55435` produced a Flycast bound to UDP
**19713** regardless, which is GGPO's own default and the only port it listens on. There is no
`GGPOPort` key anywhere in the binary; the client side is configurable only in that
`network:server` accepts `address:port` ("Your peer IP address and optional port").

`host_args` therefore sets no port at all and `default_port` is 19713, which is the real number
rather than a wish. The client is pointed at `{peer_address}:{peer_port}`, and both resolve to
19713 through the emulator's declared port.

This is why `BuildLaunchFragment` now prefers the emulator's declared `default_port` over the
session port. The session port is chosen when the *lobby* opens, before anyone has picked a game,
so it cannot know which emulator will run — it was always the generic 55435 fallback. RetroArch
declares 55435 too, so nothing changes for it, but the field is no longer decorative.

**The frontend still forwards the session port, not the emulator's.** `TryMapPortsAsync(hostPort,
lobbyPort)` runs at lobby-open with the same pre-emulator information, so a Flycast session over
the internet would have 55435 forwarded and 19713 closed. LAN is unaffected. Fixing this means
mapping the port once the host commits a game, which is the same UPnP-lifetime area that already
caused the second-launch bugs, so it is deliberately left for its own change.

### Flycast GGPO is symmetric: both peers need the other's address
`ActAsServer` and `LocalPort` belong to the **Naomi** networking tab — their tooltips are "Create a
local server for Naomi network games" and "The local UDP port to use". The GGPO tab has no server
concept at all: it has `Play as Player 1` ("Deselect to play as player 2") and a single `Peer` field
("Your peer IP address and optional port"). The "leave blank to find a server automatically" tooltip
that suggests a listening mode belongs to Naomi's `Server`, not to GGPO's `Peer`.

So there is no host that merely listens. Both sides dial each other and the only asymmetry is who is
player 1. With only the client given an address, both peers sat at `Starting Network` forever.

The host therefore has to learn the client's address. `NetplayLobby` records each member's
`RemoteAddress` from the ENet connection when its identity arrives, and `StartHostedGame` passes the
first remote member's address into `BeginHosting`, so `{peer_address}` resolves on both sides. This is
also why the lobby is limited to two players for Flycast — `max_players` is 2 and GGPO is a pair.

Once that landed the peers genuinely connect, which is what turned an indefinite `Starting Network`
hang into a fast, explicit failure.

### `Peer verification failed` is Flycast rejecting a mismatched peer, not a network problem
After the peers connect, GGPO exchanges a verification step, and any mismatch stops both sides with
`GGPOException in ggpo_idle: Verification mismatch` on the side that detects it and
`Peer reported verification failure` on the other. It is not a connectivity failure — reaching this
error is proof the transport works.

Two causes were ruled out by measurement: BIOS (both load real BIOS under the frontend; only a
hand-rolled launch without `XDG_DATA_HOME` fell back to `reios`) and the render settings the frontend
appends per machine, which were made identical and changed nothing.

**The emulator builds are not verified to match.** `GetInstalledVersion` reads
`installed_version.txt` from the emulator directory, and neither machine has one for Flycast, so it
returns null on both. The lobby's version convergence compares null to null, reports agreement, and
lets the session start. The Windows build reports `v2.6` dated January 2026 while the Linux AppImage
was installed in July 2026 — almost certainly different releases, which is exactly what GGPO's
verification exists to reject.

A missing version file is therefore not a cosmetic gap: for any emulator installed outside the
frontend, or before version tracking existed, the convergence check silently passes and the mismatch
surfaces as an unexplained emulator-side error instead.

### A netplay session has to plug in the second controller itself
Flycast's default maple layout is `device1 = 0` (a controller in port A) and `device2 = 10`, where
10 is *None*. The emulated Dreamcast therefore has exactly one controller plugged in, so a netplay
session connects, synchronises and plays — while the second player simply does not exist as far as
the game is concerned.

`input:device2=0` puts a controller in port B for netplay launches only. It is deliberately not set
outside netplay, where a phantom second pad can change how a single-player game behaves.

The expansion slots are left alone. Port A carries VMUs (`device1.1 = 1`), port B keeps the existing
`10`. Adding a VMU to port B would change the emulated machine on one side only unless it were set on
both, and that is exactly the kind of divergence GGPO rejects.

### Digital inputs always cross GGPO; the analog stick only if you ask
`network:GGPOAnalogAxes` is a three-way enum — 0 `Disabled`, 1 `Horizontal`, 2 `Full` — and it
defaults to 0. Buttons and the d-pad travel regardless, so a session with it unset looks *almost*
right: two players, both responsive, and a completely dead thumbstick.

Netplay launches now force `2` on both sides. It has to be identical on both, because Flycast treats
a difference as a hard error rather than degrading — `GGPO analog settings are different from peer`.
Passing it from the launch arguments is what guarantees that, since each machine's own `emu.cfg` is
free to disagree.

### Netplay peers must share the emulated console's state, not just the ROM
`Peer verification failed` was traced to `dc_nvmem.bin` and the game's VMU save differing between the
two machines. `dc_boot.bin` and `dc_flash.bin` matched; the NVRAM did not, because it is per-machine
state that drifts as each install is used. Copying the host's NVRAM and VMU to the client was what
finally let a session start.

**The frontend does not enforce this yet.** It already refuses to start when the *ROM* differs, and
the same reasoning applies to everything else that makes up the emulated machine. For Flycast that is
`dc_nvmem.bin` plus the per-game VMU files — both already named in `sync_include`, which is why they
exist on both machines but with per-device contents. Until a client adopts the host's copies, Flycast
netplay works only when the two installs happen to have converged.

### `installed_version.txt` is what makes version convergence real
`GetInstalledVersion` reads that file from the emulator directory and returns null when it is absent.
Neither machine had one for Flycast, so the lobby compared null to null, reported agreement, and let
the session start. Both builds happened to be v2.6, so nothing broke — but a genuine mismatch would
have passed the same check and surfaced as an unexplained emulator-side failure.

An emulator installed outside the frontend, or before version tracking existed, has no version file.
Treat a null version as *unknown*, not as *matching*.

### Flycast's config keys were read out of the binary, not guessed
`flycast -help` documents only `-config section:key=value`, so the key names had to come from
somewhere. They are laid out as string literals in the executable: the `network` section is
followed by `Enable`, `ActAsServer`, `DNS`, `server`, `LocalPort`, `EmulateBBA`, `EnableUPnP`,
`GGPO`, `GGPODelay`, `Stats`, `GGPOAnalogAxes`, `GGPOChat`.

A second cluster containing `NetworkEnable`, `NetworkServer` and `GGPOEnable` is a **Lua API**
binding list — it sits next to `maple`, `memory` and `input` — and those names are not config
keys. Using `network:GGPOEnable` would silently do nothing, because `-config` accepts any
section:key pair and simply stores unknown ones.

### RetroArch must not do its own NAT traversal, because the frontend already did it
`NetplayPortMapper` maps the netplay port over UPnP before launching, and RetroArch then asks the
router to map **the same port again**. The second request is refused — routers do not hand the same
external port to two mappings — and RetroArch reports `Netplay UPnP Port Mapping Failed` followed by
`Your room is not connectable from the internet`, which reads as a router problem when it is really
the frontend and the emulator competing for one mapping.

`netplay_nat_traversal = "false"` in the shipped `retroarch.cfg` stops the duplicate attempt. The
mapping still exists; the frontend owns it and releases it on cancel, on emulator exit and on tree
exit, which RetroArch cannot do for a mapping it did not make. `netplay_public_announce` is off for
a separate reason: sessions here are formed in the lobby, and there is no reason to list a private
game on RetroArch's public server.

This supersedes the earlier reading of that measurement as a plain router refusal.

### Netplay can be driven from the command line, because a lobby needs two machines
`--netplay-host` and `--netplay-join=<address>` are read by `ApplyStartupSessionArguments` at the end
of `MainScene._Ready` and call the same handlers the start-menu buttons do. They exist because a
lobby cannot be tested from one keyboard: verifying host and client behaviour means driving two
machines at once, and the Arch test box (see `LINUX-TESTING.md`) has no way to take input remotely.

They take the host address directly rather than a join code, so a test does not depend on the
clipboard. Both are read from `OS.GetCmdlineUserArgs`, which means they must follow a bare `--` on
the command line.

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

### RetroArch on Linux is an AppImage that rewrites its own HOME
The Linux `RetroArch.7z` from the libretro buildbot contains no `retroarch` binary. It ships
`RetroArch-Linux-x86_64.AppImage` alongside a `RetroArch-Linux-x86_64.AppImage.home` directory, and
the AppImage sets `$HOME` to that directory on launch. Every RetroArch-owned path — `cores`,
`system`, `assets`, `info`, `database` — therefore lives under
`RetroArch-Linux-x86_64.AppImage.home/.config/retroarch/`, not flat in the install directory the way
the Windows build lays them out.

`executable_name.linux` and `core_directory.linux` in `install_scripts/retroarch/meta.json` are
written against that layout. Declaring `executable_name.linux` as `retroarch` (the obvious guess)
makes `IsEmulatorInstalled` resolve a path that never exists, so a fully successful download and
extraction still reports as "not installed".

The Linux `default_config` deliberately does **not** override `system_directory`,
`libretro_directory`, `assets_directory` and friends. Because the AppImage already points `$HOME` at
its bundled tree, RetroArch's own defaults are already correct; overriding them just hardcodes the
versioned folder name in a second place. Only `savefile_directory` and `savestate_directory` are
redirected, so saves land in the central save store like every other emulator.

### PCSX2's Linux AppImage keeps its config in a nested `PCSX2/` directory
`-portable` on Windows makes PCSX2 read `inis/PCSX2.ini` next to the executable. The Linux AppImage
cannot do that — its "app directory" is a read-only mount — so it creates a `PCSX2/` data directory
*next to the AppImage* and uses `PCSX2/inis/PCSX2.ini` instead. Anything written to `inis/` on Linux
is inert: PCSX2 generates its own config about half a second after install and never reads ours.

This is why `install_scripts/pcsx2/default_config/linux/PCSX2/inis/PCSX2.ini` exists, and why every
`config_file_relative_path` in the PCSX2 metadata — the four `settings_fields` and
`controller_config` — is OS-scoped. Without that, the in-app graphics settings and controller
mappings silently write to a file PCSX2 ignores.

The symptom is the first-run setup wizard appearing forever: `SetupWizardIncomplete = false` is
being written to the wrong file. PCSX2 is the only emulator that does this. Dolphin (`User/`),
Azahar (`user/`), PPSSPP (`memstick/`) and Flycast (`data/`) all keep their data where the shipped
`default_config` already puts it.

`[Folders]` in the Linux config points `Bios` and `MemoryCards` at `../bios` and `../memcards` so
they resolve back out to the emulator install directory. That keeps `emulator_bios_path` and
`relative_save_path` identical across platforms — PCSX2 accepts the parent-relative paths and does
not rewrite them.

### Default configs can be scoped per OS
`install_scripts/<emulator>/default_config/` is copied wholesale on install, but a `windows/`,
`linux/` or `macos/` subdirectory inside it is treated as an overlay rather than as content: the
base directory is copied with those names excluded, then the one matching the running OS is copied
over the top. This exists because emulators that ship a different directory layout per platform
(RetroArch) need different path settings in the same config file name.

### `core_directory` is OS-scoped
`core_directory` accepts either a plain string or an object keyed by OS name, resolved through
`ResolveOsScopedValue` like `core_file_name` beside it. Existing metadata using a plain string keeps
working unchanged.

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

## Discrete GPU preference

On hybrid laptops the compositor (Linux) or the driver (Windows) hands out the integrated GPU by
default, which is wrong for both the frontend's background shader work and for every emulator.
`ConfigManager.PreferDiscreteGpu` (`[Graphics] PreferDiscreteGpu`, default true) drives
`DiscreteGpuPreference`.

The two platforms need completely different mechanisms:

- **Linux** sets PRIME offload environment variables on the child `ProcessStartInfo`. They are
  applied before `ApplyLaunchEnvironment`, so an emulator's own `launch_env` in `meta.json` can
  still override them.
- **Windows** writes `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`, keyed by full executable
  path with the value `GpuPreference=2;`. This is the same key the Windows Graphics Settings UI
  writes. It is registered for each emulator as it launches, and for our own executable at startup.

### The registry call is isolated in its own method on purpose
`Microsoft.Win32.Registry` is Windows-only. A platform check *inside* a method does not stop the JIT
from having to resolve the type when that method is compiled, so the registry work lives in
`WriteWindowsGpuPreference`, marked `[SupportedOSPlatform("windows")]` and `NoInlining`, behind an
`OperatingSystem.IsWindows()` guard in the caller. Inlining it back would risk a type-load failure on
Linux, where the method is still reached (and returns early) on every emulator launch.

### The setting cannot move the frontend itself on Linux
PRIME offload variables must exist in the environment *before* the process starts, so the app cannot
apply them to itself once running. On Linux the setting therefore affects launched emulators only;
moving the frontend requires the variables in whatever launches it (`.desktop` entry, wrapper
script, or `tools/linux-test/deploy.ps1 -Gpu discrete` when testing). On Windows the registry key
covers the frontend too, but only from the next launch onward.

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

### A quick-switch fade with nothing to fade back in leaves the list invisible
`BeginQuickSwitchFade` drops `gameList` and `detailsPanel` to alpha 0 and relies on the
`TransitionToSystem` that follows to bring them back. Nothing did when the carousel could not
actually move: `Next`/`Previous` wrapped a single-entry list back to the same index,
`SelectSystemByIndex` hit its `index == currentGameSystemIndex` early return, and the fade was never
reversed. The page looked empty because it was invisible, and re-entering the mode fixed it only
because that path runs a full transition.

Latent for any one-entry list, and collections made it routine — most users have exactly one
Favorites collection.

Fixed at both ends. `Next`/`Previous` return false when there is nothing to move to, so the caller
never starts the fade; and every early return in `SelectSystemByIndex` calls
`CancelQuickSwitchFade`, so a fade can never outlive the transition that was supposed to end it.

### Closing a panel has to hand focus back
`gameList` keeps its own focus for controller navigation, and nothing restored it when the start menu
closed — focus stayed on a button that was no longer visible, so the list ignored input until a click
gave it back. `UpdateMouseFocus` did not rescue it either, since that only runs on mouse motion.

`MainScene` now subscribes to the panel's `Closed` signal rather than restoring focus inside
`ToggleStartMenu`, so it holds however the panel was dismissed — toggle, Back, or a menu action. It
is guarded by `IsAnyMenuOpen` so a panel closing beneath another one does not steal focus from
whatever is still open.

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

### Dot-prefixed save items are the emulator's, never a game's
A netplay client in RetroArch does not write to the normal save path. It redirects SRAM into
`<savefile_directory>/.netplay/`, so the joining player's own save is not overwritten by the host's
state. Measured by joining a local session: the host wrote
`saves/Pokemon - Emerald Version (USA, Europe).srm`, the client wrote
`saves/.netplay/Pokemon - Emerald Version (USA, Europe).srm`.

RetroArch declares no `sync_include`, which means "sync everything", so `SyncAfterExit` would have
treated `.netplay` as one game's save unit and uploaded it to RomM as `.netplay.folder.zip` under
whatever game was being played — once per session, for every joining player.

`ShouldSyncSaveItem` now rejects any item whose name starts with `.` before the allowlist is
consulted. This is deliberately a rule in code rather than a `sync_include` entry per recipe.
`sync_include` is an allowlist, so covering RetroArch that way means enumerating every save extension
across 41 systems and 44 cores; a single omission silently stops a real save from syncing, which is a
worse failure than the one being fixed. The saves directory of this install already holds both `.srm`
and `.sav` for the same game, which is how little the extension set can be trusted. A per-recipe
allowlist for RetroArch should be built empirically, one system at a time, as each is verified.

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

## Collections

### Collections are projected into `GameSystem` with negative ids
A RomM collection becomes a `GameSystem` carrying `IsCollection = true` and a synthetic **negative**
`Id`, with its games registered in `gameCache` under that id. Real platform ids from RomM are always
positive, so the two can never collide and `systemId < 0` is a reliable test for "this is a
projection, not a platform".

Doing it this way means `SystemCarousel.Populate`, the game grid, the card pool and the art pipeline
all work on collections unchanged — the carousel takes a `List<GameSystem>` and does not care where
the entries came from.

`Project` clears existing negative-id entries before rebuilding, so it is idempotent and self-heals
if a stale projection ever reached `games.cache`.

### A collection is a view, never a re-homing
Games in a collection are the **same `Game` objects** already in `gameCache`, so each one keeps the
`System` it was given at cache build. `LaunchEmulatorWithGame` reads `game.System.Slug`, not the
carousel selection, so a game launched from a collection still resolves its real platform's emulator.

This is load-bearing and easy to break. `OnRefreshCurrentSystemGamesPressed` assigns
`game.System = currentSystem` to every game it fetches; running it while a collection is selected
would re-home the whole collection onto a synthetic system with no emulator and break launching for
those games everywhere, not just on that page. It now returns early on a collection, as does
`OnSelectBiosMenuPressed` — BIOS and preferred-emulator are per-platform concepts with no meaning
here.

### The start menu's emulator actions follow the game, not the carousel
`GetCurrentPlatformSystem` resolves the selected game's own `System` first and only falls back to the
carousel entry. Every emulator action in the start menu — launch, install, update, uninstall — goes
through it, so the buttons name the emulator that would actually run the highlighted game.

It used to read the carousel system's slug directly. That was already slightly wrong (the menu
described the system rather than the selection) and became outright broken with collections: a
collection's slug maps to no emulator, so `GetMappedEmulator` returned empty and every emulator
button went permanently dead on a collection page.

The fallback returns null rather than the carousel entry when that entry is a collection, so a
collection with no selected game disables the actions instead of resolving to nonsense.
`OnLaunchEmulatorPressed` needs the `GameSystem` itself, not just the emulator name, which is why the
helper returns the system and `GetCurrentEmulator` is layered on top rather than the other way round.

### Collections bypass the emulator filter, and are not cached
`GetCache` filters out systems with no mapped-and-installed emulator unless `ShowAllSystems` is set.
A collection has no emulator by definition, so the collections branch returns before that filter
rather than being exempted inside it.

They are also fetched fresh on every load rather than written to `systems.cache`. Smart collections
are evaluated server-side from `filter_criteria` and change without the client doing anything, so a
cached copy would go stale invisibly. The cost is one extra request per launch.

Virtual collections need their own type. `VirtualCollectionSchema.id` is a **string** where the other
two are integers, so deserialising them into `Collection` would fail on the id alone. `Project` takes
both lists and normalises them into one internal shape whose `SourceId` is a string, which is safe
because the synthetic system id is assigned by the projection and never derived from the source id —
the source id only reaches the slug.

Ordering is favourites, then real collections, then virtual ones grouped by `type`. Virtual
collections sort last deliberately: they are auto-generated per genre, franchise and publisher, so
there can be a great many of them and they would otherwise bury the collections a user actually
curated.

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

### A signal per chunk is what made the updater slow, not the network
`AppUpdater` read the release zip in 8 KB chunks and fired
`CallDeferred(EmitSignal, UpdateDownloadProgress, …)` on **every one of them**. For the 87 MB v1.0.14
release that is 11,097 marshalled main-thread calls, each one re-rendering a progress bar and
re-setting a label, queued onto the same thread that is trying to draw at 60 fps.

The network was never the constraint: the same asset pulls in **1.0 s at 89 MB/s** measured with
curl on this machine. The download was starved by its own progress reporting.

The fix is the pattern the note above already describes — the worker publishes bytes with
`Interlocked.Exchange`, `_Process` emits at most once per frame, and the buffer matches
`DownloadManager` at 1 MB. That turns ~11,000 emissions into at most one per frame, and 11,097 reads
into 87. `_Process` also skips emitting when the value has not changed, so an idle updater costs
nothing.

### Installer downloads reach the downloads page as external transfers
`UniversalInstaller` does its own HTTP (see above) and so never appeared on the downloads page —
emulator installs emitted only `EmulatorInstallationCompleted`, with no progress at all. That was
tolerable when an install was a single archive; it stopped being tolerable when RetroArch grew a
227 MB cores bundle on top of its own download.

`DownloadManager.BeginExternalTransfer` / `ReportExternalTransferProgress` / `CompleteExternalTransfer`
let a transfer the manager does not own publish through the *existing* `DownloadProgressUpdated` and
`DownloadCompleted` signals, so `DownloadProgressUI` needed no changes. They are held in a
`ConcurrentDictionary` rather than the plain `List` used for game downloads, because the installer
resumes on a task thread after its awaits and would otherwise mutate that list while `_Process`
iterates it. Reads still happen only in `_Process`, so the threading rule above is unchanged.

A missing `Content-Length` reports a total of 0, which `DownloadProgressDisplay.ApplyTo` already turns
into an indeterminate bar, so no extra guard is needed at the call site.

Every transfer the user starts or waits on now reports: ROMs and firmware through `DownloadManager`
directly, the emulator archive and cores bundle through `UniversalInstaller`, save sync through
`RomMAPI`, and the app update through `AppUpdater`.

### Box art is the one download deliberately kept off the page
`RomMAPI.DownloadAssetAsync` serves both save sync and `AssetManager`'s cover art, and reports
progress **only when given a `displayName`**. Save sync passes one; `AssetManager` does not, so art
stays silent.

That asymmetry is the point. The asset queue runs two workers continuously while the user browses and
fetches a cover for nearly every game they scroll past — hundreds of small files, each finishing in
well under a second. Listing them would flood the downloads page with entries appearing and vanishing
faster than they can be read, and bury the ROM or emulator install the page exists to show. Opting in
by name means a new caller is silent until someone decides it is worth showing, rather than the
reverse.

### `HttpClient` needs an infinite timeout for large transfers
`HttpClient.Timeout` covers the whole operation, body included, even with
`HttpCompletionOption.ResponseHeadersRead`. The default 100 s therefore aborts any download slower
than roughly 2.3 MB/s once it passes ~227 MB — which the RetroArch cores bundle does exactly.
`DownloadManager` already set `Timeout.InfiniteTimeSpan`; `UniversalInstaller` and `AppUpdater` did
not, and now do. Cancellation is the `CancellationTokenSource`'s job, not the timeout's.

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
