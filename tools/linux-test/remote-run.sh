#!/usr/bin/env bash
# Runs on the Arch test machine. Pushed alongside the build by deploy.ps1.
# Usage: remote-run.sh <run|stop|status|logs|shot> [args...]

set -uo pipefail

AppDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
StateDir="$AppDir/.test-state"
LogFile="$StateDir/run.log"
PidFile="$StateDir/run.pid"
Executable="$AppDir/romm-frontend.x86_64"

mkdir -p "$StateDir"

detect_session_desktop() {
    local sessionId sessionType desktop
    for sessionId in $(loginctl list-sessions --no-legend 2>/dev/null | awk '{print $1}'); do
        sessionType="$(loginctl show-session "$sessionId" -p Type --value 2>/dev/null)"
        case "$sessionType" in
            wayland|x11)
                desktop="$(loginctl show-session "$sessionId" -p Desktop --value 2>/dev/null)"
                if [ -n "$desktop" ]; then
                    echo "$desktop"
                    return 0
                fi
                ;;
        esac
    done
    return 1
}

# An SSH session inherits none of the graphical session's environment, so the
# Wayland socket, dbus bus and XWayland display have to be rediscovered here.
load_graphical_session_environment() {
    export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"

    if [ -z "${WAYLAND_DISPLAY:-}" ]; then
        for socket in "$XDG_RUNTIME_DIR"/wayland-*; do
            case "$socket" in *.lock) continue ;; esac
            if [ -S "$socket" ]; then
                export WAYLAND_DISPLAY="$(basename "$socket")"
                break
            fi
        done
    fi

    export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=$XDG_RUNTIME_DIR/bus}"
    export XDG_SESSION_TYPE="${XDG_SESSION_TYPE:-wayland}"

    if [ -z "${XDG_CURRENT_DESKTOP:-}" ]; then
        export XDG_CURRENT_DESKTOP="$(detect_session_desktop)"
    fi

    if [ -z "${DISPLAY:-}" ]; then
        for socket in /tmp/.X11-unix/X*; do
            if [ -S "$socket" ]; then
                export DISPLAY=":${socket##*/X}"
                break
            fi
        done
    fi

    # Without this XWayland rejects the connection ("Authorization required, but no
    # authorization protocol specified") and Godot's X11 driver fails before it can
    # fall back to Wayland.
    if [ -z "${XAUTHORITY:-}" ]; then
        for candidate in "$HOME/.Xauthority" "$XDG_RUNTIME_DIR/xauth_"*; do
            if [ -f "$candidate" ]; then
                export XAUTHORITY="$candidate"
                break
            fi
        done
    fi
}

# On a hybrid laptop the compositor hands out the integrated GPU by default, so the
# app renders on the iGPU and reports GL Compatibility. These are the PRIME offload
# variables that move it to the discrete card.
select_discrete_gpu() {
    export __NV_PRIME_RENDER_OFFLOAD=1
    export __GLX_VENDOR_LIBRARY_NAME=nvidia
    export __VK_LAYER_NV_optimus=NVIDIA_only
    export DRI_PRIME=1
    if [ -f /usr/share/vulkan/icd.d/nvidia_icd.json ]; then
        export VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/nvidia_icd.json
    fi
}

select_integrated_gpu() {
    export DRI_PRIME=0
    unset __NV_PRIME_RENDER_OFFLOAD __GLX_VENDOR_LIBRARY_NAME __VK_LAYER_NV_optimus VK_ICD_FILENAMES
}

report_session_environment() {
    echo "XDG_RUNTIME_DIR=${XDG_RUNTIME_DIR:-<unset>}"
    echo "WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-<unset>}"
    echo "DISPLAY=${DISPLAY:-<unset>}"
    echo "XDG_CURRENT_DESKTOP=${XDG_CURRENT_DESKTOP:-<unset>}"
}

running_pid() {
    [ -f "$PidFile" ] || return 1
    local pid
    pid="$(cat "$PidFile" 2>/dev/null)"
    [ -n "$pid" ] || return 1
    kill -0 "$pid" 2>/dev/null || return 1
    echo "$pid"
}

stop_app() {
    local pid
    if pid="$(running_pid)"; then
        kill "$pid" 2>/dev/null
        for _ in $(seq 1 20); do
            kill -0 "$pid" 2>/dev/null || break
            sleep 0.25
        done
        kill -9 "$pid" 2>/dev/null
        echo "stopped pid $pid"
    else
        pkill -f "$Executable" 2>/dev/null && echo "stopped stray processes" || echo "not running"
    fi
    rm -f "$PidFile"
}

start_app() {
    local gpu="default"
    while [ $# -gt 0 ]; do
        case "$1" in
            --gpu) gpu="$2"; shift 2 ;;
            --)    shift; break ;;
            *)     break ;;
        esac
    done

    if [ ! -x "$Executable" ]; then
        echo "executable missing or not executable: $Executable" >&2
        return 1
    fi

    stop_app >/dev/null 2>&1
    load_graphical_session_environment

    case "$gpu" in
        discrete)   select_discrete_gpu ;;
        integrated) select_integrated_gpu ;;
    esac

    if [ -z "${WAYLAND_DISPLAY:-}" ] && [ -z "${DISPLAY:-}" ]; then
        echo "no Wayland or X display found - is a desktop session logged in?" >&2
        report_session_environment >&2
        return 1
    fi

    : > "$LogFile"
    {
        echo "=== launch $(date --iso-8601=seconds) ==="
        report_session_environment
        echo "gpu: $gpu"
        echo "VK_ICD_FILENAMES=${VK_ICD_FILENAMES:-<unset>}"
        echo "args: $*"
        echo "=== app output ==="
    } >> "$LogFile"

    # An exported build does not flush stdout on print, and stdout to a file is block
    # buffered, so GD.Print output only lands in the log minutes late or on exit.
    setsid stdbuf -oL -eL "$Executable" "$@" >> "$LogFile" 2>&1 &
    echo $! > "$PidFile"

    sleep 3

    # The app re-execs itself onto the discrete GPU and the original process exits, so
    # the pid recorded above is not necessarily the surviving one.
    if ! running_pid >/dev/null; then
        local relaunchedPid
        relaunchedPid="$(pgrep -f "$Executable" | head -1)"

        if [ -n "$relaunchedPid" ]; then
            echo "$relaunchedPid" > "$PidFile"
            echo "started pid $relaunchedPid (relaunched itself)"
            return 0
        fi

        echo "process exited during startup" >&2
        return 1
    fi

    echo "started pid $(cat "$PidFile")"
}

try_screenshot_tool() {
    local tool="$1" output="$2"
    command -v "$tool" >/dev/null 2>&1 || return 1
    rm -f "$output"

    case "$tool" in
        grim)             grim "$output" >/dev/null 2>&1 ;;
        grimblast)        grimblast save screen "$output" >/dev/null 2>&1 ;;
        wayshot)          wayshot -f "$output" >/dev/null 2>&1 ;;
        spectacle)        spectacle -b -n -f -o "$output" >/dev/null 2>&1; wait_for_file "$output" ;;
        gnome-screenshot) gnome-screenshot -f "$output" >/dev/null 2>&1 ;;
        maim)             maim "$output" >/dev/null 2>&1 ;;
        import)           import -window root "$output" >/dev/null 2>&1 ;;
    esac

    [ -s "$output" ]
}

wait_for_file() {
    for _ in $(seq 1 20); do
        [ -s "$1" ] && return 0
        sleep 0.25
    done
    return 1
}

# The preferred tool is compositor-specific: grim needs wlr-screencopy, which KWin
# and Mutter do not implement, so it is present-but-broken on KDE and GNOME. Each
# candidate is actually run and the result checked rather than trusting `command -v`.
screenshot_tool_preference() {
    case "${XDG_CURRENT_DESKTOP:-}" in
        *KDE*|*kde*)       echo "spectacle grim grimblast wayshot maim import" ;;
        *GNOME*|*gnome*)   echo "gnome-screenshot grim wayshot maim import" ;;
        *)                 echo "grim grimblast wayshot spectacle gnome-screenshot maim import" ;;
    esac
}

capture_screenshot() {
    local output="${1:-$StateDir/screenshot.png}"
    load_graphical_session_environment

    local tool
    for tool in $(screenshot_tool_preference); do
        if try_screenshot_tool "$tool" "$output"; then
            echo "$output"
            return 0
        fi
    done

    echo "no working screenshot tool - tried: $(screenshot_tool_preference)" >&2
    return 1
}

# Rewrites only the given keys inside [RomM], leaving DeviceId, [UI], [Input] and
# every other section on the test machine untouched.
merge_login_config() {
    local valuesFile="$1"
    local config="$AppDir/config.cfg"

    if [ ! -f "$config" ]; then
        printf '[RomM]\n\n' > "$config"
    fi

    awk -v valuesFile="$valuesFile" '
        function emitMissingKeys(   key) {
            for (key in newValue) {
                if (!(key in seen)) {
                    print key "=" newValue[key]
                    seen[key] = 1
                }
            }
        }
        BEGIN {
            while ((getline line < valuesFile) > 0) {
                separator = index(line, "=")
                if (separator > 0) {
                    newValue[substr(line, 1, separator - 1)] = substr(line, separator + 1)
                }
            }
        }
        /^\[/ {
            if (inRomMSection) emitMissingKeys()
            inRomMSection = ($0 == "[RomM]")
            print
            next
        }
        {
            if (inRomMSection) {
                separator = index($0, "=")
                if (separator > 0) {
                    key = substr($0, 1, separator - 1)
                    gsub(/^[ \t]+|[ \t]+$/, "", key)
                    if (key in newValue) {
                        print key "=" newValue[key]
                        seen[key] = 1
                        next
                    }
                }
            }
            print
        }
        END { if (inRomMSection) emitMissingKeys() }
    ' "$config" > "$config.merged" || return 1

    mv "$config.merged" "$config"
    rm -f "$valuesFile"
    echo "merged login keys into config.cfg"
}

Command="${1:-status}"
shift 2>/dev/null || true

case "$Command" in
    run)    start_app "$@" ;;
    stop)   stop_app ;;
    status)
        if pid="$(running_pid)"; then echo "running pid $pid"; else echo "not running"; fi
        load_graphical_session_environment
        report_session_environment
        ;;
    logs)   tail -n "${1:-200}" "$LogFile" 2>/dev/null || echo "no log yet" ;;
    merge-login) merge_login_config "$@" ;;
    shot)   capture_screenshot "$@" ;;
    env)    load_graphical_session_environment; report_session_environment ;;
    *)      echo "usage: remote-run.sh <run|stop|status|logs|shot|env|merge-login>" >&2; exit 2 ;;
esac
