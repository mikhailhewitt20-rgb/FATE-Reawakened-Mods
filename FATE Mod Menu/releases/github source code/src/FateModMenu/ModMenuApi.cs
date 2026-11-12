using System;
using System.Collections.Generic;
using UnityEngine;
namespace ZombieGhost.ModMenu
{
 // Public surface for other mods. A mod registers a module once and adds the
 // options it wants shown in the pause menu's Mod Settings screen. Values stay
 // in the mod's own config: the menu only reads and writes through delegates.
 public static class ModMenuApi
 {
  internal static readonly List<ModModule> Modules=new List<ModModule>();
  // 1 was a flat list of every mod's options. 2 adds one screen per mod, colour
  // rows, draggable sliders and image previews.
  public static int ApiVersion=>2;
  public static bool IsInputBlocked=>ModMenuPlugin.BlocksInput;
  public static IReadOnlyList<ModModule> Registered=>Modules;
  public static ModModule RegisterModule(string id,string displayName)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Length>96||id.Contains("|"))throw new ArgumentException("Use a stable unique mod GUID as the module ID");
   if(Modules.Count>=64)throw new InvalidOperationException("At most 64 modules can register");
   if(Modules.Exists(module=>module.Id==id))throw new InvalidOperationException("Module already registered: "+id);
   var registered=new ModModule(id,displayName);
   Modules.Add(registered);
   ModMenuPlugin.RequestRefresh();
   return registered;
  }
 }
 public sealed class ModModule : IDisposable
 {
  internal readonly List<ModSetting> Settings=new List<ModSetting>();
  public string Id{get;}
  public string Name{get;}
  public bool Disposed{get;private set;}
  internal ModModule(string id,string name){Id=id;Name=string.IsNullOrWhiteSpace(name)?id:name;}
  public ModToggle RegisterToggle(string id,string label,Func<bool> get,Action<bool> set,string category=null)
  {Validate(id,label,get,set);var value=Read(get,false);var setting=new ModToggle(Id,Name,id,label,category,get,set,value);Add(setting);return setting;}
  public ModSlider RegisterSlider(string id,string label,Func<float> get,Action<float> set,float min,float max,float step=.05f,string category=null,string format=null)
  {Validate(id,label,get,set);if(max<min){var swap=min;min=max;max=swap;}if(step<=0f)step=.01f;
   var setting=new ModSlider(Id,Name,id,label,category,get,set,min,max,step,format,Read(get,min));Add(setting);return setting;}
  public ModChoice RegisterChoice(string id,string label,Func<int> get,Action<int> set,string[] options,string category=null)
  {Validate(id,label,get,set);if(options==null||options.Length==0)throw new ArgumentException("A choice needs at least one option");
   var index=Math.Max(0,Math.Min(options.Length-1,Read(get,0)));var setting=new ModChoice(Id,Name,id,label,category,get,set,options,index);Add(setting);return setting;}
  // A plain button row, for things like "Reset to defaults" or "Reload config".
  public ModAction RegisterAction(string id,string label,Action invoke,string category=null)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Length>96||id.Contains("|"))throw new ArgumentException("Unique option ID required");
   if(invoke==null)throw new ArgumentException("An action is required");
   var setting=new ModAction(Id,Name,id,label,category,invoke);Add(setting);return setting;
  }
  // A wrapped explanatory line, for settings that need a sentence of context.
  public ModNote RegisterNote(string id,string text,string category=null)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Length>96||id.Contains("|"))throw new ArgumentException("Unique option ID required");
   if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("Note text is required");
   var setting=new ModNote(Id,Name,id,text,category);Add(setting);return setting;
  }
  // An RGB colour: one row with a swatch and the hex value, then a slider per
  // channel. The mod owns the colour; the menu only reads and writes it.
  public ModColor RegisterColor(string id,string label,Func<Color> get,Action<Color> set,string category=null)
  {
   Validate(id,label,get,set);
   var setting=new ModColor(Id,Name,id,label,category,get,set,Read(get,Color.white));
   Add(setting);return setting;
  }
  // A live picture, drawn by the mod from its own settings. Use it for anything
  // that is easier to look at than to read, such as a reticle.
  public ModPreview RegisterPreview(string id,string label,Func<Texture2D> get,string category=null,float height=150f)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Length>96||id.Contains("|"))throw new ArgumentException("Unique option ID required");
   if(get==null)throw new ArgumentException("An image getter is required");
   var setting=new ModPreview(Id,Name,id,label,category,get,height);Add(setting);return setting;
  }
  // Back to the values the getters returned when the module registered.
  public void ResetDefaults(){foreach(var setting in Settings.ToArray())if(!setting.Removed)setting.Reset();ModMenuPlugin.RequestRefresh();}
  public void Dispose()
  {
   if(Disposed)return;
   Disposed=true;
   foreach(var setting in Settings)setting.Removed=true;
   Settings.Clear();
   ModMenuApi.Modules.Remove(this);
   ModMenuPlugin.RequestRefresh();
  }
  private void Add(ModSetting setting)
  {
   if(Disposed)throw new ObjectDisposedException("Module "+Id+" is disposed");
   if(Settings.Count>=256)throw new InvalidOperationException("At most 256 options per module");
   foreach(var existing in Settings)if(existing.Id==setting.Id)throw new ArgumentException("Duplicate option ID: "+setting.Id);
   Settings.Add(setting);
   ModMenuPlugin.RequestRefresh();
  }
  private static void Validate(string id,string label,Delegate get,Delegate set)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Length>96||id.Contains("|"))throw new ArgumentException("Unique option ID required");
   if(get==null||set==null)throw new ArgumentException("A getter and a setter are required");
   if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("A label is required");
  }
  private static bool Read(Func<bool> get,bool fallback){try{return get();}catch{return fallback;}}
  private static float Read(Func<float> get,float fallback){try{return get();}catch{return fallback;}}
  private static int Read(Func<int> get,int fallback){try{return get();}catch{return fallback;}}
  private static Color Read(Func<Color> get,Color fallback){try{return get();}catch{return fallback;}}
 }
 // One row in a mod's settings screen. Rows are owned by the module that made them.
 public abstract class ModSetting
 {
  public string Id{get;}
  public string Label{get;}
  public string Category{get;}
  internal string Owner,OwnerName;
  internal bool Removed;
  internal ModSetting(string owner,string ownerName,string id,string label,string category)
  {
   Owner=owner;OwnerName=ownerName;Id=id;Label=label;Category=string.IsNullOrWhiteSpace(category)?"General":category;
  }
  internal abstract void Reset();
  // Drives the screen's redraw check; must change whenever the shown value changes.
  internal abstract string Describe();
 }
 public sealed class ModToggle : ModSetting
 {
  internal Func<bool> Get;internal Action<bool> Set;internal bool Default;
  internal ModToggle(string owner,string ownerName,string id,string label,string category,Func<bool> get,Action<bool> set,bool defaultValue)
   : base(owner,ownerName,id,label,category){Get=get;Set=set;Default=defaultValue;}
  public bool Value{get=>!Removed&&Get!=null&&Get();set=>SetValue(value);}
  public void SetValue(bool value)
  {
   if(Removed||Set==null)return;
   Set(value);ModMenuPlugin.RequestRefresh();
  }
  internal override void Reset()=>SetValue(Default);
  internal override string Describe()=>Removed?"removed":Value?"1":"0";
 }
 public sealed class ModSlider : ModSetting
 {
  internal Func<float> Get;internal Action<float> Set;internal float Min,Max,Step,Default;internal string Format;
  internal ModSlider(string owner,string ownerName,string id,string label,string category,Func<float> get,Action<float> set,float min,float max,float step,string format,float defaultValue)
   : base(owner,ownerName,id,label,category){Get=get;Set=set;Min=min;Max=max;Step=step;Format=format;Default=defaultValue;}
  public float Value{get=>!Removed&&Get!=null?Clamp(Get()):Min;set=>SetValue(value);}
  public string Text=>Format!=null?Value.ToString(Format):Value.ToString(Step>=1f?"0":Step>=.1f?"0.0":"0.00");
  // Where the value sits between min and max, for the slider track.
  public float Normalized=>Max<=Min?0f:Mathf.Clamp01((Value-Min)/(Max-Min));
  public void SetValue(float value)
  {
   if(Removed||Set==null)return;
   Set(Clamp(Snap(value)));ModMenuPlugin.RequestRefresh();
  }
  // Dragging hands over a 0-1 position; arrow presses hand over steps.
  public void Seek(float normalized)=>SetValue(Min+(Max-Min)*Clamp(normalized));
  public void Nudge(int direction)=>SetValue(Value+direction*Step);
  internal override void Reset()=>SetValue(Default);
  internal override string Describe()=>Removed?"removed":Value.ToString("R");
  private float Clamp(float value)=>Math.Max(Min,Math.Min(Max,value));
  // Keep values on the step grid so text and config stay tidy.
  private float Snap(float value)=>Min+Mathf.Round((value-Min)/Step)*Step;
 }
 public sealed class ModChoice : ModSetting
 {
  internal Func<int> Get;internal Action<int> Set;internal string[] Options;internal int Default;
  internal ModChoice(string owner,string ownerName,string id,string label,string category,Func<int> get,Action<int> set,string[] options,int defaultIndex)
   : base(owner,ownerName,id,label,category){Get=get;Set=set;Options=options;Default=defaultIndex;}
  public int Index{get{var index=Removed||Get==null?0:Get();return Math.Max(0,Math.Min(Options.Length-1,index));}}
  public string Text=>Options.Length==0?"":Options[Index];
  public void SetIndex(int index)
  {
   if(Removed||Set==null||Options.Length==0)return;
   Set(Math.Max(0,Math.Min(Options.Length-1,index)));ModMenuPlugin.RequestRefresh();
  }
  // Wraps, so left from the first option lands on the last.
  public void Cycle(int direction)=>SetIndex(((Index+direction)%Options.Length+Options.Length)%Options.Length);
  internal override void Reset()=>SetIndex(Default);
  internal override string Describe()=>Removed?"removed":Index+"|"+Text;
 }
 public sealed class ModAction : ModSetting
 {
  internal Action Run;
  internal ModAction(string owner,string ownerName,string id,string label,string category,Action run)
   : base(owner,ownerName,id,label,category){Run=run;}
  public void Invoke(){if(Removed||Run==null)return;Run();ModMenuPlugin.RequestRefresh();}
  internal override void Reset(){}
  internal override string Describe()=>Removed?"removed":"action";
 }
 public sealed class ModNote : ModSetting
 {
  internal string Content;
  internal ModNote(string owner,string ownerName,string id,string text,string category)
   : base(owner,ownerName,id,text,category){Content=text;}
  internal override void Reset(){}
  internal override string Describe()=>Removed?"removed":Content;
 }
 // A colour setting: the screen draws a swatch, the hex value and one slider per
 // channel, all writing back through the mod's setter.
 public sealed class ModColor : ModSetting
 {
  internal Func<Color> Get;internal Action<Color> Set;internal Color Default;
  internal ModColor(string owner,string ownerName,string id,string label,string category,Func<Color> get,Action<Color> set,Color defaultValue)
   : base(owner,ownerName,id,label,category){Get=get;Set=set;Default=defaultValue;}
  public Color Value
  {
   get
   {
    var value=Removed||Get==null?Default:Get();
    return new Color(Mathf.Clamp01(value.r),Mathf.Clamp01(value.g),Mathf.Clamp01(value.b),Mathf.Clamp01(value.a));
   }
  }
  public string Hex=>"#"+ColorUtility.ToHtmlStringRGB(Value);
  // 0, 1, 2 are red, green and blue; anything else is alpha.
  public float Channel(int index)=>index==0?Value.r:index==1?Value.g:index==2?Value.b:Value.a;
  // A colour code from the clipboard or a config: "#B76063", "B76063", "#B76"
  // or a name Unity understands. False when the text is not a colour.
  public bool SetHex(string text)
  {
   if(Removed||Set==null)return false;
   if(!TryParseHex(text,out var color))return false;
   Set(color);ModMenuPlugin.RequestRefresh();
   return true;
  }
  public static bool TryParseHex(string text,out Color color)
  {
   color=default;
   if(string.IsNullOrWhiteSpace(text))return false;
   var value=text.Trim();
   if(!value.StartsWith("#"))value="#"+value;
   return ColorUtility.TryParseHtmlString(value,out color);
  }
  public void SetChannel(int index,float value)
  {
   if(Removed||Set==null)return;
   var color=Value;
   // 8-bit steps: a slider should not write 0.2313726 into a config.
   var channel=Mathf.Round(Mathf.Clamp01(value)*255f)/255f;
   if(index==0)color.r=channel;
   else if(index==1)color.g=channel;
   else if(index==2)color.b=channel;
   else color.a=channel;
   Set(color);ModMenuPlugin.RequestRefresh();
  }
  internal override void Reset(){if(Removed||Set==null)return;Set(Default);ModMenuPlugin.RequestRefresh();}
  internal override string Describe()=>Removed?"removed":Hex;
 }
 // A read-only picture the mod draws from its own settings. The getter runs when
 // the screen is built, so a mod can rebuild its texture lazily and cache it.
 public sealed class ModPreview : ModSetting
 {
  internal Func<Texture2D> Get;internal float Height;
  internal ModPreview(string owner,string ownerName,string id,string label,string category,Func<Texture2D> get,float height)
   : base(owner,ownerName,id,label,category){Get=get;Height=Mathf.Clamp(height,60f,320f);}
  public Texture2D Texture{get{try{return Removed||Get==null?null:Get();}catch{return null;}}}
  public float ImageHeight=>Height;
  internal override void Reset(){}
  internal override string Describe()=>Removed?"removed":"preview:"+Height;
 }
}
