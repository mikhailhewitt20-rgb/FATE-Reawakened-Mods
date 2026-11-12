using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif
using HarmonyLib;
using Tableflip;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FateElementalReactions;

[BepInPlugin(Guid, "FATE Elemental Reactions by ZombieGhost", "0.3.4")]
public sealed partial class Plugin : BaseUnityPlugin
{
    public const string Guid = "zombieghost.fate.elementalreactions";
    internal static Plugin? Active;
    private Harmony? harmony;
    private ConfigEntry<bool>? testCommands;
    private ConfigEntry<bool>? autoSpawnDummies;
    private bool contentPrepared;
    private bool runtimeLogged;
    private float nextPrepareAttempt;

    private bool TestCommandsEnabled => testCommands?.Value == true;
    private string CommandDirectory => Path.Combine(Paths.ConfigPath, "zombieghost.fate.elemental.commands", "commands");
    private string ProcessedDirectory => Path.Combine(Paths.ConfigPath, "zombieghost.fate.elemental.commands", "processed");

    private void Awake()
    {
        Active = this;
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        var runtimeHost = new GameObject("ZG_Elemental_Runtime");
        UnityEngine.Object.DontDestroyOnLoad(runtimeHost);
        runtimeHost.AddComponent<ElementalRuntimeDriver>().Attach(this);
        testCommands = Config.Bind("Testing", "EnableFileCommands", true, "Allow the F8 dummy hotkey and local test-kit commands from the configured command folder.");
        autoSpawnDummies = Config.Bind("Testing", "AutoSpawnTrainingDummiesInGrove", true, "Spawn elemental training targets automatically when a character enters Town of Grove.");
        harmony = new Harmony(Guid);
        var applyDamage = AccessTools.Method(typeof(CharacterVitals), "ApplyDamage");
        if (applyDamage == null) throw new MissingMethodException(typeof(CharacterVitals).FullName, "ApplyDamage");
        harmony.Patch(applyDamage,
            prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeElementalDamage)),
            postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterElementalDamage)));
        Logger.LogInfo("Using FATE's native damage floaties; elemental damage colors are applied per hit.");
        harmony.Patch(AccessTools.Method(typeof(Monster), "RespondToAttack", new[] { typeof(Character) }), prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeTrainingDummyRespond)));
        harmony.Patch(AccessTools.Method(typeof(Character), "SetAIState"), prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeTrainingDummyAI)));
        harmony.Patch(AccessTools.Method(typeof(CharacterVitals), "Die", new[] { typeof(CharacterVitals.Damage) }), prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeTrainingDummyDie)));
        harmony.Patch(AccessTools.Method(typeof(CharacterVitals), "Die", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeTrainingDummyDieQuiet)));
        Logger.LogInfo($"Standalone elemental plugin ready; persistent runtime attached; command folder: {CommandDirectory}");
    }

    internal void RuntimeUpdate()
    {
        if (!runtimeLogged)
        {
            runtimeLogged = true;
            Logger.LogInfo("ELEMENT_RUNTIME_STARTED");
        }
        if (!contentPrepared && Time.unscaledTime >= nextPrepareAttempt)
        {
            nextPrepareAttempt = Time.unscaledTime + 1f;
            try
            {
                var db = SingletonBehaviour<FateDB>.Instance;
                if (db != null && db.AllItems != null && db.AllItems.Count > 0)
                {
                    PrepareElementalGems(db);
                    contentPrepared = true;
                }
            }
            catch (Exception e) { Logger.LogDebug("Waiting for FATE item database: " + e.Message); }
        }
        UpdateElementalTesting();
        UpdateElementalIndicators();
    }

    internal void RuntimeLateUpdate() => LockTrainingDummies();

    private void OnDestroy()
    {
        Logger.LogInfo("ELEMENT_PLUGIN_OBJECT_DESTROYED.");
    }

    private void OnApplicationQuit()
    {
        harmony?.UnpatchSelf();
        if (ReferenceEquals(Active, this)) Active = null;
    }
}

public sealed class ElementalRuntimeDriver : MonoBehaviour
{
    private Plugin? owner;
    internal void Attach(Plugin plugin) => owner = plugin;
    private void Update()
    {
        var plugin = owner;
        if (!ReferenceEquals(plugin, null)) plugin.RuntimeUpdate();
    }
    private void LateUpdate()
    {
        var plugin = owner;
        if (!ReferenceEquals(plugin, null)) plugin.RuntimeLateUpdate();
    }
}
