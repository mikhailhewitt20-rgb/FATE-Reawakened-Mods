// Integration example for an independent mod; NOT compiled as a plugin.
// It registers settings with FATE Mod Menu through reflection so the menu stays
// optional: no DLL reference and no hard dependency are needed.
using System;
using BepInEx;
using HarmonyLib;

[BepInPlugin("example.mod.settings", "Example Mod", "0.1.0")]
[BepInDependency("zombieghost.modmenu", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleModSettings : BaseUnityPlugin
{
    private object module;
    private float nextTry;

    private void Update()
    {
        // Rebind may load after this plugin; retry until it is available.
        if (module != null || UnityEngine.Time.unscaledTime < nextTry) return;
        nextTry = UnityEngine.Time.unscaledTime + 2f;
        Register();
    }

    private void Register()
    {
        try
        {
            var api = AccessTools.TypeByName("ZombieGhost.ModMenu.ModMenuApi");
            var register = api?.GetMethod("RegisterModule", new[] { typeof(string), typeof(string) });
            if (register == null) return;
            module = register.Invoke(null, new object[] { Info.Metadata.GUID, "Example Mod" });
            var type = module.GetType();

            var toggle = type.GetMethod("RegisterToggle");
            toggle?.Invoke(module, new object[] { "example.enabled", "Enable the example feature",
                new Func<bool>(() => true), new Action<bool>(value => { }), "General" });

            var slider = type.GetMethod("RegisterSlider");
            slider?.Invoke(module, new object[] { "example.radius", "Effect radius",
                new Func<float>(() => 2f), new Action<float>(value => { }), 0.5f, 10f, 0.5f, "General", null });

            var choice = type.GetMethod("RegisterChoice");
            choice?.Invoke(module, new object[] { "example.mode", "Mode",
                new Func<int>(() => 0), new Action<int>(value => { }),
                new[] { "Subtle", "Normal", "Loud" }, "General" });

            var action = type.GetMethod("RegisterAction");
            action?.Invoke(module, new object[] { "example.reload", "Reload config",
                new Action(() => Logger.LogInfo("config reloaded")), "General" });
        }
        catch (Exception exception)
        {
            Logger.LogWarning("FATE Mod Menu registration skipped: " + exception.Message);
            module = null;
            nextTry = UnityEngine.Time.unscaledTime + 30f;
        }
    }

    private void OnDestroy()
    {
        (module as IDisposable)?.Dispose();
        module = null;
    }
}
