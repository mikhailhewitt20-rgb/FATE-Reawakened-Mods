using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ZombieGhost.Rebind
{
 public sealed class RebindScrollRect:ScrollRect
 {
  public override void OnScroll(PointerEventData data)
  {
   var original=data.scrollDelta;
   try{data.scrollDelta=new Vector2(EditorInputPolicy.Wheel(original.x),EditorInputPolicy.Wheel(original.y));StopMovement();base.OnScroll(data);}
   finally{data.scrollDelta=original;}
  }
 }
}
