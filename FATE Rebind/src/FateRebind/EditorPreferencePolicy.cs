namespace ZombieGhost.Rebind
{
 internal enum EditorOption {None,SwapSticks,WheelZoom,InvertCameraX,InvertCameraY}
 internal static class EditorPreferencePolicy
 {
  internal static bool Default(EditorOption option)=>option==EditorOption.WheelZoom;
  internal static string Key(EditorOption option)=>option==EditorOption.WheelZoom?"settings.camera.wheel-zoom":option==EditorOption.InvertCameraX?"settings.controller.invert-camera-x":option==EditorOption.InvertCameraY?"settings.controller.invert-camera-y":ControllerBindingRules.SwapKey;
  internal static float Axis(float value,bool controller,bool horizontal,bool invert)
   =>!controller?value:value*(horizontal?-1:1)*(invert?-1:1);
 }
}
