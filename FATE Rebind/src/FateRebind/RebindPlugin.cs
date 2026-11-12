using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 [BepInPlugin("zombieghost.rebind","FATE Rebind","0.1.19")]
 public sealed partial class RebindPlugin : BaseUnityPlugin
 {
  private static RebindPlugin instance;
  public static bool BlocksInput=>!ReferenceEquals(instance,null) && (instance.open || Time.unscaledTime<instance.releaseAt);
  internal static void RequestRefresh(){if(!ReferenceEquals(instance,null))instance.nextDiscover=0;}
  private BindingStore store;
  private readonly Dictionary<InputAction,RegisteredAction> tracked=new Dictionary<InputAction,RegisteredAction>();
  private readonly Dictionary<InputAction,Dictionary<string,string>> baselines=new Dictionary<InputAction,Dictionary<string,string>>();
  private readonly HashSet<InputAction> suspended=new HashSet<InputAction>();
  private readonly List<BindingSlot> slots=new List<BindingSlot>();
  private bool open,changing,suspending;
  private float cancelHandledUntil;
  private float nextDiscover,releaseAt;
  private Harmony harmony;
  private string status="Click a binding to change it. Changes save automatically.";

  private sealed class BindingSlot
  {
   internal RegisteredAction Entry;internal int Index;internal string Key,Label,Map,Original,Group,Category;
   internal bool ReadOnly;
   internal InputAction Action=>Entry.Action;
   internal string Path=>Action.bindings[Index].effectivePath;
  }
  private static string Key(RegisteredAction entry,int index)
  {
   var binding=entry.Action.bindings[index];
   bool explicitProvider=entry.Owner!="game"&&!entry.Owner.StartsWith("auto.",StringComparison.Ordinal);
   string custom=entry.BindingKey?.Invoke(index);
   string structural=binding.name+"|"+binding.path+"|"+binding.groups;
   for(int i=index-1;i>=0;i--)if(entry.Action.bindings[i].isComposite){structural+="|"+entry.Action.bindings[i].name+"|"+entry.Action.bindings[i].path;break;}
   int ordinal=0;for(int i=0;i<index;i++){var prior=entry.Action.bindings[i];if(prior.name==binding.name&&prior.path==binding.path&&prior.groups==binding.groups)ordinal++;}
   return BindingIdentity.Key(entry.Owner,entry.Id,entry.Owned?(binding.name==SecondaryBindingPlanner.ControllerPrimaryMarker?"controller.primary":binding.name==SecondaryBindingPlanner.ControllerMarker&&!EditorInputPolicy.GamepadPath(entry.Action.bindings[0].path)?"controller.secondary":index==0?"primary":"secondary"):custom??(explicitProvider?BindingIdentity.StableToken(structural+"|"+ordinal):binding.id!=Guid.Empty?binding.id.ToString("D"):index+":"+binding.path));
  }
  private void Awake()
  {
   instance=this;store=new BindingStore(Path.Combine(Paths.ConfigPath,"ZombieGhost.Rebind","bindings.xml"));
   try {store.Load();}catch(Exception ex){status="Settings could not be loaded; original file preserved. "+ex.Message;Logger.LogError(status);}
   swapControllerSticks=store.TryGet(ControllerBindingRules.SwapKey,out var stickLayout)&&stickLayout=="1";
   LoadCameraOptions();
   harmony=new Harmony("zombieghost.rebind");
   var broker=AccessTools.TypeByName("Cleverous.Prodigy.Input.InputBroker");
   var tick=AccessTools.Method(broker,"ManagedTick");
   if(tick!=null)harmony.Patch(tick,prefix:new HarmonyMethod(typeof(RebindPlugin),nameof(BeforeBroker)));
   var blocker=AccessTools.PropertyGetter(AccessTools.TypeByName("Tableflip.InputBlocker"),"Blocked");
   if(blocker!=null)harmony.Patch(blocker,postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(BlockInput)));
   var playerGate=AccessTools.PropertyGetter(AccessTools.TypeByName("Cleverous.Prodigy.ProdigyPlayer"),"IsInputBlocked");
   if(playerGate!=null)harmony.Patch(playerGate,postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(BlockInput)));
   InstallZoomBindings();InstallCameraAxisHooks();
   InputSystem.onActionChange+=ActionChanged;Discover();InstallSettingsHooks();InstallDriver();InstallControllerInput();
   Logger.LogInfo("FATE Rebind ready: Controls editor ready.");
  }
  private static bool BeforeBroker(object __instance)
  {if(!BlocksInput)return true;AccessTools.Method(__instance.GetType(),"ResetInputs")?.Invoke(__instance,null);return false;}
  private static void BlockInput(ref bool __result){if(BlocksInput)__result=true;}
  private void ActionChanged(object subject,InputActionChange change)
  {
   if(change==InputActionChange.BoundControlsChanged)zoomRoutesDirty=true;
   if(changing||suspending)return;
   if(change==InputActionChange.ActionEnabled && BlocksInput && subject is InputAction action)Suspend(action);
   else if(change==InputActionChange.ActionMapEnabled && BlocksInput && subject is InputActionMap map)foreach(var item in map.actions)Suspend(item);
   if(change==InputActionChange.BoundControlsChanged || change==InputActionChange.ActionEnabled || change==InputActionChange.ActionMapEnabled)nextDiscover=0;
  }
  private void Tick()
  {

   TickInputMode();
   var keyboard=Keyboard.current;
   if(captureSlot?.Entry.Removed==true || candidate?.Entry.Removed==true){CancelCapture();status="That mod's controls were removed; saved settings were kept.";}

   if(open && keyboard!=null && keyboard.escapeKey.wasPressedThisFrame && operation==null && Time.unscaledTime>=cancelHandledUntil)
   {if(captureAt>0){CancelCapture();cancelHandledUntil=Time.unscaledTime+.1f;}else CloseControlsMenu();}
   if(open && nativeRoot!=null && !nativeRoot.activeInHierarchy)DetachControls();

   if(captureAt>0 && Time.unscaledTime>=captureAt){captureAt=0;StartCapture();}
   if(!open && suspended.Count>0 && Time.unscaledTime>=releaseAt)RestoreActions();
   if(Time.unscaledTime>=nextDiscover && operation==null && captureAt==0 && candidate==null){nextDiscover=Time.unscaledTime+1;Discover();RefreshControlsTab();}
   RefreshNativeEditor();TickController();

  }
  private void Suspend(InputAction action)
  {if(!action.enabled || (NativeEditorActive && !controllerMode && (action.actionMap?.name=="Menu"||action.actionMap?.name=="UI")))return;suspended.Add(action);suspending=true;try{action.Disable();}finally{suspending=false;}}
  private void RestoreActions()
  {suspending=true;try{foreach(var action in suspended.ToArray())try{action.Enable();}catch(Exception ex){Logger.LogWarning("Could not restore retired action "+action.name+": "+ex.Message);}suspended.Clear();}finally{suspending=false;}}
  private void SetOpen(bool value)
  {
   if(open==value)return;CancelCapture();candidate=null;open=value;
   if(open)
   {


    foreach(var action in InputSystem.ListEnabledActions())Suspend(action);
    var broker=AccessTools.TypeByName("Cleverous.Prodigy.Input.InputBroker");var local=AccessTools.Property(broker,"Instance")?.GetValue(null,null);
    if(local!=null)AccessTools.Method(broker,"StopMove")?.Invoke(local,null);
    Discover();
   }
   else{releaseAt=Time.unscaledTime+.2f;}
  }
  private void Discover()
  {
   var found=new Dictionary<InputAction,RegisteredAction>();
   foreach(var provider in RebindApi.Providers.ToArray())foreach(var entry in provider.Actions)found[entry.Action]=entry;
   void Add(InputAction action,string assetName)
   {
    if(found.ContainsKey(action))return;
    bool native=assetName?.Replace("(Clone)","").Trim()=="ProdigyControls";
    if(!native||!EditorInputPolicy.NativeControllerAction(action.actionMap?.name,action.name))return;
    found[action]=new RegisteredAction {Owner=native?"game":"auto."+(string.IsNullOrEmpty(assetName)?"Standalone":assetName),OwnerName=native?"FATE — default controls":"Other input: "+assetName,Id=action.id.ToString("D"),Action=action,Label=action.name,Category=action.actionMap?.name??"Controls"};
   }
   foreach(var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())foreach(var map in asset.actionMaps)foreach(var action in map.actions)Add(action,asset.name);
   foreach(var action in InputSystem.ListEnabledActions())Add(action,action.actionMap?.asset?.name??"Standalone");
   foreach(var pair in tracked.Where(p=>suspended.Contains(p.Key)&&!p.Value.Removed).ToArray())if(!found.ContainsKey(pair.Key))found[pair.Key]=pair.Value;
   foreach(var dead in tracked.Keys.Where(a=>!found.ContainsKey(a)).ToArray()){tracked.Remove(dead);baselines.Remove(dead);suspended.Remove(dead);}
   foreach(var pair in found)
   {
    var entry=pair.Value;bool fresh=!tracked.TryGetValue(pair.Key,out var previous)||previous.Owner!=entry.Owner||previous.Id!=entry.Id;
    tracked[pair.Key]=entry;EnsureSecondaryBindings(entry);
    if(!baselines.TryGetValue(entry.Action,out var originals)){originals=new Dictionary<string,string>();baselines[entry.Action]=originals;}
    for(int i=0;i<entry.Action.bindings.Count;i++)
    {
     if(entry.Action.bindings[i].isComposite)continue;
     string key=Key(entry,i);
     if(!originals.ContainsKey(key))originals[key]=entry.Action.bindings[i].overridePath;
     bool savedPath=store.TryGet(key,out var path);string logical=savedPath?path:originals[key];
     if((savedPath||ControllerBindingRules.StickAxis(logical??entry.Action.bindings[i].path))&&(fresh||entry.Action.bindings[i].overridePath!=PhysicalOverride(entry.Action,i,logical)))
     {changing=true;try{ApplyPath(entry.Action,i,logical);}finally{changing=false;}}
    }
    if(BlocksInput){if(entry.Owned)suspended.Add(entry.Action);Suspend(entry.Action);}else if(entry.Owned&&fresh&&!entry.Action.enabled)entry.Action.Enable();
   }
   slots.Clear();var seen=new HashSet<string>();
   foreach(var entry in tracked.Values.OrderBy(a=>a.OwnerName,StringComparer.Ordinal).ThenBy(a=>a.Category).ThenBy(a=>a.Label))
    for(int i=0;i<entry.Action.bindings.Count;i++)
    {
     var binding=entry.Action.bindings[i];if(binding.isComposite)continue;string key=Key(entry,i);if(!seen.Add(key))continue;
     string path=binding.path??"";
     slots.Add(new BindingSlot {Entry=entry,Index=i,Key=key,Label=BindingPresentation.Name(entry.Action.name,binding.isPartOfComposite?binding.name:null,entry.Owner=="game",entry.Label),Category=BindingPresentation.Category(entry.Action.name,entry.Action.actionMap?.name,entry.Owner=="game",entry.Category),Map=entry.Action.actionMap?.name??entry.Category,Original=path,Group=SecondaryBindingPlanner.ControllerMarkerName(binding.name)||(string.IsNullOrEmpty(path)&&(binding.groups??"").IndexOf("Gamepad",StringComparison.OrdinalIgnoreCase)>=0)||path.IndexOf("Gamepad",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Controller",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Joystick",StringComparison.OrdinalIgnoreCase)>=0?"Controller":"Keyboard / mouse",ReadOnly=path.EndsWith("/position",StringComparison.OrdinalIgnoreCase)||path.EndsWith("/delta",StringComparison.OrdinalIgnoreCase)});
    }
  }
  private void Change(BindingSlot slot,string path,bool reset=false)
  {
   bool had=store.TryGet(slot.Key,out var old);
   try
   {
    if(reset)store.Remove(slot.Key);else store.Set(slot.Key,path);store.Save();
    changing=true;
    foreach(var entry in tracked.Values.Where(e=>e.Owner==slot.Entry.Owner&&e.Id==slot.Entry.Id))
     for(int i=0;i<entry.Action.bindings.Count;i++)if(Key(entry,i)==slot.Key)
     {
      string desired=reset?(baselines.TryGetValue(entry.Action,out var originals)&&originals.TryGetValue(slot.Key,out var baseline)?baseline:null):path;
      ApplyPath(entry.Action,i,desired);
     }
    status=reset?"Binding restored.":"Binding saved.";Logger.LogInfo("REBIND_SAVED "+slot.Key+" path="+(reset?"<reset>":path));
   }
   catch(Exception ex){if(had)store.Set(slot.Key,old);else store.Remove(slot.Key);status="Could not save binding: "+ex.Message;Logger.LogError(ex);}
   finally{changing=false;}
  }
  private void SwapBindings(BindingSlot target,BindingSlot other,string newPath,string oldPath)
  {
   bool hadTarget=store.TryGet(target.Key,out var savedTarget),hadOther=store.TryGet(other.Key,out var savedOther);
   string priorOther=other.Path;
   void ApplySlot(BindingSlot slot,string path)
   {
    foreach(var entry in tracked.Values.Where(e=>e.Owner==slot.Entry.Owner&&e.Id==slot.Entry.Id))
     for(int i=0;i<entry.Action.bindings.Count;i++)if(Key(entry,i)==slot.Key)ApplyPath(entry.Action,i,path);
   }
   changing=true;
   try
   {
    // Persist the pair together so a restart cannot leave half a swap saved.
    store.Set(target.Key,newPath);store.Set(other.Key,oldPath);store.Save();
    ApplySlot(target,newPath);ApplySlot(other,oldPath);
    status="Bindings swapped and saved.";
   }
   catch(Exception ex)
   {
    if(hadTarget)store.Set(target.Key,savedTarget);else store.Remove(target.Key);
    if(hadOther)store.Set(other.Key,savedOther);else store.Remove(other.Key);
    try{store.Save();ApplySlot(target,oldPath);ApplySlot(other,priorOther);}catch(Exception rollback){Logger.LogError(rollback);}
    status="Swap failed; previous settings restored. "+ex.Message;Logger.LogError(ex);
   }
   finally{changing=false;}
  }
  private void ApplyPath(InputAction action,int index,string path)
  {
   // Changing keys must not clear another mod's processor/interaction overrides.
   var binding=action.bindings[index];binding.overridePath=PhysicalOverride(action,index,path);action.ApplyBindingOverride(index,binding);
  }
  private void OnDestroy()
  {
   Logger.LogInfo("REBIND_PLUGIN_OBJECT_DESTROYED: retaining managed state, hooks and scene driver.");
  }
  private void Shutdown()
  {
   if(open && nativeRoot!=null && !nativeRoot.activeInHierarchy)DetachControls();

   DestroyNativeEditors();CancelCapture();InputSystem.onActionChange-=ActionChanged;open=false;releaseAt=0;RestoreActions();
   InputSystem.onAfterUpdate-=TickInputMode;
   UnityEngine.SceneManagement.SceneManager.sceneLoaded-=SceneLoaded;
   harmony?.UnpatchSelf();instance=null;
  }
 }
}














