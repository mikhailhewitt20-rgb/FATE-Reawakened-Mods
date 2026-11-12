# FATE Permanent Storage — 0.3.0

A player-owned storage room behind the fishing dock in the **Town of Grove**, reached through a
storage entrance in the village. The room is a fixed pocket built from the Grove dungeon's own
floor, wall and ceiling prefabs, with three persistent storage chests.

## What it does

- A storage entrance stands beside the house behind the fishing dock, marked by a gold light
  beam, a floating marker and an icon on the minimap.
- **Interact with the entrance** to enter the storage room. The exit doorway inside returns you
  to the entrance, not to wherever you last stood.
- Three storage chests keep their contents per character, even after leaving the room, changing
  scenes or restarting the game. Contents are written to
  `FatePermanentStorage_<character>.json` beside the game's save data.
- Each chest starts locked and is unlocked either with gold (`Storage.GoldUnlockCost`, default
  10,000) or with one Mimic Chest from a Mimic kill (`Mimic.MimicChestDropChance`, default 35%).
- **F9** is kept as a debug key: press it in the Grove to build and enter the room from anywhere,
  and again to leave. Entering with F9 returns you to where you pressed it.
- A small diagnostic panel shows the scene, the player and whether the room is built. Turn it off
  with `Diagnostics.ShowDebugOverlay` once the entrance is working.

## Install

1. Install **BepInEx 5 Windows x64**.
2. Place `mikhailkillszombies.FatePermanentStorage.dll` in `BepInEx/plugins/FATE_PermanentStorage/`.
3. Launch the game and load the Town of Grove.
4. Find the storage entrance beside the house behind the fishing dock, or press **F9**.

## Notes

- The entrance looks for `P_FishingDock_01` (then any object named like a fishing dock, then the
  fishing spots) and places itself on the dock-facing side of the nearest `P_House_` object. If
  the town layout changes it falls back to the player's position and logs a warning.
- The entrance is recreated whenever the Grove loads and removed with the room.
- If the entrance is missing, check `BepInEx/LogOutput.log` for `Storage entrance placed at`.
  F9 still opens the room.
- The minimap icon needs the game's `Minimap` layer. If that layer is missing the icon is
  skipped, but the light beam and floating marker stay.
- The room does not spawn monsters and does not use dungeon generation state.
