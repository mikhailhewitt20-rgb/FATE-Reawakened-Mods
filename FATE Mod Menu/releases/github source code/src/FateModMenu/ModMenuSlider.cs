using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ZombieGhost.ModMenu
{
 // The track under a slider row: click or drag anywhere on it to set the value,
 // and the fill, handle and value text follow at once. A wide range such as a
 // three-second to three-hour day cannot be crossed with arrow presses alone.
 internal sealed class ModMenuSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
 {
  private RectTransform track,fill;
  private TextMeshProUGUI caption;
  private Func<float> read;
  private Action<float> seek;
  private Func<float,string> format;
  // The screen must not rebuild the list under the pointer mid-drag.
  internal static bool Dragging{get;private set;}
  internal static ModMenuSlider Attach(RectTransform track,RectTransform fill,TextMeshProUGUI caption,
   Func<float> read,Action<float> seek,Func<float,string> format)
  {
   var slider=track.gameObject.AddComponent<ModMenuSlider>();
   slider.track=track;slider.fill=fill;slider.caption=caption;
   slider.read=read;slider.seek=seek;slider.format=format;
   slider.Refresh();
   return slider;
  }
  public void OnPointerDown(PointerEventData eventData){Dragging=true;Apply(eventData);}
  public void OnDrag(PointerEventData eventData)=>Apply(eventData);
  public void OnPointerUp(PointerEventData eventData){Dragging=false;ModMenuPlugin.RequestRefresh();}
  private void Apply(PointerEventData eventData)
  {
   if(track==null||seek==null)return;
   var camera=eventData.pressEventCamera;
   if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(track,eventData.position,camera,out var point))return;
   var width=track.rect.width;
   if(width<=0f)return;
   seek(Mathf.Clamp01((point.x-track.rect.xMin)/width));
   Refresh();
  }
  internal void Refresh()
  {
   if(track==null||fill==null||read==null)return;
   float position;
   try{position=Mathf.Clamp01(read());}catch{position=0f;}
   fill.anchorMin=new Vector2(0f,0f);
   fill.anchorMax=new Vector2(position,1f);
   fill.offsetMin=Vector2.zero;
   fill.offsetMax=Vector2.zero;
   if(caption!=null&&format!=null)
   {
    try{caption.text=format(position);}catch{caption.text="";}
   }
  }
 }
}
