using UnityEngine;
namespace MikhailKillsZombies.FateAiming
{
 // Draws the configured reticle into a small texture so the Mod Menu can show what
 // a colour, thickness or rotation change looks like without entering the game.
 // It is rebuilt only when one of the values that shape it changes.
 static class ReticlePreview
 {
  const int Size=160;
  static string cachedKey;
  static Texture2D cached;

  internal static Texture2D Render(string key,float rotation,float scale,Color crosshair,float crosshairOpacity,
   float ringScale,Color ring,float ringOpacity,float thicknessShare)
  {
   if(cached!=null&&cachedKey==key)return cached;
   if(cached!=null)UnityEngine.Object.Destroy(cached);
   cached=Raster(rotation,scale,crosshair,crosshairOpacity,ringScale,ring,ringOpacity,thicknessShare);
   cachedKey=key;
   return cached;
  }

  static Texture2D Raster(float rotation,float scale,Color crosshair,float crosshairOpacity,
   float ringScale,Color ring,float ringOpacity,float thicknessShare)
  {
   var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false);
   texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
   var pixels=new Color32[Size*Size];
   float center=(Size-1)*.5f;
   float limit=center-4f;
   // Same proportions as the drawn reticle, shrunk to fit a large scale.
   float arm=12f*scale;
   float radius=arm*ringScale;
   var fit=Mathf.Min(1f,limit/Mathf.Max(arm,Mathf.Max(radius,1f)));
   arm*=fit;
   radius*=fit;
   float thickness=Mathf.Clamp01(thicknessShare)*radius;
   float inner=radius-thickness;
   float gap=arm*.4f;
   float bar=Mathf.Max(2f,arm/4f);
   var sine=Mathf.Sin(-rotation*Mathf.Deg2Rad);
   var cosine=Mathf.Cos(-rotation*Mathf.Deg2Rad);
   var crossAlpha=Mathf.Clamp01(crosshairOpacity)*.95f;
   var ringAlpha=Mathf.Clamp01(ringOpacity);
   for(int y=0;y<Size;y++)
    for(int x=0;x<Size;x++)
    {
     float dx=x-center,dy=y-center;
     // The circle is round, so only the crosshair has to be un-rotated.
     float rx=dx*cosine-dy*sine,ry=dx*sine+dy*cosine;
     var arms=Arm(rx,ry,gap,bar,arm);
     var distance=Mathf.Sqrt(dx*dx+dy*dy);
     var circle=Mathf.Clamp01(radius-distance+.5f)*(inner<=0f?1f:Mathf.Clamp01(distance-inner+.5f));
     // The circle is drawn over the crosshair at game time; keep that order here.
     var behind=new Color(crosshair.r,crosshair.g,crosshair.b,arms*crossAlpha);
     var front=new Color(ring.r,ring.g,ring.b,circle*ringAlpha);
     pixels[y*Size+x]=Over(front,behind);
    }
   texture.SetPixels32(pixels);
   texture.Apply();
   return texture;
  }

  // Coverage of one pixel by the four arms and the centre dot.
  static float Arm(float x,float y,float gap,float bar,float arm)
  {
   var half=bar*.5f;
   var coverage=Bar(x,y,gap,gap+arm,-half,half);
   coverage=Mathf.Max(coverage,Bar(x,y,-gap-arm,-gap,-half,half));
   coverage=Mathf.Max(coverage,Bar(x,y,-half,half,gap,gap+arm));
   coverage=Mathf.Max(coverage,Bar(x,y,-half,half,-gap-arm,-gap));
   return Mathf.Max(coverage,Bar(x,y,-half,half,-half,half));
  }

  // One pixel of soft edge, so a preview does not crawl while a slider moves.
  static float Bar(float x,float y,float left,float right,float bottom,float top)
  {
   float cx=Mathf.Clamp(x,left,right),cy=Mathf.Clamp(y,bottom,top);
   float dx=x-cx,dy=y-cy;
   return Mathf.Clamp01(.5f-Mathf.Sqrt(dx*dx+dy*dy));
  }

  static Color32 Over(Color front,Color behind)
  {
   var alpha=front.a+behind.a*(1f-front.a);
   if(alpha<=.001f)return new Color32(0,0,0,0);
   var r=(front.r*front.a+behind.r*behind.a*(1f-front.a))/alpha;
   var g=(front.g*front.a+behind.g*behind.a*(1f-front.a))/alpha;
   var b=(front.b*front.a+behind.b*behind.a*(1f-front.a))/alpha;
   return new Color32(ToByte(r),ToByte(g),ToByte(b),ToByte(alpha));
  }

  static byte ToByte(float value)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255f);
 }
}
