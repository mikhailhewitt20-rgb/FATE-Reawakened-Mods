using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Tableflip;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 [BepInPlugin(Guid, "FATE Permanent Storage", "0.7.33")]
 public sealed class PermanentStoragePlugin : BaseUnityPlugin
 {
  public const string Guid = "mikhailkillszombies.fate.permanentstorage";
  internal static ManualLogSource Log;
  internal static ConfigEntry<int> GoldUnlockCost;
  internal static ConfigEntry<int> MimicChestDropChance;
  internal static ConfigEntry<string> EntrancePosition;
  internal static ConfigEntry<string> EntranceOffset;
  internal static ConfigEntry<int> WallStyle;
  internal static ConfigEntry<int> FloorStyle;
  internal static ConfigEntry<string> KeeperName;
  internal static ConfigEntry<int> KeeperModel;
  internal static ConfigEntry<string> KeeperModelName;

  internal static StorageRoomManager Manager;
  bool tickErrorReported;
  Harmony harmony;

  void Awake()
  {
   Log = Logger;
   GoldUnlockCost = Config.Bind("Storage", "GoldUnlockCost", 10000,
    "Gold required to unlock one storage position. Set to 0 for free gold unlocks.");
   MimicChestDropChance = Config.Bind("Mimic", "MimicChestDropChance", 35,
    "Percent chance for a Mimic's ordinary death loot to also include one Mimic Chest. 0 disables the drop.");
   EntrancePosition = Config.Bind("Entrance", "ManualPosition", "",
    "Optional exact entrance position as 'x,y,z'; empty places it automatically beside the house behind the fishing dock. Stand where the entrance should be, press F10 to write Player XYZ to the log, and copy those numbers here.");
   EntranceOffset = Config.Bind("Entrance", "Offset", "0,0,0",
    "Offset added to the automatic entrance position as 'x,y,z'. Use this to nudge the entrance without setting an exact position.");

   WallStyle = Config.Bind("Walls", "Style", 0,
    "Wall tile set for the inside of the storage room, as a 0-based index into every complete dungeon wall set the game ships. Cycle it live with the 'Storage room wall set' slider in FATE Mod Menu, or change this number and the room picks it up within a second.");
   FloorStyle = Config.Bind("Floors", "Style", 0,
    "Floor texture set for the inside of the storage room, as a 0-based index into every dungeon floor material the game ships. Cycle it live with the 'Storage room floor set' slider in FATE Mod Menu, or change this number and the room picks it up within a second. The floor is independent of the wall set.");
   KeeperName = Config.Bind("Storage Keeper", "Name", "Storage Keeper",
    "Name shown above the Storage Keeper and in the interaction prompt. Edit it live with the 'Storage Keeper name' text box in FATE Mod Menu.");
   KeeperModel = Config.Bind("Storage Keeper", "Model", 0,
    "NPC model used by the Storage Keeper, as a 0-based index into the town, merchant, party and curated character models. Cycle it live with the 'Storage Keeper model' slider in FATE Mod Menu.");
   KeeperModelName = Config.Bind("Storage Keeper", "ModelName", StorageKeeperOptions.DefaultModelName,
    "Preferred Storage Keeper model by character name. The default is Dreya the Healer. An empty value falls back to the Model index above. Changing the model slider writes this name so the choice survives scene reloads and model-list changes.");

   // Start the room manager before optional patches. A failed patch must never prevent the
   // storage room itself from being added to the Grove.
   var manager = new GameObject("FATE_PermanentStorageManager");
   Manager = manager.AddComponent<StorageRoomManager>();
   DontDestroyOnLoad(manager);
   Log.LogInfo("FATE Permanent Storage 0.7.33 manager created: " + (Manager != null) + ".");

   try
   {
    MimicChestItem.EnsureRegistered();
   }
   catch (Exception ex)
   {
    Log.LogError("Mimic Chest item registration failed: " + ex);
   }

   harmony = new Harmony(Guid);
   TryPatchAll(typeof(MimicDeathPatch));
   TryPatchAll(typeof(FateDbItemsPatch));
   TryPatchAll(typeof(MimicItemPrefabPatch));
   TryPatchAll(typeof(MimicItemNamePatch));
   TryPatchAll(typeof(MimicChestTooltipPatch));
   TryPatchAll(typeof(StoragePlayerUpdatePatch));
   PatchStash("FlushStashSave", nameof(StashProxyPatches.Skip), nameof(StashProxyPatches.SkipFlush));
   PatchStash("OnInventoryInit", nameof(StashProxyPatches.Skip), nameof(StashProxyPatches.SkipInit));
   Log.LogInfo("FATE Permanent Storage 0.7.33 ready. The entrance is at the open spot beside the fishing dock; F10 logs your XYZ.");
  }

  void Update()
  {
   // Drive the hotkey and scene checks from the BepInEx plugin itself. The runtime
   // manager's own MonoBehaviour callbacks were not firing in the r2modman profile.
   if (Manager == null)
   {
    return;
   }
   try
   {
    Manager.PluginTick();
   }
   catch (Exception ex)
   {
    if (!tickErrorReported)
    {
     tickErrorReported = true;
     Log.LogError("Permanent storage tick failed: " + ex);
    }
   }
  }

  void TryPatchAll(Type patchType)
  {
   try
   {
    harmony.PatchAll(patchType);
   }
   catch (Exception ex)
   {
    Log.LogError("Harmony patch " + patchType.Name + " failed: " + ex);
   }
  }

  void PatchStash(string methodName, string prefixName, string prefixMethod)
  {
   try
   {
    var target = AccessTools.Method(typeof(Stash), methodName);
    if (target == null)
    {
     Logger.LogWarning("Stash." + methodName + " is missing; the storage proxy may affect the vanilla stash.");
     return;
    }
    harmony.Patch(target, prefix: new HarmonyMethod(typeof(StashProxyPatches), prefixMethod));
   }
   catch (Exception ex)
   {
    Logger.LogWarning("Could not patch Stash." + methodName + ": " + ex.Message);
   }
  }
 }

 // The game destroys plugin-owned scene objects during scene transitions in this install.
 // Player.Update survives those transitions, so use it as the reliable frame driver for
   // Grove detection and lazy manager creation.
 [HarmonyPatch(typeof(Player), "Update")]
 internal static class StoragePlayerUpdatePatch
 {
  static bool errorReported;

  static void Postfix()
  {
   try
   {
    StorageRoomManager.EnsureRuntime();
    if (StorageRoomManager.Instance != null)
    {
     StorageRoomManager.Instance.PluginTick();
    }
   }
   catch (Exception ex)
   {
    if (!errorReported)
    {
     errorReported = true;
     PermanentStoragePlugin.Log.LogError("Permanent storage Player.Update driver failed: " + ex);
    }
   }
  }
 }

 // The storage room uses an invisible Stash instance to run the stock storage grid for the
 // chest that is currently open. These two patches make that instance inert for the vanilla
 // town stash save: it never loads the town stash into itself and never writes its own items
 // into FateSaveData.stashStorage.
 internal static class StashProxyPatches
 {
  public static bool Skip(Stash __instance)
  {
   return __instance == null || __instance.GetComponent<StorageStashProxy>() == null;
  }

  public static bool SkipFlush(Stash __instance)
  {
   return Skip(__instance);
  }

  public static bool SkipInit(Stash __instance)
  {
   return Skip(__instance);
  }
 }
}
