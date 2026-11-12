# FATE Permanent Storage — 0.2.1

This build adds a small player-owned storage room built from the Grove dungeon's existing floor, wall and ceiling assets. It does **not** add a village entrance or minimap marker yet.

## What it does

- Press **F9** in the Grove to build a fixed 3×3 dungeon room at a hidden coordinate and teleport the player into it.
- The room uses the game's Grove dungeon tile prefabs; it does not generate monsters or dungeon save data.
- Three persistent storage chests use the character's own save slot and keep their contents after leaving the room or restarting the game.
- Press **F9** again, or interact with the exit doorway, to return to the position where you pressed F9.
- A small on-screen diagnostic panel shows the scene, the player, and whether the room is built.

## Install

1. Install **BepInEx 5 Windows x64**.
2. Place `mikhailkillszombies.FatePermanentStorage.dll` in `BepInEx/plugins/FATE_PermanentStorage/`.
3. Launch the game and load the Town of Grove.
4. Press **F9**.

## Notes

- If the diagnostic panel does not appear, the plugin is not loaded; check `BepInEx/LogOutput.log` for `FATE Permanent Storage 0.2.1 TEST manager created.`
- The room is a static pocket built from the game's Grove dungeon prefabs; it does not spawn monsters or use dungeon generation state.
- Chest records are loaded from `FatePermanentStorage_<character>.json` in the game's persistent data folder.
