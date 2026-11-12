using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace ZombieGhost.ModMenu
{
 // Two steps, matching how the game's own menus nest: the first screen lists the
 // mods that registered settings, and choosing one opens that mod's own screen of
 // sliders, toggles, colours and pictures. Escape walks back one step.
 internal sealed class ModMenuScreen : MonoBehaviour
 {
  private const float HeadingHeight=46f,HeadingStep=58f,RowHeight=52f,RowStep=58f,NoteHeight=40f,
   ModuleRowHeight=50f,ModuleRowStep=56f,
   // Each row keeps a gutter at both ends for its arrows, so a button can never
   // land on top of the label or the value text.
   Gutter=.075f;
  private RectTransform rows,area,previewHost,window;
  private TextMeshProUGUI subtitle,status;
  private readonly List<Selectable> order=new List<Selectable>();
  private string signature;
  private ModModule open;
  private bool resetScroll;
  private string flash;
  private float flashUntil;
  private float nextSnapshotAt;
  private const float SnapshotInterval=0.2f;

  internal static ModMenuScreen Create(Transform canvas)
  {
   var go=new GameObject("FATE Mod Menu",typeof(RectTransform));
   var rect=(RectTransform)go.transform;
   rect.SetParent(canvas,false);
   rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
   rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
   rect.SetAsLastSibling();
   var screen=go.AddComponent<ModMenuScreen>();
   screen.Build();
   return screen;
  }

  internal void Invalidate()=>signature=null;

  private void Build()
  {
   ModMenuTheme.Font=FindFont();
   var dim=ModMenuTheme.Box(transform,"Backdrop",0,0,1,1,new Color(0,0,0,.62f));
   dim.GetComponent<Image>().raycastTarget=true;

   var panel=ModMenuTheme.Box(transform,"Window",.17f,.055f,.83f,.945f,ModMenuTheme.Panel);
   window=panel;
   var outline=panel.gameObject.AddComponent<Outline>();
   outline.effectColor=new Color(.55f,.40f,.20f,.90f);outline.effectDistance=new Vector2(2,-2);
   var top=ModMenuTheme.Box(panel,"Top rule",.03f,.936f,.97f,.944f,ModMenuTheme.Gold);
   top.GetComponent<Image>().raycastTarget=false;
   var bottom=ModMenuTheme.Box(panel,"Bottom rule",.03f,.145f,.97f,.151f,new Color(.68f,.49f,.24f,.70f));
   bottom.GetComponent<Image>().raycastTarget=false;

   ModMenuTheme.Caption(panel,"MOD SETTINGS",.04f,.855f,.70f,.925f,26,ModMenuTheme.Gold,FontStyles.Bold);
   subtitle=ModMenuTheme.Caption(panel,"",.04f,.80f,.96f,.855f,14,ModMenuTheme.Muted);

   area=ModMenuTheme.Box(panel,"List",.04f,.175f,.96f,.795f);
   var viewport=ModMenuTheme.Box(area,"Viewport",0,0,.965f,1,ModMenuTheme.Inset);
   viewport.gameObject.AddComponent<RectMask2D>();
   rows=ModMenuTheme.Box(viewport,"Content",0,1,1,1);
   rows.anchorMin=new Vector2(0,1);rows.anchorMax=new Vector2(1,1);
   rows.pivot=new Vector2(.5f,1);rows.anchoredPosition=Vector2.zero;rows.sizeDelta=new Vector2(0,RowStep);
   var track=ModMenuTheme.Box(area,"Scrollbar",.97f,0,1,1,ModMenuTheme.ScrollTrack);
   var handle=ModMenuTheme.Box(track,"Handle",0,0,1,1,ModMenuTheme.Gold);
   var scrollbar=track.gameObject.AddComponent<Scrollbar>();
   scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();
   scrollbar.direction=Scrollbar.Direction.BottomToTop;
   var scroll=area.gameObject.AddComponent<ScrollRect>();
   scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.vertical=true;
   scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;
   scroll.verticalScrollbar=scrollbar;
   ModMenuScroll.Attach(scroll,rows,viewport,RowStep*0.45f);

   // The picture pane sits beside the list, not inside it, so a preview stays in
   // view however far the settings are scrolled.
   previewHost=ModMenuTheme.Box(panel,"Preview",.695f,.175f,.96f,.795f,ModMenuTheme.Inset);
   previewHost.gameObject.AddComponent<RectMask2D>();
   var frame=previewHost.gameObject.AddComponent<Outline>();
   frame.effectColor=new Color(.47f,.37f,.24f,.55f);frame.effectDistance=new Vector2(1,-1);
   previewHost.gameObject.SetActive(false);

   ModMenuTheme.Control(panel,"Back",.04f,.045f,.19f,.12f,Back);
   status=ModMenuTheme.Caption(panel,"",.21f,.045f,.96f,.12f,14,ModMenuTheme.Muted);
   BuildRows();
  }

  // Escape and the Back button both climb one step: mod screen, then mod list,
  // then out of the menu.
  private void Back()
  {
   if(open!=null){open=null;resetScroll=true;Invalidate();BuildRows();return;}
   ModMenuPlugin.CloseScreen();
  }

  private void BuildRows()
  {
   for(int i=rows.childCount-1;i>=0;i--)
   {
    var child=rows.GetChild(i).gameObject;
    child.SetActive(false);
    Destroy(child);
   }
   order.Clear();
   signature=Snapshot();
   var settingsView=open!=null&&!open.Disposed;
   var hasPreview=settingsView&&open.Settings.Any(s=>s is ModPreview&&!s.Removed);
   // Without a preview the list takes the whole width back.
   area.anchorMin=new Vector2(.04f,.175f);
   area.anchorMax=hasPreview?new Vector2(.655f,.795f):new Vector2(.96f,.795f);
   previewHost.gameObject.SetActive(hasPreview);
   if(resetScroll){resetScroll=false;rows.anchoredPosition=Vector2.zero;}
   var offset=6f;
   var count=0;
   if(!settingsView)offset=BuildModules(offset);
   else offset=BuildSettings(open,offset,ref count);
   rows.sizeDelta=new Vector2(0,Mathf.Max(offset,RowStep));
   subtitle.text=settingsView
    ?open.Name+"  /  "+count+" setting"+(count==1?"":"s")
    :"Choose a mod to adjust. Key bindings stay in Options > Controls.";
   status.text=ModMenuApi.Registered.Count(m=>!m.Disposed)==0
    ?"FATE Mod Menu "+ModMenuPlugin.Version+" / API v"+ModMenuApi.ApiVersion
    :count+" setting"+(count==1?"":"s")+" / API v"+ModMenuApi.ApiVersion;
   // A paste result stays readable for a moment instead of vanishing on rebuild.
   if(!string.IsNullOrEmpty(flash)&&Time.unscaledTime<flashUntil)status.text=flash;
   if(hasPreview)BuildPreviewPane(open);
   ChainNavigation();
   if(order.Count>0&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(order[0].gameObject);
  }

  // Step one: one row per mod that registered anything.
  private float BuildModules(float offset)
  {
   var modules=ModMenuApi.Registered.Where(m=>!m.Disposed&&m.Settings.Any(s=>!s.Removed))
    .OrderBy(m=>m.Name,StringComparer.Ordinal).ToArray();
   if(modules.Length==0)
   {
    var empty=Row(offset,64);
    ModMenuTheme.Caption(empty,"No mod settings are registered yet. A mod appears here once it registers with FATE Mod Menu.",
     .02f,0,.98f,1,15,ModMenuTheme.Muted);
    return offset+80f;
   }
   foreach(var module in modules)
   {
    var target=module;
    var count=module.Settings.Count(s=>!s.Removed);
    var row=Row(offset,ModuleRowHeight);
    offset+=ModuleRowStep;
    order.Add(RowButton(row,module.Name,count+(count==1?" setting":" settings"),()=>{open=target;resetScroll=true;Invalidate();BuildRows();}));
   }
   return offset;
  }

  // Step two: that mod's own screen, grouped by category.
  private float BuildSettings(ModModule module,float offset,ref int count)
  {
   var visible=module.Settings.Where(s=>!s.Removed).ToArray();
   foreach(var category in visible.Select(s=>s.Category).Distinct().ToArray())
   {
    var heading=Row(offset,HeadingHeight);
    offset+=HeadingStep;
    var band=heading.gameObject.AddComponent<Image>();
    band.color=ModMenuTheme.Header;band.raycastTarget=false;
    var divider=ModMenuTheme.Box(heading,"Divider",.015f,0,.985f,.03f,new Color(.68f,.49f,.24f,.70f));
    divider.GetComponent<Image>().raycastTarget=false;
    ModMenuTheme.Caption(heading,category,.02f,.06f,.62f,.96f,19,ModMenuTheme.Gold,FontStyles.Bold);
    order.Add(ModMenuTheme.Control(heading,"Defaults",.80f,.14f,.985f,.88f,()=>{module.ResetCategory(category);Invalidate();}));

    foreach(var setting in visible.Where(s=>s.Category==category))
    {
     count++;
     switch(setting)
     {
      case ModNote note:
      {
       var noteRow=Row(offset,NoteHeight);
       offset+=NoteHeight+6f;
       ModMenuTheme.Caption(noteRow,note.Content,.02f,0,.98f,1,14,ModMenuTheme.Muted);
       break;
      }
      case ModPreview preview:
      {
       // Drawn in the pinned pane beside the list, never inside the scroll.
       break;
      }
      case ModColor color:
      {
       var header=Row(offset,RowHeight);
       offset+=RowStep;
       ModMenuTheme.Caption(header,color.Label,.02f,0,.40f,1,16,ModMenuTheme.Text);
       var swatch=ModMenuTheme.Box(header,"Swatch",.42f,.22f,.50f,.78f,color.Value);
       var border=swatch.gameObject.AddComponent<Outline>();
       border.effectColor=new Color(.47f,.37f,.24f,.90f);border.effectDistance=new Vector2(1,-1);
       // The chip is an editable field: click it to type a code or press Ctrl+V to
       // paste one; Copy puts the current code on the clipboard for reuse.
       var target=color;
       var chip=ModMenuTheme.Box(header,"Hex",.52f,.18f,.845f,.82f,ModMenuTheme.Inset);
       var chipBorder=chip.gameObject.AddComponent<Outline>();
       chipBorder.effectColor=new Color(.47f,.37f,.24f,.70f);chipBorder.effectDistance=new Vector2(1,-1);
       var hexText=ModMenuTheme.Caption(chip,color.Hex,.06f,0,.94f,1,15,ModMenuTheme.Text);
       ModMenuHexField.Attach(chip,hexText,()=>target.Hex,value=>target.SetHex(value),message=>Flash(message));
       order.Add(ModMenuTheme.Control(header,"Copy",.855f,.18f,.99f,.82f,()=>Copy(target.Hex)));
       for(int channel=0;channel<3;channel++)
       {
        var index=channel;
        var channelRow=Row(offset,RowHeight);
        offset+=RowStep;
        SliderRow(channelRow,ChannelName(index),
         ()=>color.Channel(index),
         position=>color.SetChannel(index,position),
         direction=>color.SetChannel(index,color.Channel(index)+direction*5f/255f),
         _=>Mathf.RoundToInt(color.Channel(index)*255f).ToString());
       }
       break;
      }
      case ModText text:
      {
       var header=Row(offset,RowHeight);
       offset+=RowStep;
       ModMenuTheme.Caption(header,text.Label,.02f,0,.34f,1,16,ModMenuTheme.Text);
       var chip=ModMenuTheme.Box(header,"Text",.36f,.18f,.96f,.82f,ModMenuTheme.Inset);
       var chipBorder=chip.gameObject.AddComponent<Outline>();
       chipBorder.effectColor=new Color(.47f,.37f,.24f,.70f);chipBorder.effectDistance=new Vector2(1,-1);
       var value=ModMenuTheme.Caption(chip,text.Text,.06f,0,.94f,1,15,ModMenuTheme.Text);
       ModMenuTextField.Attach(chip,value,()=>text.Text,v=>{text.SetText(v);return true;},message=>Flash(message),text.MaxLength);
       break;
      }
      case ModTrack track:
      {
       // A song: a drawn play triangle, the title, the on/off box, and the loop timer
       // as arrows around a draggable track. User-added songs also get a remove cross.
       var row=Row(offset,RowHeight);
       offset+=RowStep;
       var playing=track.IsPlaying;
       order.Add(ModMenuTheme.Icon(row,playing?ModMenuTheme.Square():ModMenuTheme.Triangle(),0f,.22f,.040f,.78f,
        playing?new Color(.85f,.55f,.30f):ModMenuTheme.Gold,()=>{if(track.IsPlaying)track.Stop();else track.Preview();Invalidate();}));
       var left=.046f;
       if(track.RemoveAction!=null)
       {
        order.Add(ModMenuTheme.Icon(row,ModMenuTheme.Cross(),.046f,.30f,.078f,.70f,new Color(.78f,.42f,.32f),()=>{track.Remove();Invalidate();}));
        left=.086f;
       }
       var title=ModMenuTheme.Caption(row,track.Label,left,0,.55f,1,16,ModMenuTheme.Text,FontStyles.Bold);
       title.overflowMode=TextOverflowModes.Ellipsis;
       order.Add(Checkbox(row,track.Enabled,()=>{track.ToggleEnabled();Invalidate();}));
       order.Add(Glyph(row,"<",.605f,.30f,.635f,.72f,()=>{track.NudgeLoop(-1);Invalidate();}));
       var loopCaption=ModMenuTheme.Caption(row,track.LoopText,.64f,.58f,.965f,1f,12,ModMenuTheme.Gold,FontStyles.Normal,TextAlignmentOptions.Center);
       ModMenuTrackRow.Attach(row,track,loopCaption);
       order.Add(Glyph(row,">",.968f,.30f,.998f,.72f,()=>{track.NudgeLoop(1);Invalidate();}));
       break;
      }
      default:
      {
       var row=Row(offset,RowHeight);
       offset+=RowStep;
       switch(setting)
       {
        case ModToggle toggle:
         ModMenuTheme.Caption(row,setting.Label,.02f,0,.56f,1,16,ModMenuTheme.Text);
         order.Add(ModMenuTheme.Control(row,toggle.Value?"On":"Off",.62f,.16f,1,.86f,()=>{toggle.SetValue(!toggle.Value);Invalidate();}));
         break;
        case ModSlider slider:
         // SliderRow draws the label itself, above its own value and track.
         SliderRow(row,slider.Label,
          ()=>slider.Normalized,
          position=>slider.Seek(position),
          direction=>slider.Nudge(direction),
          _=>slider.Text);
         break;
        case ModChoice choice:
         ModMenuTheme.Caption(row,setting.Label,.02f,0,.56f,1,16,ModMenuTheme.Text);
         NumberRow(row,choice.Text,choice.Cycle);
         break;
        case ModAction action:
         // A wide action is the one command a list is built around, so it reads as a
         // bar across the row instead of a small button beside a label.
         if(action.Wide)
         {
          order.Add(ModMenuTheme.Control(row,action.Label,.45f,.06f,.996f,.94f,()=>{action.Invoke();Invalidate();}));
          break;
         }
         ModMenuTheme.Caption(row,setting.Label,.02f,0,.56f,1,16,ModMenuTheme.Text);
         order.Add(ModMenuTheme.Control(row,"Run",.62f,.16f,1,.86f,()=>{action.Invoke();Invalidate();}));
         break;
       }
       break;
      }
     }
    }
   }
   return offset;
  }

  // Copy puts the code on the clipboard; the chip field beside it takes one back,
  // either typed or pasted with Ctrl+V.
  private void Copy(string hex)
  {
   try
   {
    GUIUtility.systemCopyBuffer=hex;
    Flash("Copied "+hex+".");
   }
   catch { Flash("Could not reach the clipboard."); }
  }

  // Shown at once, even while a hex field is open and the list is not redrawn.
  private void Flash(string message,float seconds=3.5f)
  {
   flash=message;
   flashUntil=Time.unscaledTime+seconds;
   if(status!=null)status.text=message;
  }

  // The still-visible picture column: each preview gets a frame and its own label,
  // stacked from the top of the pane.
  private void BuildPreviewPane(ModModule module)
  {
   for(int i=previewHost.childCount-1;i>=0;i--)
   {
    var child=previewHost.GetChild(i).gameObject;
    child.SetActive(false);
    Destroy(child);
   }
   var previews=module.Settings.OfType<ModPreview>().Where(preview=>!preview.Removed).ToArray();
   var column=window!=null&&window.rect.width>1f?window.rect.width*(.96f-.695f):190f;
   var available=Mathf.Clamp(column-26f,80f,260f);
   var running=10f;
   foreach(var preview in previews)
   {
    var size=Mathf.Clamp(Mathf.Min(preview.ImageHeight,available),80f,260f);
    var caption=ModMenuTheme.Caption(previewHost,preview.Label,0,0,1,0,14,ModMenuTheme.Muted,
     FontStyles.Normal,TextAlignmentOptions.Center);
    Pin(caption.rectTransform,-running,22f);
    running+=26f;
    var frame=ModMenuTheme.Box(previewHost,"Frame",.5f,.5f,.5f,.5f,ModMenuTheme.Panel);
    frame.anchorMin=new Vector2(.5f,1f);frame.anchorMax=new Vector2(.5f,1f);frame.pivot=new Vector2(.5f,1f);
    frame.anchoredPosition=new Vector2(0,-running);
    frame.sizeDelta=new Vector2(size,size);
    var outline=frame.gameObject.AddComponent<Outline>();
    outline.effectColor=new Color(.47f,.37f,.24f,.75f);outline.effectDistance=new Vector2(1,-1);
    var texture=preview.Texture;
    if(texture==null)
     ModMenuTheme.Caption(frame,"Preview unavailable",.06f,.06f,.94f,.94f,12,ModMenuTheme.Muted,
      FontStyles.Normal,TextAlignmentOptions.Center);
    else
    {
     var image=ModMenuTheme.Box(frame,"Image",.04f,.04f,.96f,.96f);
     var raw=image.gameObject.AddComponent<RawImage>();
     raw.texture=texture;raw.color=Color.white;raw.raycastTarget=false;
    }
    running+=size+14f;
   }
  }

  // A clickable row in the mod list: name on the left, what it contains on the right.
  private Button RowButton(RectTransform row,string label,string trailing,Action clicked)
  {
   var background=row.gameObject.AddComponent<Image>();
   background.color=ModMenuTheme.RowFill;
   var outline=row.gameObject.AddComponent<Outline>();
   outline.effectColor=new Color(.47f,.37f,.24f,.70f);outline.effectDistance=new Vector2(1,-1);
   var button=row.gameObject.AddComponent<Button>();
   button.targetGraphic=background;
   var colors=button.colors;
   colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.86f,.62f,1f);
   colors.pressedColor=new Color(.8f,.59f,.32f,1f);colors.selectedColor=new Color(1f,.90f,.70f,1f);
   button.colors=colors;
   ModMenuTheme.Caption(row,label,.03f,0,.68f,1,18,ModMenuTheme.Text);
   ModMenuTheme.Caption(row,trailing,.70f,0,.97f,1,15,ModMenuTheme.Muted,FontStyles.Normal,TextAlignmentOptions.MidlineRight);
   button.onClick.AddListener(()=>clicked());
   return button;
  }

  // "< value >" row for choices, where a short list only needs two arrows.
  private void NumberRow(RectTransform row,string value,Action<int> step)
  {
   order.Add(ModMenuTheme.Control(row,"<",.58f,.16f,.66f,.86f,()=>{step(-1);Invalidate();}));
   var caption=ModMenuTheme.Caption(row,value,.665f,.16f,.90f,.86f,16,ModMenuTheme.Text,
    FontStyles.Normal,TextAlignmentOptions.Center);
   caption.overflowMode=TextOverflowModes.Ellipsis;
   order.Add(ModMenuTheme.Control(row,">",.905f,.16f,1,.86f,()=>{step(1);Invalidate();}));
  }

  // Label and value above a draggable track, with an arrow in each gutter for a
  // keyboard or controller, so every input style works and nothing overlaps.
  private void SliderRow(RectTransform row,string label,Func<float> read,Action<float> seek,Action<int> nudge,Func<float,string> format)
  {
   ModMenuTheme.Caption(row,label,.09f,.46f,.60f,1f,16,ModMenuTheme.Text);
   var caption=ModMenuTheme.Caption(row,format(read()),.60f,.46f,.91f,1f,16,ModMenuTheme.Gold,
    FontStyles.Normal,TextAlignmentOptions.MidlineRight);
   var track=ModMenuTheme.Box(row,"Track",.09f,.06f,.91f,.40f,ModMenuTheme.Inset);
   var fill=ModMenuTheme.Box(track,"Fill",0,0,Mathf.Clamp01(read()),1,ModMenuTheme.Gold);
   fill.GetComponent<Image>().raycastTarget=false;
   var handle=ModMenuTheme.Box(track,"Handle",0,0,0,1,new Color(1f,.94f,.78f,1f));
   handle.anchorMin=new Vector2(Mathf.Clamp01(read()),0f);
   handle.anchorMax=new Vector2(Mathf.Clamp01(read()),1f);
   handle.sizeDelta=new Vector2(8f,8f);
   handle.anchoredPosition=Vector2.zero;
   handle.GetComponent<Image>().raycastTarget=false;
   ModMenuSlider.Attach(track,fill,caption,read,seek,format);
   order.Add(ModMenuTheme.Control(row,"<",0f,.14f,Gutter,.86f,()=>{nudge(-1);Invalidate();}));
   order.Add(ModMenuTheme.Control(row,">",1f-Gutter,.14f,1f,.86f,()=>{nudge(1);Invalidate();}));
  }

  // A bare glyph button: the play triangle and the loop-timer arrows, which the
  // native look draws without a frame around them.
  private Button Glyph(RectTransform row,string glyph,float left,float bottom,float right,float top,Action clicked)
  {
   var rect=ModMenuTheme.Box(row,glyph,left,bottom,right,top,new Color(.22f,.16f,.10f,.55f));
   var button=rect.gameObject.AddComponent<Button>();
   button.targetGraphic=rect.GetComponent<Image>();
   var colors=button.colors;
   colors.normalColor=new Color(1f,1f,1f,.35f);colors.highlightedColor=new Color(1f,.86f,.62f,1f);
   colors.pressedColor=new Color(.8f,.59f,.32f,1f);colors.selectedColor=new Color(1f,.90f,.70f,1f);
   button.colors=colors;
   ModMenuTheme.Caption(rect,glyph,.02f,0,.98f,1,18,ModMenuTheme.Gold,FontStyles.Bold,TextAlignmentOptions.Center);
   button.onClick.AddListener(()=>clicked());
   return button;
  }

  // The enable box beside a song: gold-filled when the song plays, dark when it is
  // switched off. A filled box needs no glyph, so it reads at a glance.
  private Button Checkbox(RectTransform row,bool on,Action toggle)
  {
   var box=ModMenuTheme.Box(row,"Check",.565f,.24f,.595f,.76f,ModMenuTheme.Inset);
   var outline=box.gameObject.AddComponent<Outline>();
   outline.effectColor=new Color(.62f,.47f,.26f,.95f);outline.effectDistance=new Vector2(1,-1);
   if(on)
   {
    var fill=ModMenuTheme.Box(box,"On",.18f,.18f,.82f,.82f,ModMenuTheme.Gold);
    fill.GetComponent<Image>().raycastTarget=false;
   }
   var button=box.gameObject.AddComponent<Button>();
   button.targetGraphic=box.GetComponent<Image>();
   var colors=button.colors;
   colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.86f,.62f,1f);
   colors.pressedColor=new Color(.8f,.59f,.32f,1f);colors.selectedColor=new Color(1f,.90f,.70f,1f);
   button.colors=colors;
   // Press, not release: the redraw that follows the write would otherwise destroy the
   // box before the click is released.
   box.gameObject.AddComponent<ModMenuPress>().Pressed=toggle;
   return button;
  }

  private RectTransform Row(float offset,float height)
  {
   var row=ModMenuTheme.Box(rows,"Row",0,1,1,1);
   row.anchorMin=new Vector2(0,1);row.anchorMax=new Vector2(1,1);
   row.pivot=new Vector2(.5f,1);
   row.anchoredPosition=new Vector2(0,-offset);
   row.sizeDelta=new Vector2(0,height);
   return row;
  }

  // Anchors a child of the preview pane to the top, spanning its width.
  private static void Pin(RectTransform rect,float top,float height)
  {
   rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(1f,1f);rect.pivot=new Vector2(.5f,1f);
   rect.anchoredPosition=new Vector2(0f,top);
   rect.sizeDelta=new Vector2(0f,height);
  }

  private void ChainNavigation()
  {
   if(order.Count<2)return;
   for(int i=0;i<order.Count;i++)
   {
    var navigation=order[i].navigation;
    navigation.mode=Navigation.Mode.Explicit;
    navigation.selectOnUp=order[(i+order.Count-1)%order.Count];
    navigation.selectOnDown=order[(i+1)%order.Count];
    order[i].navigation=navigation;
   }
  }

  internal void Tick()
  {
   // An open text field owns the keyboard, Escape included, and the list must not
   // rebuild out from under it.
   if(ModMenuHexField.Editing||ModMenuTextField.Editing)return;
   var keyboard=Keyboard.current;
   var gamepad=Gamepad.current;
   if((keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)||(gamepad!=null&&gamepad.buttonEast.wasPressedThisFrame))
   {Back();return;}
   // Rebuilding the rows under the pointer would end a drag after one event.
   if(ModMenuSlider.Dragging)return;
   // Settings from all loaded mods are polled for external changes. A 5 Hz check is
   // enough for live values and avoids walking every registered row on every frame.
   var now=Time.unscaledTime;
   if(now<nextSnapshotAt)return;
   nextSnapshotAt=now+SnapshotInterval;
   var current=Snapshot();
   if(current!=signature){signature=current;BuildRows();}
  }

  // Any registered value change alters this, which is what triggers a redraw.
  private string Snapshot()
  {
   var text=new StringBuilder(256);
   text.Append(open==null?"<mods>":open.Id).Append('\n');
   foreach(var module in ModMenuApi.Registered)
   {
    text.Append(module.Id).Append('#');
    foreach(var setting in module.Settings)text.Append(setting.Id).Append('=').Append(setting.Describe()).Append(';');
   }
   return text.ToString();
  }

  private static string ChannelName(int index)=>index==0?"Red":index==1?"Green":"Blue";

  private static TMP_FontAsset FindFont()
  {
   foreach(var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
    if(text!=null&&text.font!=null&&text.transform.root.name!="FATE Mod Menu")return text.font;
   return null;
  }
 }
}
