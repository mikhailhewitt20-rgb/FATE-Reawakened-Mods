# FATE Aiming

Target-mode aiming for **FATE: Reawakened**. Turns the free cursor into a reticle, drives the
camera with horizontal mouse movement, and keeps movement on WASD only.

**Requires both [FATE WASD](https://thunderstore.io/c/fate-reawakened/) and FATE Camera Turn.**
Aiming mode will not load without them. [FATE Mod Menu](https://thunderstore.io/c/fate-reawakened/)
is optional and holds every reticle setting; [FATE Rebind](https://thunderstore.io/c/fate-reawakened/p/MikhailKillsZombies/Rebinds_Mod/)
is optional and only holds the key bindings. Aiming works with neither installed.

By **MikhailKillsZombies**. Standalone Windows BepInEx 5 plugin. MIT license.

## Install and remove

Import the **Thunderstore ZIP** as a local mod in your FATE: Reawakened r2modman profile, or merge
its `BepInEx` folder beside the game EXE. Install BepInExPack, FATE WASD and FATE Camera Turn
first: local imports do not install dependencies. Launch **modded**.

Uninstall through the manager, or remove only `BepInEx/plugins/FATEAim/mikhailkillszombies.FateAiming.dll`.
The config at `BepInEx/config/mikhailkillszombies.fate.aiming.cfg` does not load any code. Normal
click movement and the vanilla cursor return on the next launch.

## Aiming mode

Press the aim key (**middle mouse button** by default) to enter aiming mode:

- The cursor becomes a reticle. Aiming switches the game to its own controller-style cursor state, so
  the game's arrow and the operating-system pointer are both hidden; nothing but the reticle is drawn
  over the game.
- **Left/right mouse movement turns the camera.** The reticle stays on the vertical centre line.
- **Moving the mouse up raises the reticle** from the centre of the screen to just below the top
  edge, then stops. It never drops below the centre. The camera never tilts: the reticle reaches
  distant ground the camera cannot look at directly. Click targeting and spells follow the drawn
  reticle exactly, because the pointer stays warped onto it.
- **Clicking never walks** (see `Controls.ClickToWalk` below). Clicking a nearby NPC or enemy still
  selects, interacts or attacks as before; clicking open ground does nothing.
- **Attack and interact can be split onto different buttons.** By default one button stays
  context-sensitive, exactly like the game. Turn on `SeparateActionButtons` to give attack and
  interact their own buttons instead; both appear in FATE Rebind as **Attack (aiming)** and
  **Interact (aiming)**.
- The compass follows the aimed camera instead of snapping back.
- Opening a menu, opening the spell wheel, dragging an item, opening FATE Mod Menu's settings,
  rebinding a key in FATE Rebind or dying suspends aiming and returns the normal cursor. Aiming
  resumes when the menu closes.
- Alt-tabbing or changing scene **cancels** aiming instead of suspending it: the arrow comes back
  parked on the reticle and aiming stays off until you press the aim key again, so a hidden cursor
  can never follow you out of the game or into a loading screen.
- **A connected controller switches the whole mod off.** The reticle, the hidden cursor, the
  mouse-turned camera and the aim key all stand down, because the pad has its own cursor and camera
  controls and the two would fight over the same frame. It is the connection that counts, not
  whether the pad has been used, so a controller left plugged in keeps aiming off and one plugged
  in mid-game takes effect at once. Unplug it and aiming is available again, still off, with no
  restart. A controller that presents itself as a plain joystick counts too.

Press the aim key again (or release it, in hold mode) to return to normal controls.

## Reticle settings, in FATE Mod Menu

With **FATE Mod Menu** installed, press Escape and choose **Mod Settings**, pick **FATE Aiming**, and
you get a screen of its own — sliders, toggles, colour pickers and a live picture of the reticle:

- **Aim** — *Invert up/down aim* and *Hold the key to aim*.
- **Crosshair** — rotation, scale, opacity, **crosshair colour** (a swatch, the hex value and red,
  green and blue sliders), and a **preview** showing what the current crosshair looks like.
- **Circle** — opacity, thickness, size, **circle colour**, all reflected in the same preview.

Every slider can be dragged, and the arrows still step it for a keyboard or controller. Changes
apply immediately while aiming, and each category has a **Defaults** button.

**Circle thickness** is a share of the circle's radius, from a hairline to `1`, where the hole
closes completely and the circle becomes a solid disc.

The same values are ordinary config settings (`Reticle.*`), so they can also be edited in r2modman.
Without FATE Mod Menu this mod still works exactly the same; only the in-game screen is missing.

## Click to walk

`Controls.ClickToWalk` is **off by default**, so the mouse never moves the character. Clicks still
select, attack and interact. The mod drops the walk destination a click asks for before it reaches
the player, so attacks and interactions behave exactly as they do on the ground, and turning the
setting back on restores vanilla click-to-move immediately.

Nothing about the player's own movement state is touched, so a click the interface owns — a hotbar
slot, the spell wheel, anything in an open menu — cannot stop the player or interrupt a walk. Only
a click that was actually going to walk the character is dropped.

FATE WASD moves the player through the same game call that a click uses, so keyboard movement is
untouched: the click gate opens only after other movement mods have had their say, and a held
movement key is never mistaken for a click.

## Settings

`BepInEx/config/mikhailkillszombies.fate.aiming.cfg`, also editable in r2modman's config editor:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Aiming.Enabled` | true | Turns the whole mod off without uninstalling. |
| `Aiming.HoldToAim` | false | Unchecked: the key toggles aiming. Checked: hold the key to aim. |
| `Aiming.Binding` | `<Mouse>/middleButton` | Aim key used when FATE Rebind is not installed. |
| `Aiming.SeparateActionButtons` | false | Unchecked: one context-sensitive click. Checked: attack and interact get their own buttons. |
| `Aiming.AttackBinding` | `<Mouse>/leftButton` | Attack button when actions are separated. |
| `Aiming.InteractBinding` | `<Keyboard>/e` | Interact button when actions are separated. |
| `Aiming.MouseSensitivity` | 0.25 | Camera degrees per pixel of horizontal movement. |
| `Aiming.VerticalSensitivity` | 0.6 | Reticle pixels per pixel of vertical movement. |
| `Aiming.InvertVertical` | false | Checked: moving the mouse up lowers the reticle. |
| `Aiming.ReticleSize` | 1.0 | Crosshair scale. |
| `Reticle.Rotation` | 0 | Crosshair rotation in degrees, -180 to 180. |
| `Reticle.Opacity` | 1.0 | Crosshair opacity. |
| `Reticle.CrosshairRed` / `Green` / `Blue` | 1 / 0.86 / 0.55 | Crosshair colour, 0 to 1 per channel. |
| `Reticle.RingOpacity` | 0.6 | Circle opacity; 0 hides it. |
| `Reticle.RingThickness` | 0.1 | Circle thickness as a share of its radius, 0.01 to 1. `1` is a solid disc. |
| `Reticle.RingScale` | 1.6 | Circle size relative to the crosshair. |
| `Reticle.RingRed` / `Green` / `Blue` | 1 / 0.86 / 0.55 | Circle colour, 0 to 1 per channel. |
| `Controls.ClickToWalk` | false | Allow the mouse to walk the player. Off drops the walk a click asks for. |
| `Aiming.Diagnostics` | true | Two-second state lines in the BepInEx log. Turn off once aiming works. |

`HoldToAim`, `InvertVertical` and `ClickToWalk` are check marks in r2modman.

`Internal.BindingMigrated` and `Internal.RingThicknessMigrated` are markers, not preferences: they
record one-time conversions (the aim key moving to the middle mouse button, and the circle
thickness changing from pixels to a share of the radius). Leave them checked; clearing them would
apply that migration again on the next launch.

## Testing notes for this build (0.5.5)

Validated by compilation and package inspection; the cursor and click paths should be playtested:

1. Aim on: only the reticle is visible, with no arrow anywhere over the window; aim off: the
   arrow returns exactly on the reticle, even after a resolution change or opening and closing a
   menu, and can select something on the first frame it comes back. Alt-tab away and back: aiming
   is off and the arrow is visible and free to move.
2. Left/right turns the camera; moving the mouse up raises the reticle, moving it down lowers it to
   the centre and no further, and an enemy under the reticle is attacked, not one behind you. The
   reticle does not drift while the mouse is still. With `InvertVertical` on, the vertical
   direction flips and everything else stays the same.
3. Clicking open ground never moves the player with aiming off or on. Enabling
   `Controls.ClickToWalk` restores normal movement without a restart.
4. Clicking a nearby NPC interacts; clicking an enemy at range attacks or does nothing; a distant
   melee enemy is not walked toward.
5. Open the inventory, the spell wheel and the FATE Rebind Controls tab while aiming: the cursor
   must be visible and clickable in each, and aiming must resume after closing them. The same
   applies to the Mod Settings screen, which must show a normal cursor.
6. With `SeparateActionButtons` on, left click attacks and only attacks; the interact button talks
   to an NPC in range and does nothing out of range.
7. Spells cast at the drawn reticle, including far targets and while the reticle is raised.
8. **Pause → Mod Settings → FATE Aiming** shows Aim, Crosshair and Circle. Dragging each slider
   changes the drawn reticle immediately; the colour rows' swatch and hex update as the channel
   sliders move, and the preview matches the reticle you see when you resume. Pushing circle
   thickness to the top fills the circle in completely. **Defaults** restores every row.
9. With FATE Rebind installed, rebinding "Toggle aiming mode" still works, the aim key does not fire
   while the Controls menu is capturing, and a restart keeps the new key. Rebind no longer lists any
   reticle sliders or ticks.
10. With neither FATE Mod Menu nor FATE Rebind installed, aiming still works from the config file
    and the log reports the reticle settings were not registered anywhere.
11. Plug in a controller: aiming must stay off at once and from a fresh launch, with no reticle, no
    hidden cursor and no answer from the aim key, one log line saying a controller is connected,
    and the game's own controller cursor and camera working normally. Unplug it while the game is
    running: aiming becomes available, still off, and the aim key works as before. A controller
    that reports as a plain joystick must count too.
12. Walk with WASD and click a hotbar slot, then the spell wheel, then an open inventory: the player
    must keep walking through every one of those clicks, and clicks on open ground must still never
    start a walk — with aiming on and off.
13. With FATE Rebind not installed, `BepInEx/LogOutput.log` stays free of
    `Could not find type named ZombieGhost.Rebind.RebindApi` warnings: the optional lookups no
    longer warn about a partner mod that is not there.
14. Move on WASD with no mouse button held, and again while holding attack (separate action
    buttons off): the player keeps walking in both cases, and clicking open ground still never
    starts a walk.

## Known limits

- With `ClickToWalk` off, clicking a far target no longer approaches it; walk with WASD and click
  when in range.
- Vertical aim stops at the top edge by design. The camera pitch is untouched, matching the
  game's inability to look up.
- Not compatible with built-in co-op keyboard controls; use the
  standalone WASD and Camera Turn plugins that this package declares.

