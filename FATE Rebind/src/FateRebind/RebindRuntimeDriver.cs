using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private static bool quitting;
  private void InstallDriver()
  {
   quitting=false;
   SceneManager.sceneLoaded+=SceneLoaded;
   var spawn=AccessTools.Method(AccessTools.TypeByName("Tableflip.PlayerManager"),"RequestPlayerSpawn");
   if(spawn!=null)harmony.Patch(spawn,postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(PlayerSpawned)));
   EnsureDriver();
  }
  private void SceneLoaded(Scene scene,LoadSceneMode mode){EnsureDriver();}
  private static void PlayerSpawned(object __instance){instance?.EnsureDriver(__instance as Component);}
  private void EnsureDriver(Component anchor=null)
  {
   if(quitting || UnityEngine.Object.FindObjectOfType<RebindRuntimeDriver>()!=null)return;
   if(anchor==null)
   {
    var type=AccessTools.TypeByName("Tableflip.PlayerManager");
    anchor=Resources.FindObjectsOfTypeAll(type).OfType<Component>().FirstOrDefault(c=>c!=null && c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy);
   }
   var root=anchor!=null?anchor.gameObject:new GameObject("FATE.Rebind.Runtime");
   if(anchor==null)DontDestroyOnLoad(root);
   root.AddComponent<RebindRuntimeDriver>().useGUILayout=false;
   Logger.LogInfo("REBIND_DRIVER_ATTACHED: "+root.name);
  }
  internal static void RuntimeTick(){instance?.Tick();}

  internal static void RuntimeQuit(){if(quitting)return;quitting=true;instance?.Shutdown();}
  internal static void RuntimeStarted(){instance?.Logger.LogInfo("REBIND_DRIVER_STARTED: runtime callbacks active.");}
 }
 public sealed class RebindRuntimeDriver:MonoBehaviour
 {
  private bool reported;
  private void Update(){if(!reported){reported=true;RebindPlugin.RuntimeStarted();}RebindPlugin.RuntimeTick();}

  private void OnApplicationQuit(){RebindPlugin.RuntimeQuit();}
 }
}

