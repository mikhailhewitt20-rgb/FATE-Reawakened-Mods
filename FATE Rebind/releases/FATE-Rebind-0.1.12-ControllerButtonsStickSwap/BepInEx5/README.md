# FATE Rebind 0.1.12

Vanilla input rebinding by MikhailKillsZombies.

## Install

Close the game and replace the previous Rebind version by importing
FATE_Rebind-0.1.12-Thunderstore.zip into r2modman. Start modded and open Controls.
BepInExPack remains the dependency. Wheel scrolling and automatic device switching
retain the behaviour confirmed in 0.1.11.

## Controller repair

The previous controller screen could fail to refresh because a UI object name
contained a slash (Category [LB / RB]). Unity interpreted it as a hierarchy path.
Creation and lookup now use stable slash-free IDs instead of displayed labels.
That repair restores the action list, focus/navigation and controller capture flow.

## Stick option and button bindings

The first controller row is Swap sticks. It swaps movement and camera/aim axes
between left and right sticks. Stick clicks (L3/R3) stay independently rebindable
buttons. The option affects controller axes only, not keyboard/mouse mappings or
the physical left-stick/D-pad controls used to navigate this menu. It is saved
with your preferences and restored when native input assets load after a restart.

The other controller rows are button mappings: face buttons, shoulders, triggers,
D-pad directions, stick clicks and Menu/View. Raw stick-axis rebinding rows are
removed. Primary and secondary button slots remain available.

## Controller commands

- D-pad/LS up/down: select a row. Selected rows scroll into view.
- On Swap sticks: A toggles, left/right chooses off/on, Y restores the default.
- On a button row: left/right chooses Primary/Secondary; A starts capture.
- Tap the new button and wait about one second. Hold B for about 0.7 seconds to
  cancel capture; a quick B tap can still be assigned. Escape also cancels.
- X clears the selected button; Y resets both buttons on the selected action.
- LB/RB changes category, View changes registered provider, B returns to Options.
- In a conflict dialog: up/down chooses, A confirms, B cancels.

Keyboard/mouse interaction still uses the existing editor. Moving/clicking the
mouse or typing switches back when no capture/conflict is in progress. F10 has no
menu. Cursor visibility and locking remain owned by the game.

## Tests to run

1. Navigate the controller screen and select a button row using D-pad/LS.
2. Assign a secondary controller button; verify it in gameplay and after restart.
3. Check conflict confirmation, clear/reset, hold-B cancel and a quick B assignment.
4. Toggle Swap sticks and verify movement/camera exchange. Check L3/R3 remain
   separate button assignments, then restore default sticks.
5. Confirm keyboard/mouse assignments and normal wheel scrolling remain intact.

## Storage, compatibility and verification

Settings remain at BepInEx/config/ZombieGhost.Rebind/bindings.xml. The stick option
has its own preference key; no native game input file is edited. Existing saved
button assignments are retained. Old hidden axis preferences remain on disk.
There is no WASD movement implementation in this mod.

Offline tests cover the slash-name regression, button-only controller validation,
reversible movement/camera path swapping, independent stick-click buttons, saved
preferences, native action plans, scrolling, storage, API and startup lifecycle.
Both loader variants compile. Actual controller input, the refresh repair and
stick behaviour still need user testing. No game was launched or controlled.

Uninstall through r2modman with the game closed; keep the config folder to preserve
assignments. Other mods require explicit API registration; no other mod is bundled.
