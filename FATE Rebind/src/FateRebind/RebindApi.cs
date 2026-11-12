using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 public static class RebindApi
 {
  internal static readonly List<RebindProvider> Providers=new List<RebindProvider>();
  public static bool IsInputBlocked=>RebindPlugin.BlocksInput;
  public static RebindProvider RegisterProvider(string id,string displayName)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Length>96||id.Contains("|")||id.StartsWith("auto.")||id=="game")throw new ArgumentException("Use a stable unique mod GUID as the provider ID");
   if(Providers.Any(p=>p.Id==id))throw new InvalidOperationException("Provider already registered: "+id);
   var provider=new RebindProvider(id,displayName);Providers.Add(provider);RebindPlugin.RequestRefresh();return provider;
  }
 }
 public sealed class RebindProvider : IDisposable
 {
  public string Id{get;} public string Name{get;}
  internal readonly List<RegisteredAction> Actions=new List<RegisteredAction>();
  internal readonly List<RebindToggle> Toggles=new List<RebindToggle>();
  internal readonly List<RebindSlider> Sliders=new List<RebindSlider>();
  internal RebindProvider(string id,string name){Id=id;Name=string.IsNullOrWhiteSpace(name)?id:name;}
  public void RegisterAction(string id,InputAction action,string displayName=null,string category=null,Func<int,string> bindingKey=null)
  {
   if(action==null||string.IsNullOrWhiteSpace(id)||id.Contains("|")||Actions.Any(a=>a.Id==id))throw new ArgumentException("Unique action ID and a real InputAction required");
   Actions.Add(new RegisteredAction {Owner=Id,OwnerName=Name,Id=id,Action=action,Label=displayName??action.name,Category=category??action.actionMap?.name??"Controls",BindingKey=bindingKey});
   RebindPlugin.RequestRefresh();
  }
  public RebindButton RegisterButton(string id,string displayName,string defaultPath,string category="Controls")
  {
   var action=new InputAction(id,InputActionType.Button,defaultPath,expectedControlType:"Button");
   try {RegisterAction(id,action,displayName,category);Actions.Last().Owned=true;if(!RebindApi.IsInputBlocked)action.Enable();return new RebindButton(action);}
   catch {action.Dispose();throw;}
  }
  // A check mark row in the Controls list, for a mod preference that is not a binding.
  // The mod owns the value; the editor only reads and writes it through the delegates.
  public RebindToggle RegisterToggle(string id,string displayName,Func<bool> get,Action<bool> set,string category)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Contains("|")||Toggles.Any(t=>t.Id==id))throw new ArgumentException("Unique option ID required");
   if(get==null||set==null)throw new ArgumentException("A getter and a setter are required");
   var toggle=new RebindToggle {Owner=Id,OwnerName=Name,Id=id,Label=string.IsNullOrWhiteSpace(displayName)?id:displayName,Category=string.IsNullOrWhiteSpace(category)?"Options":category,Get=get,Set=set,DefaultValue=get()};
   Toggles.Add(toggle);RebindPlugin.RequestRefresh();return toggle;
  }
  // A slider row for a numeric mod preference, drawn inline in the Controls list.
  public RebindSlider RegisterSlider(string id,string displayName,Func<float> get,Action<float> set,float min,float max,float step,string category)
  {
   if(string.IsNullOrWhiteSpace(id)||id.Contains("|")||Sliders.Any(s=>s.Id==id))throw new ArgumentException("Unique option ID required");
   if(get==null||set==null)throw new ArgumentException("A getter and a setter are required");
   if(max<min){var swap=min;min=max;max=swap;}
   if(step<=0f)step=.01f;
   var slider=new RebindSlider {Owner=Id,OwnerName=Name,Id=id,Label=string.IsNullOrWhiteSpace(displayName)?id:displayName,Category=string.IsNullOrWhiteSpace(category)?"Options":category,Get=get,Set=set,Min=min,Max=max,Step=step,DefaultValue=get()};
   Sliders.Add(slider);RebindPlugin.RequestRefresh();return slider;
  }
  public void Dispose(){RebindApi.Providers.Remove(this);foreach(var entry in Actions){entry.Removed=true;if(entry.Owned)entry.Action.Dispose();}Actions.Clear();foreach(var toggle in Toggles)toggle.Removed=true;Toggles.Clear();foreach(var slider in Sliders)slider.Removed=true;Sliders.Clear();RebindPlugin.RequestRefresh();}
 }
 public sealed class RebindButton
 {
  private readonly InputAction action;
  internal RebindButton(InputAction action){this.action=action;}
  public bool IsPressed=>!RebindApi.IsInputBlocked&&action.enabled&&action.IsPressed();
  public bool WasPressedThisFrame=>!RebindApi.IsInputBlocked&&action.enabled&&action.WasPressedThisFrame();
  public bool WasReleasedThisFrame=>!RebindApi.IsInputBlocked&&action.enabled&&action.WasReleasedThisFrame();
 }
 // Backs a check mark in the Controls editor for one boolean mod preference.
 public sealed class RebindToggle
 {
  internal string Owner,OwnerName,Id,Label,Category;
  internal Func<bool> Get;internal Action<bool> Set;
  internal bool DefaultValue,Removed;
  public bool Value{get=>!Removed&&Get!=null&&Get();set=>SetValue(value);}
  public void SetValue(bool value){if(Removed||Set==null)return;Set(value);RebindPlugin.RequestRefresh();}
  // Controller reset button: back to the value the mod started with.
  public void Reset()=>SetValue(DefaultValue);
 }
 // Backs a slider in the Controls editor for one numeric mod preference.
 public sealed class RebindSlider
 {
  internal string Owner,OwnerName,Id,Label,Category;
  internal Func<float> Get;internal Action<float> Set;
  internal float Min,Max,Step,DefaultValue;internal bool Removed;
  public float Value{get=>!Removed&&Get!=null?Math.Max(Min,Math.Min(Max,Get())):Min;set=>SetValue(value);}
  public string Text=>Value.ToString(Step>=1f?"0":Step>=.1f?"0.0":"0.00");
  public void SetValue(float value){if(Removed||Set==null)return;Set(Math.Max(Min,Math.Min(Max,value)));RebindPlugin.RequestRefresh();}
  public void Reset()=>SetValue(DefaultValue);
 }
 internal sealed class RegisteredAction
 {internal string Owner,OwnerName,Id,Label,Category;internal InputAction Action;internal bool Owned,Removed;internal Func<int,string> BindingKey;}
}
