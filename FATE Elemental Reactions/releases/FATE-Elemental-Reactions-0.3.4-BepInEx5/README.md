# FATE Elemental Reactions 0.3.4

Standalone elemental combat mod for FATE: Reawakened. It contains no co-op implementation or dependency.

## Elements and testing

- Existing Ruby gems apply flat Fire damage (Pyro).
- Existing Opal gems apply flat Ice damage (Cryo).
- Existing Turquoise gems apply flat Electrical damage (Electro).
- Adds tier-matched Citrine (Geo), Cyanite (Anemo), and Aquamarine (Hydro) socket gems. Their inventory icons are recolored yellow, cyan, and deep blue; each inherits depth and rarity from the matching existing gem tier. A one-shot command in this package grants the variants to the loaded character.
- Gem descriptions show weapon damage and corresponding armor resistance.
- Element indicators appear above innately elemental monsters and entities that have been hit by an element. Two applied elements can be shown at once; their auras expire in four seconds.
- Pyro + Cryo triggers **Melt** (2x Pyro-on-Cryo, 1.5x Cryo-on-Pyro damage); Pyro + Electro triggers **Overloaded** splash damage; Cryo + Electro triggers **Superconduct**, lowering physical resistances briefly. Reaction names float above the affected target.
- FATE's native damage numbers remain visible and are tinted by the active weapon element. Physical damage stays white, Geo uses yellow, Anemo cyan, and Hydro deep blue. Other native floaties remain unchanged.
- Training targets automatically spawn in the Grove field south of the dungeon portal. **F8** can request them again; it uses the game's Unity Input System.
Pyro, Cryo, and Electro retain their current reaction behavior. Geo, Anemo, and Hydro gems apply their element tint and target indicator; their reaction behaviors can be added in a later update.

## Install

Import the zip as a local package in r2modman or Thunderstore Mod Manager for FATE: Reawakened. Requires BepInExPack 5.4.2305. Launch the modded profile and load an existing character.
