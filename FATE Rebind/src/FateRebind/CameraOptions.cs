using HarmonyLib;
using Cleverous.Prodigy.Input;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private bool wheelZoomEnabled=true,invertCameraX,invertCameraY;
  private void LoadCameraOptions()
  {wheelZoomEnabled=ReadOption(EditorOption.WheelZoom);invertCameraX=ReadOption(EditorOption.InvertCameraX);invertCameraY=ReadOption(EditorOption.InvertCameraY);}
  private bool ReadOption(EditorOption option)=>store.TryGet(EditorPreferencePolicy.Key(option),out var value)?value=="1":EditorPreferencePolicy.Default(option);
  private bool OptionValue(EditorOption option)=>option==EditorOption.SwapSticks?swapControllerSticks:option==EditorOption.WheelZoom?wheelZoomEnabled:option==EditorOption.InvertCameraX?invertCameraX:invertCameraY;
  private void AssignOption(EditorOption option,bool value)
  {if(option==EditorOption.WheelZoom)wheelZoomEnabled=value;else if(option==EditorOption.InvertCameraX)invertCameraX=value;else if(option==EditorOption.InvertCameraY)invertCameraY=value;}
  private void SetEditorOption(EditorOption option,bool value)
  {
   if(option==EditorOption.SwapSticks){SetSwapSticks(value);return;}
   if(option==EditorOption.None||OptionValue(option)==value)return;
   string key=EditorPreferencePolicy.Key(option);bool old=OptionValue(option),had=store.TryGet(key,out var saved);
   try{store.Set(key,value?"1":"0");store.Save();AssignOption(option,value);status=option==EditorOption.WheelZoom?(value?"Wheel zoom on. Zoom bindings are hidden.":"Wheel zoom off. Assign zoom buttons under Camera."):"Camera axis preference saved.";}
   catch(System.Exception ex){if(had)store.Set(key,saved);else store.Remove(key);AssignOption(option,old);status="Could not save camera option: "+ex.Message;Logger.LogError(ex);}
   zoomRoutesDirty=true;nativeSignature=controllerSignature=null;
  }
  private void InstallCameraAxisHooks()
  {
   harmony.Patch(AccessTools.PropertyGetter(typeof(InputBroker),"CameraRotateInputValue"),postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(CameraHorizontalAxis)));
   harmony.Patch(AccessTools.PropertyGetter(typeof(InputBroker),"CameraZoomInputValue"),postfix:new HarmonyMethod(typeof(RebindPlugin),nameof(CameraVerticalAxis)));
  }
  private static void CameraHorizontalAxis(InputBroker __instance,ref float __result)
  {if(!ReferenceEquals(instance,null))__result=EditorPreferencePolicy.Axis(__result,(int)__instance.LocalInputBehavior==0,true,instance.invertCameraX);}
  private static void CameraVerticalAxis(InputBroker __instance,ref float __result)
  {if(!ReferenceEquals(instance,null))__result=EditorPreferencePolicy.Axis(__result,(int)__instance.LocalInputBehavior==0,false,instance.invertCameraY);}
 }
}
