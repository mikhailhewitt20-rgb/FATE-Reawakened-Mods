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
The config at `BepInEx/config/mikhailkillszombies.fate.aiming.cfg` does not load any code. Normal click
movement and the vanilla cursor return on the next launch.

## Aiming mode

Press the aim key (**Left Alt** by default) to enter aiming mode:

- The cursor becomes a reticle and the operating-system cursor is hidden.
- **Left/right mouse movement turns the camera.** The reticle stays on the vertical centre line.
- **Up/down mouse movement raises the reticle** from the centre of the screen to just below the
  top edge, then stops. It never drops below the centre. The camera never tilts: the reticle
  reaches distant ground the camera cannot look at directly.
- **Clicking never walks.** Clicking a nearby NPC or enemy still selects, interacts or attacks as
  before; clicking open ground does nothing. Walking is WASD only, so a melee target has to be
  closed on foot.
- **Attack and interact can be split onto different buttons.** By default one button stays
  context-sensitive, exactly like the game: attack an enemy, interact with an NPC. Turn on
  `SeparateActionButtons` to give attack and interact their own buttons instead; both appear in
  FATE Rebind as **Attack (aiming)** and **Interact (aiming)**.
- The compass follows the aimed camera instead of snapping back.
- Opening a menu, dragging an item or dying suspends aiming and restores the normal cursor. It
  resumes when the game does.

Press the aim key again (or release it, in hold mode) to return to normal controls.

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
| `Aiming.ReticleSize` | 1.0 | Reticle scale. |

`HoldToAim` is a check mark switch in r2modman: ticked means hold-to-aim, cleared means
toggle-to-aim.

## Testing notes for this build

This is a 0.1.0 test build, validated by compilation and package inspection only. It has not been
playtested in game. Please check:

1. Reticle appears and the arrow cursor disappears on toggle; both return on toggle off.
2. Left/right mouse turns the camera smoothly; up/down raises and lowers only the reticle, and it
   stops at the top edge and at the centre.
3. Clicking open ground never moves the player, including with a target already selected.
4. Clicking a nearby NPC interacts; clicking an enemy at range attacks; a distant melee enemy is
   not walked toward.
5. With `SeparateActionButtons` on, left click attacks and only attacks; the interact button talks
   to an NPC in range and does nothing out of range; neither walks the player. Rebind both under
   *Combat* in FATE Rebind, confirm the new keys work, and confirm a restart keeps them.
6. Spells cast at the reticle, including far targets.
7. Menus, inventory drag, fishing and death suspend aiming correctly, and it resumes afterwards.
8. With FATE Rebind installed, rebinding "Toggle aiming mode" works and survives a restart.
9. Toggling `HoldToAim` and restarting changes the input style.

## Known limits

- Movement is locked to WASD while aiming, so click-to-loot and click-to-talk from a distance no
  longer approach the target; stand next to it first.
- Vertical aim stops at the top edge by design. The camera pitch is untouched, matching the
  game's inability to look up.
- Not compatible with the original PartGhost co-op bundle's built-in keyboard controls; use the
  standalone WASD and Camera Turn plugins that this package declares.
