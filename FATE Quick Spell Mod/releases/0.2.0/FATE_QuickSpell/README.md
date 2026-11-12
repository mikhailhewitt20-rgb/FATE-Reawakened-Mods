# FATE Quick Spell

Direction-based spell wheel for **FATE: Reawakened**. Hold the wheel key, flick the mouse towards
the spell you want, let go: that spell becomes active. Nothing is clicked, and no cursor is shown
while the wheel is up. The wheel can also hold more than the game's six spells, up to all
eighteen.

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
  mouse is moving — the direction of the last movement, not where a cursor sits. A short travel
  memory (about a quarter second) means the pick follows what you are doing now, and it survives
  the pointer reaching a window edge because the pointer is put back in the middle each frame.
- **Release the key.** The highlighted spell is activated, exactly as if its slot had been
  clicked. The pointer reappears where it was before the wheel opened.
- **Release without flicking** and nothing changes: the wheel opens on the spell that is already
  active.
- **Empty slots cannot be picked.** Aiming at one keeps the previous selection.
- **A controller keeps the game's own wheel.** With a game pad the game already picks from the
  stick, so this mod stays out of it entirely.
- **Only a release picks a spell.** Closing the wheel any other way — a menu, death, a scene
  change, alt-tabbing away — restores the pointer and changes nothing.
- **Alt-tabbing or losing focus** during a hold cancels it and gives the pointer back.

The wheel's own highlights are used, so the selected slot lights up exactly as it does for a hover
or a game pad.

## More spells on the wheel

The game draws six slots. `Wheel.Slots` (or the **Spells on the wheel** slider in FATE Mod Menu)
raises that to anywhere between 6 and 18:

- The ring is rebuilt with room for the count you choose. The wheel group grows and the icons
  shrink so they stay clear of one another; at 6 nothing moves at all.
- The spellbook's **Quick Menu** ring grows with it, so the extra slots can be filled the usual
  way: right-click a spell, then click the slot. Assignments, tooltips and the assign markers all
  work on the added slots.
- Slots are saved with the rest of your spells, and a wheel longer than the game's own six loads
  back as it was.
- **F1 to F6 keep their keys and their badges.** The added slots have no badge and no key: they
  are reached through the wheel, which is why nothing in FATE Rebind has to change.
- Lowering the count hides the extra slots without losing what was on them; raising it again
  brings them back. A change takes effect the next time the wheel or the spellbook is opened.

## Settings

`BepInEx/config/mikhailkillszombies.fate.quickspell.cfg`, also editable in r2modman's config
editor:

| Setting | Default | Meaning |
| --- | --- | --- |
| `General.Enabled` | true | Turns the whole mod off without uninstalling; the game's own click-to-select wheel returns. |
| `Wheel.Slots` | 6 | How many spells the wheel holds, 6 to 18. The ring grows to fit and the icons shrink; the game's own six keep their layout and keys. |
| `Wheel.FlickPixels` | 28 | How far the mouse must travel, in pixels, before a direction is chosen. Lower picks a spell sooner; higher needs a more deliberate flick. |
| `Wheel.Diagnostics` | true | Logs each hold, each highlight and the spell that was picked. Turn off once it works. |

## Testing notes for this build (0.2.0)

Validated by compilation only; the wheel path and the grown ring should be playtested:

1. Hold the wheel key and release without moving the mouse: the wheel opens with the current spell
   highlighted and nothing changes; the pointer comes back where it was.
2. Hold, flick up/right/down/left, release: the slot drawn in that direction is highlighted and
   becomes the active spell. Repeat around the whole ring, including slots next to each other, and
   check the highlight does not flicker while the mouse is held between two of them.
3. No arrow and no operating-system pointer must be visible while the wheel is up, and the pointer
   must not travel to the screen edge no matter how hard the mouse is flicked.
4. Empty slots: aim at one with fewer spells than slots; the last valid spell stays highlighted and
   releasing keeps the current spell.
5. Open the wheel and close it with a menu or by dying instead of releasing: no spell change, and
   the pointer is visible and free.
6. With a game pad, the wheel behaves exactly as the unmodded game (stick picks, no hidden pointer
   from this mod).
7. At the default 6 the wheel looks and behaves exactly as in 0.1.0: same positions, same badges.
8. Set the slider to 18 and open the wheel: eighteen slots around the ring, icons clear of each
   other, no key badges on the added ones. The spellbook's Quick Menu shows the same eighteen, and
   right-clicking a spell then clicking one of the new slots assigns it.
9. With several of the new slots filled, hold and flick towards one of them and release: that
   spell becomes active, and it is still there after a save and reload.
10. Set the count back to 6: the wheel shows six again, and the spells that were on the hidden
    slots come back if you raise the count again.
11. With the wheel set to 18 and more than six spells assigned, learn a new spell: it fills the
    first empty slot rather than stopping at six.

## Known limits

- The pick needs a direction, so a pure click on a slot no longer picks anything while the mod is
  on; turn `General.Enabled` off for the vanilla wheel.
- Slots are matched to the directions they are drawn in, so a wheel layout change is followed
  automatically, but an empty slot in the direction you aimed at blocks that direction rather than
  falling through to the next one.
- The grown ring reuses the game's own slot art. The key badges on the added slots are hidden by
  name; if a future patch renames them, the badges may reappear on slots with no key.
- Aiming is not needed; the mod works with or without FATE Aiming installed.
