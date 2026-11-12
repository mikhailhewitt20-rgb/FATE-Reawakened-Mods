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
