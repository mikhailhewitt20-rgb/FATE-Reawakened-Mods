# FATE Mod Menu API v1

Look up the API at runtime instead of referencing `ZombieGhost.ModMenu.dll`:

```csharp
[BepInDependency("zombieghost.modmenu", BepInDependency.DependencyFlags.SoftDependency)]
// Add this attribute to your existing BepInEx plugin.
```

Registration is optional for you and harmless when FATE Mod Menu is absent: the
lookup returns null and your plugin keeps working. Call the API on Unity's main
thread, normally from your plugin's `Awake`/`Start`, and register only options
that are not key bindings — bindings belong to FATE Rebind's Controls editor.

```csharp
var api = AccessTools.TypeByName("ZombieGhost.ModMenu.ModMenuApi");
if (api == null) return;
var module = api.GetMethod("RegisterModule").Invoke(null, new object[] { "your.plugin.guid", "Your mod" });

// Toggle: a boolean preference.
var invert = (bool)module.GetType().GetMethod("RegisterToggle").Invoke(module, new object[] {
    "aim.invert-vertical", "Invert up/down",
    new Func<bool>(() => invertVertical.Value),
    new Action<bool>(value => invertVertical.Value = value),
    "Aim options" });

// Slider: a numeric preference. Step drives the arrow amount and the decimals shown.
module.GetType().GetMethod("RegisterSlider").Invoke(module, new object[] {
    "reticle.scale", "Crosshair scale",
    new Func<float>(() => reticleScale.Value),
    new Action<float>(value => reticleScale.Value = value),
    0.5f, 3f, 0.05f, "Reticle", null });

// Choice: an enum-style option. Left/right wraps through the list.
module.GetType().GetMethod("RegisterChoice").Invoke(module, new object[] {
    "aim.mode", "Aim mode",
    new Func<int>(() => aimMode.Value),
    new Action<int>(value => aimMode.Value = value),
    new[] { "Off", "Hold", "Toggle" }, "Aim options" });

// Action: a button row for commands such as reloading a config.
module.GetType().GetMethod("RegisterAction").Invoke(module, new object[] {
    "aim.reload", "Reload config", new Action(ReloadConfig), "Aim options" });

// Note: one wrapped line of context under the same heading.
module.GetType().GetMethod("RegisterNote").Invoke(module, new object[] {
    "aim.note", "Holding the aim key also blocks camera drag.", "Aim options" });
```

If you reference the DLL directly the same calls are typed:

```csharp
var module = ModMenuApi.RegisterModule("your.plugin.guid", "Your mod");
var scale = module.RegisterSlider("reticle.scale", "Crosshair scale",
    () => reticleScale.Value, value => reticleScale.Value = value, 0.5f, 3f, 0.05f, "Reticle");
```

## Rules

- **IDs are permanent.** Provider IDs are your mod GUID; option IDs are unique
  within your module and must stay stable across releases so nothing is confused
  for a different option. `|` is reserved; IDs are capped at 96 characters.
- **You own the value and the storage.** The menu only reads and writes through
  your getters and setters. Persist in your own BepInEx config.
- **Defaults are snapshotted at registration.** `ResetDefaults` on the heading and
  the row reset put each option back to what its getter returned at registration.
- **The menu is rebuilt on change.** Setting a value through the API redraws the
  row; you do not need to call anything.
- **Dispose when you unload** (`module.Dispose()`), which removes every row.
- **Input blocking.** Use `ModMenuApi.IsInputBlocked` to stop your own input while
  the settings screen is open, the same way you use `RebindApi.IsInputBlocked`.
- **`ModMenuApi.ApiVersion`** is `1`. Check a method exists before calling it if you
  want to support older menu builds.
- Registration is capped at 64 modules and 256 options per module.

## Layout

Rows appear under `<your mod name> / <category>` when you register more than one
category, and under `<your mod name>` when you only use one. Within a category the
rows keep registration order, so order your calls the way you want them read.
