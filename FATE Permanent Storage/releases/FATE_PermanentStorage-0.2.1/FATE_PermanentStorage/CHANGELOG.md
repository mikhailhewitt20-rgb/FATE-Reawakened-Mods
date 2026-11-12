# Changelog

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
