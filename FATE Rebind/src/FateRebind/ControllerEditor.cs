using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private bool controllerMode;
  private string controllerSignature;
  private int controllerIndex,controllerColumn,controllerDialog;
  private int focusDrawIndex=-1,focusDrawColumn=-1,focusDrawDialog=-1;

  private int detectionFrame=-1,navDirection,swapAxisDirection;
  private float inputGuardUntil,nextNavAt,cancelHold;
  private Vector2 previousLeft,previousRight;
  private BindingRow[] controllerRows=Array.Empty<BindingRow>();
  private readonly List<ControllerRowView> controllerViews=new List<ControllerRowView>();
  private readonly List<RectTransform> controllerRects=new List<RectTransform>();

  private readonly List<Button> controllerDialogButtons=new List<Button>();
  private Transform ControllerSurface=>nativeRoot.transform.Find("ControllerSurface");
  private bool IsControllerSlot(BindingSlot slot)=>!slot.ReadOnly&&slot.Group=="Controller"&&(ControllerBindingRules.ButtonPath(slot.Original)||(string.IsNullOrEmpty(slot.Original)&&(slot.Entry.Owner=="game"?ControllerBindingRules.NativeButtonAction(slot.Map,slot.Action.name):slot.Action.type==InputActionType.Button)));
  private BindingSlot[] ControllerSlots()=>slots.Where(IsControllerSlot).ToArray();
  private void InstallControllerInput(){InputSystem.onAfterUpdate+=TickInputMode;}
  private void TickInputMode()
  {
   if(detectionFrame==Time.frameCount)return;detectionFrame=Time.frameCount;
   var pad=Gamepad.current;var mouse=Mouse.current;var keyboard=Keyboard.current;
   if(controllerMode&&pad==null){CancelCapture();SetControllerMode(false);return;}
   var left=pad?.leftStick.ReadValue()??Vector2.zero;var right=pad?.rightStick.ReadValue()??Vector2.zero;
   bool padInput=pad!=null&&(pad.allControls.OfType<ButtonControl>().Any(button=>button.wasPressedThisFrame)||(left.magnitude>.55f&&(left-previousLeft).magnitude>.25f)||(right.magnitude>.55f&&(right-previousRight).magnitude>.25f));
   previousLeft=left;previousRight=right;
   if(operation!=null||captureAt>0||candidate!=null)return;
   bool mouseInput=mouse!=null&&(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame||mouse.middleButton.wasPressedThisFrame||mouse.scroll.ReadValue().sqrMagnitude>0||mouse.delta.ReadValue().sqrMagnitude>36);
   bool keyInput=keyboard!=null&&keyboard.anyKey.wasPressedThisFrame;
   if(padInput)SetControllerMode(true);else if(mouseInput||keyInput)SetControllerMode(false);
  }
  private void SetControllerMode(bool value)
  {
   if(controllerMode==value)return;controllerMode=value;controllerIndex=controllerColumn=controllerDialog=0;navDirection=swapAxisDirection=0;
   controllerSignature=nativeSignature=null;inputGuardUntil=Time.unscaledTime+.18f;
   Logger.LogInfo("REBIND_INPUT_MODE: "+(value?"controller":"keyboard/mouse"));
   if(open&&value)UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
   if(open){RestoreActions();foreach(var action in InputSystem.ListEnabledActions())Suspend(action);ApplyInputSurface();}
  }
  private void ApplyInputSurface()
  {
   if(nativeRoot==null)return;
   KeyboardSurface.gameObject.SetActive(!controllerMode);ControllerSurface.gameObject.SetActive(controllerMode);
  }

  private sealed class ControllerRowView
  {
   internal GameObject Marker;
   internal Outline Primary,Secondary;
   internal Toggle Toggle;
   internal EditorOption Option;
   internal RebindToggle ProviderToggle;
   internal RebindSlider ProviderSlider;
   internal TextMeshProUGUI ValueLabel;
  }
  private void BuildControllerSurface(Transform parent)
  {
   var root=Box(parent,"ControllerSurface",0,0,1,1);
   Caption(root,"FATE REBIND — CONTROLLER",.02f,.92f,.98f,.99f,24);
   Caption(root,"Action",.03f,.86f,.39f,.91f,16);
   Caption(root,"Primary",.43f,.86f,.61f,.91f,16);
   Caption(root,"Secondary",.68f,.86f,.86f,.91f,16);
   CreateScrollList(root,"Actions",.02f,.20f,.98f,.86f);
   Caption(root,"",.02f,.11f,.98f,.18f,16).name="Controller status";
   Box(root,"Dialog",.02f,.11f,.98f,.18f);
   var hints=Box(root,"Hints",.02f,.015f,.98f,.09f);ControllerHints(hints);
  }
  private void RefreshControllerEditor(bool force=false)
  {
   var available=ControllerSlots();bool iconsReady=GamepadIcon("<Gamepad>/buttonSouth")!=null;
   string signature=iconsReady+"|"+swapControllerSticks+"|"+invertCameraX+"|"+invertCameraY+"|"+status+"|"+string.Join(";",ProviderToggles().Select(t=>t.Id+":"+t.Value))+"|"+string.Join(";",ProviderSliders().Select(s=>s.Id+":"+s.Value))+"|"+(captureSlot?.Key??"")+"|"+(candidate?.Key??"")+"|"+string.Join(";",available.Select(s=>s.Key+":"+s.Path));
   if(!force&&signature==controllerSignature){UpdateControllerFocus(false);return;}controllerSignature=signature;
   var scroll=ControllerSurface.Find("Actions").GetComponent<ScrollRect>();float position=scroll.content.anchoredPosition.y;
   var rows=scroll.content;ClearChildren(rows);controllerViews.Clear();controllerRects.Clear();
   controllerRows=VisibleRows();controllerIndex=Mathf.Clamp(controllerIndex,0,Math.Max(0,controllerRows.Length-1));
   float offset=0;string previous=null;
   for(int i=0;i<controllerRows.Length;i++)
   {
    var entry=controllerRows[i];int index=i;
    string section=(entry.Owner!="game"&&!entry.IsOption?entry.OwnerName+" / ":"")+entry.Category;
    if(section!=previous)
    {
     if(previous!=null)offset+=18;
     var header=PixelRow(rows,"Controller section",offset,40);Caption(header,section,.015f,0,.99f,1,24).fontStyle=FontStyles.Bold;
     var line=Box(header,"Divider",.015f,0,.99f,.025f,new Color(.68f,.49f,.24f,.6f));line.GetComponent<Image>().raycastTarget=false;offset+=48;previous=section;
    }
    var row=PixelRow(rows,"Controller action",offset,55);offset+=62;controllerRects.Add(row);
    var view=new ControllerRowView{Option=entry.Option,ProviderToggle=entry.ProviderToggle,ProviderSlider=entry.ProviderSlider};controllerViews.Add(view);
    var marker=Box(row,"Controller focus",0,.1f,.008f,.9f,Gold);marker.GetComponent<Image>().raycastTarget=false;view.Marker=marker.gameObject;
    Caption(row,entry.Label,.025f,0,.37f,1,18).color=new Color(.91f,.87f,.79f);
    if(entry.ProviderSlider!=null){view.ValueLabel=Caption(row,entry.ProviderSlider.Text,.70f,0,.98f,1,18);view.ValueLabel.alignment=TextAlignmentOptions.MidlineRight;continue;}
    if(entry.HasToggle)
    {

     view.Toggle=entry.IsProviderToggle?ProviderCheckbox(row,entry.ProviderToggle,operation==null&&captureAt==0&&candidate==null):ControllerCheckbox(row,entry.Option,operation==null&&captureAt==0&&candidate==null);
     continue;
    }
    for(int column=0;column<2;column++)
    {
     int slotIndex=column;var slot=entry.Inputs.ElementAtOrDefault(column);float left=column==0?.40f:.66f,right=column==0?.62f:.88f;
     var cell=Control(row,"",left,.06f,right,.94f,()=>{if(Time.unscaledTime<inputGuardUntil||operation!=null||captureAt>0||candidate!=null)return;controllerIndex=index;controllerColumn=slotIndex;QueueControllerBinding();},true);
     cell.interactable=slot!=null;FillGamepadCell(cell,slot);
     if(column==0)view.Primary=cell.GetComponentInChildren<Outline>();else view.Secondary=cell.GetComponentInChildren<Outline>();
    }
    Control(row,"Reset",.90f,.12f,1,.88f,()=>{foreach(var slot in entry.Inputs)Change(slot,null,true);}).interactable=operation==null&&captureAt==0&&candidate==null;
   }
   rows.sizeDelta=new Vector2(0,Mathf.Max(70,offset));
   var dialog=ControllerSurface.Find("Dialog");ClearChildren(dialog);controllerDialogButtons.Clear();
   if(candidate!=null)
   {
    controllerDialogButtons.Add(Control(dialog,"Keep both",0,0,.32f,1,()=>{Change(candidate,candidatePath);candidate=null;}));
    var conflicts=Conflicts(candidate,candidatePath);
    var swap=Control(dialog,"Swap",.34f,0,.66f,1,()=>{var other=Conflicts(candidate,candidatePath);if(other.Length==1)SwapBindings(candidate,other[0],candidatePath,candidate.Path);candidate=null;});swap.interactable=conflicts.Length==1&&!conflicts[0].ReadOnly;controllerDialogButtons.Add(swap);
    controllerDialogButtons.Add(Control(dialog,"Cancel",.68f,0,1,1,()=>{CancelCapture();status="Previous binding kept.";}));
   }
   ControllerSurface.Find("Controller status").GetComponent<TextMeshProUGUI>().text=candidate!=null?"":operation!=null||captureAt>0?status:status.StartsWith("Click a binding",StringComparison.Ordinal)?"":status;
   var hints=ControllerSurface.Find("Hints");ClearChildren(hints);ControllerHints(hints);
   Canvas.ForceUpdateCanvases();scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(position,0,Mathf.Max(0,rows.rect.height-scroll.viewport.rect.height)));focusDrawIndex=-1;UpdateControllerFocus(false);
  }
  private void UpdateControllerFocus(bool scrollTo)
  {
   if(!NativeEditorActive||!controllerMode)return;
   if(!scrollTo&&focusDrawIndex==controllerIndex&&focusDrawColumn==controllerColumn&&focusDrawDialog==controllerDialog)return;
   focusDrawIndex=controllerIndex;focusDrawColumn=controllerColumn;focusDrawDialog=controllerDialog;
   for(int i=0;i<controllerViews.Count;i++)
   {
    var view=controllerViews[i];bool selected=i==controllerIndex;view.Marker.SetActive(selected);
    if(view.Toggle!=null)view.Toggle.SetIsOnWithoutNotify(view.ProviderToggle!=null?view.ProviderToggle.Value:OptionValue(view.Option));
    if(view.ValueLabel!=null&&view.ProviderSlider!=null)view.ValueLabel.text=view.ProviderSlider.Text;
    if(view.Primary!=null)view.Primary.effectColor=selected&&controllerColumn==0?Gold:new Color(.47f,.37f,.24f,.85f);
    if(view.Secondary!=null)view.Secondary.effectColor=selected&&controllerColumn==1?Gold:new Color(.47f,.37f,.24f,.85f);
   }
   for(int i=0;i<controllerDialogButtons.Count;i++)controllerDialogButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontStyle=i==controllerDialog?FontStyles.Bold:FontStyles.Normal;
   if(!scrollTo||controllerIndex>=controllerRects.Count)return;
   var scroll=ControllerSurface.Find("Actions").GetComponent<ScrollRect>();var rect=controllerRects[controllerIndex];float top=-rect.anchoredPosition.y,bottom=top+rect.rect.height,current=scroll.content.anchoredPosition.y;
   if(top<current)current=top;else if(bottom>current+scroll.viewport.rect.height)current=bottom-scroll.viewport.rect.height;
   scroll.StopMovement();scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(current,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
  }
  private void ScrollController(float axis)
  {
   float delta=EditorInputPolicy.StickScroll(axis,Time.unscaledDeltaTime);if(delta==0)return;
   var scroll=ControllerSurface.Find("Actions").GetComponent<ScrollRect>();scroll.StopMovement();
   float top=Mathf.Clamp(scroll.content.anchoredPosition.y+delta,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));scroll.content.anchoredPosition=new Vector2(0,top);
   float center=top+scroll.viewport.rect.height*.5f,best=float.MaxValue;int nearest=controllerIndex;
   for(int i=0;i<controllerRects.Count;i++){var rect=controllerRects[i];float distance=Mathf.Abs(-rect.anchoredPosition.y+rect.rect.height*.5f-center);if(distance<best){best=distance;nearest=i;}}
   controllerIndex=nearest;UpdateControllerFocus(false);
  }
  private BindingSlot ControllerSelection()=>controllerRows.Length==0?null:controllerRows[Mathf.Clamp(controllerIndex,0,controllerRows.Length-1)].Inputs.ElementAtOrDefault(controllerColumn);
  private void QueueControllerBinding()
  {
   if(controllerRows.Length>0&&controllerRows[controllerIndex].HasToggle)
   {
    var row=controllerRows[controllerIndex];
    if(row.ProviderSlider!=null)row.ProviderSlider.Reset();
    else if(row.IsProviderToggle)row.ProviderToggle.SetValue(!row.ProviderToggle.Value);else{var option=row.Option;SetEditorOption(option,!OptionValue(option));}
    return;
   }
   var slot=ControllerSelection();if(slot!=null)QueueCapture(slot);else status="This mod has not provided that binding slot.";
  }
  private void TickController()
  {
   if(!controllerMode||!NativeEditorActive)return;var pad=Gamepad.current;if(pad==null)return;
   if(operation!=null||captureAt>0)
   {
    cancelHold=operation!=null&&pad.buttonEast.isPressed?cancelHold+Time.unscaledDeltaTime:0;
    if(cancelHold>.65f){CancelCapture();cancelHold=0;status="Rebinding cancelled; previous binding kept.";}return;
   }
   cancelHold=0;if(Time.unscaledTime<inputGuardUntil)return;
   var direction=pad.dpad.ReadValue();if(direction.sqrMagnitude<.2f)direction=pad.leftStick.ReadValue();
   int vertical=direction.y>.55f?-1:direction.y<-.55f?1:0;
   bool step=vertical!=0&&(vertical!=navDirection||Time.unscaledTime>=nextNavAt);
   if(step)
   {
    nextNavAt=Time.unscaledTime+(vertical!=navDirection?.35f:.14f);
    if(candidate!=null){controllerDialog=(controllerDialog+vertical+3)%3;for(int attempt=0;attempt<3&&controllerDialogButtons.Count==3&&!controllerDialogButtons[controllerDialog].interactable;attempt++)controllerDialog=(controllerDialog+vertical+3)%3;}
    else controllerIndex=Mathf.Clamp(controllerIndex+vertical,0,Math.Max(0,controllerRows.Length-1));
   }
   navDirection=vertical;
   if(candidate!=null)
   {
    if(pad.buttonEast.wasPressedThisFrame){CancelCapture();status="Previous binding kept.";}
    else if(pad.buttonSouth.wasPressedThisFrame&&controllerDialog<controllerDialogButtons.Count&&controllerDialogButtons[controllerDialog].interactable)controllerDialogButtons[controllerDialog].onClick.Invoke();
    UpdateControllerFocus(false);return;
   }
   if(controllerRows.Length>0&&controllerRows[controllerIndex].HasToggle)
   {
    int horizontal=direction.x>.55f?1:direction.x<-.55f?-1:0;
    if(horizontal!=0&&horizontal!=swapAxisDirection)
    {
     var row=controllerRows[controllerIndex];
     if(row.ProviderSlider!=null){row.ProviderSlider.SetValue(row.ProviderSlider.Value+horizontal*row.ProviderSlider.Step);if(controllerIndex<controllerViews.Count&&controllerViews[controllerIndex].ValueLabel!=null)controllerViews[controllerIndex].ValueLabel.text=row.ProviderSlider.Text;}
     else if(row.IsProviderToggle)row.ProviderToggle.SetValue(horizontal>0);else SetEditorOption(row.Option,horizontal>0);
    }
    swapAxisDirection=horizontal;
   }
   else{swapAxisDirection=0;if(direction.x>.55f)controllerColumn=1;else if(direction.x<-.55f)controllerColumn=0;}
   if(pad.leftShoulder.wasPressedThisFrame||pad.rightShoulder.wasPressedThisFrame){NavigateNativeTab(pad.leftShoulder.wasPressedThisFrame?-1:1);return;}
   if(pad.buttonEast.wasPressedThisFrame){CloseControlsMenu();return;}
   if(pad.buttonSouth.wasPressedThisFrame)QueueControllerBinding();
   else if(pad.buttonWest.wasPressedThisFrame){var slot=ControllerSelection();if(slot!=null)Change(slot,"");}
   else if(pad.buttonNorth.wasPressedThisFrame&&controllerRows.Length>0){var row=controllerRows[controllerIndex];if(row.ProviderSlider!=null)row.ProviderSlider.Reset();else if(row.IsProviderToggle)row.ProviderToggle.Reset();else if(row.IsOption)SetEditorOption(row.Option,EditorPreferencePolicy.Default(row.Option));else foreach(var slot in row.Inputs)Change(slot,null,true);}
   if(captureAt>0){UpdateControllerFocus(true);return;}
   if(!step)ScrollController(pad.rightStick.ReadValue().y);UpdateControllerFocus(step);
  }
 }
}


