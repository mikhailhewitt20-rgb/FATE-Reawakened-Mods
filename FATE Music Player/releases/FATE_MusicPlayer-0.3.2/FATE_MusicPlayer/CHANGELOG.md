## 0.3.2 — working buttons, drawn icons, music only

The row controls were unusable in the first API v3 build. Fixed:

- **Play is a drawn triangle** (the game's font has no play, tick or arrow glyphs, so the typed ones rendered as empty boxes), and it plays the first five seconds. The pause menu pauses the game's audio listener, which swallowed the preview; the listener is now held open for the preview and handed back afterwards, and every preview is logged with the clip name and length.
- **Selection is gone.** The Select/Selected chip did nothing useful.
- **A cross removes songs you added**, next to their play button. Pack songs go away with their pack, and the game's own tracks cannot be deleted.
- **The checkbox is a filled gold box when on**, so it no longer depends on a tick glyph.
- **Dragging the loop timer no longer freezes the list.** The old code held a drag flag that could stick if the pointer was released off the track, which stopped every later redraw — buttons then looked dead. The row now redraws itself and the value is written once the drag settles.
- **Only music is listed.** The game's music list also holds footsteps, pickups and ambience, which is why categories like *Ambient Barrel* appeared. Soundtrack slots are the entries named as music.
- **Town music first, then dungeons**, then the rest; headings are *Town Music*, *Dungeon Music* and *Fate Choice Music*.

## 0.3.1 — the player still shows on an older menu

0.3.0 refused to register anything when FATE Mod Menu was older than 0.4.0: the module was disposed and the player had no screen at all ("FATE Mod Menu 2 has no media rows"). It now falls back to the rows an older menu does have — on/off, **Select song**, **Play first 5 seconds** and a **Loop timer** slider per song — and says so once instead of every two seconds. Installing **FATE_ModMenu 0.4.0** still gives the compact one-row list.

Also fixed: on a load where the audio manager exists before its music list is filled in, the first scan found zero songs and the player stayed empty for the whole session. It now keeps rescanning for a few seconds until songs appear.

## 0.3.0 — one row per song, with a loop timer you can hear

Every song is now a single row instead of three: a play button for the five-second preview, the title, **Select**, the on/off check, and a loop timer. The list is far easier to scan, and FATE Mod Menu's 256-setting cap now fits about 250 songs instead of 70. Requires FATE Mod Menu 0.4.0 (API v3).

- **The loop timer** is the slider beside each song. At 0 the track ends and starts again at its loop point. Raise it and the song's last seconds overlap its first seconds, so the loop has no seam. Every change to it plays the ending into the beginning at the new overlap, so the timer can be tuned by ear; drag it, or use the arrows for tenths of a second.
- Loop timers are stored per song, exported into song packs, and come back when a pack is installed. A category's **Defaults** button clears them along with the toggles and the pick.
- **Selection is a toggle** on the row, and the menu's per-category **Defaults** now resets only that category instead of every category at once.
- Category headings come from the game's own music-slot names (*Temple of Fate*, *Dungeons*), in the order FATE lists them.
- Rows are no longer re-registered when a value changes, so **Defaults** resets to the mod's real defaults instead of freezing whatever was set last.
- **ADD SONG**, **EXPORT SONG PACK** and **RESCAN SONG PACKS** are full-width bars under their headings.

## 0.2.0 — settings appear in the Mod Menu, previews mute the game

This game destroys the loader's plugin object moments after startup, which stopped every Update the plugin owned, so it never reached the menu API and no module appeared. Patching, song scanning and menu registration now run from a Harmony postfix on `AudioManager.Update` plus a ticker object the mod owns, and the log reports `Registered with FATE Mod Menu: N song row(s)`. The five-second preview now mutes FATE's own music channels while it plays and restores their volumes afterwards. The Rebind requirement was removed from the menu package it depended on.

## 0.1.0 — first release

Music player for FATE Reawakened, shown inside **Mod Settings → FATE Music Player**.

- Every location slot the game offers is listed as its own category, seeded with the game's own tracks.
- Per song: an on/off toggle, **Select song**, and **Play first 5 seconds**.
- Per category: **Add song from this PC…** (native file window) and **Reset this category**.
- **Song packs**: export your own songs as a Thunderstore-ready zip, or install someone else's pack. Packs only add rows; removing a pack removes exactly its songs and nothing else.
- Untouched slots keep vanilla behaviour, including the game's own fades and shuffle.
