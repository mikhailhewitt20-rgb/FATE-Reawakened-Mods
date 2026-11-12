# FATE Permanent Storage — 0.7.31

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
- The room is **player-only**: pets stay outside when you enter, and a pet or summon that is
  already inside is moved back to the entrance. The Storage Keeper is the only other occupant.
- The room starts empty apart from the **Storage Keeper**, who explains that Mimic bodies become
  storage chests. Interact with the Keeper while carrying a Mimic Chest to start placement: a
  translucent chest preview follows the cursor, left-click places it, and right-click or Esc
  cancels. Placing consumes one Mimic Chest and costs no gold. Each placed chest has its own
  stock-sized stash and keeps its contents per character, even after leaving the room, changing
  scenes or restarting the game. Contents and positions are written to
  `FatePermanentStorage_<character>.json` beside the game's save data, with four rolling recovery
  copies; the room holds up to 24
  placed chests.
- Mimics still drop a Mimic Chest on a 35% roll (`Mimic.MimicChestDropChance`). Floor drops use
  the Mimic model; a death drop replaces the stand-in with the actual death-pose corpse, and
  picking the item up removes the corpse with it. The supplied chest artwork is used only as the
  inventory icon, where the chest occupies a 2×2 (four-slot) square. Gold is no longer used for
  storage chests.
- The room's **wall set** and **floor set** are chosen independently in the mod menu and swap live.
  The exit door and ceiling follow the wall set; the floor keeps its own selection, so any wall
  theme can sit over any floor texture.
- The room is surrounded by a six-tile-wide layer of the selected dungeon ceiling. Its four straight
  inner edges sit 1.54 m outward. The old extra corner overlap tiles are gone; the released v4
  bake records the clean perimeter layout.
- **F10** writes your current coordinates to the log. There is no debug teleport and no in-game
  room editor: enter and leave through the storage entrance, and change the room's wall and floor
  sets from **Mod Settings → FATE Permanent Storage**.
- The room's fixed baked layout is applied automatically. The shipped roof bake is the permanent
  default arrangement; 0.7.25 removes the old F11/F12 editing modes and all of their keybinds.

## Room layout

The room ships with its baked roof layout already applied. There is no in-game placement or
editing mode, and no editor keybinds. If you need a different room arrangement for a new release,
edit the baked `ceiling_offsets.baked.json` (and `wall_layout.baked.json` if your build includes
one) in the development workspace and repackage the mod.

## Placing storage chests

The room is empty when you first enter. The **Storage Keeper** stands just inside the exit door.
Talk to them with the normal interact key:

- With no Mimic Chest, they explain that Mimics sometimes leave a body behind and that one body
  becomes one storage chest.
- With at least one Mimic Chest, choose **Place chest**, then move the translucent preview around
  the room. **Left-click** places the chest and consumes one Mimic Chest. **Right-click** or
  **Esc** cancels without consuming anything.

Placement only starts inside the room, and the preview turns red if it is too close to the Keeper,
another chest, or the walls. The controls appear in the game's own bottom gameplay-hint area, with
the current action and the available controls together. While placing, press **R** (the default
**Rotate storage chest** bind) to rotate the preview in 90-degree steps; the preview's final rotation
is saved with the placed chest. The rotation action is registered with **FATE Rebind**, so it can be
rebound to another keyboard key or mouse button and is stored in Rebind's bindings file. Placed
chests are permanent for that character: they are saved with their room-local position, rotation
and contents, and are rebuilt with the room on every visit. Up to 24 chests can be placed. Existing
saves from before this version keep any chest that still contained items by converting it into a
placed chest at its old position; empty legacy chest slots are removed.

### Repositioning chests

Talk to the Storage Keeper and choose **Reposition**. The room enters reposition mode without
consuming a Mimic body. Interact with any placed chest to pick it up, move the preview with the
mouse, press **R** to rotate it, and left-click to put it down. You stay in reposition mode so you
can move another chest; press **Esc** to finish. If you cancel a move with right-click or Esc while
the preview is active, the chest returns to where it started.

## Changing the floor, walls and Storage Keeper

Open the pause menu, choose **Mod Settings**, then **FATE Permanent Storage**. The room has two
independent sliders:

- **Storage room wall set** swaps the complete wall theme, the exit door and the ceiling. Every
  complete wall set in the game is offered, the Grove's own sets first, and the room rebuilds
  instantly while you are standing in it.
- **Storage room floor set** swaps only the floor material set. Every distinct dungeon floor
  material from a template with the room's tile size is offered, so the floor texture changes
  independently of the wall theme. The room applies the selected template's `floorMaterials` to
  its floor tiles, matching how the dungeon generator colors real dungeon floors.

Chests no longer automatically turn to face the centre of the room. Their saved rotation is exactly
the rotation you choose. Saves from older versions have the old centre-facing rotation removed once
on load.

The **Storage Keeper** heading has a **name** text box and a **model** slider. Click the name chip,
type the name and press **Enter** (Escape cancels, Ctrl+V pastes). The model slider cycles through
the NPC models loaded in the town plus the party, merchant and curated character prefabs;
changing it rebuilds only the Keeper, leaving chests and the room untouched. The curated list
includes the women who were missing before, such as **Dreya the Healer** (the default), **Asiya
the Healer**, **Hikko the Salesman**, **Kira the Jeweler**, **Jinth the Seer**, **Sakina the
Priestess** and **Madame Yaga**, along with the other townswomen and magic sellers. The name
updates the interaction prompt and the floating nameplate immediately. The chosen character name
is saved as `Storage Keeper.ModelName` so the Keeper keeps the same look even if the town loads
late or the model list changes.

Placed chests use the game's stock stash interface, with a separate item list for every chest.
Each character's chests are stored in `FatePermanentStorage_<character>.json`, keyed by that
character's save ID. Names above the chests face the camera and identify each storage position.

The floor runs one cell past the room on every side, so it reaches the walls. The south doorway uses
the selected wall theme's authored door prop at world Z=3998.5 and plays that door's OpenSound
on entry and exit.

The same values live in `BepInEx/config/mikhailkillszombies.fate.permanentstorage.cfg` as
`Walls.Style`, `Floors.Style`, `Storage Keeper.Name`, `Storage Keeper.Model` and
`Storage Keeper.ModelName`. `Model` is a 0-based index in the same order the log lists the models;
`ModelName` takes precedence when it matches a loaded character. Wall and floor changes are picked
up within a second when edited by hand; Keeper changes apply immediately from the mod menu, and
`ModelName` applies when the room is rebuilt. The chosen values are saved, so the room looks the same next session.

The log lines `Storage room wall sets: …` and `Storage room floor sets: …` list every available
set by name, so you know what each index is. `Storage Keeper models: …` lists the model indices.

## Changing the entrance position

The default is the open spot beside the dock at **32.7, 0.5, 9.5**; Y is snapped to the ground.
If you want it somewhere else:

1. Stand where it should go and press **F10**, then read `Location (F10): player=(…)` in
   `BepInEx/LogOutput.log`.
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
4. Find the storage entrance beside the fishing dock.

## Notes

- The entrance is recreated whenever the Grove loads and removed with the room.
- If the entrance is missing, check the log for `Storage entrance placed at` or
  `Could not create the storage entrance`.
- The mod-menu controls only appear when **FATE Mod Menu** is installed. Without it, the wall,
  floor and Keeper values still work through the config file.
- The room's own collision shell keeps the player inside even if a wall prefab has no collider.
- The room does not spawn monsters and does not use dungeon generation state.
