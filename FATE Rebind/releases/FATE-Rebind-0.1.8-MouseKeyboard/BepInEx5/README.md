# FATE Rebind 0.1.8

A keyboard-and-mouse binding editor by MikhailKillsZombies.

## Install and test

Close the game and replace the previous Rebind mod in r2modman with this
Thunderstore ZIP. Keep BepInExPack enabled. Start modded, then open Controls.

The editor uses the game's font, buttons and existing decorative window frame.
The list is inset within the frame so the native borders and corners remain
visible. Providers, categories and actions have scrolling lists.

This version shows editable keyboard keys, mouse buttons and mouse-wheel inputs
only. Controller/device filters and automatic pointer-position rows are removed.
The game can still use its normal controller mappings; this editor leaves them
alone. Group reset affects only the keyboard/mouse bindings shown by this version.

Click an input button to capture a replacement. Escape cancels. Remove unbinds a
row; Reset restores it. For a single conflicting action, Swap exchanges bindings.
Keep both and Cancel are also available. F10 remains a standalone fallback.

Check the frame/corners, scroll the action list, rebind Inventory, verify it in
gameplay, then restart and check it saved. Confirm the search and category buttons
work and no controller rows appear. The new frame layout requires user testing.

## Settings and mod support

Existing saved assignments remain at
BepInEx/config/ZombieGhost.Rebind/bindings.xml for compatibility. Previous saved
controller assignments are preserved. No native input assets or game assemblies
are edited. The prior settings file is kept as bindings.xml.bak.

The startup-safe runtime driver from the working 0.1.6 build is retained.
Independent mods can register providers and actions with RebindApi; InputAction
assets are discovered automatically. Raw polling requires the owning mod to use
the API or an adapter. See API.md and ModControlsExample.cs.

## Verification and removal

Release builds, offline storage/naming/device filtering, lifecycle and API checks
pass. No game was launched/controlled; gameplay testing remains with the user.
Uninstall through r2modman with the game closed. Keep the config folder if you want
to retain your assignments. The package does not bundle a loader or co-op mod.
