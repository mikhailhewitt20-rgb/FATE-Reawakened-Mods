# FATE Music Player

Take over the soundtrack of *FATE Reawakened*. FATE picks music from slots — town, dungeon, Fate events, main menu — and this mod lists every track in each slot so you can keep it, mute it, replace it, or add your own.

Everything lives in **pause menu → Mod Settings → FATE Music Player**.

## What you can do

- **Toggle a song off** with the On/Off row beside it. That track is skipped from then on.
- **Select song** pins one track to its location: whenever FATE asks for that slot, your pick plays instead.
- **Play first 5 seconds** previews a track without touching what is playing.
- **Add song from this PC…** opens a file window; pick an `.ogg`, `.wav` or `.mp3` and it joins that category with the same toggle, select and preview rows. The file is copied into the pack you export, not into the game.
- **Reset this category** puts every song back to enabled and clears the pick.
- **Export song pack…** writes a complete Thunderstore package of your own songs, keeping the categories and slots you gave them.
- **Rescan installed packs** after dropping a pack in by hand.

Slots you never touch keep vanilla behaviour, including the game's own fades and shuffle.

## Song packs

Install a pack like any other Thunderstore mod. Its songs appear under their own categories and compete only inside the location slot they declare. Packs are additive by design:

- Installing a pack adds rows; it never rewrites your selections or your own songs.
- A pack's songs are identified by the pack folder, so removing the package removes exactly those rows and leaves everything else as it was.

Pack authors: see [`PACK_FORMAT.md`](PACK_FORMAT.md).

## Where things are stored

| Path | Contents |
| --- | --- |
| `BepInEx/config/FateMusicPlayer/library.json` | Only the changes you made (toggles, picks) plus songs you added. |
| `BepInEx/plugins/FateMusicPlayer/packs/<pack>/` | Installed song packs. |

## Requirements

BepInEx 5 (`BepInEx-BepInExPack-5.4.2305`) and [FATE Mod Menu](https://thunderstore.io/c/rv-there-yet/) for the settings screen. The mod loads without the menu, but then it has nowhere to show its list.

## Known limits

- Music is selected per location slot. Two different locations that share one slot in the game share one list.
- The menu shows up to 70 songs (three rows each, against the menu's 256-row cap); large pack collections are reported in a note.
- Files are decoded on first use, so the first play of a custom song can be silent for a frame or two.
