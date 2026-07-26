# Netplay Design

Status: **proposal** — not yet implemented.

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

1. **Schema and manager.** `netplay` block, `NetplayManager`, `{netplay}` placeholder, join
   codes, direct LAN connection. Validate against Flycast and RetroArch together, since they
   have genuinely different transports and will keep the abstraction honest.
2. **Parity.** Emulator version pinning in `UniversalInstaller`, ROM hash verification,
   `affects_determinism` on settings fields.
3. **Relay.** Userspace tunnel for players not on the same LAN.
4. **Coverage.** gopher64, then PPSSPP via `config_file`. Dolphin when PR #13288 lands.

Phase 1 is only useful on a LAN. That is intentional — it proves the launch path before any
network service exists.

## Open questions

- Does gopher64 accept a server address from the command line, or only via its UI and LAN
  discovery? Determines whether it lands in phase 1 or needs a config-file path.
- RetroArch core installation: bundle cores with the recipe, or use its own core downloader?
  Affects version pinning, since core version is part of parity.
- Which RetroArch cores are the netplay-capable defaults per system, and which systems belong
  in `unsupported_systems`?
