## 0.3.9 — the title screen plays from Intro Music

- **The main menu is now controlled by the category.** `MainMenuUI.Start` starts exactly one intro slot chosen by Fate version (`IntroFate1`–`IntroFate4`) and layers an ambience clip on top, so editing any row other than the one your version picked could never be heard. The mod now hooks that call: once anything in **Intro Music** is edited, the screen plays a pick from that category instead of the game's fixed choice.
- **An added song wins.** If the category has a song you added, that is what the title screen plays. With no added song, it picks among the intro rows still switched on, so turning rows off changes the title music and turning them all off silences it.
- Untouched, the title screen is exactly vanilla, and the log says `Title music: <name> (slot <n>)` whenever the mod takes it over.
- The library now logs one line per heading with its row count and slot numbers, so a row that never plays can be traced from `BepInEx/LogOutput.log`.

## 0.3.8 — the character-select intro has its own category

- **Intro Music.** The intro that plays while a new character is made uses its own slots (`IntroFate1`–`IntroFate4` and `IntroAmbientWoods`). Their names carry no "Music", which is what the menu used to decide something was a soundtrack slot, so they were skipped and there was nothing to toggle. They now appear as **Intro Music**, listed above Town, with the same preview, on/off and loop timer as every other slot.
- Untouched, the intro plays exactly as the game ships it. Turn a row off and that phase of the intro stays silent. A song added to the category joins its first slot (`IntroFate1`), which is the phase that plays first, because the intro's slots are played one after another.

## 0.3.7 — packs installed through a mod manager are found

- **Packs installed with r2modman or Thunderstore Mod Manager now load.** Both managers give every package its own folder under `plugins`, so a pack installed that way lands in `BepInEx/plugins/<Package>/FateMusicPlayer/packs/`, while the player only looked beside its own DLL. It now reads its own `packs` folder, `BepInEx/plugins/FateMusicPlayer/packs`, and every `BepInEx/plugins/*/FateMusicPlayer/packs`, with each pack read once even if it turns up in more than one of them.
- **The log says where it looked.** Every scan now prints `Looking for song packs in <folder>`, then `Pack <id>: N song(s) from <folder>`, or a warning when a pack folder has nothing playable, so a pack that does not appear can be traced without guessing.
- Hand-copied packs are unchanged: a folder dropped into `BepInEx/plugins/FateMusicPlayer/packs/` still works.

## 0.3.6 — one row per song, and removing a song keeps the menu open

- **A song belongs to a category once.** The same file could be imported twice into one category, leaving two rows of one track: heard twice at once, with the loop timer applied to both. A second import now replaces the first — the older entry and the settings saved for it are deleted — and duplicates already in `library.json` are cleaned out the next time the list is built, keeping the newest import.
- **Removing a song no longer closes the settings screen.** Removing a song re-registered the whole module, and the menu only draws settings for a module that is still alive, so it dropped the player back to the module list. The row is now taken out of the module in place: the rest of the page stays exactly as it was, and the song list is trimmed without being rebuilt, so the rows on screen keep working.

## 0.3.5 — dungeon songs play, packs export what you tuned

- **The Dungeon Music rows now decide what a dungeon plays.** FATE's dungeons never call `PlayMusic`: `LevelGenerator.PlayDungeonMusic` fades the manager's channels and then plays the floor's own clip list on `LevelGenerator`'s own source, so turning a dungeon song off changed a list the game never read. That call is now hooked. Once anything in the dungeon slot is edited — a row turned off, one selected, or a file added — the mod silences the floor's source and takes the slot over, so turned-off rows stay silent, added and pack songs play, and the loop timer applies there. An untouched dungeon slot keeps the floor's vanilla track.
- **A song chosen for the village no longer follows you into the dungeon.** The game stops its music through `AudioManager.StopMusic`, which only fades the manager's own channels, so a track running on the mod's loop sources kept playing over the dungeon. `StopMusic` and `ForceStopMusic` are hooked now, and a slot the mod does not serve ends the loop player as well.
- **Exported packs carry the values you tuned.** The exporter used to read the entry saved when a file was added, so every `loopSeconds` came out `0.0`. It now reads the live rows, leaves out songs whose file has moved instead of listing tracks the package does not contain, keeps two same-named files apart, and reopens the finished zip to check `manifest.json`, `README.md` and `pack.json` before reporting success.
- The loop timer reaches 10 seconds in 0.1s steps, and the song counts and skip counts are reported when an export finishes.

## 0.3.4 — registration restored

- **The mod is listed again.** 0.3.3 started its ticker and its Harmony hook from inside `Tick`, so nothing was ever ticked: no library scan, no registration with the menu, no music interception. Both drivers are started explicitly at load again.
- Previews, the preview logging and the singleton-safe `AudioManager` read from 0.3.3 are unchanged.
- The game's tracks are rescanned until they appear, which matters on loads where the audio manager exists before its music list is filled in.

## 0.3.3 — previews play, added songs join their category

- **The five-second preview now plays.** The audio source was created during the plugin's `Awake`, while Unity was still starting up; an object made that early does not survive into the game, so every preview returned before it could log anything. The source is now created on the first frame the game is really running, and each press logs `Preview asked for …` followed by the clip name and its length.
- **The mod no longer breaks the game's own AudioManager.** Reading `AudioManager.Instance` during `Awake` made FATE build that singleton before it was ready, and its `Init()` threw a NullReferenceException at launch (it is in `Player.log`). The cached singleton field is read instead, so nothing is created early.
- **Added songs join their location's category.** An added song used to be filed under a second heading of its own ("Town" beside "Town Music"); its saved slot is resolved to the playlist it belongs to, so the row appears inside the existing group.
- Requires FATE Mod Menu 0.4.1 or newer for the one-row-per-song layout; the older three-row layout still works.

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
