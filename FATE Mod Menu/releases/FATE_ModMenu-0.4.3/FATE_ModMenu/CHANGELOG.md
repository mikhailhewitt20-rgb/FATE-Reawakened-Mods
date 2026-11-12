# 0.4.3

- **Text rows (API v4).** `RegisterText` adds a single-line value such as a display name. Click the chip to type, **Ctrl+V** pastes, **Enter** applies and **Escape** cancels. The field reads the Input System directly, like the colour code field, so it works in FATE without the legacy input manager.
- Text rows cap their value at the registered `maxLength` (default 24, maximum 64) and keep the list from rebuilding while the field is open.

# 0.4.2

- **Small row icons act on the press, not the release.** Writing a value redraws the settings list, and that destroys the row under the pointer before the click is released, so a press that started between the two did nothing. Play, stop and the on/off box now fire immediately and can no longer look dead.
- The row's play triangle, stop square and remove cross are unchanged; this only changes when they respond.

# 0.4.1

- **Drawn icons.** The game's font has no play, cross or tick character, so the typed glyphs in the first v3 build rendered as empty boxes and the row looked broken. The play triangle, the stop square and the remove cross are now generated sprites, and the on/off box is filled gold when it is on instead of drawing a tick.
- **`RegisterTrackRemoveAction(trackId, action)`** puts a cross beside a track's play button, for songs the user installed and may want removed again.
- **Selection is gone from media rows.** The Select/Selected chip never did anything; the two getter/setter arguments stay in `RegisterTrack` so existing callers keep compiling, and are ignored. Pass `null, null`.
- **A drag can no longer freeze the screen.** The row's drag flag is now per row and cleared on release, and the loop value is no longer part of the change check, so moving the timer redraws in place instead of rebuilding the list under the pointer. A stuck flag had stopped every redraw, which made the buttons look dead.
- ASCII arrows (`<` / `>`) are used instead of the font's triangle characters, which the game's font also lacks for some sizes.

# 0.4.0

- **Media rows (API v3).** A mod can now register a song-style row: a play/stop button, the title, a **Select** chip, an on/off check box and a loop-time slider with arrows and a draggable track. FATE Music Player uses it, and the row drew the need for it: three settings per track never fitted a screen.
- **Wide action bars.** `RegisterWideAction` draws the usual button as a bar across the row, for the one command a list is built around such as **ADD SONG**.
- **Per-heading Defaults.** A heading's **Defaults** now resets only that heading. It used to reset the whole mod, which made a category reset wipe every other category too. Mods can register `SetResetCallback` and `SetCategoryResetCallback` so state their rows do not own, such as a pinned song or a running preview, is cleared with them.
- Values changed through the API no longer need a re-registration to redraw; the menu still redraws itself on change.

# 0.3.4

- **The wheel scrolls the right way.** Each notch now moves the list the way the wheel is turned: forward reveals the settings above, back reveals the ones below. Every mod's screen shares this handler, so FATE Aiming's reticle settings scroll correctly too.

# 0.3.3

- **Colour codes are editable.** Click the hex chip on a colour row and the field opens: type `#B76063`, press **Ctrl+V** to paste one, **Enter** applies it and **Escape** cancels. The footer says whether it was accepted, and the swatch, sliders and any preview follow at once.
- **Copy** beside the chip puts the current code on the clipboard.
- The field reads the keyboard from the Input System, so it works in a game that never enables the legacy input manager — a plain text field would not.
- **Tighter list, clearer sections:** rows are about 15% shorter and category bands are taller with more space around them, so more settings fit on screen and the groups are easier to tell apart.

# 0.3.2

- **Wheel scrolling is half as fast again**, about half a row per notch.
- **Paste a colour code.** On a colour row, copy something like `#B76063` and press the hex chip: the colour is applied, and a rejected or empty clipboard is reported in the footer instead of failing silently. API v2 gains `ModColor.SetHex` and `ModColor.TryParseHex` for mods that want to read a code themselves.

# 0.3.1

- **Nothing overlaps any more.** Every row keeps a gutter at both ends for its arrows, so a label or value can no longer be drawn underneath a button, and rows are tall enough for the label line and the track to sit apart.
- **The preview is pinned beside the list** instead of scrolling with it, so the picture stays in view however far down the settings you are. Mods without a preview keep the full width.
- **The wheel scrolls about one row per notch.** It used to move several screens at once because the list multiplied its sensitivity by the legacy input path's 120-unit deltas.
- Opening a mod or going back starts that list at the top; changing a value leaves the list where it was.

# 0.3.0

- **Two steps, like the game's own menus:** Mod Settings now lists the installed mods first, and choosing one opens that mod's own screen. Escape or **Back** climbs one step at a time.
- **Real sliders:** every numeric row has a track you can click or drag, with a fill and handle, plus the arrows for a keyboard or controller. Wide ranges are no longer hundreds of key presses.
- **Colour rows:** a swatch, the hex value, and one slider per channel. Mods hand over a colour; the menu writes back a full value in 8-bit steps.
- **Preview rows:** a mod can show a picture it draws from its own settings, such as what the aiming reticle looks like right now.
- Slider values now snap to the step grid, so a config does not collect values like `0.2313726`.
- API v2 adds `RegisterColor` and `RegisterPreview`; v1 calls still work unchanged.

# 0.2.0

- **The plugin no longer depends on its own object surviving.** The game destroys the loader's plugin object moments after it is created (`MOD_MENU_DESTROYED` appeared right after startup in every earlier build), which is why nothing ever ticked and no patch ever fired. All state is now static and the work runs from Harmony postfixes on the game's own methods.
- The `Mod Settings` row is added from a `PauseUI.Awake` postfix, so it exists the first time the window is built, and is re-fitted on every pause-open.
- F10 is read from an `InputBroker.ManagedTick` postfix — the game's own input tick — instead of a plugin `Update`, and the settings screen is ticked from there too.
- Every step still logs, including `MOD_MENU_PAUSE_AWAKE`, `MOD_MENU_BUTTON_INSTALLED` and `MOD_MENU_HOTKEY`, so a remaining failure names itself.

# 0.1.5

- **The screen and the hotkey now run from the plugin's own object**, kept across scene changes. In this game BepInEx's manager object stops ticking after the first scene loads, which is why 0.1.3 and 0.1.4 logged nothing at all after `Awake` — no hotkey, no pause hook, no button.
- The `Mod Settings` row is repaired once a second until it exists, so it appears even if the pause hooks never fire, and reappears after a scene change.
- Shutting the plugin down no longer removes its Harmony patches, which is what silenced the previous builds.

# 0.1.4

- The **Mod Settings** row is now added when the pause UI starts rather than only after the pause key reaches `PauseUI.OnMenuButton`, so the button is present the first time the pause menu is opened.
- Each step now logs, so a missing button or a dead hotkey reports whether the pause UI was found, whether the row was cloned, the native and grown window sizes, and what the hotkey saw (keyboard, pause instances, canvas).

# 0.1.3

- **F10 now opens the game's own pause menu first**, with the settings screen on top, so the cursor, the paused state and button focus are the game's rather than assumed. Pressing F10 while the pause menu is already up opens the screen directly, and F10 again closes it. Scenes without a pause menu fall back to pausing and showing the cursor directly.
- The Mod Settings row is re-fitted on every pause-menu open, measured from the native window height, so it is no longer clipped or added twice after the window resizes.
- The settings screen can now attach to a camera-space canvas, not only an overlay canvas, so F10 also works in scenes whose UI is drawn through a camera.
- The hotkey reads both the new Input System and, when the game enables it, legacy input.

# 0.1.2

- **F10** opens the settings screen from anywhere in a loaded game and pauses the game while it is up. This gives the screen a way in even when the pause-menu button does not appear.
- The pause-menu button path now logs each step, so a missing button reports whether the pause menu fired, whether the options button was found, and what the cloned row and its container look like.

# 0.1.1

- Verified the pause-menu button in game: it appears directly under Options and opens the settings screen.
- Rebind is now a soft dependency, so the screen still loads when Rebinds Mod is missing or older. Thunderstore still installs both together.
- Package manifest is written without a byte-order mark and the archive uses forward-slash paths, so it imports cleanly on every platform.

# 0.1.0

First release.

- Adds a **Mod Settings** button to the pause menu, directly under Options, using the game's own button art and navigation.
- Module registry: mods register toggles, sliders, choices, action buttons and notes, grouped by mod and category.
- Per-section **Defaults** button restores the values each mod reported at registration.
- Keyboard/mouse and controller navigation; Escape or gamepad B closes the screen and leaves the pause menu open.
- Bindings are not duplicated here; remapping stays in Options > Controls through Rebinds Mod.
