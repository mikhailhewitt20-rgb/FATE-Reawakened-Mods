# FATE Rebind 0.1.13

Input customization by MikhailKillsZombies.

## Install

Close the game and replace the previous Rebind version in r2modman with
FATE_Rebind-0.1.13-Thunderstore.zip. Keep BepInExPack enabled and Start modded.
Open Controls. Input still switches automatically between keyboard/mouse and
controller views, with no changes to the confirmed wheel-scroll behaviour.

## Cleaner screens

Both views now show their actions in one scrollable list. Category/provider pages
and the large controller reference diagram are removed; section headings group
all available actions in place. Keyboard/mouse retains global search.

Swap sticks is a native-style checkbox in the controller list. Controller binding
cells display the game's button pictograms, with + for an empty slot. Primary and
secondary inputs are separate selectable cells. Compact icon hints replace the
long repeated instructions. Icons/checkmark artwork are loaded from the game,
not redistributed. An unsupported/missing icon falls back to a short input label.

The Back buttons are removed from both tabs. Use the game's normal tabs; LB/RB
switch tabs with a controller. B (or Escape) closes the whole pause menu outside
capture/confirmation. There is no F10 window.

## Complete controller button assignments

Every supported vanilla button action now has two controller slots, including
those without a factory controller assignment. This makes 30 controller button
actions editable, 10 more than the previous factory-only list. Keyboard/mouse
keeps its 32 logical action rows. Gamepad-only stick axes remain under Swap sticks
and do not expose dormant keyboard WASD walking.

Existing assignments keep their stable IDs. New unassigned controller slots use
the game's Gamepad binding group and stable per-action IDs. Mods must explicitly
register live actions; no absent mod or movement feature is simulated.

## Controller interaction

- D-pad/left stick up/down selects rows. Left/right chooses primary/secondary.
- A toggles the checkbox or starts capture on the selected binding cell.
- Tap the new button, then wait about one second. Hold B for about 0.7 seconds to
  cancel capture; a short B tap remains assignable. Escape also cancels.
- X clears the selected binding. Y restores both defaults on the action.
- Right stick up/down scrolls smoothly. Focus follows the visible list so A never
  edits an action left off-screen by scrolling. This remains physical right-stick
  scrolling even when gameplay sticks are swapped.
- LB/RB switches vanilla tabs. B closes the pause menu outside capture/conflicts.
- In a conflict dialog, D-pad chooses Keep both, Swap or Cancel; A confirms and B
  cancels. Icons in the footer show the commands for the current state.

## Test checklist

1. Check the single list, checkbox and actual button icons; scroll with RS.
2. Use LB/RB to move to Journal/Options and back without a Back button.
3. Assign a controller key to a previously keyboard-only action such as Spellbook
   or Stats & skills. Verify it works in gameplay and survives restarting.
4. Test primary/secondary capture, clear, reset, conflict handling and tap/hold B.
5. Test Swap sticks and confirm keyboard/mouse assignments remain unchanged.

## Storage and verification

Preferences remain at BepInEx/config/ZombieGhost.Rebind/bindings.xml. Stick swapping
has a separate key; no native game input files or assemblies are edited. The
startup-safe runtime driver and native cursor ownership are retained.

Both loader builds and offline tests pass: persistence, stable/idempotent binding
plans, 32 keyboard and 30 controller action pairs, native icon fields/checkbox,
right-stick scroll bounds, native tabs, cursor ownership and lifecycle contracts.
Gameplay input, the new layout/icons and hardware navigation still require user
testing. No game was launched or controlled and this ZIP was not auto-installed.

Uninstall through r2modman with the game closed. Keep the config folder to retain
your assignments. API documentation and a generic integration example are included.
