# FATE Quick Spell

Direction-based spell wheel for **FATE: Reawakened**. Hold the wheel key, flick the mouse towards
the spell you want, let go: that spell becomes active. Nothing is clicked, and no cursor is shown
while the wheel is up.

The game's wheel is left exactly as it is: no slot is moved, resized or added, the icons stay where
the game drew them, and the mod adds no entry of its own to any menu. Only the way a spell is
picked changes.

By **MikhailKillsZombies**. Standalone Windows BepInEx 5 plugin. MIT license.

## Install and remove

Import the **Thunderstore ZIP** as a local mod in your FATE: Reawakened r2modman profile, or merge
its `BepInEx` folder beside the game EXE. Install BepInExPack first. Launch **modded**.

Uninstall through the manager, or remove only
`BepInEx/plugins/FATEQuickSpell/mikhailkillszombies.FateQuickSpell.dll`. The config at
`BepInEx/config/mikhailkillszombies.fate.quickspell.cfg` does not load any code; the vanilla wheel
returns on the next launch.

## The wheel

The game's own wheel waits for you to move the cursor onto a slot and click it. This mod replaces
that with a direction:

- **Hold the spell wheel key** as usual. The wheel opens, the game's arrow is hidden and the
  operating-system pointer is hidden and pinned to the middle of the screen.
- **Flick the mouse towards a slot.** The highlighted slot is the one lying in the direction the
  mouse is moving â€” the direction of the last movement, not where a cursor sits. A short travel
  memory (about a quarter second) means the pick follows what you are doing now, and it survives
  the pointer reaching a window edge because the pointer is put back in the middle each frame.
- **Release the key.** The highlighted spell is activated, exactly as if its slot had been
  clicked. The pointer reappears where it was before the wheel opened.
- **Release without flicking** and nothing changes: the wheel opens on the spell that is already
  active.
- **Empty slots cannot be picked.** Aiming at one keeps the previous selection.
- **A controller keeps the game's own wheel.** With a game pad the game already picks from the
  stick, so this mod stays out of it entirely. The left stick keeps walking while the wheel is up;
  the game normally freezes you while any wheel is open. Walking is only restored when the spell
  wheel is the one thing blocking input: a menu, a pause or a locked movement still stops you.
- **Only a release picks a spell.** Closing the wheel any other way â€” a menu, death, a scene
  change, alt-tabbing away â€” restores the pointer and changes nothing.

The wheel's own highlights are used, so the selected slot lights up exactly as it does for a hover
or a game pad.

## Settings

`BepInEx/config/mikhailkillszombies.fate.quickspell.cfg`, also editable in r2modman's config
editor:

| Setting | Default | Meaning |
| --- | --- | --- |
| `General.Enabled` | true | Turns the whole mod off without uninstalling; the game's own click-to-select wheel returns. |
| `Wheel.FlickPixels` | 28 | How far the mouse must travel, in pixels, before a direction is chosen. Lower picks a spell sooner; higher needs a more deliberate flick. |
| `Wheel.Diagnostics` | true | Logs each hold, each highlight and the spell that was picked. Turn off once it works. |

## If the wheel does not change

The mod says what it is doing in `BepInEx/LogOutput.log`, so a hold can be followed step by step:

| Line | Meaning |
| --- | --- |
| `driver running.` | The per-frame part is alive. Without it the mod still picks, but it cannot notice a wheel closed by something other than the key. |
| `driver attached.` | The driver was created, or put back after a scene load. |
| `a controller is connected; the mod stays out of the wheel, but the left stick still walks.` | A game pad is plugged in, so the mod leaves the game's own stick wheel alone; the left stick still walks while the wheel is open. |
| `wheel key held with the mod switched off.` | `General.Enabled` is false. |
| `wheel: travel=â€¦px needs â€¦, slots=â€¦, spells=â€¦` | The hold is live and the flick is being measured (logged twice a second with `Diagnostics` on). |
| `wheel: walking is free (stick 0.85).` | The left stick is being read while the wheel is open, so the player can walk (logged at most once a second with `Diagnostics` on). |
| `Spell wheel: slot N (â€¦).` | A slot was picked; releasing commits it. |
| `FATE Aiming found: its cursor handling stands down while the wheel is held (4 hooks).` | FATE Aiming is installed and will not hand the cursor back during a hold. |
| `FATE Aiming is not installed; the wheel hides the cursor on its own.` | Nothing else touches the cursor, so the mod hides it alone. |

## Known limits

- The pick needs a direction, so a pure click on a slot no longer picks anything while the mod is
  on; turn `General.Enabled` off for the vanilla wheel.
- Slots are matched to the directions they are drawn in, so the mod follows the wheel wherever the
  game puts the icons, but an empty slot in the direction you aimed at blocks that direction rather
  than falling through to the next one.
- The wheel holds the game's own six spells; the mod does not add slots.
- Aiming is not needed; the mod works with or without FATE Aiming installed. When FATE Aiming is
  present, its cursor handling stands down for as long as the wheel is held, so the pointer stays
  hidden instead of being handed back when Aiming suspends itself for a menu; with Aiming absent or
  idle, nothing about the cursor changes.
