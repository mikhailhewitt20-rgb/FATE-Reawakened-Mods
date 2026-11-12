## 0.2.2 — the list actually registers

0.2.1 registered one row and then stopped: the preview row was added with an empty label, the menu requires a label on every row, and the resulting exception aborted the rest of the module and left that half-built module in the menu. Every row is now added independently, a row that fails is logged by reason and skipped instead of taking the list with it, and a failed registration disposes its partial module so the next attempt is not refused as "already registered".

The tick and play marks are drawn with the game's own font, which has no `✓`, `✗` or `▶` glyphs — that is why the row appeared with a blank value box. Queue membership is now an ASCII checkbox (`[x]` in the shuffle, `[ ]` left out) and playback is the row `-> 5s` with a `|>` play mark. `FateChoiceMusic` is **Fate events** again, and the `IntroFate1-4` slots are **Intro** instead of Other music.

The game only has a handful of music slots: `DungeonMusic` (dungeon1-3), `FateChoiceMusic` (fate), `MusicTempleFate2` (town1, town2) and the four `IntroFate` slots. Everything else in that list is an `Ambient…` slot — cave, palace, volcano, town and dungeon atmosphere beds — and stays out of the song list, still playing exactly as before.

## 0.2.1 — location categories, music only, ticks instead of "Select song"

Songs are grouped by where they play instead of by slot type: **Grove**, **Druantia**, **Typhon**, **Temple of Fate**, **Ekbatan**, **Chamber of Trials**, each realm's dungeon music under its own place, then **Dungeons** for slots that name no place. The `ambiance …` loops the game files in the music slots are no longer listed as songs; the game keeps playing them exactly as before. **Select song** is gone: every song now has a tick that keeps it in that place's shuffle and takes it out when unticked, so shuffling is per song rather than one forced track. Clicking a row no longer closes the menu — the module is only rebuilt when the song list itself changes, and then only after the screen is closed. The preview is an arrow instead of words, and it is audible while the menu is open: the preview source ignores `AudioListener.pause`, bypasses listener effects, borrows the listener volume when the pause screen has zeroed it, and the log records a `Preview '<track>' …` line for each press.

The song list no longer needs **Rescan installed packs** to appear: the scan re-runs until the game's slot list is actually filled, which used to happen after the one scan the mod made.

Untouched installs are byte-for-byte vanilla: every slot passes straight through to FATE until a song is unticked or one of your own files is added to it.

## 0.2.0 — settings appear in the Mod Menu, previews mute the game

This game destroys the loader's plugin object moments after startup, which stopped every Update the plugin owned, so it never reached the menu API and no module appeared. Patching, song scanning and menu registration now run from a Harmony postfix on `AudioManager.Update` plus a ticker object the mod owns, and the log reports `Registered with FATE Mod Menu: N song row(s)`. The five-second preview now mutes FATE's own music channels while it plays and restores their volumes afterwards. The Rebind requirement was removed from the menu package it depended on.

## 0.1.0 — first release

Music player for FATE Reawakened, shown inside **Mod Settings → FATE Music Player**.

- Every location slot the game offers is listed as its own category, seeded with the game's own tracks.
- Per song: an on/off toggle, **Select song**, and **Play first 5 seconds**.
- Per category: **Add song from this PC…** (native file window) and **Reset this category**.
- **Song packs**: export your own songs as a Thunderstore-ready zip, or install someone else's pack. Packs only add rows; removing a pack removes exactly its songs and nothing else.
- Untouched slots keep vanilla behaviour, including the game's own fades and shuffle.
