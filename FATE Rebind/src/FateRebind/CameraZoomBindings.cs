using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Cleverous.Prodigy;
using Cleverous.Prodigy.Input;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private void InstallZoomBindings()
  {
   harmony.Patch(AccessTools.Method(typeof(ProdigyCamera),"Update"),
    postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(ApplyNormalizedZoom)),
    transpiler:new HarmonyMethod(typeof(RebindPlugin),nameof(RewriteZoomKeyGate)));
   harmony.Patch(AccessTools.Method(typeof(ProdigyCamera),"OnCamZoomInput"),prefix:new HarmonyMethod(typeof(RebindPlugin),nameof(AllowNativeZoomEvent)));
   Logger.LogInfo("REBIND_ZOOM_INSTALLED: directional wheel and consistent keyboard/mouse zoom.");
  }
  private static IEnumerable<CodeInstruction> RewriteZoomKeyGate(IEnumerable<CodeInstruction> instructions)
   =>ZoomKeyGate.Rewrite(instructions,AccessTools.Method(typeof(UnityEngine.Input),"GetKey",new[]{typeof(KeyCode)}),
    AccessTools.Method(typeof(RebindPlugin),nameof(DisableNativeHeldZoom)));
  // Keyboard/mouse zoom is applied once below instead of by two native paths.
  private static bool DisableNativeHeldZoom(KeyCode unused)=>false;
  private static bool AllowNativeZoomEvent()
  {
   var broker=InputBroker.Instance;return broker==null||(int)broker.LocalInputBehavior!=1;
  }
  private static readonly AccessTools.FieldRef<ProdigyCamera,Vector3> ZoomTarget=AccessTools.FieldRefAccess<ProdigyCamera,Vector3>("targetZoomAmount");
  private static readonly AccessTools.FieldRef<ProdigyCamera,Vector3> ZoomMinimum=AccessTools.FieldRefAccess<ProdigyCamera,Vector3>("camMinZoom");
  private static readonly AccessTools.FieldRef<ProdigyCamera,Vector3> ZoomMaximum=AccessTools.FieldRefAccess<ProdigyCamera,Vector3>("camMaxZoom");
  private static readonly AccessTools.FieldRef<ProdigyCamera,IAgent> ZoomFollow=AccessTools.FieldRefAccess<ProdigyCamera,IAgent>("m_follow");
  private sealed class ZoomRoute
  {internal int Direction;internal ButtonControl[] Buttons;}
  private InputAction zoomAction;
  private ZoomRoute[] zoomRoutes=Array.Empty<ZoomRoute>();
  private bool zoomRoutesDirty=true;
  private int zoomWheelFrame=-1;
  private static bool NativeZoom(InputAction action)=>action?.name=="CameraZoom"&&action.actionMap?.name=="CharacterInputs";
  private void PrepareZoomRoutes(InputAction action)
  {
   if(ReferenceEquals(action,zoomAction)&&!zoomRoutesDirty)return;
   zoomAction=action;var routes=new List<ZoomRoute>();
   foreach(var binding in action.bindings)
   {
    int direction=ZoomInputPolicy.Direction(binding.name);if(binding.isComposite||direction==0)continue;
    string path=ZoomInputPolicy.CanonicalWheel(binding.effectivePath,binding.name);
    if(!BindingPresentation.IsMouseKeyboardPath(path)||string.IsNullOrEmpty(path))continue;
    if(ZoomInputPolicy.WheelPath(path))continue;
    using(var found=InputSystem.FindControls(path))
    {
     var buttons=found.OfType<ButtonControl>().Where(c=>c.device is Keyboard||c.device is Mouse).ToArray();
     if(buttons.Length>0)routes.Add(new ZoomRoute{Direction=direction,Buttons=buttons});
    }
   }
   zoomRoutes=routes.ToArray();zoomRoutesDirty=false;
  }
  private static void ApplyNormalizedZoom(ProdigyCamera __instance)
  {
   var p=instance;if(ReferenceEquals(p,null)||BlocksInput||Tableflip.InputBlocker.AnyBlockingUiMenuIsOpen||Tableflip.InventoryUI.IsDraggingItem)return;
   var broker=InputBroker.Instance;if(broker==null||(int)broker.LocalInputBehavior!=1)return;
   var action=broker.Inputs?.CharacterInputs.CameraZoom;if(action==null||!action.enabled)return;
   var follow=ZoomFollow(__instance);if(follow==null||follow.Vitals==null||follow.Vitals.IsDead||follow.MyTransform==null)return;
   bool heldIn=false,heldOut=false;float wheelIn=0,wheelOut=0;
   bool readWheel=p.zoomWheelFrame!=Time.frameCount;p.zoomWheelFrame=Time.frameCount;
   if(p.wheelZoomEnabled)
   {
    var mouse=Mouse.current;if(mouse!=null&&readWheel){float delta=mouse.scroll.ReadValue().y;wheelIn=ZoomInputPolicy.WheelAmount(delta,true);wheelOut=ZoomInputPolicy.WheelAmount(delta,false);}
   }
   else{p.PrepareZoomRoutes(action);foreach(var route in p.zoomRoutes)if(route.Buttons!=null)foreach(var button in route.Buttons)if(button.isPressed){if(route.Direction>0)heldIn=true;else heldOut=true;break;}}
   float distance=ZoomInputPolicy.Distance(heldIn,heldOut,wheelIn,wheelOut,Time.unscaledDeltaTime);
   if(distance==0)return;
   var target=ZoomTarget(__instance);float a=ZoomMinimum(__instance).z,b=ZoomMaximum(__instance).z;
   target.z=Mathf.Clamp(target.z+distance,Math.Min(a,b),Math.Max(a,b));ZoomTarget(__instance)=target;
  }
 }
}
