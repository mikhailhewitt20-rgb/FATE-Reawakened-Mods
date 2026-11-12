using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace ZombieGhost.Rebind
{
 internal static class BindingPresentation
 {
  private static readonly Dictionary<string,string> Names=new Dictionary<string,string>(StringComparer.Ordinal)
  {
   ["Fire1"]="Attack / interact",["Fire2"]="Cast spell",["RightClick"]="Secondary click",["Interact"]="Interact",
   ["Rage"]="Activate rage",["CycleWeapon"]="Switch weapon",["Stationary"]="Stand still",["Map"]="Toggle map",
   ["HotbarOne"]="Hotbar 1",["HotbarTwo"]="Hotbar 2",["HotbarThree"]="Hotbar 3",["HotbarFour"]="Hotbar 4",
   ["ToggleRun"]="Toggle running",["ToggleItems"]="Show item labels",["SpellWheel"]="Open spell wheel",
   ["PetAndAllyFocus"]="Focus pet / ally",["CameraZoom"]="Camera zoom",["CameraRotate"]="Camera rotation",
   ["Cancel"]="Back / cancel",["ScrollWheel"]="Scroll",["Click"]="Confirm click",["RightToggle"]="Next tab",["LeftToggle"]="Previous tab",
   ["Navigate"]="Menu navigation",["Submit"]="Confirm selection",["Point"]="Pointer position",["Look"]="Aim / pointer",
   ["SortInventory"]="Sort inventory",["QuickMoveItems"]="Quick move / sell items",["LockItems"]="Lock items",["AltInteract"]="Alternate interaction",
   ["Skills"]="Stats & skills",["Spellbook"]="Spellbook",["Quest"]="Quest journal",["Inventory"]="Inventory",["Menu"]="Pause menu",["Move"]="Movement"
  };
  internal static bool IsMouseKeyboardPath(string path)=>string.IsNullOrEmpty(path)||path.StartsWith("<Keyboard>/",StringComparison.OrdinalIgnoreCase)||path.StartsWith("<Mouse>/",StringComparison.OrdinalIgnoreCase);
  internal static string Pretty(string value)=>Regex.Replace(value??"","([a-z])([A-Z])","$1 $2");
  internal static string Name(string action,string part,bool native,string label)
  {
   if(!native)return Pretty(label)+(string.IsNullOrEmpty(part)?"":" / "+Pretty(part));
   if(action=="CameraRotate" && (part=="negative"||part=="positive"))return part=="negative"?"Camera left":"Camera right";
   if(action=="CameraZoom" && (part=="negative"||part=="positive"))return part=="negative"?"Zoom out":"Zoom in";
   if((action=="Move"||action=="Navigate")&&!string.IsNullOrEmpty(part))return (action=="Move"?"Move ":"Navigate ")+part;
   if(action.StartsWith("SpellSlot",StringComparison.Ordinal))return "Select spell "+action.Substring(9);
   string result=Names.TryGetValue(action,out var name)?name:Pretty(action);
   return result+(string.IsNullOrEmpty(part)?"":" / "+Pretty(part));
  }
  internal static string Category(string action,string map,bool native,string category)
  {
   if(!native)return string.IsNullOrWhiteSpace(category)?"Mod controls":Pretty(category);
   if(action.StartsWith("Camera",StringComparison.Ordinal)||action=="Look")return "Camera";
   if(action=="Move"||action=="Stationary"||action=="ToggleRun")return "Movement";
   if(action=="PetAndAllyFocus")return "Party & pet";
   if(action.StartsWith("Fire",StringComparison.Ordinal)||action.StartsWith("SpellSlot",StringComparison.Ordinal)||action=="Rage"||action=="SpellWheel"||action=="CycleWeapon")return "Combat & spells";
   if(action.StartsWith("Hotbar",StringComparison.Ordinal)||action=="ToggleItems"||action=="SortInventory"||action=="QuickMoveItems"||action=="LockItems"||action=="Interact"||action=="AltInteract")return "Items & interaction";
   return "Menus & interface";
  }
 }
}

