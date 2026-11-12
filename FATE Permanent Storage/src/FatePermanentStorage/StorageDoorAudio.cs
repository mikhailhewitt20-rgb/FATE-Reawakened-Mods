using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // Use the same OpenSound clip assigned to the game's dungeon door prefabs instead of the
 // portal transition sound. Door audio is stored directly on Deftly.Door rather than in AudioData.
 internal static class StorageDoorAudio
 {
  static AudioClip openClip;
  static bool lookupLogged;
  static string lastEmptyDoorScene;

  // Cache the dungeon door's authored OpenSound as soon as that scene loads. The storage
  // entrance lives in the town, where no dungeon Door component may exist yet.
  public static bool TryCache()
  {
   if (openClip != null) return true;
   openClip = FindDungeonDoorOpenSound();
   return openClip != null;
  }

  // The selected doorway prop is already a real dungeon door prefab, so take its authored
  // OpenSound immediately. This also works while the room is in Grove town and no dungeon scene
  // is loaded for the scene-wide fallback search.
  public static bool TryCacheFromAsset(GameObject prefab)
  {
   if (prefab == null) return openClip != null;
   try
   {
    var door = prefab.GetComponentInChildren<Deftly.Door>(true);
    if (door == null || door.OpenSound == null) return false;
    openClip = door.OpenSound;
    PermanentStoragePlugin.Log.LogInfo("Using the themed room door OpenSound '" + openClip.name + "'.");
    return true;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not read the themed door sound: " + ex.Message);
    return false;
   }
  }

  public static void Play(Vector3 position)
  {
   try
   {
    TryCache();
    if (openClip != null)
    {
     AudioSource.PlayClipAtPoint(openClip, position, 1f);
    }
    else if (!lookupLogged)
    {
     lookupLogged = true;
     PermanentStoragePlugin.Log.LogWarning("Could not find the dungeon door OpenSound asset; no portal sound was played.");
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not play the dungeon door sound: " + ex.Message);
   }
  }

  static AudioClip FindDungeonDoorOpenSound()
  {
   Type doorType = FindType("Deftly.Door");
   if (doorType == null) return null;
   var openSound = doorType.GetField("OpenSound", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
   if (openSound == null) return null;
   var components = Resources.FindObjectsOfTypeAll(doorType);
   int dungeonDoors = 0;
   foreach (var component in components)
   {
    if (component == null) continue;
    var door = component as Component;
    if (door == null || !door.gameObject.scene.IsValid() ||
        door.gameObject.scene.name.IndexOf("Dungeon", StringComparison.OrdinalIgnoreCase) < 0) continue;
    dungeonDoors++;
    var clip = openSound.GetValue(component) as AudioClip;
    if (clip != null)
    {
     PermanentStoragePlugin.Log.LogInfo("Using the active dungeon door OpenSound '" + clip.name + "'.");
     return clip;
    }
   }
   if (dungeonDoors > 0 && lastEmptyDoorScene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
   {
    lastEmptyDoorScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    PermanentStoragePlugin.Log.LogWarning("Found " + dungeonDoors + " dungeon door(s), but none had an OpenSound clip assigned.");
   }
   return null;
  }

  static Type FindType(string fullName)
  {
   foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
   {
    try
    {
     var found = assembly.GetType(fullName, false);
     if (found != null) return found;
    }
    catch { }
   }
   return null;
  }
 }
}
