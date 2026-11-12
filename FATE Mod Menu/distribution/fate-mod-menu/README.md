# FATE Mod Menu

**One place for every mod's settings.** Adds a **Mod Settings** button to FATE: Reawakened's pause menu and shows the options each installed mod registers, in a screen styled to match the game.

By **MikhailKillsZombies**. Standalone Windows BepInEx 5 plugin.

## What it does

- A **Mod Settings** button under **Options** in the pause menu.
- **F10** opens the same screen anywhere in a loaded game, bringing up the pause menu first so the cursor and pause behave natively; F10 again closes it.
- **Two steps**: the first screen lists the mods that registered settings, and choosing one opens that mod's own screen. Escape or **Back** goes up one step.
- Per-mod screens with a **Defaults** button per category, holding check marks, draggable sliders, enum-style choices, command buttons, colour pickers and short notes.
- **Sliders** can be dragged or clicked anywhere on the track, with arrows for a keyboard or controller.
- **Colour rows** show a swatch and the hex value, with one slider per channel. Click the hex chip to type or paste a colour code, and **Copy** puts the current one on the clipboard.
- **Text rows** are single-line values such as a display name. Click the chip, type or paste, press **Enter** to apply and **Escape** to cancel.
- **Previews** let a mod draw a picture of what its settings do, such as the aiming reticle, in a pane pinned beside the list so it stays in view while you scroll.
- Mouse and keyboard, or controller: the native up/down navigation includes the new button and the list rows.
- Escape or gamepad B closes the settings screen and leaves the pause menu open.

Key bindings are **not** duplicated here. Remapping stays in **Options > Controls** through [Rebinds Mod](https://thunderstore.io/c/fate-reawakened/p/MikhailKillsZombies/Rebinds_Mod/).

## It grows with your mod list

The menu is moduleable. A mod registers a module and the options it owns; the menu builds the rows from that registry, so new mods appear without the menu being updated. Nothing is hard-coded per mod. Mod authors: see `API.md` and `ModSettingsExample.cs` inside `BepInEx/plugins/FateModMenu`.

A mod that does not register anything does not appear, and installing an arbitrary DLL does not create settings by itself.

## Install

1. Install **BepInEx 5 Windows x64** (the FATE BepInExPack uses 5.4.23.5).
2. Install **Rebinds Mod** as well — mod managers pull it in with this package, and the two are developed together.
3. Close the game, extract the archive, and merge its **BepInEx** folder beside `FateReawakened.exe`. The plugin belongs at `BepInEx/plugins/FateModMenu/ZombieGhost.ModMenu.dll`.
4. Launch modded, load a character, then press **F10** or open the pause menu and choose **Mod Settings**.

## Removal

Uninstall with the game closed and remove only `BepInEx/plugins/FateModMenu`. The menu stores no values of its own, so no config is left behind and no game save is touched.
