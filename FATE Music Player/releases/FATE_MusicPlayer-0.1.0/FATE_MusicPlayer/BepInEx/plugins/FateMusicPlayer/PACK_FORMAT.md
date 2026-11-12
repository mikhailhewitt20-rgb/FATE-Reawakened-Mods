# Song pack format

A song pack is an ordinary Thunderstore package that drops files into
`BepInEx/plugins/FateMusicPlayer/packs/<packId>/`. Installing it through r2modman or
Thunderstore Mod Manager copies those files into the profile, and the music player
picks them up the next time the game starts (or when **Rescan packs** is pressed).

Removing the package removes its folder, and its songs disappear from every list on
the next scan. Nothing the user changed about the base game or about other packs is
touched: packs only ever add rows.

## Layout

```
BepInEx/plugins/FateMusicPlayer/packs/MyPack/
    pack.json
    dungeon_bell.ogg
    village_dawn.ogg
manifest.json          (Thunderstore metadata, at the archive root)
README.md              (at the archive root)
icon.png               (at the archive root)
```

`packId` is the folder name and must be stable across releases, because it is half
of every song ID that user settings are stored against.

## pack.json

```json
{
  "id": "MyPack",
  "name": "Bell and Dawn",
  "author": "Someone",
  "version": "1.0.0",
  "songs": [
    {
      "title": "Dungeon Bell",
      "category": "Dungeons",
      "bucket": "DungeonMusic",
      "file": "dungeon_bell.ogg",
      "loop": true,
      "volume": 0.8
    },
    {
      "title": "Village Dawn",
      "category": "Villages",
      "bucket": "Town",
      "file": "village_dawn.ogg"
    }
  ]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | no | Defaults to the folder name. Changing it orphans saved settings. |
| `name` | no | Shown in the menu heading and in exported packs. |
| `author` | no | Free text, copied into exported `manifest.json`. |
| `version` | no | Free text, copied into exported `manifest.json`. |
| `songs[].title` | yes | Row label in the menu. |
| `songs[].category` | yes | Menu heading the song appears under. New headings are fine. |
| `songs[].bucket` | yes | Which game music slot this song replaces. One of the values in `bucket` below, or a pack-local bucket name shared with the base game slot. |
| `songs[].file` | yes | Audio file inside the pack folder. `.ogg`, `.wav` and `.mp3` are accepted. |
| `songs[].loop` | no | Defaults to `true`. |
| `songs[].volume` | no | `0`–`1`, defaults to `1`. |

## Buckets

A bucket is the game's own music slot: the value the game passes to
`AudioManager.PlayMusic`. The player lists the real slots it found in the running
game, each labelled with the game's own clip name, and the default songs in each
bucket are the game's own tracks. A pack song with `"bucket": "Town"` competes only
with the other songs in that bucket: when the game asks for village music, the
player picks one enabled song from that bucket.

If a pack names a bucket the game never asks for, its songs simply never play; the
row still appears so the mistake is visible rather than silent.

## Export

**Export song pack** in the player writes a complete Thunderstore package:
`manifest.json`, a generated `README.md`, and the chosen audio files under the
`packs/<packId>/` path. The exported zip can be uploaded to Thunderstore as-is.
