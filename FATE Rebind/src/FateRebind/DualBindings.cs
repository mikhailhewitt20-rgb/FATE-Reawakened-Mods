using System;
using System.Linq;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private void EnsureSecondaryBindings(RegisteredAction entry)
  {
   if(entry.Owner!="game"&&!entry.Owned)return;
   if(entry.Owner=="game"&&!EditorInputPolicy.NativeControllerAction(entry.Action.actionMap?.name,entry.Action.name))return;
   var original=entry.Action.bindings.Select(b=>new BindingDefinition{Id=b.id.ToString("D"),Name=b.name,Path=b.path,Groups=b.groups,Processors=b.processors,Interactions=b.interactions,Composite=b.isComposite,Part=b.isPartOfComposite}).ToArray();
   var keyboardPlan=entry.Owner!="game"||VanillaBindingPolicy.Allows(entry.Action.actionMap?.name,entry.Action.name)?SecondaryBindingPlanner.Plan(original):Array.Empty<BindingDefinition>();
   var plan=keyboardPlan.Concat(SecondaryBindingPlanner.PlanController(original,entry.Id,entry.Owner=="game"?ControllerBindingRules.NativeButtonAction(entry.Action.actionMap?.name,entry.Action.name):entry.Action.type==InputActionType.Button)).ToArray();if(plan.Length==0)return;
   var action=entry.Action;var asset=action.actionMap?.asset;
   var enabled=asset!=null?asset.actionMaps.SelectMany(m=>m.actions).Where(a=>a.enabled).ToArray():action.actionMap!=null?action.actionMap.actions.Where(a=>a.enabled).ToArray():action.enabled?new[]{action}:Array.Empty<InputAction>();
   bool wasChanging=changing;changing=true;
   try
   {
    // Unity requires the entire asset disabled while adding bindings, not just this action.
    foreach(var item in enabled)item.Disable();
    foreach(var item in plan)
    {
     var binding=new InputBinding{path=item.Path,name=item.Name,groups=item.Groups,processors=item.Processors,interactions=item.Interactions,id=Guid.Parse(item.Id),isComposite=item.Composite,isPartOfComposite=item.Part};
     action.AddBinding(binding);
    }
   }
   finally{foreach(var item in enabled)item.Enable();changing=wasChanging;}
  }
  private sealed class BindingRow
  {
   internal string Label,Category,Owner,OwnerName;internal BindingSlot[] Inputs;internal EditorOption Option;internal RebindToggle ProviderToggle;internal RebindSlider ProviderSlider;
   internal bool IsOption=>Option!=EditorOption.None;
   internal bool IsProviderToggle=>ProviderToggle!=null;
   internal bool HasToggle=>IsOption||IsProviderToggle||ProviderSlider!=null;
  }
  private static BindingRow OptionRow(string label,EditorOption option,string category)=>new BindingRow{Label=label,Option=option,Category=category,Owner="game",Inputs=Array.Empty<BindingSlot>()};
  // Check marks contributed by other mods through RebindApi.RegisterToggle.
  private RebindToggle[] ProviderToggles()=>RebindApi.Providers.SelectMany(p=>p.Toggles).Where(t=>!t.Removed).ToArray();
  private BindingRow[] ProviderToggleRows()=>ProviderToggles()
   .Where(t=>string.IsNullOrWhiteSpace(search)||(t.Label+" "+t.Category+" "+t.OwnerName).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
   .Select(t=>new BindingRow{Label=t.Label,Category=t.Category,Owner=t.Owner,OwnerName=t.OwnerName,ProviderToggle=t,Inputs=Array.Empty<BindingSlot>()}).ToArray();
  private RebindSlider[] ProviderSliders()=>RebindApi.Providers.SelectMany(p=>p.Sliders).Where(s=>!s.Removed).ToArray();
  private BindingRow[] ProviderSliderRows()=>ProviderSliders()
   .Where(s=>string.IsNullOrWhiteSpace(search)||(s.Label+" "+s.Category+" "+s.OwnerName).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
   .Select(s=>new BindingRow{Label=s.Label,Category=s.Category,Owner=s.Owner,OwnerName=s.OwnerName,ProviderSlider=s,Inputs=Array.Empty<BindingSlot>()}).ToArray();
  private BindingRow[] VisibleRows()=>controllerMode?new[]{OptionRow("Swap sticks",EditorOption.SwapSticks,"Controller options"),OptionRow("Invert camera left/right",EditorOption.InvertCameraX,"Controller options"),OptionRow("Invert camera up/down",EditorOption.InvertCameraY,"Controller options")}.Concat(ProviderToggleRows()).Concat(ProviderSliderRows()).Concat(BindingRows()).ToArray():new[]{OptionRow("Use scroll wheel for zoom",EditorOption.WheelZoom,"Camera options")}.Concat(ProviderToggleRows()).Concat(ProviderSliderRows()).Concat(BindingRows()).ToArray();
  private BindingRow[] BindingRows()=>(controllerMode?ControllerSlots().OrderBy(s=>s.Entry.Owner=="game"?0:1).ThenBy(s=>s.Entry.OwnerName).ThenBy(s=>s.Category).ThenBy(s=>s.Label).ToArray():VisibleSlots()).GroupBy(slot=>slot.Entry.Owner+"|"+slot.Entry.Id+"|"+slot.Label)
   .Select(group=>new BindingRow{Label=group.First().Label,Category=group.First().Category,Owner=group.First().Entry.Owner,OwnerName=group.First().Entry.OwnerName,Inputs=group.Take(2).ToArray()}).ToArray();
 }
}



