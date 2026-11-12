using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ZombieGhost.ModMenu
{
 // Small drawing kit so the settings screen matches FATE's pause menu instead of
 // looking like a foreign overlay. Colours follow the gold-on-brown native theme.
 internal static class ModMenuTheme
 {
  internal static readonly Color Gold=new Color(.92f,.72f,.38f),Muted=new Color(.64f,.56f,.42f),
   Text=new Color(.91f,.87f,.79f),Panel=new Color(.10f,.075f,.055f,.99f),
   Header=new Color(.25f,.18f,.10f,.80f),ButtonFill=new Color(.24f,.18f,.12f,.98f),
   Inset=new Color(.07f,.055f,.04f,.98f),ScrollTrack=new Color(.10f,.08f,.055f),
   RowFill=new Color(.17f,.13f,.085f,.96f);
  internal static TMP_FontAsset Font;
  internal static RectTransform Box(Transform parent,string name,float left,float bottom,float right,float top,Color? color=null)
  {
   var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
   var rect=(RectTransform)go.transform;
   rect.anchorMin=new Vector2(left,bottom);rect.anchorMax=new Vector2(right,top);
   rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
   if(color.HasValue){var image=go.AddComponent<Image>();image.color=color.Value;}
   return rect;
  }
  internal static TextMeshProUGUI Caption(Transform parent,string value,float left,float bottom,float right,float top,int size,Color color,
   FontStyles style=FontStyles.Normal,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
  {
   var rect=Box(parent,"Label",left,bottom,right,top);
   var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
   if(Font!=null){text.font=Font;text.fontSharedMaterial=Font.material;}
   text.fontSize=size;text.color=color;text.text=value;text.alignment=align;
   text.fontStyle=style;text.richText=false;text.raycastTarget=false;
   text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;
   return text;
  }
  // Icons are drawn, not typed: the game's font has no play, cross or tick glyphs,
  // so a text glyph renders as an empty box. A generated sprite always shows.
  private static Sprite triangle,square,cross;
  internal static Sprite Triangle()
  {
   if(triangle!=null)return triangle;
   triangle=Build(24,delegate(int x,int y)
   {
    var half=11.5f;
    var limit=(23f-x)/23f*half;
    return Mathf.Abs(y-11.5f)<=limit;
   });
   return triangle;
  }
  internal static Sprite Square()
  {
   if(square!=null)return square;
   square=Build(24,delegate(int x,int y){return x>=3&&x<=20&&y>=3&&y<=20;});
   return square;
  }
  internal static Sprite Cross()
  {
   if(cross!=null)return cross;
   cross=Build(24,delegate(int x,int y){return Mathf.Abs(x-y)<=2||Mathf.Abs(x+y-23)<=2;});
   return cross;
  }
  private static Sprite Build(int size,Func<int,int,bool> opaque)
  {
   var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
   texture.filterMode=FilterMode.Bilinear;
   var pixels=new Color32[size*size];
   for(int y=0;y<size;y++)
    for(int x=0;x<size;x++)
     pixels[y*size+x]=opaque(x,y)?new Color32(255,255,255,255):new Color32(255,255,255,0);
   texture.SetPixels32(pixels);
   texture.Apply();
   return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
  }
  // A bare drawn icon with the native hover tint, for play, stop and remove.
  internal static Button Icon(Transform parent,Sprite sprite,float left,float bottom,float right,float top,Color color,Action clicked)
  {
   var rect=Box(parent,"Icon",left,bottom,right,top);
   var image=rect.gameObject.AddComponent<Image>();
   image.sprite=sprite;image.color=color;image.preserveAspect=true;
   var button=rect.gameObject.AddComponent<Button>();
   button.targetGraphic=image;
   var colors=button.colors;
   colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.88f,.68f,1f);
   colors.pressedColor=new Color(.8f,.59f,.32f,1f);colors.selectedColor=new Color(1f,.92f,.76f,1f);
   button.colors=colors;
   // The Button is kept for its hover tint only; the action runs on the press so a
   // redraw between press and release cannot swallow it.
   button.gameObject.AddComponent<ModMenuPress>().Pressed=clicked;
   return button;
  }
  internal static Button Control(Transform parent,string label,float left,float bottom,float right,float top,Action clicked)
  {
   var rect=Box(parent,label,left,bottom,right,top,ButtonFill);
   var outline=rect.gameObject.AddComponent<Outline>();
   outline.effectColor=new Color(.47f,.37f,.24f,.90f);outline.effectDistance=new Vector2(1,-1);
   var button=rect.gameObject.AddComponent<Button>();
   button.targetGraphic=rect.GetComponent<Image>();
   var colors=button.colors;
   colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.86f,.62f,1f);
   colors.pressedColor=new Color(.8f,.59f,.32f,1f);colors.selectedColor=new Color(1f,.90f,.70f,1f);
   button.colors=colors;
   var text=Caption(rect,label,.05f,0,.95f,1,15,Gold,FontStyles.Normal,TextAlignmentOptions.Center);
   button.onClick.AddListener(()=>clicked());
   return button;
  }
 }
}
