# FATE Mod Menu — source

The pause menu's **Mod Settings** screen for *FATE Reawakened*. Any BepInEx plugin can
register toggles, sliders, choices, colours, previews, buttons and notes, and they
appear in a native-looking screen under **pause menu → Mod Settings**.

Published source, kept here so other modders have a working reference instead of a DLL.

## What is in this repository

| Path | Purpose |
| --- | --- |
| `src/FateModMenu/ModMenuApi.cs` | The public API: modules, rows, defaults, disposal. |
| `src/FateModMenu/ModMenuScreen.cs` | Two-step UI: mod list, then one screen per mod grouped by category. |
| `src/FateModMenu/ModMenuPlugin.cs` | BepInEx entry point, pause-menu button, hotkey, Harmony patches. |
| `src/FateModMenu/ModMenuTheme.cs` | Gold-on-brown drawing kit that matches the native pause menu. |
| `src/FateModMenu/ModMenuScroll.cs`, `ModMenuSlider.cs`, `ModMenuHexField.cs` | Scroll, drag and hex-paste helpers. |
| `distribution/fate-mod-menu/` | Files shipped inside the Thunderstore package: `API.md`, example mod, readme, changelog, licence, icon. |
| `Package-FateModMenu.ps1` | Builds and validates the Thunderstore zip. |

## Build

Needs the .NET SDK, and the game installed at the path in `FateModMenu.csproj`
(`GameManaged`). BepInEx 5 core comes from the shared workspace `tools/bepinex`
folder; adjust `LoaderCore` if your layout differs.

```powershell
dotnet build src/FateModMenu/FateModMenu.csproj -c Release
./Package-FateModMenu.ps1
```

The Thunderstore package is the BepInEx 5 variant only:
`src/FateModMenu/bin/BepInEx5/Release/netstandard2.1/ZombieGhost.ModMenu.dll`.

## Writing a mod against the menu

Read [`distribution/fate-mod-menu/API.md`](distribution/fate-mod-menu/API.md) first.
The short version: look the API up by reflection, register a module once, then add
rows whose getters and setters read and write **your** config.

```csharp
var api = AccessTools.TypeByName("ZombieGhost.ModMenu.ModMenuApi");
if (api == null) return;                          // menu absent: keep working anyway
var module = api.GetMethod("RegisterModule")
    .Invoke(null, new object[] { Info.Metadata.GUID, "My Mod" });
var type = module.GetType();
type.GetMethod("RegisterToggle").Invoke(module, new object[] {
    "my.option", "Enable my feature",
    new Func<bool>(() => enabled.Value), new Action<bool>(v => enabled.Value = v), "General" });
```

Rules that matter:

- IDs are permanent. Module IDs are your plugin GUID, option IDs are unique per
  module, `|` is reserved, both are capped at 96 characters.
- The menu never stores your values. Persist them in your own BepInEx config and
  read live in the getter so a change takes effect immediately.
- Do not register key bindings here; they belong to **FATE Rebind → Controls**.
- Call everything on Unity's main thread, and `Dispose()` the module when your
  plugin unloads. Registration is capped at 64 modules and 256 options per module.
- The menu rebuilds its rows whenever a `Describe()` value changes, so a getter
  must be cheap and must reflect the value that is actually shown.

## Licence

See `distribution/fate-mod-menu/LICENSE`.
