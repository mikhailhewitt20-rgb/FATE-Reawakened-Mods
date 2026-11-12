using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ZombieGhost.ModMenu
{
 // Fires on pointer-down instead of on click release. Setting a value makes the screen
 // redraw, which destroys the row under the pointer before the release that would have
 // raised onClick — a small icon then looks dead even though it was hit. Acting on the
 // press itself removes that whole class of failure.
 internal sealed class ModMenuPress : MonoBehaviour, IPointerDownHandler
 {
  internal Action Pressed;

  public void OnPointerDown(PointerEventData eventData)
  {
   if(Pressed==null)return;
   Pressed();
  }
 }
}
