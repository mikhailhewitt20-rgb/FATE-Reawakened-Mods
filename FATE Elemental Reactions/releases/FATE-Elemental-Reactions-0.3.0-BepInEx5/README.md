# FATE Elemental Reactions 0.3.1

Standalone elemental combat mod for FATE: Reawakened. It contains no co-op implementation or dependency.

## Elements and testing

- Existing Ruby gems apply flat Fire damage (Pyro).
- Existing Opal gems apply flat Ice damage (Cryo).
- Existing Turquoise gems apply flat Electrical damage (Electro).
- Gem descriptions show weapon damage and corresponding armor resistance.
- Element indicators appear above innately elemental monsters and entities that have been hit by an element. Two applied elements can be shown at once; their auras expire in four seconds.
- Pyro + Cryo triggers **Melt** (2x Pyro-on-Cryo, 1.5x Cryo-on-Pyro damage); Pyro + Electro triggers **Overloaded** splash damage; Cryo + Electro triggers **Superconduct**, lowering physical resistances briefly. Reaction names float above the affected target.
- Damage numbers float above the damaged target and use the damage element's color.
- Training targets automatically spawn in the Grove field south of the dungeon portal. **F8** can request them again; it uses the game's Unity Input System.
- A one-time command grants existing Ruby, Opal and Turquoise gems plus six pre-socketed swords that alternate between two and six sockets.

Only the game's existing gems are used; this package does not add new element gem item types. Reactions currently cover the three existing elements (Pyro, Cryo and Electro).

## Install

Import the zip as a local package in r2modman or Thunderstore Mod Manager for FATE: Reawakened. Requires BepInExPack 5.4.2305. Launch the modded profile and load an existing character.
