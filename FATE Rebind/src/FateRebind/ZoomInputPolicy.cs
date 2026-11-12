using System;
namespace ZombieGhost.Rebind
{
 internal static class ZoomInputPolicy
 {
  internal static bool WheelPath(string path)=>!string.IsNullOrEmpty(path)&&path.IndexOf("/scroll",StringComparison.OrdinalIgnoreCase)>=0;
  internal static string CanonicalWheel(string path,string part,float capturedDirection=0)
  {
   if(!WheelPath(path)||path.EndsWith("/up",StringComparison.OrdinalIgnoreCase)||path.EndsWith("/down",StringComparison.OrdinalIgnoreCase))return path;
   if(!path.EndsWith("/y",StringComparison.OrdinalIgnoreCase)&&!path.EndsWith("/scroll",StringComparison.OrdinalIgnoreCase))return path;
   int scroll=path.IndexOf("/scroll",StringComparison.OrdinalIgnoreCase);
   bool up=capturedDirection>0||capturedDirection==0&&part=="positive";
   return path.Substring(0,scroll)+"/scroll/"+(up?"up":"down");
  }
  internal static int Direction(string part)=>part=="positive"?1:part=="negative"?-1:0;
  internal static float WheelAmount(float delta,bool up)
   =>float.IsNaN(delta)||float.IsInfinity(delta)?0:Math.Min(1,Math.Max(0,up?delta:-delta));
  internal static float Distance(bool zoomIn,bool zoomOut,float wheelIn,float wheelOut,float dt)
  {
   if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)dt=0;
   return ((zoomIn?1:0)-(zoomOut?1:0))*8*Math.Min(dt,.1f)+wheelIn-wheelOut;
  }
 }
}
