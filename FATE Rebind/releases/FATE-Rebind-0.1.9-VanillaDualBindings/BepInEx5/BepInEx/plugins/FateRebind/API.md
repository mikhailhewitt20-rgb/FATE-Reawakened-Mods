# FATE Rebind API v1

Reference `ZombieGhost.Rebind.dll` and the game's `Unity.InputSystem.dll` without
copying them into your own mod package. Declare a BepInEx hard dependency on
`zombieghost.rebind`, minimum version `0.1.0`, and the corresponding Thunderstore
package dependency once the publishing team/package exists. Choose the DLL built
for your BepInEx major version. The public registration surface is the same.

Call the API on Unity's main thread, normally from your plugin's Awake/Start.
Retain the provider and dispose it when your mod unloads. Provider/action IDs must
be stable across releases; display names may change.

```csharp
[BepInDependency("zombieghost.rebind", "0.1.0")]
// Add this attribute to your existing BepInEx plugin.

// Awake:
provider = RebindApi.RegisterProvider("your.plugin.guid", "Your mod");
cameraDrag = provider.RegisterButton(
    "camera.drag", "Hold to rotate camera", "<Mouse>/middleButton", "Camera");

// Your existing camera code:
if (!RebindApi.IsInputBlocked && cameraDrag.IsPressed)
    RotateYourCamera();

// OnDestroy:
provider.Dispose();
```

`RebindButton` supplies `IsPressed`, `WasPressedThisFrame`, and
`WasReleasedThisFrame`. These return false while the rebind menu owns input.
Registering a button does not itself add gameplay behaviour.

For a mod-owned InputAction, use:

```csharp
provider.RegisterAction("special.action", myInputAction,
    "Special action", "Abilities");
```

You retain ownership of external actions: enable, disable and dispose them as
your own system requires. The menu temporarily suspends enabled actions and
restores them. The registry does not dispose your external actions. Actions
created by `RegisterButton` are owned/disposed by the provider instead.

Native FATE controls are discovered automatically; there is no need to register
`Persistent/Move` again just because your mod consumes it. A mod can use its saved
direction overrides without duplicating movement code.

New InputActionAsset instances are discovered while running, and saved overrides
are applied to matching clones after scene changes. Standalone enabled actions
are also discovered; explicit registration gives them stable names and identity.

For explicitly registered actions, default binding identity is based on the
original path, groups, part/composite identity and duplicate ordinal, avoiding
random GUIDs generated every launch. If you change the structure/default paths
across releases, provide your own stable logical binding IDs:

```csharp
provider.RegisterAction("my.action", action, "My action", "Controls",
    bindingIndex => myStableBindingIds[bindingIndex]);
```

Do not keep reapplying hard-coded overrides every frame after registration.
Do not register placeholder controls for missing mods or run another mod's
movement/camera code in this utility. Keyboard polling bypasses InputActions;
replace those specific reads with registered button state in the owning mod.

The initial release edits existing bindings; it does not invent action slots,
replace native control schemes, or supply an arbitrary macro/scripting engine.


A companion adapter must declare hard dependencies on Rebind and the mod it integrates. Register only live actions from that installed mod; do not expose placeholder WASD/movement controls from Rebind alone. Native dormant Persistent/Move and internal UI maps are intentionally hidden in the base editor.

