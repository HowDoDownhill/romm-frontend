# UI Panel Refactor

Working plan for replacing the ad-hoc panel/popup construction with a single `UiPanel` node type.
Phases land as separate commits on `main`; the app must launch and be verified between each.

Rationale that outlives this document belongs in `docs/DESIGN-NOTES.md`, not here. Delete this file
once P4 is merged.

---

## Why

Three incompatible idioms exist for the same panel — `PanelContainer` + mica material +
`StyleBoxFlat` radius 12 + `MarginContainer` + content:

| Idiom | Where |
|---|---|
| Scene-authored | `main_scene.tscn`: StartMenu, DownloadProgressPopup, ChangelogContainer, SettingsMenuContainer |
| Dedicated class | `SystemJumpPopup`, `ReleasePickerPopup` (near-verbatim duplicates of each other) |
| Inline in `_Ready` | `fuzzySearchPopup`, built in `MainScene._Ready` |

Consequences, measured:

- 8 `mica_panel.tres` load sites, 9 hand-written corner-radius recipes, `StyleEntryButton`
  duplicated across two popups, borders retro-fitted by a whole-scene `MicaBorder.AttachToAll` sweep.
- 69 `.Visible =` writes across the UI scripts, 25 of them for the start menu and bios selector alone.
- 53 `[Export]`s on `MainScene`, each a rename landmine per `CLAUDE.md`.
- `MainScene._Input` is a ~450-line manually-ordered priority chain.

### The animation problem specifically

`Visible` **is** the open/closed state, so an animation has nowhere to live and had to be bolted on
from outside. `PopupAnimator` is a static side-table keyed by instance id with a parallel `hiding`
set, existing purely because "visible but closing" is not representable. Every caller then
compensates: `IsClosing` on `SystemJumpPopup`, `IsHiding` checks in `_Process`, and
`MainSceneSectionHandler.IsTransitioning` solving the identical problem one level up.

Better animation control requires open/closed to become a real state first. That is P0.

---

## Target: `UiPanel`

A `[GlobalClass]` `Control` subclass, usable both as a node dropped into a `.tscn` and as something
constructed in code — which is what collapses the three idioms into one.

```csharp
[GlobalClass]
public partial class UiPanel : Control
{
    public enum PanelState { Closed, Opening, Open, Closing }
    public enum PanelTransition { None, Fade, ScaleFade, SlideFromBottom, SlideFromTop }

    [Export] public PanelTransition Transition;
    [Export] public float OpenDuration;
    [Export] public float CloseDuration;
    [Export] public float RestingScale;
    [Export] public bool Modal;
    [Export] public bool UseMica;
    [Export] public int CornerRadius;
    [Export] public float BackdropDim;

    public PanelState State { get; }
    public bool IsOpen  => State is PanelState.Open or PanelState.Opening;
    public bool IsBusy  => State is PanelState.Opening or PanelState.Closing;

    [Signal] public delegate void OpenedEventHandler();
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void AboutToCloseEventHandler();

    public void Open();
    public void Close();
    public void Toggle();

    protected virtual void OnOpened();
    protected virtual void OnClosed();
    public virtual bool HandleInput(InputEvent inputEvent);
}
```

### What it absorbs

| Concern | Today | On `UiPanel` |
|---|---|---|
| State | `Visible` + static side-table | `PanelState`, `Open`/`Close`/`Toggle`, three signals |
| Animation | `PopupAnimator`, one hardcoded style | Exported transition/duration/curve per panel, `virtual` for bespoke motion |
| Styling | 8 mica loads, 9 radius recipes, border sweep | `UseMica` / `CornerRadius` / `BackdropDim`; builds its own border |
| Focus | `gameList?.GrabFocus()` in ~10 places | Captures focus owner on open, restores on close |
| Input | MainScene priority chain | `Modal` + `UiPanelStack` routing to topmost open panel |

Deleted outright: `PopupAnimator`, `MicaBorder`, `SystemJumpPopup.IsClosing`,
`PopupAnimator.IsHiding`, `MainSceneSectionHandler.IsTransitioning`.

### Constraints carried forward from DESIGN-NOTES

These are load-bearing and a naive `UiPanel` would reintroduce all three:

1. Panels must **not** be `TopLevel`. Staying in the normal draw flow, added last, is what lets Godot
   auto-copy the back buffer for the mica shader. `TopLevel` plus a manual `BackBufferCopy` did not
   feed the screen texture under `gl_compatibility`.
2. The scale target must never be the panel root when a backdrop is present — scaling the root scales
   the backdrop and pulls its edges off screen. `UiPanel` owns its backdrop, so it can pick the
   content root itself rather than relying on callers to pass one.
3. Content-sized panels have not been laid out at their new size when open is called, so the pivot
   must be set once from current size and again after the layout pass.

Keep the mica `StyleBoxFlat` opaque — it defines only the rounded shape; the shader replaces the fill.

---

## Phases

Motion stays pixel-identical to today throughout (Cubic/Out, 0.18s open, 0.12s close, 0.96 resting
scale). Any visual change during these phases is a bug, not a decision. Retuning happens afterwards
on a stable base.

### P0 — `UiPanel` base + code-built popups

Port `SystemJumpPopup` and `ReleasePickerPopup` first: self-contained, already own their input via
`HandleInput`, already carry a closing flag. Delete `PopupAnimator`.

Verify: frosted glass still blurs on both popups; a popup dismissed and immediately re-triggered
stays open rather than finishing its fade-out.

### P1 — `UiPanelStack` + input routing

Stack tracks open modal panels; routes input to the topmost before `MainScene` sees it. Removes
MainScene's need to infer closure from `if (!releasePickerPopup.Visible)`.

Preserve: footer shortcuts stay controller-only; manual `Pressed` emission still bypasses Godot's
disabled gate, so disabled re-checks must survive the move.

### P2 — scene-authored popups

StartMenu, BiosSelector, Changelog, DownloadProgress. Roughly 20 `[Export]`s leave `MainScene`,
along with the matching `MainScenePopupHandler` methods.

Every `[Export]` removal must be matched in the `.tscn` `node_paths=PackedStringArray(...)` in the
same change, then `grep scenes/` to confirm nothing references the old name. A missed one compiles
cleanly and nulls at runtime.

### P3 — sections

GameList / Downloads / Settings become non-modal `UiPanel`s with a section-enter transition;
`MainSceneSectionHandler` reduces to a router over `CurrentSection`.

Preserve: logical state flips up front so the header label describes the destination; focus applies
only once the incoming section has arrived; an interrupted transition snaps every section to a known
state rather than stranding one at half alpha.

### P4 — scene split

Each panel and section to its own `.tscn` under `scenes/ui/panels/` and `scenes/sections/`, instanced
from a thin shell. `main_scene.tscn` drops from 849 lines to roughly 150. This removes the remaining
structural walks in C# — `startMenuContainer.GetParent()?.GetParent()` and
`gameList.GetParent().GetParent()`.

---

## Deferred

**P5 — theme consolidation.** A single `Theme` resource with `ThemeTypeVariation`s replacing the
per-popup `AddThemeStyleboxOverride` recipes and the duplicated `StyleEntryButton`. Out of the agreed
scope; revisit after P4.
