# FATE Permanent Storage — 0.3.2

A player-owned storage room behind the fishing dock in the **Town of Grove**, reached through a
storage entrance in the village. The room is a fixed pocket built from the Grove dungeon's own
floor, wall and ceiling prefabs, with three persistent storage chests.

## What it does

- A storage entrance stands at the open spot beside the fishing dock (**32.7, 0.5, 9.5** in the
  Grove), marked by a gold light beam, a floating marker and an icon on the minimap.
- **Interact with the entrance** to enter the storage room. The exit doorway inside returns you
  to the entrance, not to wherever you last stood.
- Three storage chests keep their contents per character, even after leaving the room, changing
  scenes or restarting the game. Contents are written to
  `FatePermanentStorage_<character>.json` beside the game's save data.
- Each chest starts locked and is unlocked either with gold (`Storage.GoldUnlockCost`, default
  10,000) or with one Mimic Chest from a Mimic kill (`Mimic.MimicChestDropChance`, default 35%).
- **F9** is the debug entry: build and enter the room from anywhere in the Grove, and leave again.
  **F10** writes your current coordinates to the log.
- The on-screen overlay shows your live XYZ coordinates and the entrance, dock and house
  positions. Turn it off with `Diagnostics.ShowDebugOverlay`.

## Changing the entrance position

The default is the open spot beside the dock at **32.7, 0.5, 9.5**; Y is snapped to the ground.
If you want it somewhere else:

1. Stand where it should go and read **Player XYZ** in the overlay, or press **F10** and read
   `Location (F10): player=(…)` in `BepInEx/LogOutput.log`.
2. Set `Entrance.ManualPosition = x,y,z` in
   `BepInEx/config/mikhailkillszombies.fate.permanentstorage.cfg`. Leave it empty to keep the
   default spot.
3. Alternatively set `Entrance.Offset = x,y,z` to nudge the default spot by a few metres.

Every entrance build logs `Storage entrance placed at (x, y, z); dock=(…), house=…`, and F10 logs
both your position and the entrance position, so a bad spot can be reported exactly. If the
default spot is ever off the town NavMesh (a changed town layout), the old house-based placement
is used and the reason is logged.

## Install

1. Install **BepInEx 5 Windows x64**.
2. Place `mikhailkillszombies.FatePermanentStorage.dll` in `BepInEx/plugins/FATE_PermanentStorage/`.
3. Launch the game and load the Town of Grove.
4. Find the storage entrance beside the fishing dock, or press **F9**.

## Notes

- The entrance is recreated whenever the Grove loads and removed with the room.
- If the entrance is missing, check the log for `Storage entrance placed at` or
  `Could not create the storage entrance`. F9 still opens the room.
- The minimap icon needs the game's `Minimap` layer. If that layer is missing the icon is
  skipped, but the light beam and floating marker stay.
- The room does not spawn monsters and does not use dungeon generation state.
