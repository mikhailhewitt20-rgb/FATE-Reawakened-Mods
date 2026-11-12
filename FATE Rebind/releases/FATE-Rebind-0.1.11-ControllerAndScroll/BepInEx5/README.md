# FATE Rebind 0.1.11

Vanilla input rebinding by MikhailKillsZombies.

## Install

Close the game, replace the earlier Rebind version by importing
FATE_Rebind-0.1.11-Thunderstore.zip into r2modman, and Start modded. Open Controls.
BepInExPack is the declared dependency. No loader or game artwork is bundled.

## Scrolling repair

Wheel deltas are normalized before reaching the scroll list. A wheel notch moves
about one action row, including mice/drivers that report 120 rather than 1.
Fine fractional deltas remain proportional. Scroll inertia is disabled so wheel
movement stops instead of travelling to the end. Scrollbar dragging still works.

## Automatic device views

Keyboard presses or meaningful mouse activity show the existing keyboard/mouse
editor. Controller button presses or deliberate stick movement switch to a
separate controller screen. Connection alone or small stick drift does not switch
views. Capturing/confirming a binding freezes device switching until it finishes.
Disconnecting the gamepad returns to the keyboard/mouse screen.

The controller screen displays FATE's native Xbox illustration, loaded from the
installed game, and a dedicated list of native controller actions. It has its own
navigation and does not depend on remappable gameplay/menu actions for selection.
The chosen action is scrolled into view as the selection moves.

## Controller interaction

- D-pad or left stick up/down: select an action; held input repeats.
- Left/right: choose Primary or Secondary for that action.
- A: rebind the selected input.
- X: clear the selected input. Y: restore both defaults on the selected action.
- LB/RB: previous/next category. View: cycle registered providers.
- B: return to Options when not capturing.
- During capture: tap the control to assign it. Hold B for roughly 0.7 seconds to
  cancel. A quick tap of B can therefore still be assigned. Escape also cancels.
- Controller capture waits about one second to distinguish a tap from hold-cancel.
- During a conflict: up/down chooses Keep both, Swap or Cancel; A confirms, B cancels.

Both screens keep primary/secondary assignments independently. Controller input
never overwrites keyboard/mouse defaults. Controller mappings include the game's
real left-stick movement; dormant keyboard WASD walking remains hidden. No WASD
movement mod or movement implementation is supplied.

The Windows/hardware cursor remains under FATE's control. There is no F10 window.

## Keyboard/mouse interaction

Click an input to capture a key/mouse control. Escape cancels. The small x removes
that input; Reset restores both on the row. Swap, Keep both and Cancel handle
conflicts. Categories, search and provider lists retain the previous behaviour.

## Tests to run in game

1. Scroll one notch: the list should move a little rather than jump to the bottom.
2. Press a gamepad button: confirm the Xbox diagram and controller list appear.
3. Navigate beyond the visible rows using D-pad/LS; verify the focus remains visible.
4. Rebind a secondary controller input, test it in gameplay, restart and confirm
   persistence. Check primary, clear/reset, conflict controls and hold-B cancel.
5. Move/click the mouse or type a key: confirm the keyboard/mouse screen returns.
6. Verify both device views preserve the other device's assignments.

## Storage and verification

Saved bindings retain the existing compatible location:
BepInEx/config/ZombieGhost.Rebind/bindings.xml. Native input files are not edited.
The startup-safe runtime driver and scene recovery are retained.

Offline tests cover normalized wheel values, 32 keyboard/mouse action pairs,
24 independent controller pairs, stable IDs/idempotent expansion, Xbox labels,
no keyboard WASD exposure, storage, API and lifecycle contracts. Both builds pass.
The controller illustration's exact sprite name was confirmed in the local game
assets. Actual controller hardware, auto-switching, capture/navigation and the new
view still require user testing. No game was launched or controlled by the agent.

Uninstall through r2modman with the game closed; retain the config folder if you
want to keep assignments. Other mods require explicit Rebind API registration.
