# FATE Permanent Storage — 0.7.22

A player-owned storage room behind the fishing dock in the **Town of Grove**, reached through a
storage entrance in the village. The room is a large, fixed 6×6-cell pocket built from the game's
own dungeon floor, wall, ceiling and door prefabs, ringed by dungeon walls and lit to keep the
floor readable against the surrounding space, with storage chests the player places themselves and the
Storage Keeper who turns Mimic bodies into those chests.

## What it does

- A storage entrance stands at the open spot beside the fishing dock (**32.7, 0.5, 9.5** in the
  Grove), marked by a gold light beam, a floating marker and a **chest icon** on the minimap.
- **Interact with the entrance** to enter the storage room immediately. You arrive just inside the
  theme-matched exit door, which returns you to the entrance, not to
  wherever you last stood. A save made inside the room loads back into the room, with the room
  rebuilt around the player, so the room works like any other space instead of stranding you in it.
- The room starts empty apart from the **Storage Keeper**, who explains that Mimic bodies become
  storage chests. Interact with the Keeper while carrying a Mimic Chest to start placement: a
  translucent chest preview follows the cursor, left-click places it, and right-click or Esc
  cancels. Placing consumes one Mimic Chest and costs no gold. Each placed chest has its own
  stock-sized stash and keeps its contents per character, even after leaving the room, changing
  scenes or restarting the game. Contents and positions are written to
  `FatePermanentStorage_<character>.json` beside the game's save data; the room holds up to 24
  placed chests.
- Mimics still drop a Mimic Chest on a 35% roll (`Mimic.MimicChestDropChance`). A dropped Mimic
  Chest uses the Mimic's own corpse as its world model, with the chest icon floating above the
  body; picking it up removes the corpse with it. In the inventory the chest occupies a 2×2
  (four-slot) square. Gold is no longer used for storage chests.
- The room floor, door, walls and ceiling follow one selected dungeon theme. The wall-set slider
  in the mod menu swaps the complete look live, so the floor and exit door stay matched to walls.
- The room is surrounded by a six-tile-wide layer of the selected dungeon ceiling. Its four straight
  inner edges sit 1.54 m outward, with extra overlapping tiles at the corners to close the roof.
- **F9** is the debug entry: build and enter the room from anywhere in the Grove, and leave again.
  **F10** writes your current coordinates to the log. **F11** toggles the roof tile editor; **F12** toggles the wall layout editor inside the room. **PageUp/PageDown** cycle the wall palette, including corner pieces.
- The on-screen overlay shows your live XYZ coordinates, the entrance, dock and house positions
  and the current wall set. Turn it off with `Diagnostics.ShowDebugOverlay`.

## Roof tile editor (click-and-drag, bake for release)

While you are inside the storage room you can reposition the ceiling/roof surround tiles and bake those positions into a release:

- **F11** toggles roof edit mode on/off (only inside the room).
- **Left-click and drag** a roof tile to move it horizontally (XZ). **Up/Down arrow keys** raise/lower the selected tile in 0.1 m steps. **+/- keys** scale it uniformly; the mouse wheel does not edit roof tiles.
- **Delete** removes the selected tile. **Insert** places or restores a tile at the cursor's nearest roof-grid cell. Added and deleted tiles persist across room rebuilds.
- **F7** resets the currently selected tile back to its computed default position.
- **F6** bakes the current layout for release: it saves your edits and writes `ceiling_offsets.baked.json` next to the plugin DLL (`BepInEx/plugins/FATE_PermanentStorage/`). The mod loads baked offsets from that file as defaults and also applies your local edits from `BepInEx/config/mikhailkillszombies.fate.permanentstorage.ceiling_offsets.json`.
- To ship a layout in a release, keep `ceiling_offsets.baked.json` in the plugin folder when packaging (the package script includes it automatically if present in `distribution/fate-permanent-storage/`). Offsets are stored as deltas from the computed base position so they stay correct when wall height/alignment changes.

## Wall layout editor (click, drag, place, delete, rotate, bake)

While inside the storage room, press **F12** to edit wall pieces for the selected dungeon tileset:

- **Left-click and drag** a wall piece to move it around the room.
- **Q/E** rotate the selected piece by 90 degrees.
- **PageUp/PageDown** cycle wall prefab types, including conventional and inner corners. **Insert** places the selected type under the cursor; **Delete** removes the selected piece.
- **F7** resets a base wall piece to its generated position. Added pieces return to where they were placed.
- **F6** saves the layout for local use and bakes it to `wall_layout.baked.json` next to the plugin DLL. Layouts are keyed to the active wall tileset, so edits for one theme do not affect other themes.
- The package script includes a non-empty `wall_layout.baked.json` from the distribution folder or the Default profile. Place the baked file in the distribution folder to ship the saved wall arrangement.

Use **F11** or **F12** to switch between the roof and wall editors; only one editor runs at a time.

## Placing storage chests

The room is empty when you first enter. The **Storage Keeper** stands just inside the exit door.
Talk to them with the normal interact key:

- With no Mimic Chest, they explain that Mimics sometimes leave a body behind and that one body
  becomes one storage chest.
- With at least one Mimic Chest, choose **Place chest**, then move the translucent preview around
  the room. **Left-click** places the chest and consumes one Mimic Chest. **Right-click** or
  **Esc** cancels without consuming anything.

Placement only starts inside the room, and the preview turns red if it is too close to the Keeper,
another chest, or the walls. Placed chests are permanent for that character: they are saved with
their room-local position and contents, and are rebuilt with the room on every visit. Up to 24
chests can be placed. Existing saves from before this version keep any chest that still contained
items by converting it into a placed chest at its old position; empty legacy chest slots are
removed.

## Changing the floor and walls

Open the pause menu, choose **Mod Settings**, then **FATE Permanent Storage**, and drag the
**Storage room wall set** slider. Every step swaps the whole room over to a different dungeon
tile set — every complete wall set in the game is offered, the Grove's own sets first — and the room
rebuilds instantly while you are standing in it, so you can keep dragging until you like one.

Placed chests use the game's stock stash interface, with a separate item list for every chest.
Each character's chests are stored in `FatePermanentStorage_<character>.json`, keyed by that
character's save ID. Names above the chests face the camera and identify each storage position.

The floor runs one cell past the room on every side, so it reaches the walls. The south doorway uses
the selected dungeon theme's authored door prop at world Z=3998.5 and plays that door's OpenSound
on entry and exit.

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
