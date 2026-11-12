# Changelog

- 0.3.4: Keep runtime and damage handling alive when Unity destroys the BepInEx component; tint FATE's native damage floaties through the hit prefix. Deliver the new gem variants through the package's one-shot file command.
- 0.3.3: Restore FATE's native damage numbers and tint them per hit; keep physical damage white. Add tier-matched Citrine/Geo, Cyanite/Anemo, and Aquamarine/Hydro gems with recolored inventory icons and one-time first-character delivery. Run the runtime loop directly on the persistent plugin component.
- 0.3.2: Hide FATE's original damage popup callback so colored numbers are the only damage numbers; keep physical damage white for future Geo color space; preserve healing, mana, experience, and gold floaties.
- 0.3.1: Keep the persistent runtime and Harmony hooks alive after BepInEx destroys its plugin component, and clean up on application exit.
- 0.3.0: Add short two-element auras, Melt/Overloaded/Superconduct reactions, floating reaction labels and colored damage numbers. Fix F8 through Unity's Input System, auto-spawn Grove targets, and grant pre-socketed swords with the test kit.
- 0.2.2: Run hotkeys and command polling from a persistent Unity driver; wait for a character before granting the first-run sword kit; log hotkey and runtime startup.
- 0.2.1: Add F8 Grove dummy spawning and a one-time six-sword socketable tester kit.
- 0.2.0: Configure Ruby, Opal, and Turquoise as Pyro, Cryo, and Electro; show elemental target indicators.
