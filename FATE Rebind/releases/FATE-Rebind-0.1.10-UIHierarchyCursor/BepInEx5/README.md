# FATE Rebind 0.1.10

A vanilla keyboard/mouse binding editor by MikhailKillsZombies.

## Install and test

Close the game and replace the older Rebind version in r2modman with
FATE_Rebind-0.1.10-Thunderstore.zip. Keep BepInExPack enabled. Start modded and
open Settings / Controls.

## This update

- Categories have larger bold titles, tinted bands, dividers and more spacing.
  Action labels are smaller, indented and cream-coloured.
- Primary/secondary binding selectors use ornate native pause-menu artwork.
- Edge filters, reset, back and clear buttons use subdued bordered styling with
  compact native button artwork. Selected filters have a gold edge marker.
- Native button decorations are copied as visual images only; game callbacks are
  not cloned. A solid backing prevents transparent templates becoming text-only.
- Rebind no longer sets Cursor.visible or cursor locking, and no longer forces the
  native cursor-visibility method. FATE manages its own game cursor.

Test the category hierarchy, hover/click on bindings and filters, and type in
search. Confirm there is only the game cursor while interacting with this screen
and after leaving it. The visual and cursor changes require your in-game check.

## Binding behaviour

Each vanilla action has primary/secondary inputs. Click one to rebind it; Escape
cancels. The small x clears that input. Reset restores both inputs on the row.
Swap exchanges bindings when exactly one compatible action conflicts. Reset group
requires confirmation. Provider, category and action lists scroll.

Only usable vanilla keyboard/mouse bindings appear by default. Dormant WASD
walking and internal UI actions are hidden. Camera WASD keys retain their native
camera meaning. No movement mod is included; future movement integration requires
both Rebind and the actual movement mod. Other mod controls require explicit API
registration. F10 has no menu and can be bound normally.

## Settings and verification

Existing settings remain at BepInEx/config/ZombieGhost.Rebind/bindings.xml for
compatibility. Secondary binding IDs and primary preferences remain unchanged.
No game asset or assembly files are edited.

Both loader builds and offline tests pass, including the 32 vanilla action pairs,
secondary ID stability, camera-axis structure, device policy, startup lifecycle,
button-role contracts and absence of cursor API calls in the plugin. Gameplay
confirmation remains with the user; no game was launched or controlled.

API details and a generic example are included. No loader or other mod is bundled.
Uninstall through r2modman with the game closed; keep the config folder if you want
your saved assignments retained.
