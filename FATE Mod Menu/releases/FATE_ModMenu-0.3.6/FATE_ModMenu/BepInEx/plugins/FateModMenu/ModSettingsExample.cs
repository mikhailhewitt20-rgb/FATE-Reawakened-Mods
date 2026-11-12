// Integration example for an independent mod; NOT compiled as a plugin.
// It registers settings with FATE Mod Menu through reflection so the menu stays
// optional: no DLL reference and no hard dependency are needed.
using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("example.mod.settings", "Example Mod", "0.1.0")]
[BepInDependency("zombieghost.modmenu", BepInDependency.DependencyFlags.Optional)]
public sealed class ExampleModSettings : BaseUnityPlugin
{
    private object module;
    private float nextTry;
    private Texture2D preview;
    private string previewKey;

    private void Update()
    {
        // The menu is another plugin; retry until its API is available.
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

            type.GetMethod("RegisterToggle")?.Invoke(module, new object[] { "example.enabled", "Enable the example feature",
                new Func<bool>(() => true), new Action<bool>(value => { }), "General" });

            type.GetMethod("RegisterSlider")?.Invoke(module, new object[] { "example.radius", "Effect radius",
                new Func<float>(() => 2f), new Action<float>(value => { }), 0.5f, 10f, 0.5f, "Look", null });

            // A colour is one row: swatch, hex value and a slider per channel.
            type.GetMethod("RegisterColor")?.Invoke(module, new object[] { "example.colour", "Effect colour",
                new Func<Color>(() => Color.white), new Action<Color>(value => { }), "Look" });

            // A preview is a picture of your own making, refreshed when the screen
            // rebuilds. Cache it and only redraw when a value behind it changed.
            type.GetMethod("RegisterPreview")?.Invoke(module, new object[] { "example.preview", "What it looks like",
                new Func<Texture2D>(BuildPreview), "Look", 160f });

            type.GetMethod("RegisterChoice")?.Invoke(module, new object[] { "example.mode", "Mode",
                new Func<int>(() => 0), new Action<int>(value => { }), new[] { "Subtle", "Normal", "Loud" }, "General" });

            type.GetMethod("RegisterAction")?.Invoke(module, new object[] { "example.reload", "Reload config",
                new Action(() => Logger.LogInfo("config reloaded")), "General" });
        }
        catch (Exception exception)
        {
            Logger.LogWarning("FATE Mod Menu registration skipped: " + exception.Message);
            module = null;
            nextTry = UnityEngine.Time.unscaledTime + 30f;
        }
    }

    private Texture2D BuildPreview()
    {
        const string key = "example";
        if (preview != null && previewKey == key) return preview;
        if (preview != null) UnityEngine.Object.Destroy(preview);
        preview = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pixels = new Color32[64 * 64];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 220, 140, 90);
        preview.SetPixels32(pixels);
        preview.Apply();
        previewKey = key;
        return preview;
    }

    private void OnDestroy()
    {
        if (preview != null) UnityEngine.Object.Destroy(preview);
        (module as IDisposable)?.Dispose();
        module = null;
    }
}
