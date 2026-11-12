using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private readonly Dictionary<string,Sprite> padIcons=new Dictionary<string,Sprite>();
  private UnityEngine.Object gamepadIconSet;
  private Sprite GamepadIcon(string path)
  {
   string field=ControllerBindingRules.IconField(path);if(field==null)return null;
   if(padIcons.TryGetValue(field,out var cached)&&cached!=null)return cached;
   var type=AccessTools.TypeByName("Tableflip.GamepadIcons");
   if(gamepadIconSet==null&&type!=null)gamepadIconSet=Resources.FindObjectsOfTypeAll(type).FirstOrDefault(set=>(AccessTools.Field(type,"buttonSouth")?.GetValue(set) as Sprite)?.name=="Icon-Xbox_A");
   var sprite=gamepadIconSet!=null?AccessTools.Field(type,field)?.GetValue(gamepadIconSet) as Sprite:null;
   if(sprite==null&&field=="selectButton")sprite=Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(item=>item!=null&&item.name=="SteamDeck_Select");
   if(sprite!=null)padIcons[field]=sprite;return sprite;
  }
  private void FillGamepadCell(Button button,BindingSlot slot)
  {
   var label=button.GetComponentInChildren<TextMeshProUGUI>();
   var sprite=slot==null?null:GamepadIcon(slot.Path);
   label.text=sprite!=null?"":slot==null?"—":string.IsNullOrEmpty(slot.Path)?"+":EditorInputPolicy.ControllerLabel(slot.Path);
   if(sprite==null)return;
   var rect=Box(button.transform,"Input glyph",.5f,.5f,.5f,.5f);rect.sizeDelta=new Vector2(38,38);rect.anchoredPosition=Vector2.zero;
   var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
  }
  private Toggle ControllerCheckbox(Transform parent,EditorOption option,bool idle=true)
   =>ControllerCheckbox(parent,()=>OptionValue(option),value=>SetEditorOption(option,value),option+" checkbox",idle);
  private Toggle ProviderCheckbox(Transform parent,RebindToggle entry,bool idle=true)
   =>ControllerCheckbox(parent,()=>entry.Value,value=>entry.SetValue(value),entry.Id+" checkbox",idle);
  private Toggle ControllerCheckbox(Transform parent,Func<bool> get,Action<bool> set,string name,bool idle)
  {
   var rect=Box(parent,name,.75f,.5f,.75f,.5f,new Color(.13f,.1f,.075f));rect.sizeDelta=new Vector2(34,34);
   var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=Gold;outline.effectDistance=new Vector2(1,-1);
   var mark=Box(rect,"Native checkmark",.1f,.1f,.9f,.9f).gameObject.AddComponent<Image>();mark.sprite=Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sprite=>sprite!=null&&sprite.name=="Icon_Checkmark-Options");mark.preserveAspect=true;mark.raycastTarget=false;
   var toggle=rect.gameObject.AddComponent<Toggle>();toggle.targetGraphic=rect.GetComponent<Image>();toggle.graphic=mark;toggle.SetIsOnWithoutNotify(get());toggle.interactable=idle;toggle.onValueChanged.AddListener(value=>set(value));return toggle;
  }
  // Inline numeric slider for a mod option: drag with the mouse, left/right on a controller.
  private Slider SliderCell(Transform row,RebindSlider entry,bool idle)
  {
   var area=Box(row,"Slider",.44f,.3f,.85f,.7f,new Color(.16f,.125f,.09f));
   var fill=Box(area,"Fill",0,.3f,1,.7f,Gold);
   var handle=Box(area,"Handle",0,.05f,.04f,.95f,new Color(.96f,.8f,.5f));
   var slider=area.gameObject.AddComponent<Slider>();
   slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
   slider.direction=Slider.Direction.LeftToRight;
   slider.minValue=entry.Min;slider.maxValue=entry.Max;slider.wholeNumbers=false;
   slider.SetValueWithoutNotify(entry.Value);
   var label=Caption(row,entry.Text,.87f,0,1,1,16);label.alignment=TextAlignmentOptions.MidlineRight;
   slider.onValueChanged.AddListener(value=>{entry.SetValue(value);label.text=entry.Text;});
   slider.interactable=idle;
   return slider;
  }
  private void ControllerHints(Transform parent)
  {
   bool capturing=operation!=null||captureAt>0;
   var controls=capturing?new[]{"buttonEast"}:candidate!=null?new[]{"dpad","buttonSouth","buttonEast"}:new[]{"buttonSouth","buttonWest","buttonNorth","rightStick","leftShoulder","buttonEast"};
   var labels=capturing?new[]{"Hold to cancel"}:candidate!=null?new[]{"Choose","Confirm","Cancel"}:new[]{"Assign","Clear","Reset","Scroll","Tabs","Close"};
   for(int i=0;i<controls.Length;i++)
   {
    float start=i/(float)controls.Length;var item=Box(parent,"Hint",start,0,start+1f/controls.Length,1);
    var image=Box(item,"Glyph",0,.05f,.23f,.95f).gameObject.AddComponent<Image>();image.sprite=GamepadIcon("<Gamepad>/"+controls[i]);image.preserveAspect=true;image.raycastTarget=false;image.enabled=image.sprite!=null;
    if(controls.Length==6&&i==4){image.rectTransform.anchorMax=new Vector2(.17f,.95f);var right=Box(item,"Next tab glyph",.18f,.05f,.35f,.95f).gameObject.AddComponent<Image>();right.sprite=GamepadIcon("<Gamepad>/rightShoulder");right.preserveAspect=true;right.raycastTarget=false;Caption(item,labels[i],.38f,0,.99f,1,14);}else Caption(item,labels[i],.26f,0,.99f,1,14);
   }
  }
 }
}




