# FATE Rebind 0.1.9

A vanilla keyboard/mouse binding editor by MikhailKillsZombies.

## Install

Close the game and replace the earlier Rebind version in r2modman by importing
FATE_Rebind-0.1.9-Thunderstore.zip. Keep BepInExPack enabled, Start modded, then
open Settings / Controls. The F10 shortcut and standalone window are removed.
F10 can now be assigned as a normal binding.

## Changes

- Compact vanilla quest-row buttons replace the ornate pause-menu button design.
- Each vanilla action has Primary and Secondary inputs on the same row.
- Click either input to capture a key or mouse control. The × beside each input
  removes that binding; Reset restores both inputs on that row.
- Swap, Keep both and Cancel are offered when a new input conflicts with another
  action. Category/provider lists and the action list remain scrollable.
- Only usable vanilla controls are listed by default. The unused Persistent/Move
  WASD vector and internal UI navigation/click actions are excluded.
- Camera WASD keys remain camera controls, as they are in the vanilla game.
- Secondary bindings preserve signed camera-axis composites and use stable IDs.
  Existing paired defaults, such as mouse-wheel/W-S camera zoom, share one row.

No movement mod or movement implementation is included. A future movement adapter
must depend on BOTH Rebind and the actual movement mod and register its live
controls only when that mod is present. Rebind alone does not enable WASD walking.
Other mods' controls are shown only when explicitly registered through the API;
the internal DefaultInputActions asset is no longer exposed as a mod provider.

## Test

1. Open Controls and confirm there is no WASD walking or DefaultInputActions group.
2. Set a secondary key for Inventory and verify both keys work in gameplay.
3. Restart and confirm the secondary key survives. Clear it with ×, test Reset,
   and test a conflict swap/cancel.
4. Set a secondary Camera left/right key and verify the direction is correct.
5. Press F10 outside capture: it should not open a menu. Try assigning F10.

The new secondary bindings are runtime additions to native InputActions, not game
asset edits. Existing assignments remain compatible at
BepInEx/config/ZombieGhost.Rebind/bindings.xml. Old hidden-action preferences are
retained on disk without applying them to absent/unsupported features.

## Integration

See API.md and ModControlsExample.cs. RegisterButton-created actions receive a
secondary slot automatically. For externally owned actions, the owning mod or
adapter should supply two appropriate bindings before registration; Rebind does
not restructure arbitrary third-party action/composite layouts.

## Verification and removal

Both loader variants build. Offline planning tests cover all 32 editable vanilla
logical rows, two inputs per row, stable IDs, repeated scene/discovery passes,
vanilla-only policy and camera axis composition. Actual gameplay, secondary keys
and the compact button appearance still require user testing. No game was launched
or controlled and this ZIP was not automatically installed.

Uninstall through r2modman with the game closed. Keep the config folder to retain
your saved assignments. The package includes neither a loader nor another mod.
