using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private Component controlsScreen,settingsTabs;
  private readonly HashSet<int> reportedUiErrors=new HashSet<int>();
  private readonly Dictionary<GameObject,GameObject> installedScreens=new Dictionary<GameObject,GameObject>();
  private readonly Dictionary<Graphic,bool> referenceGraphics=new Dictionary<Graphic,bool>();
  private bool NativeEditorActive=>nativeRoot!=null && controlsScreen!=null && controlsScreen.gameObject.activeInHierarchy;
  private void InstallSettingsHooks()
  {
   var type=AccessTools.TypeByName("Tableflip.OptionsTabs");
   harmony.Patch(AccessTools.Method(type,"Init"),postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(TabsInitialized)));
   harmony.Patch(AccessTools.Method(type,"SetTab"),postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(TabsInitialized)));
   harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Tableflip.PauseUI"),"Update"),prefix:new HarmonyMethod(typeof(RebindPlugin),nameof(AllowNativeTabShortcuts)));
  }
  private static bool AllowNativeTabShortcuts()=>ReferenceEquals(instance,null) || !instance.NativeEditorActive || (!instance.controllerMode && instance.captureSlot==null && instance.candidate==null);
  private static void TabsInitialized(object __instance){instance?.TryInstallControlsTab((Component)__instance);}
  private void TryInstallControlsTab(Component tabs)
  {
   try{InstallControlsTab(tabs);}catch(Exception ex){if(reportedUiErrors.Add(tabs.GetInstanceID()))Logger.LogError("Controls editor could not attach. "+ex);}
  }
  private void InstallControlsTab(Component tabs)
  {
   var entries=AccessTools.Field(tabs.GetType(),"tabs")?.GetValue(tabs) as Array;
   if(entries==null)return;
   foreach(var entry in entries)
   {
    var type=entry.GetType();
    if(Convert.ToInt32(AccessTools.Field(type,"tabType").GetValue(entry))!=0)continue;
    var screen=AccessTools.Field(type,"tabScreen").GetValue(entry) as GameObject;if(screen==null)return;
    if(!installedScreens.TryGetValue(screen,out var root)||root==null)
    {
     root=BuildNativeEditor(screen.transform);installedScreens[screen]=root;
     foreach(var graphic in screen.GetComponentsInChildren<Graphic>(true))
     {if(graphic.transform.IsChildOf(root.transform)||IsNativeWindowDecoration(graphic,screen.transform))continue;referenceGraphics[graphic]=graphic.enabled;graphic.enabled=false;}
     var lifecycle=root.AddComponent<RebindScreenLifecycle>();
     lifecycle.Opened=()=>ActivateNativeEditor(screen,tabs,root);
     lifecycle.Closed=()=>{if(controlsScreen==screen.transform)DetachControls();};
     Logger.LogInfo("REBIND_CONTROLS_INSTALLED: "+screen.name);
    }
    if(screen.activeInHierarchy)ActivateNativeEditor(screen,tabs,root);
   }
  }
  private void ActivateNativeEditor(GameObject screen,Component tabs,GameObject root)
  {
   if(open&&controlsScreen==screen.transform)return;
   if(open)SetOpen(false);
   controlsScreen=screen.transform;settingsTabs=tabs;nativeRoot=root;
   // Return previously suspended UI actions before switching from the standalone window.
   RestoreActions();SetOpen(true);if(controllerMode){inputGuardUntil=Time.unscaledTime+.18f;UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);}RefreshNativeEditor(true);
   Logger.LogInfo("REBIND_SETTINGS_OPEN: "+slots.Count+" slots");
  }
  private void RefreshControlsTab()
  {
   foreach(var item in Resources.FindObjectsOfTypeAll(AccessTools.TypeByName("Tableflip.OptionsTabs")))
    if(item is Component tabs&&tabs.gameObject.scene.IsValid())TryInstallControlsTab(tabs);
  }
  private void DetachControls(){SetOpen(false);controlsScreen=null;settingsTabs=null;nativeRoot=null;}
  private void NavigateNativeTab(int direction)
  {
   if(settingsTabs==null||operation!=null||captureAt>0||candidate!=null)return;
   AccessTools.Method(settingsTabs.GetType(),direction>0?"NextTab":"PreviousTab")?.Invoke(settingsTabs,null);
  }
  private void CloseControlsMenu()
  {
   var pauseType=AccessTools.TypeByName("Tableflip.PauseUI");
   var pause=Resources.FindObjectsOfTypeAll(pauseType).OfType<Component>().FirstOrDefault(c=>c!=null&&c.gameObject.activeInHierarchy);
   if(pause!=null){var tabs=settingsTabs;if(tabs!=null){var set=AccessTools.Method(tabs.GetType(),"SetTab");set.Invoke(tabs,new[]{Enum.Parse(set.GetParameters()[0].ParameterType,"None")});}AccessTools.Method(pauseType,"OnResumeButton")?.Invoke(pause,null);}else DetachControls();
  }
  private void DestroyNativeEditors()
  {
   foreach(var root in installedScreens.Values)if(root!=null)Destroy(root);installedScreens.Clear();
   foreach(var pair in referenceGraphics)if(pair.Key!=null)pair.Key.enabled=pair.Value;referenceGraphics.Clear();
  }
 }
 public sealed class RebindScreenLifecycle:MonoBehaviour
 {
  internal Action Opened,Closed;
  private void OnEnable(){Opened?.Invoke();}
  private void OnDisable(){Closed?.Invoke();}
 }
}






