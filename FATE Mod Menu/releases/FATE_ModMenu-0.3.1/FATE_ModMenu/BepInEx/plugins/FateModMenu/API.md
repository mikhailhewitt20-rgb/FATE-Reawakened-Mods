# FATE Mod Menu API v2

Look up the API at runtime instead of referencing `ZombieGhost.ModMenu.dll`:

```csharp
[BepInDependency("zombieghost.modmenu", BepInDependency.DependencyFlags.SoftDependency)]
// Add this attribute to your existing BepInEx plugin.
```

Registration is optional for you and harmless when FATE Mod Menu is absent: the
lookup returns null and your plugin keeps working. Call the API on Unity's main
thread, normally from your plugin's `Awake`/`Start`, and register only options
that are not key bindings — bindings belong to FATE Rebind's Controls editor.

The menu is two steps: **pause menu → Mod Settings → your mod → your settings**.
Your module gets its own screen, grouped by the categories you register, so give
each group a short name and keep the setting count small enough to scan.

```csharp
var api = AccessTools.TypeByName("ZombieGhost.ModMenu.ModMenuApi");
if (api == null) return;
var module = api.GetMethod("RegisterModule").Invoke(null, new object[] { "your.plugin.guid", "Your mod" });
var type = module.GetType();

// Toggle: a boolean preference.
type.GetMethod("RegisterToggle").Invoke(module, new object[] {
    "aim.invert-vertical", "Invert up/down",
    new Func<bool>(() => invertVertical.Value),
    new Action<bool>(value => invertVertical.Value = value),
    "Aim" });

// Slider: a numeric preference. Step drives the arrows, the drag and the decimals
// shown. Category groups rows; format is an optional .NET number format.
type.GetMethod("RegisterSlider").Invoke(module, new object[] {
    "reticle.scale", "Crosshair scale",
    new Func<float>(() => reticleScale.Value),
    new Action<float>(value => reticleScale.Value = value),
    0.5f, 3f, 0.05f, "Crosshair", null });

// Choice: an enum-style option. Left/right wraps through the list.
type.GetMethod("RegisterChoice").Invoke(module, new object[] {
    "aim.mode", "Aim mode",
    new Func<int>(() => aimMode.Value),
    new Action<int>(value => aimMode.Value = value),
    new[] { "Off", "Hold", "Toggle" }, "Aim" });

// Colour: a swatch, the hex value, and one slider per channel. The menu writes
// back a full Color, so alpha is yours to keep or ignore.
type.GetMethod("RegisterColor").Invoke(module, new object[] {
    "reticle.crosshair-colour", "Crosshair colour",
    new Func<Color>(() => crosshairColour.Value),
    new Action<Color>(value => crosshairColour.Value = value),
    "Crosshair" });

// Preview: a picture you draw from your own settings, shown at a fixed height in
// the pane beside the list, so it stays visible however far the rows are scrolled.
// The getter runs when the screen is built, so rebuild and cache your texture
// only when a value it depends on has changed.
type.GetMethod("RegisterPreview").Invoke(module, new object[] {
    "reticle.preview", "What it looks like",
    new Func<Texture2D>(BuildReticlePreview), "Crosshair", 170f });

// Action: a button row for commands such as reloading a config.
type.GetMethod("RegisterAction").Invoke(module, new object[] {
    "aim.reload", "Reload config", new Action(ReloadConfig), "Aim" });

// Note: one wrapped line of context under the same heading.
type.GetMethod("RegisterNote").Invoke(module, new object[] {
    "aim.note", "Holding the aim key also blocks camera drag.", "Aim" });
```

If you reference the DLL directly the same calls are typed:

```csharp
var module = ModMenuApi.RegisterModule("your.plugin.guid", "Your mod");
var scale = module.RegisterSlider("reticle.scale", "Crosshair scale",
    () => reticleScale.Value, value => reticleScale.Value = value, 0.5f, 3f, 0.05f, "Crosshair");
var colour = module.RegisterColor("reticle.crosshair-colour", "Crosshair colour",
    () => crosshairColour.Value, value => crosshairColour.Value = value, "Crosshair");
```

## Rules

- **IDs are permanent.** Provider IDs are your mod GUID; option IDs are unique
  within your module and must stay stable across releases so nothing is confused
  for a different option. `|` is reserved; IDs are capped at 96 characters.
- **You own the value and the storage.** The menu only reads and writes through
  your getters and setters. Persist in your own BepInEx config, and read it live
  so a slider takes effect immediately.
- **Defaults are snapshotted at registration.** `ResetDefaults` on the heading and
  the row reset put each option back to what its getter returned at registration.
- **The menu is rebuilt on change.** Setting a value through the API redraws the
  rows; you do not need to call anything. A preview's getter runs on each rebuild.
- **Dispose when you unload** (`module.Dispose()`), which removes the mod and
  every row it owned.
- **Input blocking.** Use `ModMenuApi.IsInputBlocked` to stop your own input while
  the settings screen is open, the same way you use `RebindApi.IsInputBlocked`.
- **`ModMenuApi.ApiVersion`** is `2`. Check a method exists before calling it if you
  want to support older menu builds: v1 had no colour rows and no previews.
- Registration is capped at 64 modules and 256 options per module.

## Layout

The first screen lists every mod that registered something. Choosing a mod opens
its own screen: a heading per category, a **Defaults** button per heading, then
the rows in the order you registered them. Put the most useful setting first, and
use a note where a value needs a sentence of explanation.
