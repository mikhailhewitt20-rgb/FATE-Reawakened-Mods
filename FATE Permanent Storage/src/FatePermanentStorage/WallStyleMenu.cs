using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // Registers the room's look and the Storage Keeper's identity with FATE Mod Menu. The menu
 // is a separate mod, so it is reached by reflection: this assembly keeps working, unchanged,
 // without it installed, and older menu builds simply skip the rows they cannot draw.
 internal static class WallStyleMenu
 {
  static bool attempted;
  static bool registered;
  static object menuModule;
  static MethodInfo sliderMethod;
  static MethodInfo textMethod;
  static bool modelRowRegistered;
  static object modelSlider;
  static FieldInfo modelSliderMaxField;
  static int registeredModelMax = -1;
  static MethodInfo menuRefreshMethod;
  static readonly HashSet<string> chestNameRows = new HashSet<string>();

  public static void EnsureRegistered()
  {
   if (registered || attempted)
   {
    return;
   }
   if (RoomBuilder.WallStyleCount == 0)
   {
    return;
   }
   StorageKeeperOptions.EnsureModels();
   attempted = true;
   try
   {
    Type api = FindType("ZombieGhost.ModMenu.ModMenuApi");
    if (api == null)
    {
     PermanentStoragePlugin.Log.LogInfo("FATE Mod Menu is not installed; the room style stays available through the config file.");
     return;
    }
    var registerModule = api.GetMethod("RegisterModule", BindingFlags.Public | BindingFlags.Static);
    if (registerModule == null)
    {
     PermanentStoragePlugin.Log.LogWarning("FATE Mod Menu does not expose RegisterModule; the room settings stay config-only.");
     return;
    }
    object module = registerModule.Invoke(null, new object[] { PermanentStoragePlugin.Guid, "FATE Permanent Storage" });
    if (module == null)
    {
     return;
    }
    Type moduleType = module.GetType();
    var registerSlider = moduleType.GetMethod("RegisterSlider", BindingFlags.Public | BindingFlags.Instance);
    if (registerSlider == null)
    {
     PermanentStoragePlugin.Log.LogWarning("FATE Mod Menu has no slider row; the room style stays config-only.");
     return;
    }
    menuModule = module;
    sliderMethod = registerSlider;

    int lastWall = Math.Max(0, RoomBuilder.WallStyleCount - 1);
    registerSlider.Invoke(module, new object[]
    {
     "room.wallstyle",
     "Storage room wall set",
     new Func<float>(() => RoomBuilder.WallStyleIndex),
     new Action<float>(value => ApplyWall((int)Math.Round(value))),
     0f,
     (float)lastWall,
     1f,
     "Room",
     "0"
    });

    if (RoomBuilder.FloorStyleCount > 0)
    {
     int lastFloor = Math.Max(0, RoomBuilder.FloorStyleCount - 1);
     registerSlider.Invoke(module, new object[]
     {
      "room.floorstyle",
      "Storage room floor set",
      new Func<float>(() => RoomBuilder.FloorStyleIndex),
      new Action<float>(value => ApplyFloor((int)Math.Round(value))),
      0f,
      (float)lastFloor,
      1f,
      "Room",
      "0"
     });
    }

    // API v4 adds free-text rows. The storage mod still works with an older menu; only the
    // name field disappears until the menu is updated.
    var registerText = moduleType.GetMethod("RegisterText", BindingFlags.Public | BindingFlags.Instance);
    textMethod = registerText;
    if (registerText != null)
    {
     registerText.Invoke(module, new object[]
     {
      "keeper.name",
      "Storage Keeper name",
      new Func<string>(KeeperNameValue),
      new Action<string>(ApplyKeeperName),
      "Storage Keeper",
      24
     });
    }
    else
    {
     PermanentStoragePlugin.Log.LogWarning("FATE Mod Menu is older than API v4; the Storage Keeper name box is unavailable.");
    }

    int modelCount = StorageKeeperOptions.Count;
    TryRegisterKeeperModel(module, registerSlider);
    if (!modelRowRegistered)
    {
     PermanentStoragePlugin.Log.LogWarning("No NPC models were found yet for the Storage Keeper slider; it will be added when the Keeper is first built.");
    }

    registered = true;
    PermanentStoragePlugin.Log.LogInfo("Registered the room and Keeper controls with FATE Mod Menu (" +
     RoomBuilder.WallStyleCount + " wall style(s), " + RoomBuilder.FloorStyleCount + " floor style(s), " +
     modelCount + " Keeper model(s)).");
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not register the storage settings with FATE Mod Menu: " + ex.Message);
   }
  }

  // The town NPCs may finish loading after the room module registers. The first room build
  // calls this again; by then the list exists and the model row can be added to the module.
  public static void EnsureKeeperModelRegistered()
  {
   if (!registered || menuModule == null || sliderMethod == null) return;
   StorageKeeperOptions.EnsureModels();
   if (!modelRowRegistered)
   {
    TryRegisterKeeperModel(menuModule, sliderMethod);
   }
   RefreshKeeperModelRange();
  }

  public static void RegisterChestName(StorageChestRecord record)
  {
   if (!registered || menuModule == null || textMethod == null || record == null || !record.placed ||
       string.IsNullOrEmpty(record.id) || chestNameRows.Contains(record.id)) return;
   try
   {
    string chestId = record.id;
    string label = StorageChest.DefaultChestName(record) + " name";
    textMethod.Invoke(menuModule, new object[]
    {
     "chest.name." + chestId,
     label,
     new Func<string>(() => StorageRoomManager.GetChestMenuName(chestId)),
     new Action<string>(value => StorageRoomManager.SetChestMenuName(chestId, value)),
     "Storage Chests",
     24
    });
    chestNameRows.Add(chestId);
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not add a name field for " + record.id + " to FATE Mod Menu: " + ex.Message);
   }
  }

  static void TryRegisterKeeperModel(object module, MethodInfo registerSlider)
  {
   if (modelRowRegistered || module == null || registerSlider == null) return;
   int modelCount = StorageKeeperOptions.Count;
   if (modelCount <= 0) return;
    modelSlider = registerSlider.Invoke(module, new object[]
   {
    "keeper.model",
    "Storage Keeper model",
    new Func<float>(KeeperModelValue),
    new Action<float>(value => ApplyKeeperModel((int)Math.Round(value))),
    0f,
    (float)Math.Max(0, modelCount - 1),
    1f,
    "Storage Keeper",
    "0"
   });
    modelSliderMaxField = modelSlider != null
     ? modelSlider.GetType().GetField("Max", BindingFlags.Instance | BindingFlags.NonPublic)
     : null;
    registeredModelMax = modelCount - 1;
    modelRowRegistered = true;
   PermanentStoragePlugin.Log.LogInfo("Registered the Storage Keeper model slider (" + modelCount + " model(s)).");
  }

  static void RefreshKeeperModelRange()
  {
   if (!modelRowRegistered || modelSlider == null || modelSliderMaxField == null) return;
   int modelMax = StorageKeeperOptions.Count - 1;
   if (modelMax <= registeredModelMax) return;
   modelSliderMaxField.SetValue(modelSlider, (float)modelMax);
   registeredModelMax = modelMax;
   try
   {
    if (menuRefreshMethod == null)
    {
     Type plugin = FindType("ZombieGhost.ModMenu.ModMenuPlugin");
     menuRefreshMethod = plugin != null
      ? plugin.GetMethod("RequestRefresh", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
      : null;
    }
    menuRefreshMethod?.Invoke(null, null);
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not refresh the Storage Keeper model slider: " + ex.Message);
   }
   PermanentStoragePlugin.Log.LogInfo("Expanded the Storage Keeper model slider to " + (modelMax + 1) + " models.");
  }

  static string KeeperNameValue()
  {
   return PermanentStoragePlugin.KeeperName != null && !string.IsNullOrWhiteSpace(PermanentStoragePlugin.KeeperName.Value)
    ? PermanentStoragePlugin.KeeperName.Value.Trim() : "Storage Keeper";
  }

  static float KeeperModelValue()
  {
   if (PermanentStoragePlugin.KeeperModel == null) return 0f;
   float value = StorageKeeperOptions.Resolve(
    PermanentStoragePlugin.KeeperModelName != null ? PermanentStoragePlugin.KeeperModelName.Value : null,
    PermanentStoragePlugin.KeeperModel.Value);
   return value;
  }

  // Keeps the config the single source of truth, so the choice survives a restart.
  static void ApplyWall(int index)
  {
   int applied = RoomBuilder.SetWallStyle(index);
   if (PermanentStoragePlugin.WallStyle != null && PermanentStoragePlugin.WallStyle.Value != applied)
   {
    PermanentStoragePlugin.WallStyle.Value = applied;
   }
  }

  static void ApplyFloor(int index)
  {
   int applied = RoomBuilder.SetFloorStyle(index);
   if (PermanentStoragePlugin.FloorStyle != null && PermanentStoragePlugin.FloorStyle.Value != applied)
   {
    PermanentStoragePlugin.FloorStyle.Value = applied;
   }
  }

  static void ApplyKeeperName(string value)
  {
   string clean = string.IsNullOrWhiteSpace(value) ? "Storage Keeper" : value.Trim();
   if (PermanentStoragePlugin.KeeperName != null)
   {
    PermanentStoragePlugin.KeeperName.Value = clean;
   }
   StorageRoomManager manager = StorageRoomManager.Instance;
   if (manager != null)
   {
    manager.ApplyKeeperName(clean);
   }
  }

  static void ApplyKeeperModel(int index)
  {
   int applied = StorageKeeperOptions.Clamp(index);
   if (PermanentStoragePlugin.KeeperModel != null)
   {
    PermanentStoragePlugin.KeeperModel.Value = applied;
   }
   if (PermanentStoragePlugin.KeeperModelName != null)
   {
    PermanentStoragePlugin.KeeperModelName.Value = StorageKeeperOptions.Name(applied);
   }
   StorageRoomManager manager = StorageRoomManager.Instance;
   if (manager != null)
   {
    manager.ApplyKeeperModel(applied);
   }
  }

  static Type FindType(string fullName)
  {
   var direct = Type.GetType(fullName + ", ZombieGhost.ModMenu");
   if (direct != null)
   {
    return direct;
   }
   foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
   {
    try
    {
     var found = assembly.GetType(fullName, false);
     if (found != null)
     {
      return found;
     }
    }
    catch (Exception)
    {
     // A dynamic or partially loaded assembly must not stop the search.
    }
   }
   return null;
  }
 }
}
