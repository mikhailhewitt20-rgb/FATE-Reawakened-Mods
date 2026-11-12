using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private TextMeshProUGUI nativeFont;
  private Button nativeButton,nativeFancyButton;
  private static bool IsOurEditor(Transform transform)
  {for(;transform!=null;transform=transform.parent)if(transform.name=="FATE Rebind Editor")return true;return false;}
  private static bool IsNativeWindowDecoration(Graphic graphic,Transform parent)
  {
   if(!(graphic is Image image)||image.sprite==null||IsOurEditor(image.transform)||image.GetComponentInParent<Selectable>()!=null)return false;
   var bounds=parent as RectTransform;if(bounds==null||bounds.rect.width<=0||bounds.rect.height<=0)return false;
   string identity=(image.name+" "+image.sprite.name).ToLowerInvariant();
   bool ornament=identity.Contains("frame")||identity.Contains("border")||identity.Contains("corner")||identity.Contains("ornament")||identity.Contains("divider");
   bool large=image.rectTransform.rect.width>=bounds.rect.width*.75f&&image.rectTransform.rect.height>=bounds.rect.height*.60f;
   return ornament||large;
  }
  private void FitInsideNativeFrame(Transform parent,RectTransform editor)
  {
   var bounds=parent as RectTransform;if(bounds==null||bounds.rect.width<=0||bounds.rect.height<=0)return;
   var frame=parent.GetComponentsInChildren<Image>(true).Where(image=>IsNativeWindowDecoration(image,parent)&&image.rectTransform.rect.width>=bounds.rect.width*.60f&&image.rectTransform.rect.height>=bounds.rect.height*.50f)
    .OrderByDescending(image=>image.rectTransform.rect.width*image.rectTransform.rect.height).FirstOrDefault();
   if(frame==null)return;
   var corners=new Vector3[4];frame.rectTransform.GetWorldCorners(corners);
   var min=bounds.InverseTransformPoint(corners[0]);var max=bounds.InverseTransformPoint(corners[2]);var rect=bounds.rect;
   editor.anchorMin=new Vector2((min.x-rect.xMin)/rect.width,(min.y-rect.yMin)/rect.height);
   editor.anchorMax=new Vector2((max.x-rect.xMin)/rect.width,(max.y-rect.yMin)/rect.height);
   editor.offsetMin=new Vector2(18,18);editor.offsetMax=new Vector2(-18,-18);
  }
  private void FindNativeTheme()
  {
   bool Ours(Transform t){for(;t!=null;t=t.parent)if(t.name=="FATE Rebind Editor")return true;return false;}
   nativeFont=Resources.FindObjectsOfTypeAll<TextMeshProUGUI>().FirstOrDefault(t=>t!=null&&t.font!=null&&!Ours(t.transform));
   var pauseType=AccessTools.TypeByName("Tableflip.PauseUI");
   nativeFancyButton=pauseType==null?null:Resources.FindObjectsOfTypeAll(pauseType).OfType<Component>().Select(pause=>AccessTools.Field(pauseType,"resumeButton")?.GetValue(pause) as Button).FirstOrDefault(button=>button!=null&&button.targetGraphic is Image image&&image.sprite!=null);
   var entryType=AccessTools.TypeByName("Tableflip.QuestEntryUI");
   nativeButton=entryType==null?null:Resources.FindObjectsOfTypeAll(entryType).OfType<Component>()
    .Select(entry=>AccessTools.Field(entryType,"cancelButton")?.GetValue(entry) as Button)
    .FirstOrDefault(button=>button!=null&&button.targetGraphic is Image image&&image.sprite!=null);
   if(nativeButton==null)nativeButton=Resources.FindObjectsOfTypeAll<Button>().Where(b=>b!=null&&!Ours(b.transform)&&b.targetGraphic is Image image&&image.sprite!=null)
    .OrderBy(b=>b.GetComponentsInChildren<Image>(true).Length).FirstOrDefault();
  }
  private void StyleNativeButton(Button target,bool ornate)
  {
   var template=ornate?nativeFancyButton:nativeButton;
   var rect=Box(target.transform,"Button backing",0,0,1,1,new Color(.16f,.13f,.10f,.98f));rect.SetAsFirstSibling();
   rect.GetComponent<Image>().raycastTarget=false;
   var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.47f,.37f,.24f,.85f);outline.effectDistance=new Vector2(1,-1);
   if(template==null)return;
   var source=template.targetGraphic as Image;var image=(Image)target.targetGraphic;
   image.sprite=source.sprite;image.type=source.type;image.color=source.color;image.fillCenter=source.fillCenter;image.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;
   target.transition=template.transition;target.colors=template.colors;target.spriteState=template.spriteState;
   var colors=target.colors;colors.normalColor=new Color(1,1,1,1);colors.highlightedColor=new Color(1,.86f,.62f,1);colors.pressedColor=new Color(.8f,.59f,.32f,1);target.colors=colors;
   if(ornate)foreach(Transform child in template.transform)CopyButtonDecoration(child,target.transform,source);
  }
  private void CopyButtonDecoration(Transform source,Transform parent,Image excluded)
  {
   if(!(source is RectTransform rect)||source.GetComponent<TMP_Text>()!=null||source.GetComponent<Selectable>()!=null)return;
   string name=source.name.ToLowerInvariant();if(name.Contains("select")||name.Contains("highlight")||name.Contains("pressed")||name.Contains("arrow"))return;
   var original=source.GetComponent<Image>();if(original==null&&source.childCount==0)return;
   var copy=Box(parent,"Native ornament",0,0,1,1);var nativeParent=rect.parent as RectTransform;
   if(nativeParent!=null&&nativeParent.rect.width>0&&nativeParent.rect.height>0)
   {
    var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=nativeParent.InverseTransformPoint(corners[0]);var b=nativeParent.InverseTransformPoint(corners[2]);var bounds=nativeParent.rect;
    copy.anchorMin=new Vector2((a.x-bounds.xMin)/bounds.width,(a.y-bounds.yMin)/bounds.height);copy.anchorMax=new Vector2((b.x-bounds.xMin)/bounds.width,(b.y-bounds.yMin)/bounds.height);
   }
   if(original!=null&&original!=excluded)
   {
    var image=copy.gameObject.AddComponent<Image>();image.sprite=original.sprite;image.type=original.type;image.color=original.color;image.fillCenter=original.fillCenter;image.pixelsPerUnitMultiplier=original.pixelsPerUnitMultiplier;image.raycastTarget=false;
   }
   copy.gameObject.SetActive(source.gameObject.activeSelf);
   foreach(Transform child in source)CopyButtonDecoration(child,copy,excluded);
  }
  private void MarkSelected(Button button,bool selected)
  {
   if(!selected)return;
   var rect=Box(button.transform,"Selected filter",0,.15f,.018f,.85f,Gold);rect.GetComponent<Image>().raycastTarget=false;
   var label=button.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.fontStyle=FontStyles.Bold;
  }
 }
}
