# FATE Permanent Storage — 0.3.4

A player-owned storage room behind the fishing dock in the **Town of Grove**, reached through a
storage entrance in the village. The room is a large, fixed 6×6-cell pocket built from the game's
own dungeon floor, wall and ceiling prefabs, with three persistent storage chests.

## What it does

- A storage entrance stands at the open spot beside the fishing dock (**32.7, 0.5, 9.5** in the
  Grove), marked by a gold light beam, a floating marker and a **chest icon** on the minimap.
- **Interact with the entrance** to enter the storage room. The exit doorway inside returns you
  to the entrance, not to wherever you last stood.
- Three storage chests keep their contents per character, even after leaving the room, changing
  scenes or restarting the game. Contents are written to
  `FatePermanentStorage_<character>.json` beside the game's save data.
- Each chest starts locked and is unlocked either with gold (`Storage.GoldUnlockCost`, default
  10,000) or with one Mimic Chest from a Mimic kill (`Mimic.MimicChestDropChance`, default 35%).
- The room walls use a real dungeon wall set, and the set can be swapped live from the mod menu.
- **F9** is the debug entry: build and enter the room from anywhere in the Grove, and leave again.
  **F10** writes your current coordinates to the log.
- The on-screen overlay shows your live XYZ coordinates, the entrance, dock and house positions
  and the current wall set. Turn it off with `Diagnostics.ShowDebugOverlay`.

## Changing the walls

Open the pause menu, choose **Mod Settings**, then **FATE Permanent Storage**, and drag the
**Storage room wall set** slider. Every step swaps the whole room over to a different dungeon
tile set — every complete wall set in the game is offered, Groves's own sets first — and the room
rebuilds instantly while you are standing in it, so you can keep dragging until you like one.

The same value lives in `BepInEx/config/mikhailkillszombies.fate.permanentstorage.cfg` as
`Walls.Style` (0-based, in the same order the log lists the sets). Editing it by hand works too:
the room picks up a changed value within a second. The chosen set is saved, so the room looks the
same next session.

The log line `Storage room wall sets: …` lists every available set by name, so you know what each
index is.

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
- The mod-menu slider only appears when **FATE Mod Menu** is installed. Without it, the wall set
  still works through `Walls.Style`.
- The room's own collision shell keeps the player inside even if a wall prefab has no collider.
- The room does not spawn monsters and does not use dungeon generation state.
