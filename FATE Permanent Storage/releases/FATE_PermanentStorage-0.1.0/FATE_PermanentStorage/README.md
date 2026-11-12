# FATE Permanent Storage

A permanent, player-owned storage room in the Grove, entered from the house behind the fishing dock.

## What it does

- **A fixed room, not a dungeon.** The room is built from the Grove dungeon's own cream floor tiles, warm wooden walls and ceiling pieces. It is placed in a hidden pocket of the town scene, has no seed, no progression, no timer and no monsters, and is never regenerated. Leaving and returning rebuilds the same room at the same coordinates.
- **A door in the Grove.** The entrance is attached to the house nearest the fishing dock. Interact with it to fade into the storage room. The exit interactable inside returns you to the village spot where you entered.
- **Seven independent chests.** Every storage position has a stable id and its own inventory, opened through the game's normal Storage Chest UI. Contents, slot positions and item properties are kept separately per chest.
- **Two ways to unlock a locked position.** Interact with a locked chest and choose **Yes** to spend one Mimic Chest, or **No** to buy the position for gold. The gold purchase has its own confirmation. A chest can never be unlocked twice, and nothing is consumed or charged until you confirm.
- **Mimic Chest drops.** Mimics can drop a new **Mimic Chest** item using the game's ordinary world-drop and pickup systems. The drop is added after the Mimic's normal loot, so vanilla Mimic loot is untouched.
- **Grove music and lighting.** The room uses the Grove temple music and warm point lights, not dungeon ambience.

## Config

`BepInEx/config/mikhailkillszombies.fate.permanentstorage.cfg`

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| Storage | `GoldUnlockCost` | `10000` | Gold needed to unlock one storage position. `0` makes gold unlocks free. |
| Mimic | `MimicChestDropChance` | `35` | Percent chance that a Mimic also drops one Mimic Chest. `0` disables the drop. |

## Install

1. Install **BepInEx 5 Windows x64** (the FATE BepInExPack uses 5.4.23.5).
2. Extract the archive and merge its **BepInEx** folder beside `FateReawakened.exe`. The plugin belongs at `BepInEx/plugins/FATE_PermanentStorage/mikhailkillszombies.FatePermanentStorage.dll`.
3. Load a save in the Town of Grove. The entrance door appears on the house behind the fishing dock.

## Persistence

Unlocks, unlock method and every chest's contents are stored in a sidecar JSON file next to the game's save data, keyed to the current save slot. The mod also writes on the game's normal pre-save hook and immediately after every chest change, so leaving the room, reloading or restarting does not lose items. The sidecar is separate from the vanilla save and does not modify the game's own save format.

## Notes

- The room is a town pocket, not a new Unity scene. It is destroyed and rebuilt when the Grove loads, so it never leaks into other scenes or vanilla dungeons.
- The storage UI is the game's own `Stash` implementation, driven through an invisible proxy. Other stashes are temporarily disabled only while a storage chest is open and are restored when the UI closes.
- Mimic Chest is a runtime item. It has a generated icon and a small world model, and it is registered before saved inventories are rebuilt.
- If a future game update changes the Grove template names or the fishing-dock house names, the mod falls back to the nearest available house and logs a warning in `BepInEx/LogOutput.log`.

## Removal

Uninstall with the game closed and remove `BepInEx/plugins/FATE_PermanentStorage`. The sidecar storage file is left beside your save data; delete it only if you also want to discard the stored items.
