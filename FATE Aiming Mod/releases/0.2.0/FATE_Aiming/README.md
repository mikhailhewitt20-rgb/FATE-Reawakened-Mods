# FATE Aiming

Target-mode aiming for **FATE: Reawakened**. Turns the free cursor into a reticle, drives the
camera with horizontal mouse movement, and keeps movement on WASD only.

**Requires both [FATE WASD](https://thunderstore.io/c/fate-reawakened/) and FATE Camera Turn.**
Aiming mode will not load without them. [FATE Rebind](https://thunderstore.io/c/fate-reawakened/p/MikhailKillsZombies/Rebinds_Mod/)
is optional; when it is installed the aim key appears in its Controls menu as
**Toggle aiming mode**.

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

- The cursor becomes a reticle; the game's arrow cursor and the operating-system pointer are hidden.
- **Left/right mouse movement turns the camera.** The reticle stays on the vertical centre line.
- **Moving the mouse up raises the reticle** from the centre of the screen to just below the top
  edge, then stops. It never drops below the centre. The camera never tilts: the reticle reaches
  distant ground the camera cannot look at directly. Click targeting and spells follow the drawn
  reticle exactly, because the pointer stays warped onto it.
- Prefer the opposite vertical direction? Turn on `Aiming.InvertVertical` and moving the mouse up
  lowers the reticle instead. Nothing else changes. With **FATE Rebind 0.1.18 or later** installed
  the same switch is a tick in the Controls list: **FATE Aiming / Aim options → Invert up/down
  aim**. It works in both the keyboard/mouse and controller views, and changes apply at once.
- **Clicking never walks** (see `Controls.ClickToWalk` below). Clicking a nearby NPC or enemy still
  selects, interacts or attacks as before; clicking open ground does nothing.
- **Attack and interact can be split onto different buttons.** By default one button stays
  context-sensitive, exactly like the game: attack an enemy, interact with an NPC. Turn on
  `SeparateActionButtons` to give attack and interact their own buttons instead; both appear in
  FATE Rebind as **Attack (aiming)** and **Interact (aiming)**.
- The compass follows the aimed camera instead of snapping back.
- Opening a menu, opening the spell wheel, dragging an item, rebinding a key in FATE Rebind or
  dying suspends aiming and returns the normal cursor. Aiming resumes when the menu closes.
- Alt-tabbing or changing scene **cancels** aiming instead of suspending it: the arrow comes back
  parked on the reticle and aiming stays off until you press the aim key again, so a hidden cursor
  can never follow you out of the game or into a loading screen.

Press the aim key again (or release it, in hold mode) to return to normal controls.

## Click to walk

`Controls.ClickToWalk` is **off by default**, so the mouse never moves the character. Clicks still
select, attack and interact. The mod applies the game's own stand-still rule — the one bound to
**Left Shift** — for the duration of each click, so attacks and interactions behave exactly as they
do while holding stand-still in the unmodded game, and turning the setting back on restores vanilla
click-to-move immediately.

This is a normal config setting: open r2modman's **Config editor** for the profile, choose
**FATE Aiming**, and tick **Click to walk** under the **Controls** section. It applies without a
restart.

## Settings

`BepInEx/config/mikhailkillszombies.fate.aiming.cfg`, also editable in r2modman's config editor:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Aiming.Enabled` | true | Turns the whole mod off without uninstalling. |
| `Aiming.HoldToAim` | false | Unchecked: the key toggles aiming. Checked: hold the key to aim. |
| `Aiming.Binding` | `<Keyboard>/leftAlt` | Aim key used when FATE Rebind is not installed. |
| `Aiming.SeparateActionButtons` | false | Unchecked: one context-sensitive click. Checked: attack and interact get their own buttons. |
| `Aiming.AttackBinding` | `<Mouse>/leftButton` | Attack button when actions are separated. |
| `Aiming.InteractBinding` | `<Keyboard>/e` | Interact button when actions are separated. |
| `Aiming.MouseSensitivity` | 0.25 | Camera degrees per pixel of horizontal movement. |
| `Aiming.VerticalSensitivity` | 0.6 | Reticle pixels per pixel of vertical movement. |
| `Aiming.InvertVertical` | false | Checked: moving the mouse up lowers the reticle. Also a tick in FATE Rebind's Controls list. |
| `Aiming.ReticleSize` | 1.0 | Reticle scale. |
| `Controls.ClickToWalk` | false | Allow the mouse to walk the player. Off uses the game's stand-still rule. |
| `Aiming.Diagnostics` | true | Two-second state lines in the BepInEx log. Turn off once aiming works. |

`HoldToAim`, `InvertVertical` and `ClickToWalk` are check marks in r2modman.

`Internal.BindingMigrated` is a marker, not a preference: it records that an existing config's
aim key was moved from Left Alt to the middle mouse button once. Leave it checked; clearing it
would apply that migration again on the next launch.

## Testing notes for this build (0.2.0)

Validated by compilation and package inspection; the cursor and click paths should be playtested:

1. Aim on: reticle replaces the arrow; aim off: the arrow returns exactly on the reticle, even
   after a resolution change or opening and closing a menu. Alt-tab away and back: aiming is off
   and the arrow is visible.
2. Left/right turns the camera; moving the mouse up raises the reticle, moving it down lowers it to
   the centre and no further, and an enemy under the reticle is attacked, not one behind you. The
   reticle does not drift while the mouse is still. With `InvertVertical` on, the vertical
   direction flips and everything else stays the same.
3. Clicking open ground never moves the player with aiming off or on. Enabling
   `Controls.ClickToWalk` restores normal movement without a restart.
4. Clicking a nearby NPC interacts; clicking an enemy at range attacks or does nothing; a distant
   melee enemy is not walked toward.
5. Open the inventory, the spell wheel and the FATE Rebind Controls tab while aiming: the cursor
   must be visible and clickable in each, and aiming must resume after closing them.
6. With `SeparateActionButtons` on, left click attacks and only attacks; the interact button talks
   to an NPC in range and does nothing out of range.
7. Spells cast at the drawn reticle, including far targets and while the reticle is raised.
8. With FATE Rebind installed, rebinding "Toggle aiming mode" works, the aim key does not fire while
   the Controls menu is capturing, and a restart keeps the new key.
9. In FATE Rebind's Controls tab, **FATE Aiming / Aim options** shows **Invert up/down aim** with a
   tick. Ticking it flips vertical aim immediately, unticking restores it, and the choice survives a
   restart. With a fresh config the aim key starts as the middle mouse button.

## Known limits

- With `ClickToWalk` off, clicking a far target no longer approaches it; walk with WASD and click
  when in range.
- Vertical aim stops at the top edge by design. The camera pitch is untouched, matching the
  game's inability to look up.
- Not compatible with the original PartGhost co-op bundle's built-in keyboard controls; use the
  standalone WASD and Camera Turn plugins that this package declares.
