using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private GameObject nativeRoot;
  private Transform KeyboardSurface=>nativeRoot.transform.Find("MouseKeyboardSurface");
  private string nativeSignature;

  private static readonly Color Gold=new Color(.92f,.72f,.38f),Panel=new Color(.10f,.075f,.055f,.99f);
  private RectTransform Box(Transform parent,string name,float left,float bottom,float right,float top,Color? color=null)
  {
   var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
   var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(left,bottom);rect.anchorMax=new Vector2(right,top);rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
   if(color.HasValue){var image=go.AddComponent<Image>();image.color=color.Value;}
   return rect;
  }
  private TextMeshProUGUI Caption(Transform parent,string value,float left,float bottom,float right,float top,int size=17)
  {
   var rect=Box(parent,"Label",left,bottom,right,top);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
   if(nativeFont!=null){text.font=nativeFont.font;text.fontSharedMaterial=nativeFont.fontSharedMaterial;}
   text.fontSize=size;text.color=Gold;text.text=value;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
   text.richText=false;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;return text;
  }
  private Button Control(Transform parent,string value,float left,float bottom,float right,float top,Action clicked,bool ornate=false)
  {
   var rect=Box(parent,value,left,bottom,right,top,new Color(.25f,.19f,.12f));
   var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
   var colors=button.colors;colors.highlightedColor=new Color(1,.78f,.42f);colors.pressedColor=new Color(.8f,.47f,.16f);button.colors=colors;
   StyleNativeButton(button,ornate);
   var text=Caption(rect,value,.03f,0,.97f,1);text.alignment=TextAlignmentOptions.Center;
   button.onClick.AddListener(()=>clicked());return button;
  }
  private GameObject BuildNativeEditor(Transform parent)
  {
   FindNativeTheme();
   var outer=Box(parent,"FATE Rebind Editor",.10f,.14f,.90f,.855f,Panel);
   FitInsideNativeFrame(parent,outer);
   var root=Box(outer,"MouseKeyboardSurface",0,0,1,1);
   Caption(root,"FATE REBIND",.02f,.92f,.78f,.99f,22);

   var inputRect=Box(root,"Search",.02f,.85f,.98f,.91f,new Color(.19f,.15f,.11f));
   var input=inputRect.gameObject.AddComponent<TMP_InputField>();var text=Caption(inputRect,"",.02f,0,.98f,1);input.textComponent=text;input.textViewport=inputRect;input.lineType=TMP_InputField.LineType.SingleLine;
   var placeholder=Caption(inputRect,"Search actions…",.02f,0,.98f,1);placeholder.color=new Color(.55f,.5f,.4f);input.placeholder=placeholder;
   input.onValueChanged.AddListener(value=>{search=value;RefreshNativeEditor(true);});
   Caption(root,"Action",.03f,.79f,.47f,.84f,16);
   Caption(root,"Primary",.34f,.79f,.54f,.84f,16);
   Caption(root,"Secondary",.60f,.79f,.80f,.84f,16);
   CreateScrollList(root,"Actions",.02f,.21f,.98f,.79f);
   Caption(root,"",.02f,.075f,.98f,.19f,16).name="Status";
   Box(root,"Footer",.02f,.01f,.98f,.07f);
   BuildControllerSurface(outer);root.gameObject.SetActive(!controllerMode);outer.Find("ControllerSurface").gameObject.SetActive(controllerMode);
   return outer.gameObject;
  }
  private RectTransform CreateScrollList(Transform parent,string name,float left,float bottom,float right,float top)
  {
   var area=Box(parent,name,left,bottom,right,top);
   var viewport=Box(area,"Viewport",0,0,.965f,1,new Color(.07f,.055f,.04f,.98f));viewport.gameObject.AddComponent<RectMask2D>();
   var content=Box(viewport,"Rows",0,1,1,1);content.pivot=new Vector2(.5f,1);
   var track=Box(area,"Scrollbar",.97f,0,1,1,new Color(.1f,.08f,.055f));
   var handle=Box(track,"Handle",0,0,1,1,Gold);var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
   var scroll=area.gameObject.AddComponent<RebindScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=60;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.verticalScrollbar=scrollbar;
   return content;
  }
  private RectTransform PixelRow(Transform parent,string name,float offset,float height)
  {
   var row=Box(parent,name,0,1,1,1);row.pivot=new Vector2(.5f,1);row.anchoredPosition=new Vector2(0,-offset);row.sizeDelta=new Vector2(0,height);return row;
  }
  private void ClearChildren(Transform parent)
  {for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}}
  private void BindingCell(Transform row,BindingSlot slot,float left,float right,bool idle)
  {
   bool wheelSlot=slot!=null&&NativeZoom(slot.Action)&&ZoomInputPolicy.WheelPath(slot.Path);
   string display=slot==null?"Not provided":string.IsNullOrEmpty(slot.Path)||wheelSlot?"Unbound":slot.Action.GetBindingDisplayString(slot.Index);
   if(string.IsNullOrEmpty(display))display=slot?.Path??"Unbound";
   Control(row,display,left,.08f,right-.05f,.92f,()=>{if(slot!=null)QueueCapture(slot);},true).interactable=idle&&slot!=null;
   Control(row,"x",right-.045f,.08f,right,.92f,()=>{if(slot!=null)Change(slot,"");}).interactable=idle&&slot!=null&&!wheelSlot&&!string.IsNullOrEmpty(slot.Path);
  }
  private void RefreshNativeEditor(bool force=false)
  {
   if(!open || nativeRoot==null)return;
   ApplyInputSurface();if(controllerMode){RefreshControllerEditor(force);return;}
   string signature=search+"|"+wheelZoomEnabled+"|"+status+"|"+(captureSlot?.Key??"")+"|"+(candidate?.Key??"")+"|"+string.Join(";",ProviderToggles().Select(t=>t.Id+":"+t.Value))+"|"+string.Join(";",ProviderSliders().Select(s=>s.Id+":"+s.Value))+"|"+string.Join(";",slots.Select(s=>s.Key+":"+s.Path));
   if(!force&&signature==nativeSignature)return;nativeSignature=signature;
   KeyboardSurface.Find("Status").GetComponent<TextMeshProUGUI>().text=status;
   bool idle=operation==null&&captureAt==0&&candidate==null;
   var rows=(RectTransform)KeyboardSurface.Find("Actions/Viewport/Rows");ClearChildren(rows);
   var visible=VisibleRows();float offset=0;string previous=null;
   foreach(var entry in visible)
   {
    string section=(entry.Owner!="game"?entry.OwnerName+" / ":"")+entry.Category;
    if(section!=previous)
    {
     if(previous!=null)offset+=22;
     var header=PixelRow(rows,"Category heading",offset,46);
     var band=header.gameObject.AddComponent<Image>();band.color=new Color(.25f,.18f,.10f,.75f);band.raycastTarget=false;
     var title=Caption(header,section,.015f,.08f,1,1,26);title.fontStyle=FontStyles.Bold;
     var divider=Box(header,"Category divider",.015f,0,.985f,.025f,new Color(.68f,.49f,.24f,.7f));divider.GetComponent<Image>().raycastTarget=false;
     offset+=56;previous=section;
    }
    var row=PixelRow(rows,"Binding pair",offset,52);offset+=60;
    var rowLabel=Caption(row,entry.Label,.035f,0,.32f,1,17);rowLabel.color=new Color(.91f,.87f,.79f);
    if(entry.ProviderSlider!=null){rowLabel.rectTransform.anchorMax=new Vector2(.42f,1);SliderCell(row,entry.ProviderSlider,idle);continue;}
    if(entry.HasToggle){rowLabel.rectTransform.anchorMax=new Vector2(.65f,1);if(entry.IsProviderToggle)ProviderCheckbox(row,entry.ProviderToggle,idle);else ControllerCheckbox(row,entry.Option,idle);continue;}
    BindingCell(row,entry.Inputs.ElementAtOrDefault(0),.33f,.59f,idle);
    BindingCell(row,entry.Inputs.ElementAtOrDefault(1),.60f,.86f,idle);
    Control(row,"Reset",.88f,.08f,1,.92f,()=>{foreach(var slot in entry.Inputs)Change(slot,null,true);}).interactable=idle;
   }
   rows.sizeDelta=new Vector2(0,Mathf.Max(60,offset));
   if(visible.Length==0)Caption(rows,"No matching bindings. Load a character for gameplay actions.",0,0,1,1);
   var footer=KeyboardSurface.Find("Footer");ClearChildren(footer);
   if(candidate!=null)
   {
    Control(footer,"Keep both",0,0,.25f,1,()=>{Change(candidate,candidatePath);candidate=null;RefreshNativeEditor(true);});
    var conflicts=Conflicts(candidate,candidatePath);
    var swap=Control(footer,"Swap bindings",.26f,0,.51f,1,()=>{var other=Conflicts(candidate,candidatePath);if(other.Length==1)SwapBindings(candidate,other[0],candidatePath,candidate.Path);candidate=null;RefreshNativeEditor(true);});
    swap.interactable=conflicts.Length==1&&conflicts[0].Group==candidate.Group&&!conflicts[0].ReadOnly;
    Control(footer,"Cancel",.52f,0,.77f,1,()=>{CancelCapture();status="Previous binding kept.";RefreshNativeEditor(true);});
   }
   else if(captureSlot!=null)Control(footer,"Cancel rebinding",0,0,.4f,1,()=>{CancelCapture();status="Previous binding kept.";RefreshNativeEditor(true);});
  }
 }
}










