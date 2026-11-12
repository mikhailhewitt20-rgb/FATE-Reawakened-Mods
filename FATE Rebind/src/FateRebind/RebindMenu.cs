using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private InputActionRebindingExtensions.RebindingOperation operation;
  private BindingSlot captureSlot,candidate;
  private string candidatePath;
  private float captureAt;
  private string search="";




  private void QueueCapture(BindingSlot slot)
  {CancelCapture();captureSlot=slot;captureAt=Time.unscaledTime+.25f;status="Release the mouse, then press a key or control. Escape cancels.";}
  private void StartCapture()
  {
   var slot=captureSlot;if(slot==null||!open)return;
   try
   {
    if(slot.Action.enabled)suspended.Add(slot.Action);slot.Action.Disable();
    var rebind=slot.Action.PerformInteractiveRebinding(slot.Index)
     .WithCancelingThrough("<Keyboard>/escape").WithControlsExcluding("<Mouse>/position")
     .WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Pointer>/position")
     .WithControlsExcluding("<Pointer>/delta").WithTimeout(15).OnMatchWaitForAnother(.1f)
     .OnApplyBinding((op,path)=>{candidate=slot;candidatePath=path;})
     .OnComplete(op=>
     {
      operation=null;captureSlot=null;op.Dispose();RestoreCaptureAction(slot.Action);
      if(!(slot.Group=="Controller"?ControllerBindingRules.ButtonPath(candidatePath):BindingPresentation.IsMouseKeyboardPath(candidatePath))){candidate=null;status=slot.Group=="Controller"?"Choose a controller button. Stick axes are configured with Swap sticks.":"Choose a keyboard key or mouse button.";return;}
      var conflicts=Conflicts(candidate,candidatePath);
      if(conflicts.Length==0){Change(candidate,candidatePath);candidate=null;}
      else status="Also used by: "+string.Join(", ",conflicts.Take(4).Select(s=>s.Entry.OwnerName+" / "+s.Label))+". Keep both or cancel.";
     })
     .OnCancel(op=>{operation=null;captureSlot=null;op.Dispose();RestoreCaptureAction(slot.Action);cancelHandledUntil=Time.unscaledTime+.1f;status="Rebinding cancelled; previous binding kept.";});
    if(NativeZoom(slot.Action)&&slot.Group!="Controller")rebind.WithControlsExcluding("<Mouse>/scroll").WithControlsExcluding("<Mouse>/scroll/*");
    // Unity derives each composite part's control layout; do not force vector/axis
    // parts added by other mods to become buttons.
    if(slot.Group=="Controller")rebind.WithExpectedControlType("Button").WithControlsHavingToMatchPath("<Gamepad>").WithMatchingEventsBeingSuppressed(false).OnMatchWaitForAnother(1f);else rebind.WithControlsExcluding("<Gamepad>").WithControlsExcluding("<Joystick>").WithControlsExcluding("<Pen>").WithControlsExcluding("<Touchscreen>").WithControlsExcluding("<XRController>");
    operation=rebind;rebind.Start();status="Listening: "+slot.Label+(slot.Group=="Controller"?" — tap a control; hold B to cancel.":" — press a key or mouse control; Escape cancels.");
   }
   catch(Exception ex){operation?.Dispose();operation=null;captureSlot=null;RestoreCaptureAction(slot.Action);status="Could not start rebind: "+ex.Message;Logger.LogError(ex);}
  }
  private BindingSlot[] Conflicts(BindingSlot target,string path)
  {
   if(target==null)return Array.Empty<BindingSlot>();
   return slots.Where(s=>s.Key!=target.Key && !(s.Entry.Owner==target.Entry.Owner&&s.Entry.Id==target.Entry.Id) &&
    (target.Group=="Controller"?IsControllerSlot(s):IsEditableMouseKeyboard(s))&&BindingIdentity.SameControl(s.Path,path)&&BindingIdentity.Overlaps(s.Entry.Owner,s.Map,target.Entry.Owner,target.Map)).ToArray();
  }
  private void RestoreCaptureAction(InputAction action)
  {
   if(action!=null && (!BlocksInput || (NativeEditorActive && !controllerMode && (action.actionMap?.name=="Menu"||action.actionMap?.name=="UI"))) && suspended.Remove(action))action.Enable();
  }
  private void CancelCapture()
  {var target=captureSlot?.Action;captureAt=0;captureSlot=null;candidate=null;candidatePath=null;var active=operation;operation=null;if(active!=null){active.Cancel();active.Dispose();}RestoreCaptureAction(target);}
  private bool IsEditableMouseKeyboard(BindingSlot slot)=>slot.Group!="Controller"&&!slot.ReadOnly&&(slot.Entry.Owner!="game"||VanillaBindingPolicy.Allows(slot.Map,slot.Action.name))&&BindingPresentation.IsMouseKeyboardPath(slot.Original);
  private BindingSlot[] MouseKeyboardSlots()=>slots.Where(IsEditableMouseKeyboard).ToArray();
  private BindingSlot[] VisibleSlots()=>slots.Where(s=>IsEditableMouseKeyboard(s)&&(!wheelZoomEnabled||s.Entry.Owner!="game"||!NativeZoom(s.Action))&&(string.IsNullOrWhiteSpace(search)||(s.Label+" "+s.Category+" "+s.Entry.OwnerName+" "+s.Path).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(s=>s.Entry.Owner=="game"?0:1).ThenBy(s=>s.Entry.OwnerName).ThenBy(s=>s.Category,StringComparer.Ordinal).ThenBy(s=>s.Label,StringComparer.Ordinal).ThenBy(s=>s.Group,StringComparer.Ordinal).ToArray();
  private static string Pretty(string value)=>System.Text.RegularExpressions.Regex.Replace(value??"","([a-z])([A-Z])","$1 $2");
 }
}












