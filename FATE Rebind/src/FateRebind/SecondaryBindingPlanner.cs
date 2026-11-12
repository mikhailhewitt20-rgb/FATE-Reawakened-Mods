using System;
using System.Collections.Generic;
using System.Linq;
namespace ZombieGhost.Rebind
{
 internal static class VanillaBindingPolicy
 {
  private static readonly HashSet<string> Character=new HashSet<string>(StringComparer.Ordinal){"Fire1","Fire2","Interact","Rage","CycleWeapon","Stationary","Map","HotbarOne","HotbarTwo","HotbarThree","HotbarFour","ToggleRun","ToggleItems","SpellWheel","PetAndAllyFocus","CameraZoom","CameraRotate","SpellSlot1","SpellSlot2","SpellSlot3","SpellSlot4","SpellSlot5","SpellSlot6"};
  private static readonly HashSet<string> Persistent=new HashSet<string>(StringComparer.Ordinal){"AltInteract","Skills","Spellbook","Quest","Inventory","Menu"};
  internal static bool Allows(string map,string action)=>map=="CharacterInputs"?Character.Contains(action):map=="Persistent"?Persistent.Contains(action):map=="Menu"&&(action=="SortInventory"||action=="QuickMoveItems"||action=="LockItems");
 }
 internal sealed class BindingDefinition
 {
  internal string Id,Name,Path,Groups,Processors,Interactions;
  internal bool Composite,Part;
  internal BindingDefinition Copy()=> (BindingDefinition)MemberwiseClone();
 }
 internal static class SecondaryBindingPlanner
 {
  internal const string ControllerPrimaryMarker="FATE.Rebind.Controller.Primary";
  internal static bool ControllerMarkerName(string name)=>name==ControllerMarker||name==ControllerPrimaryMarker;
  internal const string ControllerMarker="FATE.Rebind.Controller.Secondary";
  internal static BindingDefinition[] PlanController(BindingDefinition[] original,string actionId=null,bool createEmptyButtons=false)
  {
   var inputs=original.Where(b=>!b.Composite&&!b.Part&&((b.Path??"").StartsWith("<Gamepad>/",StringComparison.OrdinalIgnoreCase)||ControllerMarkerName(b.Name))).ToArray();
   if(inputs.Length==0&&createEmptyButtons&&!string.IsNullOrEmpty(actionId))return new[]{
    new BindingDefinition{Id=new Guid(BindingIdentity.StableToken("controller-primary:"+actionId).Substring(0,32)).ToString("D"),Name=ControllerPrimaryMarker,Path="",Groups="Gamepad"},
    new BindingDefinition{Id=new Guid(BindingIdentity.StableToken("controller-secondary:"+actionId).Substring(0,32)).ToString("D"),Name=ControllerMarker,Path="",Groups="Gamepad"}};
   if(inputs.Length!=1)return Array.Empty<BindingDefinition>();
   var added=inputs[0].Copy();added.Id=new Guid(BindingIdentity.StableToken("controller-secondary:"+added.Id).Substring(0,32)).ToString("D");added.Name=ControllerMarker;added.Path="";
   return new[]{added};
  }
  internal const string Marker="FATE.Rebind.Secondary";
  internal static string Id(string original)=>new Guid(BindingIdentity.StableToken("secondary:"+original).Substring(0,32)).ToString("D");
  internal static string Group(BindingDefinition b)=>b.Part?b.Name??"":"";
  internal static bool Editable(BindingDefinition b)=>!ControllerMarkerName(b.Name)&&!b.Composite&&BindingPresentation.IsMouseKeyboardPath(b.Path)&&!(b.Path??"").EndsWith("/position",StringComparison.OrdinalIgnoreCase)&&!(b.Path??"").EndsWith("/delta",StringComparison.OrdinalIgnoreCase);
  internal static BindingDefinition[] Plan(BindingDefinition[] original)
  {
   var additions=new List<BindingDefinition>();var roots=new HashSet<int>();
   var counts=original.Where(Editable).GroupBy(Group).ToDictionary(g=>g.Key,g=>g.Count());
   for(int i=0;i<original.Length;i++)
   {
    var binding=original[i];if(!Editable(binding)||counts[Group(binding)]>=2)continue;
    if(!binding.Part)
    {
     var added=binding.Copy();added.Id=Id(binding.Id);added.Name=Marker;added.Path="";additions.Add(added);counts[Group(binding)]++;continue;
    }
    int root=i-1;while(root>=0&&!original[root].Composite)root--;
    if(root<0||!roots.Add(root))continue;
    var parent=original[root].Copy();parent.Id=Id(parent.Id);parent.Name=Marker;additions.Add(parent);
    for(int part=root+1;part<original.Length&&original[part].Part;part++)
    {
     var added=original[part].Copy();added.Id=Id(added.Id);added.Path="";additions.Add(added);
     if(counts.ContainsKey(Group(added)))counts[Group(added)]++;
    }
   }
   return additions.ToArray();
  }
 }
}


