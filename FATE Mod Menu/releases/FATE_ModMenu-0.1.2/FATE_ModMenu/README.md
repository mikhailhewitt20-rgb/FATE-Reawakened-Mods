# FATE Mod Menu

**One place for every mod's settings.** Adds a **Mod Settings** button to FATE: Reawakened's pause menu and shows the options each installed mod registers, in a screen styled to match the game.

By **MikhailKillsZombies**. Standalone Windows BepInEx 5 plugin.

## What it does

- A **Mod Settings** button under **Options** in the pause menu.
- **F10** opens the same screen anywhere in a loaded game and pauses it, so the screen can be checked without the pause menu.
- One scrollable list of every registered mod, grouped by mod and category, with a **Defaults** button per section.
- Check marks, numeric sliders, enum-style choices, command buttons and short notes.
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
