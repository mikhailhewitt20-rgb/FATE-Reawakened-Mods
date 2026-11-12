using System;
namespace ZombieGhost.Rebind
{
 internal static class EditorInputPolicy
 {
  internal static float Wheel(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(-1,Math.Min(1,value));
  internal static float StickScroll(float axis,float seconds)
  {
   if(float.IsNaN(axis)||float.IsInfinity(axis)||float.IsNaN(seconds)||seconds<=0||Math.Abs(axis)<=.2f)return 0;
   float magnitude=(Math.Min(1,Math.Abs(axis))-.2f)/.8f;
   return -Math.Sign(axis)*magnitude*450*Math.Min(seconds,.05f);
  }
  internal static bool GamepadPath(string path)=>!string.IsNullOrEmpty(path)&&(path.StartsWith("<Gamepad>/",StringComparison.OrdinalIgnoreCase)||path.StartsWith("<XInput",StringComparison.OrdinalIgnoreCase)||path.StartsWith("<DualShock",StringComparison.OrdinalIgnoreCase)||path.StartsWith("<DualSense",StringComparison.OrdinalIgnoreCase));
  internal static bool NativeControllerAction(string map,string action)=>VanillaBindingPolicy.Allows(map,action)||(map=="Persistent"&&action=="Move")||(map=="CharacterInputs"&&action=="Look");
  internal static string ControllerLabel(string path)
  {
   if(string.IsNullOrEmpty(path))return "Unbound";
   int slash=path.IndexOf('/');string name=slash<0?path:path.Substring(slash+1);
   switch(name){case "buttonSouth":case "{Submit}":return "A";case "buttonEast":case "{Cancel}":return "B";case "buttonWest":return "X";case "buttonNorth":return "Y";case "leftShoulder":return "LB";case "rightShoulder":return "RB";case "leftTrigger":return "LT";case "rightTrigger":return "RT";case "start":return "Menu";case "select":return "View";case "leftStick":return "Left stick";case "rightStick":return "Right stick";case "leftStickPress":return "L3";case "rightStickPress":return "R3";case "rightStick/x":return "RS left / right";case "rightStick/y":return "RS up / down";default:return BindingPresentation.Pretty(name.Replace("dpad/","D-pad "));}
  }
 }
}

