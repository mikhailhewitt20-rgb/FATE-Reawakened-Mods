# FATE Rebind — 0.1.2 test build

A standalone, modular keybind menu for FATE: Reawakened. No co-op mod is required.
Open Settings / Options, then the Controls screen. Its static reference image is replaced by the interactive binding editor. Back returns to Settings. Escape cancels an active capture first.

## First test: default controls only

Use an otherwise empty r2modman/Thunderstore profile containing BepInExPack
5.4.2305 and this mod. Import `FATE_Rebind-0.1.2-Thunderstore.zip` as a local mod
or install its published version when available. Launch **Start modded**.
Do not copy loader files or either co-op DLL into this profile.

The alternative BepInEx 6 archive is for an existing BepInEx 6 installation only;
it does not contain a runtime or claim a currently published loader dependency.
Never install both DLL variants in one profile.

1. Open Settings / Options → Controls and select **FATE — default controls**.
2. Search for Inventory, select its current keyboard binding, and press an unused
   key. If there is a conflict, explicitly keep both or cancel the change.
3. Return to the game and verify the new key works and the old binding no longer does.
4. Restart the game and check that the change remains.
5. Use **Reset** to restore the original binding. Test cancelling a capture too.
6. Open the menu in-game and verify clicking/typing there does not move, cast,
   attack or activate a native game menu underneath it.

## Features

- Discovers the game's native input assets: 45 actions, 110 bindings, including
  gameplay, menu and persistent controls. Composite directions are individual
  rows. Pointer position/delta tracking remains automatic.
- Search, provider groups and keyboard/mouse/controller filters.
- Click a binding to capture a new input; Remove unbinds it. Per-binding and confirmed provider resets are available.
- When the new input conflicts with exactly one compatible action, choose Swap bindings to exchange them, Keep both, or Cancel.
- Conflict confirmation respects native menu/gameplay contexts; persistent and
  mod controls are treated conservatively. No other binding is silently erased.
- Rebinds persist by stable action/binding identity. Clearing and resetting are
  distinct. An unavailable mod's saved entries are retained.
- Gameplay input is blocked while editing. The existing Settings screen retains its normal pause behaviour.
- Runtime overrides only: no edits to game assemblies or InputAction assets.
  Existing processor/interaction overrides are preserved.

Settings: `BepInEx/config/ZombieGhost.Rebind/bindings.xml` in the active profile.
The previous settings file is retained as `bindings.xml.bak` on later saves.
Reset restores the path captured before our override, preserving another mod's
deliberate default overrides rather than enabling controls it disabled.

## Modular mod support

Unity InputAction assets are discovered automatically. Mod authors can register
their own labelled groups/actions through `RebindApi`. Raw hard-coded polling
such as `Mouse.current.middleButton` cannot be changed generically: the owning
mod must use the API or a specific adapter. See `API.md` and the bridge example.

The current co-op mod is not installed, bundled, required or patched. Its
WASD implementation reads the game's `Persistent/Move` action, already included
in the default list. Its raw middle-mouse camera hold needs the provided bridge
pattern when the separate controls mod becomes available. That integration is
prepared, not claimed as tested against an unreleased standalone WASD mod.

## Status and removal

Both loader variants compile; offline storage, context, native-schema and binary
dependency checks pass. In-game capture/UI behaviour still requires user testing.
No game was launched/controlled and no official game-folder files were changed.

Close the game and remove this mod using the manager, or remove only
`BepInEx/plugins/FateRebind` from its profile. Leave the shared BepInEx runtime
alone if other mods need it. Remove the mod's settings directory separately only
if you also want to forget your rebinds. Other mods' configurations are untouched.





