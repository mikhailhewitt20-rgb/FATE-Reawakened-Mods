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
