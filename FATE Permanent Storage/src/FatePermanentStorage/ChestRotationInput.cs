using System;
using System.Reflection;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // The chest rotation key is owned by FATE Rebind so it appears in the Controls editor,
 // can be rebound to a keyboard key or mouse button, and is saved in Rebind's bindings.xml.
 // Reflection keeps this mod usable when Rebind is not installed.
 internal static class ChestRotationInput
 {
  static bool registered;
  static object button;
  static PropertyInfo wasPressed;

  public static void EnsureRegistered()
  {
   if (registered)
   {
    return;
   }
   try
   {
    Type api = FindType("ZombieGhost.Rebind.RebindApi");
    if (api == null)
    {
     return;
    }
    var registerProvider = api.GetMethod("RegisterProvider", BindingFlags.Public | BindingFlags.Static);
    if (registerProvider == null)
    {
     return;
    }
    object provider = registerProvider.Invoke(null, new object[]
    {
     PermanentStoragePlugin.Guid, "FATE Permanent Storage"
    });
    if (provider == null)
    {
     return;
    }
    var registerButton = provider.GetType().GetMethod("RegisterButton", BindingFlags.Public | BindingFlags.Instance);
    if (registerButton == null)
    {
     return;
    }
    button = registerButton.Invoke(provider, new object[]
    {
     "storage.rotatechest",
     "Rotate storage chest",
     "<Keyboard>/r",
     "Storage"
    });
    if (button != null)
    {
     wasPressed = button.GetType().GetProperty("WasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
    }
    registered = true;
    PermanentStoragePlugin.Log.LogInfo("Registered the chest rotation key with FATE Rebind (default R).");
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not register the chest rotation key with FATE Rebind: " + ex.Message);
   }
  }

  public static bool WasPressedThisFrame()
  {
   EnsureRegistered();
   if (button != null && wasPressed != null)
   {
    try
    {
     return (bool)wasPressed.GetValue(button);
    }
    catch (Exception ex)
    {
     PermanentStoragePlugin.Log.LogWarning("Chest rotation key read failed: " + ex.Message);
    }
   }
   // Fallback for installs without Rebind. The game normally ships the Input System,
   // but this keeps the rotate action available even when legacy input is enabled.
   return Input.GetKeyDown(KeyCode.R);
  }

  static Type FindType(string fullName)
  {
   var direct = Type.GetType(fullName + ", ZombieGhost.Rebind");
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
     // Ignore dynamic or partially loaded assemblies.
    }
   }
   return null;
  }
 }
}
