# FATE Music Player

Take over the soundtrack of *FATE Reawakened*. FATE picks music from slots — town, dungeon, Fate events, main menu — and this mod lists every track in each slot so you can keep it, mute it, replace it, or add your own.

Everything lives in **pause menu → Mod Settings → FATE Music Player**.

## Each song is one row

| Part of the row | What it does |
| --- | --- |
| **▶** | Plays the first five seconds, with FATE's own music stepped aside. Press **■** to stop. |
| **Title** | The track's name. |
| **Select** | Pins this track to its location: whenever FATE asks for that slot, your pick plays instead of the shuffle. Only one song per slot can be selected. |
| **Check box** | On/off. A song turned off is skipped from then on. |
| **Loop timer** | Seconds of the song's end that overlap its start. At **0** the song ends and starts again at the loop point, like the game. Turn it up and the end melts into the beginning, so a track that does not loop cleanly still runs without a seam. Dragging it, or using the arrows, plays the song's ending into its start at the new overlap so you can judge it by ear. |

Every category heading has a **Defaults** button that puts that category's toggles and selection back, and clears the loop timers.

## What else you can do

- **ADD SONG** under a category opens a file window; pick an `.ogg`, `.wav` or `.mp3` and it joins that category with the same preview, select, toggle and loop-timer rows.
- **EXPORT SONG PACK** writes a complete Thunderstore package of the songs you added, keeping the categories and slots you gave them.
- **RESCAN SONG PACKS** after dropping a pack in by hand.

Slots you never touch keep vanilla behaviour, including the game's own fades and shuffle.

## Song packs

Install a pack like any other Thunderstore mod. Its songs appear under their own categories and compete only inside the location slot they declare. Packs are additive by design:

- Installing a pack adds rows; it never rewrites your selections or your own songs.
- A pack's songs are identified by the pack folder, so removing the package removes exactly those rows and leaves everything else as it was.

Pack authors: see [`PACK_FORMAT.md`](PACK_FORMAT.md). A pack can ship a `loopSeconds` per song, and whoever installs it can still tune the loop timer by ear.

## Where things are stored

| Path | Contents |
| --- | --- |
| `BepInEx/config/FateMusicPlayer/library.json` | Only the changes you made (toggles, picks, loop timers) plus songs you added. |
| `BepInEx/plugins/FateMusicPlayer/packs/<pack>/` | Installed song packs. |

## Requirements

BepInEx 5 (`BepInEx-BepInExPack-5.4.2305`) and **FATE Mod Menu 0.4.0** for the settings screen and its song rows. Without the menu the mod loads but has nowhere to show its list.

FATE Mod Menu is a package dependency (`MikhailKillsZombies-FATE_ModMenu-0.4.2`), so a mod manager installs it with this mod; only a hand-installed copy needs the menu dropped into the same profile. Song packs do install this mod automatically — every exported pack declares `MikhailKillsZombies-FATE_MusicPlayer-<version>` as a dependency.

## Known limits

- Music is selected per location slot. Two different locations that share one slot in the game share one list.
- The menu shows up to about 250 songs in total (one row each, against the menu's 256-setting cap); larger collections are reported in a note.
- The loop timer overlaps on a second audio source, so a blended song cannot use the game's own fade-out for that slot.
- Files are decoded on first use, so the first play of a custom song can be silent for a frame or two.
