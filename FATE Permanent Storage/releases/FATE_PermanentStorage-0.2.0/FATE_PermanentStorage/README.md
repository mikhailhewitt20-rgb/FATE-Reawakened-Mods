# FATE Permanent Storage — 0.2.0 Test Build

This build is deliberately minimal. It does **not** add a village entrance, minimap marker, storage chests, or Mimic drops. It only tests building and entering a custom room.

## What it does

- Press **F9** in the Grove to build a fixed two-square dungeon room at a hidden coordinate and teleport the player into it.
- The room contains:
  - two 2×2 floor squares
  - enclosing walls and a ceiling
  - warm point lights
  - one exit door
- Press **F9** again, or interact with the exit door, to return to the position where you pressed F9.
- A small on-screen diagnostic panel shows whether the plugin loaded, which town was detected, and whether the room is built.

## Install

1. Install **BepInEx 5 Windows x64**.
2. Place `mikhailkillszombies.FatePermanentStorage.dll` in `BepInEx/plugins/FATE_PermanentStorage/`.
3. Launch the game and load the Town of Grove.
4. Press **F9**.

## Notes

- If the diagnostic panel does not appear, the plugin is not loaded; check `BepInEx/LogOutput.log` for `FATE Permanent Storage 0.2.0 TEST manager created.`
- The room is a pocket built from simple primitives so it does not depend on dungeon templates or container prefabs.
- The previous storage-room features remain in the source but are intentionally not used in this test build.
