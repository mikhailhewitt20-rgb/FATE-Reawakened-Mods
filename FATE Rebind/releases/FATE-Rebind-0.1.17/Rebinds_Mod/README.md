# FATE Rebind

**Your controls, your adventure.** Customize keyboard, mouse and controller actions in FATE: Reawakened's Controls tab, with a menu styled to match the game.

By **MikhailKillsZombies**. Standalone Windows BepInEx 5 plugin; co-op is not required.

## Downloads and support

- [Thunderstore / r2modman](https://thunderstore.io/c/fate-reawakened/p/MikhailKillsZombies/Rebinds_Mod/)
- [Nexus Mods](https://www.nexusmods.com/fatereawakened/mods/15)
- [FATE: Reawakened Modding Discord](https://discord.gg/pMdcfpf9r)

Use **Rebinds Mod by MikhailKillsZombies** on Thunderstore. Old ZombieGhosts listings are deprecated. Remove an old Rebind entry before installing the maintained package; keep your config to retain settings.

## Features

- Primary and secondary bindings, clear/remove, reset, and conflict handling with keep-both or swap options.
- Searchable, categorized keyboard/mouse actions in a scrollable list.
- Automatic keyboard/mouse and controller view switching.
- Controller navigation and native button icons, right-stick scrolling and LB/RB tab switching.
- Swap movement/camera sticks and invert camera left/right and up/down independently. The vertical camera axis controls zoom in FATE.
- **Use scroll wheel for zoom**, on by default. Zoom bindings are hidden while enabled; switch it off to reveal zoom-button assignments.
- Automatic saving and support for participating mods that register live actions.

This does not add WASD movement or co-op. Those need their own mods. Compatible mods must register live actions to appear; installing an arbitrary DLL does not automatically create bindings.

## Install with a mod manager

1. Select FATE: Reawakened in r2modman or Thunderstore Mod Manager.
2. Install **Rebinds Mod by MikhailKillsZombies**, including BepInExPack.
3. Launch **modded**, load a character and open **Controls**.

For updates, close the game and update this same package in the manager. Local ZIP imports are useful for testing but do not receive published updates automatically. Do not enable multiple Rebind copies together.

## Manual installation from Nexus

Install **BepInEx 5 Windows x64** first. The FATE community's [BepInExPack](https://thunderstore.io/c/fate-reawakened/p/BepInEx/BepInExPack/) uses version 5.4.23.5. The loader is not bundled; this package is not for BepInEx 6.

Close the game, extract this archive, and merge its **BepInEx** folder beside **FateReawakened.exe**. Check the plugin is at `BepInEx/plugins/FateRebind/ZombieGhost.Rebind.dll`. Launch with BepInEx active, load a character, and open **Controls**. For manual updates, replace the plugin with the game closed and keep your config.

## Controller commands

D-pad/left stick selects rows. Left/right chooses a binding slot or sets a checkbox. A assigns/toggles, X clears, Y restores defaults. Tap a new button during capture; hold B to cancel. Right stick scrolls, LB/RB switches native tabs, and B closes the pause menu outside capture.

There is no F10 window or separate Back button. Use the game's Controls tab.

## Settings and removal

Preferences save to `BepInEx/config/ZombieGhost.Rebind/bindings.xml`. The historical internal filename remains for compatibility; the author and maintained publisher are **MikhailKillsZombies**.

Uninstall through your manager with the game closed. For a manual install, remove only `BepInEx/plugins/FateRebind`. Keep the config to retain assignments, or remove that mod's config folder to start fresh. Game saves are not modified.

## Release and credits

Version **0.1.17** updates public documentation, supplied artwork and download links. Gameplay behavior is unchanged from **0.1.16**, confirmed working by the author in game. Offline build, binding/persistence and native-contract checks also pass. Report your version, input device, affected action and reproduction steps when reporting problems.

Designed and tested by MikhailKillsZombies, with AI-generated code and AI-generated promotional artwork. Native UI art and button glyphs are loaded from the installed game rather than redistributed. Code is MIT licensed; see LICENSE. API documentation and an integration example are included for mod authors.
