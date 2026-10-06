# Linux testing on a real Arch machine

Godot exports the Linux build from Windows, so the Arch machine only ever receives a finished
build — it needs no Godot, no .NET SDK and no checkout of this repo. The .NET runtime ships
inside `data_romm-frontend_linuxbsd_x86_64/`, so a bare Arch install with a desktop session is
enough.

This complements the WSL path (see `DESIGN-NOTES.md`), which is a "does it run" gate on emulated
Vulkan. The Arch machine is where GPU-dependent work — the mica blur, the moving background
shader, frame pacing — actually gets validated.

## One-time setup

On the Arch machine:

```bash
sudo pacman -S --needed openssh rsync grim && sudo systemctl enable --now sshd
```

`rsync` has to exist on **both** ends or `deploy.ps1` drops to a full tarball copy each push.

The screenshot backend is compositor-specific: `grim` needs the `wlr-screencopy` protocol,
which KWin and Mutter do not implement, so on KDE it is present-but-broken and `spectacle`
is the working tool (`gnome-screenshot` on GNOME). `remote-run.sh` reads the desktop from
`loginctl`, orders the candidates accordingly, and actually runs each one rather than
trusting `command -v`.

On Windows:

```bash
cp tools/linux-test/config.example.json tools/linux-test/config.json
```

Fill in `host` and `user`, then:

```bash
powershell -File tools/linux-test/setup.ps1
```

That generates a dedicated `~/.ssh/romm-linux-test` key, installs it on the Arch machine (one
password prompt), copies it into WSL so `rsync` can use it, and reports anything missing on
either side.

## Daily use

```bash
powershell -File tools/linux-test/deploy.ps1
```

Exports the Linux build, syncs it, and launches it on the Arch machine's desktop session.

| Command | Effect |
|---|---|
| `deploy.ps1` | Export, sync, launch |
| `deploy.ps1 -SkipBuild` | Sync and launch the build already in `build/linux` |
| `deploy.ps1 -Action sync` | Push without launching |
| `deploy.ps1 -Action logs` | Tail the remote log (`-LogLines 500` for more) |
| `deploy.ps1 -Action shot` | Screenshot the remote screen into `build/linux-test-shots/` |
| `deploy.ps1 -Action stop` | Kill the running app |
| `deploy.ps1 -Action status` | Running state plus the detected display environment |
| `deploy.ps1 -Action push-login` | Copy the RomM credentials from the local `config.cfg` |

Pass engine flags through with `-AppArgs`:

```bash
powershell -File tools/linux-test/deploy.ps1 -AppArgs '--windowed','--resolution','1920x1080'
```

## GPU selection

On a hybrid laptop the compositor hands out the integrated GPU by default, which is useless
for validating the mica blur or the background shader. `-Gpu discrete` is therefore the
default and sets the PRIME offload variables (`__NV_PRIME_RENDER_OFFLOAD`,
`__GLX_VENDOR_LIBRARY_NAME`, `__VK_LAYER_NV_optimus`, `DRI_PRIME`, `VK_ICD_FILENAMES`).
Use `-Gpu integrated` to deliberately test the iGPU path.

Check which GPU it landed on with `-Action logs`. **Read the device name, not the API name.**
This project sets `renderer/rendering_method="gl_compatibility"` in `project.godot`, so the
log always says OpenGL Compatibility — that is correct and is not a Vulkan fallback:

```
OpenGL API 3.3.0 ... Using Device: Intel - Mesa Intel(R) UHD Graphics    <- iGPU, offload failed
OpenGL API 3.3.0 ... Using Device: NVIDIA - NVIDIA GeForce RTX 3060      <- discrete, correct
```

If it stays on the iGPU, check that the discrete card's Vulkan/GL ICD is actually installed
on the test machine (`ls /usr/share/vulkan/icd.d/`, `nvidia-smi`).

## Pushing login details

`-Action push-login` (or `-PushLogin` alongside a deploy) copies `Host`, `Username`,
`Password`, `ApiKey` and `ValidLoginLastUsed` out of the local `config.cfg` so the test
machine does not need to be logged in by hand.

`DeviceId` is deliberately withheld. `RomMAPI` registers it per machine and
`SaveSyncManager` stamps it on every save, so sharing one ID between the desktop and the
test machine would make RomM treat two devices as one. The test box registers its own on
first login.

The push is a merge, not an overwrite — only the listed keys inside `[RomM]` are rewritten,
leaving the test machine's `DeviceId`, `[UI]`, `[Input]` and any other section alone.

Note that this puts your RomM password in plaintext on the test machine, exactly as it
already is on the desktop.

## What gets synced

Only the files a release ships: `romm-frontend.x86_64`, `romm-frontend.sh`, `romm-frontend.pck`,
`data_romm-frontend_*`, `install_scripts` and `tools` — the same list as `build-release.bat`.
The build folder accumulates runtime data (a `config.cfg` holding live credentials, plus roms,
saves, bios and caches) whenever the app is run in place, and a wholesale sync would ship all of
it.

The reverse also matters: `rsync --delete` would wipe the test machine's own runtime state on
every push, so `deploy.ps1` marks `config.cfg`, `saves`, `states`, `roms`, `bios`, `downloads`,
`emulators`, `*.cache` and `.test-state` as protected. The Arch machine therefore keeps its login
and its downloaded roms between deploys.

## Why the remote script rediscovers the session environment

An SSH session gets none of the graphical session's environment, so launching the binary directly
over SSH fails with no display. `remote-run.sh` reconstructs it before exec: `XDG_RUNTIME_DIR`
from the uid, `WAYLAND_DISPLAY` by scanning `$XDG_RUNTIME_DIR/wayland-*` for a socket (skipping
the `.lock` files), `DBUS_SESSION_BUS_ADDRESS` from the runtime dir, and `DISPLAY` from
`/tmp/.X11-unix/X*` as an XWayland fallback. The app is launched under `setsid` so it survives
the SSH connection closing.

`XAUTHORITY` matters too. Without it XWayland rejects the connection with "Authorization
required, but no authorization protocol specified" and Godot's X11 driver fails before it can
fall back to Wayland, so `remote-run.sh` locates `~/.Xauthority` or `$XDG_RUNTIME_DIR/xauth_*`.

## Two-machine netplay tests

A lobby needs two machines, and the Arch box takes no remote input, so both ends are driven by
command-line arguments instead (see `DESIGN-NOTES.md`). Host on Windows, join from Arch:

```bash
powershell -File tools/linux-test/deploy.ps1 -Action run -AppArgs @('--','--netplay-join=192.168.1.13')
```

The `--` is not optional: the app reads these through `OS.GetCmdlineUserArgs`, and `remote-run.sh`
consumes one `--` of its own, so the first element of `-AppArgs` supplies the app's separator.

Call `deploy.ps1` **directly** rather than through `powershell -File` for this. Both `powershell
-File` and the Bash tool re-parse the command line, and `--` is then read as a parameter name
(`the parameter name '' is ambiguous`) or the whole array collapses into one token.

An exported build does not flush stdout on print, and stdout redirected to a file is block
buffered, so `GD.Print` output — every `[Lobby]` and `[Netplay]` line — would otherwise reach the
log minutes late or only at exit. `remote-run.sh` launches under `stdbuf -oL` to get line
buffering back. `-Action logs` is truthful only because of that.

## Testing internet play, not just LAN

Internet play cannot be tested from inside the host's own network. The working rig leaves the Arch box
**dual-homed**: a USB-tethered phone carrying the default route, and WiFi keeping the LAN subnet so
SSH still works.

```bash
ip route get <host-public-ip>     # must name the tether interface, not wlan0
ip route get 192.168.1.13         # must name wlan0, or SSH dies
curl -s https://api.ipify.org      # must differ from the host's public address
```

NetworkManager usually gets the metrics right on its own (tether 100, WiFi 600). If the LAN holds the
default route, `nmcli connection modify "<wifi>" ipv4.never-default yes` keeps its subnet route and
drops its default.

**The join must name the public address explicitly:**

```bash
~/romm-frontend-test/remote-run.sh run --gpu discrete -- -- --netplay-join=<host-public-ip>
```

Using the in-app Join button here **silently tests nothing**. `OnJoinNetplayPressed` prefers a
LAN-discovered session over the clipboard code, and WiFi is on the same LAN as the host, so it connects
to `192.168.1.13` and never leaves the building. The lobby fills, the game starts, everything looks
like a pass.

Two things that cost real time and are worth doing first:

**Kill stale hosts on the Windows side.** A previous instance holds UDP 55440, the new one cannot bind,
and hosting fails. Filter on the command line, not the process name, so the Godot *editor* survives:

```powershell
Get-CimInstance Win32_Process -Filter "Name like 'Godot%'" |
  Where-Object { $_.CommandLine -like '*--netplay*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

**Launch a second game in the lobby, not just the first.** Three separate bugs lived only in the second
launch — the netplay port, the advertised address and the UPnP lifetime — because every scripted test
opened a lobby, launched once, and stopped.

The scripted flags for an unattended run are `--netplay-host --netplay-game=<romId> --netplay-auto-ready
--netplay-auto-start` on the host and `--netplay-join=<address> --netplay-auto-ready` on the client.
Auto-ready alone never starts anything; only `--netplay-auto-start` presses Start.

`--netplay-auto-download` presses the lobby's Download for you, once per selection. Without it the
download path cannot be reached unattended — acquiring a ROM needs a button press and the Arch box
takes no remote input. Point the host at a rom the client does **not** have to exercise it:

```
host:   --netplay-host --netplay-game=<romId> --netplay-auto-ready
client: --netplay-join=192.168.1.13 --netplay-auto-ready --netplay-auto-download
```

The pass condition is the client's preparedness reaching `Ready`. Watch for the transition through
`Needs game` *after* `Download complete` — that is the download signal arriving before extraction, and
the recovery to `Ready` only happens because `HandleExtractionFinished` reports again.

## Where the remote log actually is

`-Action logs` reads `.test-state/run.log`, which is the app's stdout. Godot *also* keeps its own
`~/.local/share/godot/app_userdata/romm-frontend/logs/godot.log`, and on startup it renames the previous
session's file to `godot<timestamp>.log` using the **new** session's timestamp. A rotated file therefore
carries a name that looks like the run you just started while holding the run before it, which reads as
a launch that produced impossible output. Prefer `run.log`; if you open the userdata logs, `godot.log`
is the live one.

## Troubleshooting

| Symptom | Cause |
|---|---|
| `no Wayland or X display found` | Nobody is logged in at the test machine's desktop. The session must be active, not just booted. |
| Full 182 MB transfer every push | `rsync` missing on one end. `deploy.ps1` needs it on **both**. |
| `Load key ...: invalid format` | The key was piped into WSL through PowerShell, which re-encodes it. Copy it inside WSL with `tr -d '\r' < /mnt/c/...`. |
| `no working screenshot tool` | Compositor has no supported backend installed. Install `spectacle` (KDE), `gnome-screenshot` (GNOME) or `grim` (wlroots). |
| App on the wrong GPU | See GPU selection above. |

Ad-hoc multi-line `ssh` commands with quotes or parentheses get mangled by PowerShell's
native-argument quoting before `ssh` ever sees them. That is why the real logic lives in
`remote-run.sh`, pushed as a file and invoked by name, rather than in inline command strings.
Add new remote behaviour there, not to `deploy.ps1`'s command strings.
