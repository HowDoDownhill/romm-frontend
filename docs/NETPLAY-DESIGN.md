# Netplay Design

Status: **implemented for RetroArch.** Phases 0 through 1c are done and were verified across two
machines — a Windows host and the Arch test box — covering lobby, roster, game selection, readiness,
launch, and the host's quit closing everyone's emulator. Phases 2 through 4 are open; see
[Phasing](#phasing).

## Goal

Let two or more romm-frontend users play the same game together, with the frontend handling
everything that normally makes emulator netplay painful: matching ROMs, matching emulator
builds, matching settings, and connecting through NAT.

## Non-goals

- **Streaming (Sunshine/Moonlight).** Deliberately out of scope. Sunshine serves one client
  per session, so it cannot put two remote players in one game. It is a remote-play feature,
  not a multiplayer feature, and is tracked separately.
- **Writing netcode.** We drive netplay that emulators already implement. We never sit in the
  input path.
- **A public matchmaking lobby.** Not in v1. See [Session discovery](#session-discovery).

## Emulator capability matrix

Verified July 2026.

| System | Emulator | Netplay | How it is driven |
|---|---|---|---|
| Dreamcast, Naomi | Flycast | GGPO rollback | Launch args (`-config network:*`) |
| Many | RetroArch | Lockstep + public lobby | Launch args (`--host`, `--connect`, `--port`) |
| N64 | gopher64 | P2P mesh, TURN fallback | Server address; self-hostable server |
| PSP | PPSSPP | Ad-hoc over network, relay in 1.20+ | `ppsspp.ini` |
| GC, Wii | Dolphin | Traversal server, mature | **Blocked** — no CLI, PR #13288 open since Jan 2025 |
| 3DS | Azahar | Public/private rooms | GUI only |
| DS | melonDS | Incomplete upstream | Not viable yet |
| SNES | snes9x | Legacy lockstep | Partial |
| PS1, PS2, GBA, multi | DuckStation, PCSX2, mGBA, ares | None | — |

Two consequences:

1. **RetroArch must be added as an emulator provider.** It is the only way to cover the 2D-era
   library, which is where lockstep netplay actually feels good. See
   [Emulator selection](#emulator-selection).
2. **Dolphin is blocked upstream.** Do not build UI automation to work around it. Track the PR.

## Verified RetroArch matrix

Measured, not assumed. Two local RetroArch instances per system, `--host` and
`--connect=127.0.0.1`, default core from `system_cores`, saves redirected away from the store.
A pass means the client logged `You have joined as player 2` and both sides logged the same
content CRC32. RetroArch 1.21.0 stable cores, July 2026.

| System | Core | Netplay | Ping |
|---|---|---|---|
| gb | gambatte | yes | 19 ms |
| gba | mgba | yes | 5 ms |
| genesis | genesis_plus_gx | yes | 19 ms |
| nds | melonds | yes | 7 ms |
| nes | mesen | yes | 19 ms |
| sega32 | picodrive | yes | 19 ms |
| segacd | genesis_plus_gx | yes | 6 ms |
| sms | genesis_plus_gx | yes | 7 ms |
| snes | snes9x | yes | 18 ms |
| psx | **mednafen_psx_hw** | yes | 8 ms |
| psx | swanstation | **no** | — |
| n64 | mupen64plus_next | **no** | — |
| n64 | parallel_n64 | **no** | — |
| dc | flycast | **no** | — |
| psp | ppsspp | **no** | — |

`Core does not support netplay` is RetroArch refusing a core that lacks the deterministic
serialisation netplay needs. It is a property of the core, not of the content, and no setting
works around it.

### Consequences

**Ten systems work today**, which is the entire 2D-era library where lockstep netplay feels
good. That is enough to build the plumbing against.

**psx needs its default core changed** from `swanstation` to `mednafen_psx_hw` if it stays on
RetroArch. The current default cannot netplay at all. This is the only case where the declared
fallback rescued a system, which is why the whole list was walked rather than just the defaults.

**n64, dc and psp have no libretro path** and move to their standalone emulators. That is not a
loss: gopher64 has P2P netplay with TURN fallback, Flycast has GGPO rollback, and PPSSPP has
ad-hoc. All three are better served standalone, so the netplay-capable emulator for those
systems is the standalone one and phase 4 covers them.

**Sega CD needs its BIOS staged before it will boot**, which the frontend does at launch. The
first matrix run bypassed the frontend and recorded a false failure until `bios_CD_*.bin` were
copied into `system/`. Worth remembering when testing a CD-based system outside the app.

## Emulator selection

There is **no separate "preferred netplay emulator" setting.** RetroArch is listed in
`EmulatorMap.json` like any other emulator, appears in the existing per-system Preferred
Emulator dropdown, and is chosen the same way. Netplay support is surfaced as a badge on each
entry in that list.

This keeps one concept — *which emulator runs this system* — rather than two parallel ones. It
needs no new selection machinery: `GetSupportedEmulators` already returns the candidate list
and `MainSceneSettingsHandler` already renders it. If a user picks an emulator without netplay,
the netplay option is hidden with an explanation pointing at the emulator setting, so the
reason is discoverable and the fix is one dropdown away.

**RetroArch is modelled as one emulator entry, not one per core.** This follows the existing
`ares` precedent: a single binary covering many systems, with `system_flags` selecting the
per-system behaviour. Where ares emits `--system "Mega Drive"`, RetroArch emits
`-L "cores/genesis_plus_gx_libretro.dll"`. No new schema is required.

Per-core choice within a system (bsnes vs. snes9x vs. mesen-s for SNES) is deferred. When it is
wanted it can be a `settings_fields` dropdown with
`"launch_arg_format": "-L cores/{value}"`, which the existing schema already supports. Note the
limitation: `settings_fields` are per-emulator, not per-emulator-per-system, so a per-system
core override needs schema work beyond that.

### Netplay support is per-system, not purely per-emulator

A multi-system emulator may support netplay for some of its systems and not others — RetroArch
netplay depends on the core, and non-deterministic cores do not support it. So the badge cannot
be derived from the presence of the `netplay` block alone. Add
`"unsupported_systems": ["..."]` inside the block, and resolve the badge per system-and-emulator
pair.

### Known consequence: saves do not follow an emulator switch

`relative_save_path` is per-emulator, so switching a system from snes9x to RetroArch to get
netplay changes which directory save-sync tracks, and existing saves appear to vanish. This is
pre-existing behaviour rather than something netplay introduces, but netplay will cause users to
switch emulators far more often than they do today, so it will start to bite. Worth warning on
the switch at minimum.

## Configuration: the `netplay` block

Netplay is described declaratively in each `install_scripts/<emulator>/meta.json`, alongside
`install_recipe` and `settings_fields`. Emulators without netplay omit the block and the UI
hides the option, matching how `controller_config` already degrades.

```json
"netplay": {
  "transport": "launch_args",
  "host_args": "-config network:GGPO=yes -config network:ActAsServer=yes -config network:GGPOPort={local_port}",
  "join_args": "-config network:GGPO=yes -config network:ActAsServer=no -config network:server={peer_address} -config network:GGPOPort={local_port}",
  "max_players": 2,
  "default_port": 6000,
  "requires_identical_rom": true,
  "requires_identical_version": true,
  "unsupported_systems": []
}
```

`transport` selects how the settings are applied:

- `launch_args` — substituted into a `{netplay}` placeholder in `launch_args_with_game`,
  exactly as `{settings}` works today.
- `config_file` — written before launch through the existing `IConfigurationUpdater`
  implementations. This is what PPSSPP needs, and reuses machinery already built for
  `settings_fields`.

Placeholders resolved by `NetplayManager`: `{local_port}`, `{peer_address}`, `{peer_port}`,
`{player_name}`, `{player_index}`.

## NetplayManager

A new autoload, registered with `AppInstance` like every other singleton. Responsibilities:

- Resolve host/join role and player index for a session.
- Allocate local ports and open the relay tunnel.
- Produce the launch-argument fragment or config-file writes for the chosen emulator.
- Enforce parity before launch (below), and refuse to launch when it cannot be satisfied.

It hooks `LaunchEmulatorWithGame` between save sync and `BuildAndStartEmulatorProcess`, so a
netplay launch is an ordinary launch with extra arguments.

## Parity enforcement

This is the part that makes the feature worth building here rather than leaving users to
coordinate manually, and it is the main source of real work.

Netplay desyncs unless every player has an identical ROM, an identical emulator build, and
identical relevant settings. The frontend controls all three.

**ROM parity.** Both players pull from a RomM server, and RomM exposes CRC/MD5/SHA1 per file
(already modelled on `Firmware`; the same fields exist on ROM files). The host publishes the
hash with the session; joiners verify locally and download the correct file if it differs.

**Emulator version parity.** *This requires a change to existing behaviour.* `UniversalInstaller`
currently resolves the *latest* GitHub release at install time, so two users who installed a
week apart are silently on different builds. Netplay needs the exact version recorded and
pinnable. `GetAvailableReleases` and `ReleaseOption` already exist, so the missing pieces are:
persisting the installed version per emulator, and installing a specific version on demand.

**Settings parity.** Only some `settings_fields` affect determinism. Add an
`affects_determinism` flag to the schema; the joiner adopts the host's values for those fields
for the duration of the session and restores afterwards.

## Connectivity

**Decision: userspace relay with localhost port mapping. No virtual network adapter.**

An overlay VPN (Nebula, MIT; or Tailscale/Headscale, BSD-3) was considered and rejected for
v1. ZeroTier is ruled out permanently: it is BSL 1.1, source-available rather than open
source, and incompatible with this project's GPL-3 license.

The blocker for all of them is privilege. A TUN adapter needs a driver install and admin on
Windows, root or `CAP_NET_ADMIN` on Linux, and a notarized system extension on macOS. That
turns an unzip-and-run frontend into something needing an installer, which is a real
regression on handhelds.

Instead: the app listens on `127.0.0.1:<local_port>`, tunnels to the peer, and the emulator is
pointed at localhost. Every emulator targeted in phase 1 and 2 accepts a configurable peer
address, so this is sufficient. It needs no elevated privileges, behaves identically across all
three platforms, and lives entirely in C# inside the existing process.

The cost is losing LAN broadcast autodiscovery (gopher64's automatic server discovery,
melonDS local wireless). Nebula remains available later as an opt-in power-user mode for
exactly those cases.

## Session discovery

**v1: join codes.** The host generates a short code encoding its address, game hash, emulator
and version. The joiner enters it. No server to run, no accounts, no moderation burden.

A friends list is deliberately deferred, and there is a structural reason. RomM is self-hosted
per user: two friends run two different servers with different user tables and different
libraries, so there is no shared identity to build a friends list on. Presence would require
its own service — the same service as the relay — with the hosting, cost and moderation that
implies. The session payload is designed so a broker can be added later without changing the
launch path.

## Phasing

0. **Verify per system.** *Done* — see the matrix above. Ten systems confirmed working.
1. **Schema and manager.** *Done.* `netplay` block, `NetplayManager`, `{netplay}` placeholder and
   join codes, scoped by `unsupported_systems` (`n64`, `dc`, `psp`).
1b. **Host UI.** *Done.* A netplay view in the start menu shows the join code and launches on
   confirm; the button is gated on the game being launchable. Hosting was built first because it
   needs no text entry — the code is generated and displayed, never typed.
1c. **Lobby transport.** *Done.* `NetplayLobby` is an ENet host/client carrying identity,
   roster, game selection, readiness and start/quit as RPCs. Supersedes the join-code entry
   problem: players pick a session from a list instead of typing a code, so no on-screen keyboard
   is needed. The host UI's join code remains the direct-connect fallback.

   Verified across two machines: both sides see the full roster, the host's browsing moves every
   client's carousel, readiness gates the start, and the host quitting closes the clients'
   emulators. A LAN session was confirmed to actually netplay, not merely to launch.

   Still unverified: save sync after a netplay session, and internet play through a join code —
   the latter cannot be exercised from inside the host's own network.

## Lobby panel

The lobby replaces the **details panel** rather than opening over it: both are children of the same
mica `Panel`, and `MainSceneNetplayHandler` toggles `detailsPanelContainer` against `lobbyPanel`. The
game grid stays live underneath, which is the point — the host browses normally and every selection
is pushed to the lobby, so the carousel doubles as the host's game picker.

`PushHostGameSelection` is called from `OnGameSelected`, guarded three ways: host only, ignore an
unchanged rom id, and ignore a game whose system cannot netplay. Without the rom-id guard every
frame of carousel movement would broadcast.

### The grid refresh owns details-panel visibility, so the lobby has to be checked there

`RefreshGameList` sets `detailsPanelContainer.Visible = currentlyShownGames.Count > 0` on every
rebuild. Toggling that flag when the lobby opens was not enough — the next grid refresh, which any
selection or system change triggers, put the details panel straight back over the lobby. The
visibility expression now also asks whether the lobby owns the panel, so there is a single place
deciding it rather than two fighting.

### Focus must be taken after the refresh, not before

Godot drops focus from a control the instant it is disabled, so grabbing focus and *then* refreshing
the panel loses it whenever the refresh disables that control. That is the common case rather than an
edge case: a host who has just set a game sees `Waiting For Players` disabled until somebody joins, so
focus vanished immediately and the d-pad did nothing.

`ReturnFocusToLobby` therefore refreshes first and focuses afterwards, and picks the first
**enabled and visible** lobby button rather than assuming the action button is usable.
`RefreshLobbyPanel` re-checks afterwards and re-focuses if nothing inside the panel holds focus, so
any later state change — a player joining, a download finishing — cannot strand the panel with no
focus either.

### The lobby has two focus modes, and A had to be re-routed for both

A host is either driving the lobby buttons or browsing the grid, never both, so `MainScene` routes
`Select` and `Back` through `MainSceneNetplayHandler` while a lobby is open.

- **A** confirms the hovered game and returns focus to the lobby; in lobby focus it presses whichever
  lobby button is focused.
- **B** swaps modes — from the lobby it reopens browsing, from browsing it returns without changing
  the pick.

The re-routing was not optional. `MainScene` already intercepts `Select` and calls
`GetViewport().SetInputAsHandled()`, so once focus moved to a lobby button, **A would have been
swallowed and Ready/Start/Leave could never have been pressed**. `PressFocusedLobbyButton` checks
`Disabled` before emitting `Pressed`, because manual emission otherwise bypasses the disabled gate.

Selection is confirmed rather than live: the host used to broadcast on every carousel highlight,
which put a network message behind every frame of scrolling and left no moment at which focus could
sensibly change hands.

### The session is opened first, then a game is chosen

Hosting deliberately does **not** depend on the currently selected game. An earlier version required a
supported, downloaded game before the Host button would enable, which inverted the intended flow —
open a lobby, wait for players, *then* pick something together — and left the button greyed with no
explanation once its label was fixed.

Constraint now lives in the game list instead of the button: `FilterSystemsForNetplayHost` narrows
the carousel to netplay-capable systems for anyone **in a lobby**, host or client, so an
unplayable choice cannot be made rather than being rejected after the fact. The button is enabled
whenever no session is active.

The filter is applied in `GetCache` after the existing emulator filter, so it composes with
`ShowAllSystems` and the collections branch rather than replacing them.

It falls back to the unfiltered list if the filter would empty the carousel, on the same reasoning as
the existing "no mapped emulators" fallback — an empty carousel is worse than an imperfect one.

Opening and leaving a lobby both call `RefreshBrowseSourceForLobby`, which re-runs `GetCache` and
reselects from index 0, because the filtered list is a different length and the old index may no
longer exist.

### Clients need a grace period before connecting

The host's RetroArch takes seconds to boot its core and start listening. `ReleaseMembersToStart` is
therefore sent *after* the host launches, and clients still wait `HostStartupGraceSeconds` before
launching their own — `--connect` against a host that is not yet listening simply fails, with no
retry. Five seconds is a guess that wants replacing with a real "host is listening" signal once
there is a way to observe it.

### The host is the reference build, and versions are compared against it

RetroArch refuses to connect peers on different builds, so netplay needs every player on the same
emulator version. The installed version is recorded as `installed_version.txt` in the emulator's own
directory at the end of a successful install, taken from the `ReleaseOption.VersionLabel` the
installer already resolved and previously discarded. Keeping it inside the install directory means it
cannot outlive what it describes.

**The newest version present wins, not the host's.** An earlier design made the host the reference
build, which is simpler but wrong in one common case: a host on an older build would tell a client on
a newer one to *downgrade*. Asking somebody to go backwards to join a game is the wrong direction,
and it silently spreads stale builds through a group.

Every member's installed version now rides along in the roster, and both sides compute the same
target — the highest version anyone has. Whoever is behind is the one who changes, host included, so
a host that is behind its clients sees `Update <emulator>` on its own action button and the session
converges upward.

Ordering comes from `EmulatorVersions.Compare`, which extracts the numbers from a release label and
compares them component-wise, because tags vary wildly between emulators (`v1.21.0` against
`2.7.492.r0.g66c0771`). When two labels cannot be ordered — no digits, or otherwise incomparable —
the comparison returns "equal" and the rule falls back to the old behaviour of matching the host
exactly, which is still correct, just not clever.

A mismatch blocks readiness, shows `Needs to update <emulator>` in the roster, and offers
`Update <emulator>` which installs that *specific* release via the `ReleaseOption` overload rather
than the latest. Installing the latest is what caused the drift in the first place.

Where the target really is older than what is installed — only reachable through the unorderable
fallback above — both the roster text and the button say **downgrade** rather than update. The
release picker does the same for any version older than the installed one, marking it `— downgrade`
and tinting it, with the installed one marked `— installed`, so choosing a version by hand cannot
quietly move an emulator backwards.

An installed emulator with no recorded version counts as a mismatch, since it predates this tracking
and cannot be proven to match. A client that cannot find the host's version in the available releases
logs plainly rather than silently installing something else.

### Readiness reuses `ResolveGameAction` rather than testing the pieces itself

A player is only prepared when the emulator is installed, the core is present *and* the ROM is
downloaded. `ResolveGameAction` already encodes exactly that ladder for the play button, so the lobby
asks it for `Kind == LaunchGame` instead of re-deriving the conditions. A second implementation would
inevitably drift from the first, and it would have missed the core check entirely.

The same call drives the lobby's action button, so it offers `Install <emulator>` or
`Download <game>` — whichever is actually missing — and only becomes Ready/Start once neither is.

### Readiness has to be recomputed when the prerequisites land

Preparedness is derived, not stored, so nothing would notice a download or an install finishing. The
handler subscribes to both `DownloadManager.DownloadCompleted` and
`EmulatorManager.EmulatorInstallationCompleted` and re-reports, otherwise a player who set themselves
up inside the lobby would sit at "needs setup" until something else forced a refresh.

Re-reporting also clears a stale ready flag: readiness is sent as `isPrepared && isLocallyReady`, so a
player who readied up and then lost a prerequisite cannot stay marked ready.

## Lobby model

The lobby is **per session, not a global directory**. The host of the game hosts the lobby, so the
lobby ending when they leave is correct behaviour rather than a failure — there is nothing to migrate
because the netplay session is over too.

Flow: host opens a lobby → players join → host picks a game → each client downloads it if missing →
clients report readiness → host launches and releases the others → host quitting closes everyone's
emulator, which still triggers each player's save sync on process exit.

### Discovery is the unsolved half, and RomM cannot help

RomM has **no presence API**. `/api/play-sessions` is playtime history — `PlaySessionSchema` requires
`end_time` and `duration_ms`, so sessions are recorded after they finish. Nothing across its 118
endpoints answers "who is hosting right now", and there is no websocket or event surface.

ENet does not solve this either: a client needs an address before it can connect, so a listen server
cannot advertise itself.

**`/api/devices` was the promising candidate and it does not work.** The row already exists per
client, `sync_config` is a free-form object, `ip_address` and `last_seen` are present, and `user_id`
is server-provided rather than self-reported — it would have been better identity than the lobby
handshake has. Measured instead: with a second user on the same server launching a game (which
registers a device through save sync), an **admin** token still saw only its own single row.
`GET /api/devices` is scoped to the authenticated user. By contrast `/api/users` returns all 11
accounts to that same token, so RomM exposes cross-user data where it means to; devices are not in
that category.

The remaining cross-user-writable surfaces are public collections and per-ROM notes. Both would work
as bulletin boards and both are user-facing content that would look like corruption to anyone
browsing RomM normally, so neither is used.

Discovery is therefore LAN broadcast now, with a companion service as the internet answer, behind one
swappable seam.

### LAN discovery advertises over UDP broadcast

`NetplayDiscovery` binds one `PacketPeerUdp` to port 55441 with broadcast enabled, advertises every
two seconds while hosting, and expires a session that has not been heard from for eight. The sender's
address comes from `GetPacketIP` rather than the payload, so a host cannot advertise someone else's
address.

Advertisements carry the RomM host and are dropped when it does not match the local one, which keeps
the "same server" scoping the lobby also enforces. The host's own packets come back to it, so each
instance filters on a locally generated `instance_id`.

### UPnP replaces the directory for internet play, when the router allows it

There is no serverless way to *list* internet sessions — something must sit at a known address and
answer "who is hosting". But there is a serverless way to *join* one: the join code already encodes
an address and port, so if the host can make itself reachable, no directory is needed. Internet play
becomes "share a code"; browsing stays a LAN feature.

`NetplayPortMapper` asks the router over UPnP to open the netplay port and reads the public address
back from `QueryExternalAddress`, which is then what the join code encodes. `Upnp.Discover` blocks
for seconds, so it runs on a task thread and never touches the scene tree. Mappings are deleted on
cancel, on emulator exit and on tree exit — an abandoned mapping is a hole left in someone's router.

**Expect this to fail more often than it works.** Measured on the developer's own network, RetroArch's
identical UPnP request was refused: `Netplay UPnP Port Mapping Failed`, followed by
`Your room is not connectable from the internet`. Consumer routers increasingly ship with UPnP
disabled, and carrier-grade NAT defeats it regardless of settings. The failure path therefore names
the port to forward manually rather than just reporting an error, because a user who can expose RomM
can forward a port.

**Two instances on one machine cannot both discover.** The socket binds a fixed port and Godot's
`PacketPeerUdp` exposes no address-reuse option, so the second bind fails and logs why. Testing
discovery needs two machines; the lobby itself can still be tested on one via direct connect.

### RomM identity is a filter, not proof

Peers exchange their RomM host URL and username, and a peer on a different server is disconnected.
That scopes a lobby to one RomM instance, which is what makes `rom_id` a shared identifier — the
reason "host selects game" can travel as a single integer and each client can resolve it against its
own library.

It is **not authentication**. A peer can claim any username; confirming the name exists via
`/api/users` does not prove the peer is that user. Real proof would need the client to hand over its
RomM API token, which is a credential leak and must never be done, or a challenge/response RomM does
not offer. Adequate for friends on a home server; not a security boundary.
2. **Parity.** ROM hash verification and `affects_determinism` on settings fields. Emulator
   version pinning is already solved for RetroArch — cores ship from the same versioned bundle
   as the executable, so peers on the same release cannot drift.
3. **Relay.** RetroArch's own `--mitm-session` and `netplay_use_mitm_server` may remove the need
   to build anything here. Confirmed necessary: the host logged
   `Your room is not connectable from the internet` after UPnP failed, so direct connection is
   not a realistic default.
4. **Standalone coverage.** Flycast GGPO, gopher64, then PPSSPP via `config_file` — the three
   systems libretro cannot serve. Dolphin when PR #13288 lands.

Phase 1 is only useful on a LAN. That is intentional — it proves the launch path before any
network service exists.

## Flycast status

Driven entirely by `-config network:*`, keys read out of the binary rather than guessed — see
`DESIGN-NOTES.md` for the key list and the Lua-binding lookalikes that are not config keys.

Verified on the two-machine rig:

- `dc` is offered for netplay and the lobby accepts a Dreamcast game
- both sides render the intended arguments — `network:GGPO=yes` plus `ActAsServer` on the host and
  `network:server=<host>:19713` on the client
- GGPO activates on both (each opens UDP 19713, and Flycast takes its netplay `.state.net` path)
- the 210 MB ROM hashes once and is served from `romhashes.cache` on every later run

**Not yet verified: that a session actually synchronises.** Both peers sat at ~0.5% CPU with no
`Connected to peer`, because the Windows host has two *enabled inbound Block* firewall rules for
`flycast.exe` (TCP and UDP, Private+Public). That is a machine setting, not a code path — the
frontend cannot and should not rewrite it. Re-run the LAN test once those rules are removed or
changed to Allow, and confirm `Connected to peer` and a CPU load consistent with emulation.

Internet play additionally needs the port-mapping change described in `DESIGN-NOTES.md`: the
frontend forwards the lobby-time session port, so Flycast's 19713 is never opened.

## Open questions

- Does gopher64 accept a server address from the command line, or only via its UI and LAN
  discovery? Determines whether it lands in phase 4 or needs a config-file path.
- Is `--mitm-session` enough for internet play, or is a self-hosted relay still wanted for
  privacy and reliability?

*Resolved:* psx stays on RetroArch with `mednafen_psx_hw` as its default core. n64 moves to
gopher64; dc and psp already defaulted to Flycast and PPSSPP as gen-6 disc systems, so they
needed no change.
