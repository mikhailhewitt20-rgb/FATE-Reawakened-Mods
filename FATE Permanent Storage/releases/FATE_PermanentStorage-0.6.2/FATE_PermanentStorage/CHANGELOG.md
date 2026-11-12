# Changelog

## 0.6.2

- Moved the roof editor toggle from F8 to F11 to avoid the co-op shortcut.

## 0.6.1

- Removed the fake black loading screen and delay; entering and leaving the room now teleports immediately.
- Doorway interactions play the game's portal opening sound.
- Kept the in-game roof editor and F6 bake workflow for adjusting and saving roof tile positions.

## 0.6.0

- Added an in-game roof/ceiling tile editor for the storage room. Press **F11** inside the room to toggle edit mode, then left-click and drag ceiling surround tiles to reposition them (mouse wheel adjusts height). **F7** resets the selected tile; **F6** bakes the current layout.
- Roof tile positions are saved as per-tile offsets (deltas from the computed base, keyed by grid cell) and reapplied whenever the ceiling surround is rebuilt, so layouts survive wall/floor style changes and reloads.
- Edits are stored in `BepInEx/config/mikhailkillszombies.fate.permanentstorage.ceiling_offsets.json`; **F6** also writes `ceiling_offsets.baked.json` next to the plugin DLL for shipping in a release. The mod loads baked defaults and layers user edits on top.
- The debug overlay now shows roof editor status and controls.

## 0.5.0

- Entering and leaving the room now goes through a black loading screen with a caption, like the
  game's own space transitions, instead of an instant teleport.
- Fixed being trapped in the room. A save made inside the room now rebuilds the room around the
  player on load and keeps them inside it, the exit doorway works even when that state was cold,
  and any exit point that is empty or still inside the room falls back to the village entrance
  and then to the town spawn.

## 0.4.2

- The wall ring is now measured after it is built and slid so its inner faces line up with the
  room's hitbox, at 0.6 m inside the floor edge (4000.6 … 4023.4 in the current room). Corners
  move with the rest of the ring, so every tile set gets the same straight, closed outline. The
  measured and target lines are logged as `Walls aligned to the room: …`.
- The ceiling surround moves with the walls, so the roof stays on the wall tops after alignment.

## 0.4.1

- Ceiling surround height is now measured from the built walls instead of a fixed 3.2 m, so the
  roof tiles sit on the wall tops rather than floating above them.
- The surround uses the ceiling tiles of the **chosen wall set's** template, so the roof texture
  matches the walls; the wall slider rebuilds the roof too.
- Stairs: the stair prefab is now looked up in the chosen wall set's template first, the floor
  dictionary is built on demand if it is empty, and if no stair prefab exists at all the doorway
  gets a built stairway of four stone steps in the wall material instead of being closed off.

## 0.4.0

- Stairs: the entrance stair now looks the stair model up in the template's floor dictionary
  (`CellTypes.StairwellUp`), which is where a dungeon actually gets its stairwell, falling back to
  the floor prefab list only if that is missing.
- The room is now surrounded by the dungeon's ceiling layer instead of empty black space.
  Dungeons roof every non-room cell, including two extra cells beyond the map edge, and that
  "upper floor" stone surface is what fades into the dark around a room. The room itself stays
  unroofed so the camera can see in, exactly like a dungeon room.

## 0.3.9

- Added a second mod-menu slider, **Storage room floor type**, which cycles the room's floor onto
  any dungeon floor the game ships, stored as `Floors.Style`. The overlay shows the active floor.
- The floor now runs one cell past the room on every side, so it meets the ring walls instead of
  stopping short and showing the drop beneath.
- The south gap in the wall ring is filled with a dungeon stair piece, so the entrance reads as a
  stairway into the room. If the template has no stair prefab the gap is closed with wall pieces.
- Dungeon mist now works: the skybox is removed and every loaded camera clears to the mist colour
  (fog alone cannot fill empty space), and the mist colour and density were raised so the void
  reads as grey mist rather than near-black. Everything is restored on exit.

## 0.3.8

- Removed the wall placement editor (T/Y/U/I/O/P) and its saved layout file. Placing pieces by
  hand was too fiddly to be useful.
- Room walls are built only the way dungeons build theirs: wall pieces stand on the ring of
  higher "cliff" cells around the room, and each quadrant's type comes from comparing that cell's
  height with its neighbours (corner, corner-join, inner corner, horizontal, vertical, solid).
- If that ring ever produces no pieces, the room now falls back to placing straight wall pieces
  along the room's own edges instead of leaving the room open, and both paths log a piece count.

## 0.3.7

- Hotkeys now work when the game runs with the legacy Input Manager disabled: F9, F10 and the
  editor's T/Y/U/I/O/P are read from the new Input System's keyboard first and only fall back to
  legacy Input when that keyboard is not present. This is why the room controls did nothing before.
- Wall pieces are no longer force-rotated: they keep the rotation the dungeon generator gives them,
  so the ring walls face the same way they do in a real dungeon.
- Storage chests are now a row across the room: centred on the room's X midline, 6 m in from the
  north wall, spaced 4 m apart (middle chest at 4012, 4018). The record system holds up to 11
  chests; the current 24 m room fits five at that spacing.

## 0.3.6

- Added an in-room wall editor. Press **T** to switch it on, then:
  - **Y** places a wall piece in front of the player, snapped to a half-metre grid;
  - **U** spins the nearest placed piece by 45 degrees (or the place cursor, if none is close);
  - **I** cycles the wall type the next piece uses (corner, horizontal, vertical, join, and so on);
  - **O** cycles the dungeon wall set, the same list as the mod menu slider;
  - **P** deletes the nearest placed piece.
- Placed wall pieces are saved to `BepInEx/config/mikhailkillszombies.fate.permanentstorage.walls.json`
  and rebuilt with the room every time it loads, so a hand-built layout is permanent.
- The diagnostic overlay shows the editor state, the current type and the piece count.
- The room build now logs how many ring wall pieces were placed, and warns when the ring placed
  none, so an empty wall ring is visible in the log instead of only in game.

## 0.3.5

- Walls are now placed exactly the way the dungeon generator places them: on the ring of cliff
  cells around the room, with each quadrant's type decided by comparing that cell's height with
  its neighbours. The room's corners come from the diagonal ring cells using the generator's own
  corner, corner-join and inner-corner cases instead of one hand-placed corner piece, so corners
  line up like a real dungeon.
- The storage chests moved clear of the wall line so they sit inside the room rather than behind
  the north wall.
- Added dungeon mist: while the player is inside the storage room the scene fog and camera
  background take on a dark mist, so the room no longer looks out into black space and the edge
  of the platform. Both are restored exactly when the player leaves.

## 0.3.4

- Quadrupled the room: it is now 6×6 cells (12 m square) instead of 3×3, with extra lighting so
  the larger interior is not half dark.
- The wall set slider now swaps the whole **tile set** rather than another prefab from the same
  set. Every complete wall set in the game is listed, the Grove's sets first, and each set is
  filtered out if its dungeon uses a different tile size to the room.
- Flipped every wall 180 degrees about its own pivot, so the textured face points into the room
  instead of showing the walls' backs.
- The wall set list is written to the log, so a config index can be matched to a set by name.

## 0.3.3

- Rebuilt the room walls so they close properly and face inward. Wall prefabs are now placed the
  way the game's own dungeon generator places them: cell centre plus the prefab's authored
  offset with an identity rotation. The old code applied each prefab's rotation, which turned
  the walls inside out and shifted them off the cell edges, leaving gaps.
- Every boundary cell of the room now gets its wall pieces, including all four outer corners, so
  the perimeter has no missing sections.
- Added a **Storage room wall type** slider to FATE Mod Menu that cycles the room's wall prefab
  style and rebuilds the walls live. It is stored in the config as `Walls.Style` and also works
  without FATE Mod Menu installed.
- The entrance's minimap icon is now a chest instead of a diamond, so the marker reads as
  storage at a glance.
- The diagnostic overlay shows the active wall style.

## 0.3.2

- Moved the default Grove entrance to the open spot beside the fishing dock at 32.7, 0.5, 9.5,
  with ground and NavMesh snapping. The old house-based placement is kept as a fallback if that
  spot ever stops being walkable.
- Exit teleport also uses a NavMesh-sampled point near the entrance, so leaving the room does not
  drop the player into the door or a wall.

## 0.3.1

- The diagnostic overlay now shows the player's live XYZ coordinates and the entrance, dock and
  house positions.
- F10 writes the player and entrance coordinates to the log, so a bad entrance spot can be
  reported without copying numbers off the screen.
- Added `Entrance.ManualPosition` for an exact x,y,z entrance position and `Entrance.Offset` to
  nudge the automatic spot; both are read when the entrance is created, so no rebuild is needed.
- Entrance creation logs the full dock/house/door/return coordinates.

## 0.3.0

- Restored the village storage entrance beside the house behind the fishing dock. It is created
  when the Grove finishes loading and is recreated after every town reload.
- Restored the minimap marker, the gold light beam and the floating diamond at the entrance.
- The entrance is placed on the interaction layer and gets its own clamped hitbox, while the
  copied dungeon-door's child colliders are switched off so the prompt stays at the doorway.
- Interacting with the entrance now returns the player to its doorstep on exit; F9 remains a
  debug entry that returns the player to where the key was pressed.
- Dock lookup falls back from the named dock to any fishing dock object and then to the fishing
  spots; if none is found the entrance is placed beside the player with a logged warning.

## 0.2.1

- Replaced the primitive test room with a 3×3 room assembled from the Grove dungeon's existing floor, wall and ceiling prefabs.
- Restored three persistent storage chests using the character's save slot, with contents loaded from and written to the sidecar save file.
- Moved F9 handling and Grove detection to a Harmony patch on the game's Player.Update, with the plugin update loop as a fallback, so they no longer depend on runtime manager callbacks that the game destroys during scene loads.
- Recreates the manager and diagnostic overlay at runtime if the game removes the startup objects.
- Added scene/manager/F9 diagnostic logging and an expanded on-screen status panel.
- A NavMesh bake failure no longer discards the built room.
- Teleporting now also assigns the player position directly as a fallback for the off-NavMesh room.
- Synchronizes physics transforms and bakes the room NavMesh with the player's own agent type.

## 0.2.0

- Replaced the entrance/search path with a minimal F9 test room.
- The test room has two 2x2 floor squares, a closed shell, lights and one exit door.
- F9 now builds the room and teleports the player directly into it; F9 or the exit door returns the player.
- Disabled village entrance, minimap marker and storage chest placement for this test build.

## 0.1.4

- Added a temporary on-screen diagnostic overlay showing town, player, entrance and room status.
- Added F9 to toggle the storage room directly for testing.
- Added `Diagnostics.ShowDebugOverlay` config; it defaults to enabled during diagnosis.

## 0.1.3

- Made plugin startup non-fatal: the room manager is created before optional Harmony patches, so a failed patch cannot remove the whole feature.
- Added a guaranteed fallback entrance at the player spawn if house detection or the dungeon-entrance template fails.
- Pre-builds the room when the Grove loads so room construction errors are logged immediately.
- Added detailed setup logs for the manager, town detection, interaction layer and entrance creation.

## 0.1.2

- Rebuilt the entrance from the game's own dungeon-door/portal object, preserving its collider and interaction layer.
- Removed the custom chest hitbox that was not being picked up by the player interaction scan.
- Added a high-contrast minimap marker, a gold light beam and a floating diamond marker at the entrance.
- Added logging for the copied template, entrance layer and marker creation.

## 0.1.1

- Replaced the wall-mounted entrance with a visible storage chest placed in the open beside the fishing-dock house.
- Added a chest icon on the minimap so the entrance is easy to find.
- Increased the entrance interaction range and priority so it is easier to use near the house.

## 0.1.0

- Added the permanent Grove storage room behind the fishing-dock house.
- Added seven independently unlockable storage chests using the stock Storage Chest UI.
- Added gold and Mimic Chest unlock paths with confirmation prompts.
- Added the Mimic Chest item, runtime icon and world-drop model, with a configurable Mimic drop chance.
- Added slot-keyed sidecar persistence for unlocks and chest contents.
- Added Grove music, warm lighting and a fixed room built from the Grove dungeon tile set.
