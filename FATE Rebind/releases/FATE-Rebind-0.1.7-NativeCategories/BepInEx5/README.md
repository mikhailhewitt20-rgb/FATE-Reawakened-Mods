# FATE Rebind 0.1.7

A modular input editor by MikhailKillsZombies for FATE: Reawakened.

## Install and test

Close the game and replace the earlier Rebind version in r2modman by importing
FATE_Rebind-0.1.7-Thunderstore.zip. Keep BepInExPack enabled. Start modded.
Open Settings / Options, then Controls. The reference diagram is replaced by a
scrollable editor using the game's font and button artwork. F10 retains the
working standalone window as a fallback. The screen title is simply FATE Rebind.

- Choose the game or a mod in the provider list.
- Select a category or All categories; providers, categories and actions scroll.
- Filter keyboard/mouse or controller inputs and search by action/category/input.
- Click a binding and press a new input. Escape cancels capture; F10 is reserved.
- Remove unbinds the row. Reset restores it. Reset group requires confirmation.
- If one compatible action conflicts, Swap exchanges the assignments. Keep both
  and Cancel are also available.

Native action names are clearer: Camera left/right, Zoom in/out, Move up/down,
Hotbar 1–4 and Select spell 1–6. Categories are Camera, Movement, Combat & spells,
Items & interaction, Menus & interface and Party & pet. The internal map name is
no longer repeated on every row. Mod-provided labels/categories remain supported.

## Test checklist

1. Confirm Controls has scrollbars and selectable category/provider lists.
2. Scroll down and back up; search for Camera and check the direction labels.
3. Rebind Inventory, test it in gameplay, restart and check persistence.
4. Test cancelling capture, Remove, Reset, a conflict Swap, and leaving/reopening
   the Controls tab. The native menu should remain clickable after rebinding.
5. F10 remains available if the native editor does not attach.

The confirmed 0.1.6 runtime driver is retained: UI/state survive startup cleanup
of the original plugin object and recover on scene changes. No world pause is
added; the existing Settings screen retains its normal pause behaviour.

Settings remain in BepInEx/config/ZombieGhost.Rebind/bindings.xml for compatibility
with previous builds. Existing assignments are retained; only labels/categories
change. No game input assets or assemblies are edited. The previous settings file
is retained as bindings.xml.bak.

## Mod integration

Unity InputAction assets are discovered automatically. Mods may register labelled
providers/actions through RebindApi. Raw hard-coded polling requires the owning
mod to use that API or an adapter. See API.md and ModControlsExample.cs.
No co-op DLL or other creator's mod is required or bundled.

## Verification and removal

Both loader builds and offline naming, persistence, API and lifecycle checks pass.
The new native layout and clicks still require user testing. No gameplay was
launched or controlled, and the ZIP has not been automatically installed.
Uninstall through the manager with the game closed. Keep the config folder if
you want saved assignments preserved.
