# FATE Music Player

Take over the soundtrack of *FATE Reawakened*. The mod lists the game's own music, grouped by where it plays, so you can keep it, skip it, replace it, or add your own.

Everything lives in **pause menu → Mod Settings → FATE Music Player**.

## What you can do

- **Keep a song in the shuffle** with its checkbox: `[x]` means that track takes part in the rotation for its location, `[ ]` means it is skipped. Shuffling is per song, not one forced pick.
- **Hear five seconds** with the `-> 5s` row under each song: the value box shows `|>` and either arrow plays it. It mutes FATE's own track for those seconds and restores it afterwards, and it is audible while the menu is open.
- **Add song from this PC…** opens a file window; pick an `.ogg`, `.wav` or `.mp3` and it joins that category with the same tick and arrow rows. The file is copied into the pack you export, not into the game.
- **Reset this category** ticks every song in it again.
- **Export song pack…** writes a complete Thunderstore package of your own songs, keeping the categories and slots you gave them.
- **Rescan installed packs** after dropping a pack in by hand (songs already appear on their own once the game's music list loads).

Songs are grouped by place: **Grove**, **Druantia**, **Typhon**, **Temple of Fate**, **Ekbatan**, **Chamber of Trials**, each realm's dungeon music under its own place, **Dungeons** for slots that name no place, and **Main menu**. The `ambiance …` loops the game stores beside its music are not songs and are not listed; the game keeps playing them exactly as before.

Slots you never touch keep vanilla behaviour, including the game's own fades and shuffle: the mod only takes a slot over once one of its songs is unticked or you add a file to it.

## Song packs

Install a pack like any other Thunderstore mod. Its songs appear under their own categories and compete only inside the location slot they declare. Packs are additive by design:

- Installing a pack adds rows; it never rewrites your selections or your own songs.
- A pack's songs are identified by the pack folder, so removing the package removes exactly those rows and leaves everything else as it was.

Pack authors: see [`PACK_FORMAT.md`](PACK_FORMAT.md).

## Where things are stored

| Path | Contents |
| --- | --- |
| `BepInEx/config/FateMusicPlayer/library.json` | Only the changes you made (unticked songs) plus songs you added. |
| `BepInEx/plugins/FateMusicPlayer/packs/<pack>/` | Installed song packs. |

## Requirements

BepInEx 5 (`BepInEx-BepInExPack-5.4.2305`) and [FATE Mod Menu](https://thunderstore.io/c/rv-there-yet/) for the settings screen. The mod loads without the menu, but then it has nowhere to show its list.

## Known limits

- Music is selected per location slot. Two different locations that share one slot in the game share one list.
- The menu shows up to 100 songs (two rows each, against the menu's 256-row cap); large pack collections are reported in a note.
- Controls use plain ASCII (`[x]`, `|>`) because the game's font has no tick or play glyphs.
- Files are decoded on first use, so the first play of a custom song can be silent for a frame or two.
