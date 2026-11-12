using System;
using System.Reflection;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // FATE can run with the legacy Input Manager disabled, where Input.GetKeyDown throws. The mod
 // menu handles that by reading the new Input System's Keyboard, so this does the same through
 // reflection and only falls back to legacy Input when no new-input keyboard exists.
 internal static class KeyState
 {
  static bool probed;
  static Type keyboardType;
  static PropertyInfo currentProperty;
  static bool legacyUsable = true;

  public static bool Pressed(KeyCode legacyKey, string keyboardControl)
  {
   var keyboard = CurrentKeyboard();
   if (keyboard != null)
   {
    try
    {
     var property = keyboard.GetType().GetProperty(keyboardControl);
     var control = property != null ? property.GetValue(keyboard) : null;
     if (control == null)
     {
      return false;
     }
     var wasPressed = control.GetType().GetProperty("wasPressedThisFrame");
     return wasPressed != null && (bool)wasPressed.GetValue(control);
    }
    catch (Exception)
    {
     return false;
    }
   }
   if (!legacyUsable)
   {
    return false;
   }
   try
   {
    return Input.GetKeyDown(legacyKey);
   }
   catch (Exception)
   {
    legacyUsable = false;
    return false;
   }
  }

  public static bool PressedAny(KeyCode[] legacyKeys, params string[] keyboardControls)
  {
   var keyboard = CurrentKeyboard();
   if (keyboard != null)
   {
    foreach (var keyboardControl in keyboardControls)
    {
     try
     {
      var property = keyboard.GetType().GetProperty(keyboardControl);
      var control = property != null ? property.GetValue(keyboard) : null;
      if (control == null) continue;
      var wasPressed = control.GetType().GetProperty("wasPressedThisFrame");
      if (wasPressed != null && (bool)wasPressed.GetValue(control)) return true;
     }
     catch (Exception) { }
    }
    return false;
   }
   if (!legacyUsable || legacyKeys == null) return false;
   foreach (var key in legacyKeys)
   {
    try
    {
     if (Input.GetKeyDown(key)) return true;
    }
    catch (Exception)
    {
     legacyUsable = false;
     return false;
    }
   }
   return false;
  }

  static object CurrentKeyboard()
  {
   if (!probed)
   {
    probed = true;
    keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
    if (keyboardType == null)
    {
     foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
     {
      try
      {
       keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard", false);
      }
      catch (Exception)
      {
       // Dynamic or partially loaded assemblies must not stop the search.
      }
      if (keyboardType != null)
      {
       break;
      }
     }
    }
    if (keyboardType != null)
    {
     currentProperty = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
    }
    PermanentStoragePlugin.Log.LogInfo("Input keys: " + (currentProperty != null
     ? "using the new Input System keyboard."
     : "no new-input keyboard found; using legacy Input."));
   }
   if (currentProperty == null)
   {
    return null;
   }
   try
   {
    return currentProperty.GetValue(null);
   }
   catch (Exception)
   {
    return null;
   }
  }
 }
}
