# FATE Rebind 0.1.5 — standalone UI test

A modular keybind editor for FATE: Reawakened, with its own window.
The game's Settings/Controls screen is unchanged. No co-op plugin is required.

## Install and test

1. Close the game and replace the earlier Rebind version in your r2modman profile
   by importing FATE_Rebind-0.1.5-Thunderstore.zip as a local mod.
2. Keep BepInExPack enabled; test with only BepInExPack and Rebind first.
3. Start modded. Click Rebind 0.1.5 [F10] at the top right, or press F10.
4. Load a character to discover gameplay actions. Search for Inventory, click
   its binding, and press a new key. Escape cancels capture. F10 is reserved.
5. If a new input conflicts with exactly one compatible action, Swap exchanges
   their bindings. Keep both and Cancel are also available. Remove unbinds a row;
   Reset restores it. Reset group requires confirmation.
6. Close the window, verify the new input in game, then restart and check persistence.

Changes are runtime overrides saved to
BepInEx/config/ZombieGhost.Rebind/bindings.xml in the manager profile.
The preceding file is retained as bindings.xml.bak. No game assets are edited.

Provider groups, search and keyboard/mouse/controller filters are supported.
Native composite directions have separate rows. Pointer movement stays automatic.
Input actions are suspended while the window is open and restored on closing.
The window does not add a multiplayer/world pause.

## Mod support

Unity InputAction assets are discovered automatically. Mods may register labelled
providers/actions through RebindApi. Raw hard-coded key polling requires the owning
mod to use the API or an adapter. See API.md and PartGhostControlsBridge.cs.
No PartGhost or Zombie Ghost co-op DLL is bundled or required.

## Verification and removal

Compilation, storage and binary contract checks pass. Actual window rendering,
clicks and rebinding require the user's gameplay test; they are not confirmed yet.
No game was launched or controlled, and this ZIP was not automatically installed.
Uninstall Rebind in the manager with the game closed. Keep or remove its config
folder according to whether you want to preserve saved bindings.
