using System;
namespace ZombieGhost.Rebind
{
 internal static class ControllerBindingRules
 {
  internal const string SwapKey="settings.controller.swap-sticks";
  internal const string ProviderId="ControllerProvider",CategoryId="ControllerCategory";
  internal static bool NativeButtonAction(string map,string action)=>VanillaBindingPolicy.Allows(map,action)&&action!="CameraRotate"&&action!="CameraZoom";
  internal static string IconField(string path)
  {
   if(!EditorInputPolicy.GamepadPath(path))return null;
   string name=path.Substring(path.IndexOf('/')+1);
   switch(name){case "{Submit}":return "buttonSouth";case "{Cancel}":return "buttonEast";case "start":return "startButton";case "select":return "selectButton";case "dpad/up":return "dpadUp";case "dpad/down":return "dpadDown";case "dpad/left":return "dpadLeft";case "dpad/right":return "dpadRight";default:return name.IndexOf('/')<0?name:null;}
  }
  internal static bool DirectChildId(string id)=>!string.IsNullOrEmpty(id)&&id.IndexOf('/')<0;
  internal static bool ButtonPath(string path)
  {
   if(!EditorInputPolicy.GamepadPath(path))return false;
   int slash=path.IndexOf('/');string control=path.Substring(slash+1);
   switch(control)
   {
    case "buttonSouth":case "buttonNorth":case "buttonEast":case "buttonWest":case "{Submit}":case "{Cancel}":
    case "leftShoulder":case "rightShoulder":case "leftTrigger":case "rightTrigger":case "leftStickPress":case "rightStickPress":case "select":case "start":
    case "dpad/up":case "dpad/down":case "dpad/left":case "dpad/right":return true;
    default:return false;
   }
  }
  internal static bool StickAxis(string path)=>EditorInputPolicy.GamepadPath(path)&&StickPath(path,true)!=path;
  internal static string StickPath(string path,bool swap)
  {
   if(!swap||!EditorInputPolicy.GamepadPath(path))return path;
   int slash=path.IndexOf('/');string layout=path.Substring(0,slash+1),control=path.Substring(slash+1);
   if(control=="leftStick"||control.StartsWith("leftStick/",StringComparison.Ordinal))return layout+"rightStick"+control.Substring(9);
   if(control=="rightStick"||control.StartsWith("rightStick/",StringComparison.Ordinal))return layout+"leftStick"+control.Substring(10);
   return path;
  }
 }
}


