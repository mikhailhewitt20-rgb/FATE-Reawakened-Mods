using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ZombieGhost.ModMenu
{
 // The loop timer beside a song: the seconds of the song's end that overlap its
 // start. Clicking or dragging the track sets the value; the mod hears every change
 // and replays the blend. The row redraws itself, so a drag never rebuilds the list.
 internal sealed class ModMenuTrackRow : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
 {
  private RectTransform track,fill,handle;
  private TextMeshProUGUI caption;
  private ModTrack setting;

  internal static void Attach(RectTransform row,ModTrack setting,TextMeshProUGUI caption)
  {
   var rect=ModMenuTheme.Box(row,"Loop",.64f,.06f,.965f,.46f,ModMenuTheme.Inset);
   var fill=ModMenuTheme.Box(rect,"Fill",0,0,0,1,ModMenuTheme.Gold);
   fill.GetComponent<Image>().raycastTarget=false;
   var handle=ModMenuTheme.Box(rect,"LoopHandle",0,0,0,1,new Color(1f,.94f,.78f,1f));
   handle.GetComponent<Image>().raycastTarget=false;
   var view=rect.gameObject.AddComponent<ModMenuTrackRow>();
   view.track=rect;view.fill=fill;view.handle=handle;view.caption=caption;view.setting=setting;
   view.Refresh();
  }

  public void OnPointerDown(PointerEventData eventData)=>Apply(eventData);
  public void OnDrag(PointerEventData eventData)=>Apply(eventData);
  public void OnPointerUp(PointerEventData eventData)=>ModMenuPlugin.RequestRefresh();

  // The mod may clamp the value to the song, and the arrows write too, so the row
  // reads the value back every frame instead of trusting what it sent.
  private void Update()=>Refresh();

  private void Apply(PointerEventData eventData)
  {
   if(track==null||setting==null)return;
   if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(track,eventData.position,eventData.pressEventCamera,out var point))return;
   var width=track.rect.width;
   if(width<=0f)return;
   setting.SeekLoop(Mathf.Clamp01((point.x-track.rect.xMin)/width));
   Refresh();
  }

  internal void Refresh()
  {
   if(track==null||fill==null||setting==null)return;
   var position=setting.LoopNormalized;
   fill.anchorMin=new Vector2(0f,0f);
   fill.anchorMax=new Vector2(position,1f);
   fill.offsetMin=Vector2.zero;
   fill.offsetMax=Vector2.zero;
   handle.anchorMin=new Vector2(position,0f);
   handle.anchorMax=new Vector2(position,1f);
   handle.sizeDelta=new Vector2(6f,6f);
   handle.anchoredPosition=Vector2.zero;
   if(caption!=null)caption.text=setting.LoopText;
  }
 }
}
