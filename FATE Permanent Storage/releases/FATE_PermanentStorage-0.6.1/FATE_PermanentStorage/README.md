# FATE Permanent Storage — 0.6.0

A player-owned storage room behind the fishing dock in the **Town of Grove**, reached through a
storage entrance in the village. The room is a large, fixed 6×6-cell pocket built from the game's
own dungeon floor, wall and ceiling prefabs, ringed by dungeon walls and lit through dark dungeon
mist, with three persistent storage chests.

## What it does

- A storage entrance stands at the open spot beside the fishing dock (**32.7, 0.5, 9.5** in the
  Grove), marked by a gold light beam, a floating marker and a **chest icon** on the minimap.
- **Interact with the entrance** to enter the storage room, with a short black loading screen like
  the game's own space transitions. The exit doorway inside returns you to the entrance, not to
  wherever you last stood. A save made inside the room loads back into the room, with the room
  rebuilt around the player, so the room works like any other space instead of stranding you in it.
- A row of storage chests keeps its contents per character, even after leaving the room,
  changing scenes or restarting the game. Chests sit 6 m in from the north wall, centred on the
  room's X midline and spaced 4 m apart (the middle one at 4012, 4018), so the current room fits
  five; the record format holds up to eleven for a wider room or tighter spacing. Contents are
  written to `FatePermanentStorage_<character>.json` beside the game's save data.
- Each chest starts locked and is unlocked either with gold (`Storage.GoldUnlockCost`, default
  10,000) or with one Mimic Chest from a Mimic kill (`Mimic.MimicChestDropChance`, default 35%).
- The room walls are placed the way the dungeon generator places them, on the ring of cliff cells
  around the room, so edges and corners meet like a real dungeon. The tile set can be swapped live
  from the mod menu.
- The room is surrounded by the dungeon's own ceiling layer: dungeons roof every non-room cell
  (plus two cells past the map edge), and that stone "upper floor" is what you see fading into the
  dark around a dungeon room. It sits on the measured wall tops and uses the ceiling tiles of the
  selected wall set, so roof and walls match.
- Entering the room brings up dungeon mist: the skybox is removed, every camera clears to the mist
  colour and scene fog thickens, so the room never looks out into empty space. Leaving restores
  everything exactly.
- **F9** is the debug entry: build and enter the room from anywhere in the Grove, and leave again.
  **F10** writes your current coordinates to the log. **F8** toggles the roof tile editor inside the room.
- The on-screen overlay shows your live XYZ coordinates, the entrance, dock and house positions
  and the current wall set. Turn it off with `Diagnostics.ShowDebugOverlay`.

## Roof tile editor (click-and-drag, bake for release)

While you are inside the storage room you can reposition the ceiling/roof surround tiles and bake those positions into a release:

- **F8** toggles roof edit mode on/off (only inside the room).
- **Left-click and drag** a roof tile to move it horizontally (XZ). **Mouse wheel** raises/lowers it (Y).
- **F7** resets the currently selected tile back to its computed default position.
- **F6** bakes the current layout for release: it saves your edits and writes `ceiling_offsets.baked.json` next to the plugin DLL (`BepInEx/plugins/FATE_PermanentStorage/`). The mod loads baked offsets from that file as defaults and also applies your local edits from `BepInEx/config/mikhailkillszombies.fate.permanentstorage.ceiling_offsets.json`.
- To ship a layout in a release, keep `ceiling_offsets.baked.json` in the plugin folder when packaging (the package script includes it automatically if present in `distribution/fate-permanent-storage/`). Offsets are stored as deltas from the computed base position so they stay correct when wall height/alignment changes.

## Changing the floor and walls

Open the pause menu, choose **Mod Settings**, then **FATE Permanent Storage**, and drag the
**Storage room wall set** slider. Every step swaps the whole room over to a different dungeon
tile set — every complete wall set in the game is offered, the Grove's own sets first — and the room
rebuilds instantly while you are standing in it, so you can keep dragging until you like one.

The **Storage room floor type** slider does the same for the floor, listing every dungeon floor
that uses the room's tile size. It is saved as `Floors.Style` and also reacts to hand edits within
a second.

The floor runs one cell past the room on every side, so it reaches the walls, and the doorway gap
in the south wall is filled with a dungeon stair piece so the entrance looks like a stairway
instead of an opening onto empty space.

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
- The mist only applies inside the room; the town's own fog settings are saved on entry and put
  back on exit.
- The room does not spawn monsters and does not use dungeon generation state.
