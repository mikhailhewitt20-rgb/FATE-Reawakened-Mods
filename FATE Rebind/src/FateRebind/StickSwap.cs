using System;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private bool swapControllerSticks;
  private string PhysicalOverride(InputAction action,int index,string logicalOverride)
  {
   string logical=logicalOverride??action.bindings[index].path;
   string corrected=NativeZoom(action)?ZoomInputPolicy.CanonicalWheel(logical,action.bindings[index].name):logical;
   string physical=ControllerBindingRules.StickPath(corrected,swapControllerSticks);
   return physical==logical?logicalOverride:physical;
  }
  private void SetSwapSticks(bool value)
  {
   if(swapControllerSticks==value)return;
   bool old=swapControllerSticks,had=store.TryGet(ControllerBindingRules.SwapKey,out var saved);
   try
   {
    store.Set(ControllerBindingRules.SwapKey,value?"1":"0");store.Save();swapControllerSticks=value;
    ReapplyBindings();status=value?"Sticks swapped.":"Default sticks restored.";
    Logger.LogInfo("REBIND_STICK_SWAP "+value);
   }
   catch(Exception ex)
   {
    swapControllerSticks=old;if(had)store.Set(ControllerBindingRules.SwapKey,saved);else store.Remove(ControllerBindingRules.SwapKey);
    try{store.Save();ReapplyBindings();}catch(Exception rollback){Logger.LogError(rollback);}
    status="Could not save stick layout: "+ex.Message;Logger.LogError(ex);
   }
   controllerSignature=null;
  }
  private void ReapplyBindings()
  {
   bool oldChanging=changing;changing=true;
   try
   {
    foreach(var entry in tracked.Values)
    for(int index=0;index<entry.Action.bindings.Count;index++)
    {
     if(entry.Action.bindings[index].isComposite)continue;
     string key=Key(entry,index);
     string logical=store.TryGet(key,out var saved)?saved:baselines.TryGetValue(entry.Action,out var defaults)&&defaults.TryGetValue(key,out var baseline)?baseline:null;
     if(ControllerBindingRules.StickAxis(entry.Action.bindings[index].path)||ControllerBindingRules.StickAxis(logical))ApplyPath(entry.Action,index,logical);
    }
   }
   finally{changing=oldChanging;}
  }
 }
}


