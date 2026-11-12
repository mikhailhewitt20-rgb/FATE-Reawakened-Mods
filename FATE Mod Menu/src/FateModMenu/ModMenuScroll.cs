using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ZombieGhost.ModMenu
{
 // The wheel used to jump several screens per notch because ScrollRect multiplies
 // its sensitivity by whatever the active input module reports, and the legacy path
 // reports 120 units a notch instead of 1. This moves the content itself, in rows,
 // so every mod's screen scrolls at the same readable pace on any input setup.
 internal sealed class ModMenuScroll : MonoBehaviour, IScrollHandler
 {
  private RectTransform content,viewport;
  private float rowStep;
  internal static void Attach(ScrollRect scroll,RectTransform content,RectTransform viewport,float rowStep)
  {
   // ScrollRect keeps the drag and the scrollbar; the wheel is handled here.
   scroll.scrollSensitivity=0f;
   var wheel=scroll.gameObject.AddComponent<ModMenuScroll>();
   wheel.content=content;wheel.viewport=viewport;wheel.rowStep=rowStep;
  }
  public void OnScroll(PointerEventData eventData)
  {
   if(content==null||viewport==null)return;
   var delta=eventData.scrollDelta.y;
   if(Mathf.Approximately(delta,0f))return;
   // A mouse reports one notch as 120 (legacy input) or 1 (Input System); a
   // trackpad reports fractions of a notch.
   var notches=Mathf.Abs(delta)>8f?delta/120f:delta;
   notches=Mathf.Clamp(notches,-3f,3f);
   var limit=Mathf.Max(0f,content.rect.height-viewport.rect.height);
   // Wheel forward (positive delta) reveals the rows above, so the content slides
   // down: the anchored Y offset shrinks towards the top of the list.
   var position=Mathf.Clamp(content.anchoredPosition.y-notches*rowStep,0f,limit);
   content.anchoredPosition=new Vector2(content.anchoredPosition.x,position);
  }
 }
}
