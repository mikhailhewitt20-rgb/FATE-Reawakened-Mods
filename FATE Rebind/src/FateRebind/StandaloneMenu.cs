using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ZombieGhost.Rebind
{
 public sealed partial class RebindPlugin
 {
  private Vector2 actionScroll,groupScroll;
  private GUIStyle titleStyle,textStyle,buttonStyle;
  private void DrawWindow()
  {
   if(NativeEditorActive)return;
   var saved=GUI.matrix;int oldDepth=GUI.depth;
   float scale=Mathf.Min(Screen.width/1020f,Screen.height/760f);if(scale<=0)return;
   GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.depth=-1000;
   try
   {
    if(!open){if(GUI.Button(new Rect(Screen.width/scale-175,8,165,30),"Rebind 0.1.8 [F10]"))SetOpen(true);return;}
    if(titleStyle==null)
    {
     titleStyle=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold};titleStyle.normal.textColor=new Color(1,.77f,.4f);
     textStyle=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true};buttonStyle=new GUIStyle(GUI.skin.button){fontSize=14,wordWrap=true};
    }
    var bounds=new Rect((Screen.width/scale-980)/2,(Screen.height/scale-710)/2,980,710);
    GUI.Box(bounds,GUIContent.none);
    GUILayout.BeginArea(new Rect(bounds.x+10,bounds.y+10,960,690),GUI.skin.box);
    GUILayout.BeginHorizontal();GUILayout.Label("FATE REBIND",titleStyle);GUILayout.FlexibleSpace();
    if(GUILayout.Button("Close [F10]",buttonStyle,GUILayout.Width(130)))SetOpen(false);GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal();GUILayout.Label("Search",GUILayout.Width(55));search=GUILayout.TextField(search,GUILayout.Width(350));
    GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(190));
    groupScroll=GUILayout.BeginScrollView(groupScroll);
    foreach(var group in MouseKeyboardSlots().GroupBy(s=>s.Entry.Owner))
     if(GUILayout.Button((group.Key==selectedOwner?"> ":"")+group.First().Entry.OwnerName,buttonStyle,GUILayout.Height(48)))
     {selectedOwner=group.Key;selectedCategory="All categories";resetConfirm=false;actionScroll=Vector2.zero;}
    GUILayout.EndScrollView();
    bool idle=operation==null&&captureAt==0&&candidate==null;
    GUI.enabled=idle;
    if(GUILayout.Button(resetConfirm?"Confirm reset":"Reset group",buttonStyle,GUILayout.Height(36)))
    {if(resetConfirm){foreach(var slot in MouseKeyboardSlots().Where(s=>s.Entry.Owner==selectedOwner).ToArray())Change(slot,null,true);resetConfirm=false;}else resetConfirm=true;}
    GUI.enabled=true;GUILayout.EndVertical();
    GUILayout.BeginVertical();actionScroll=GUILayout.BeginScrollView(actionScroll);
    var visible=VisibleSlots();string category=null;
    foreach(var slot in visible)
    {
     if(category!=slot.Category){GUILayout.Label(slot.Category,titleStyle);category=slot.Category;}
     GUILayout.BeginHorizontal(GUI.skin.box);
     GUILayout.Label(slot.Label,textStyle,GUILayout.Width(235),GUILayout.MinHeight(43));
     var display=string.IsNullOrEmpty(slot.Path)?"Unbound":slot.Action.GetBindingDisplayString(slot.Index);if(string.IsNullOrEmpty(display))display=slot.Path;
     GUI.enabled=idle&&!slot.ReadOnly;
     if(GUILayout.Button(slot.ReadOnly?"Automatic pointer":display,buttonStyle,GUILayout.Width(205),GUILayout.Height(43)))QueueCapture(slot);
     if(GUILayout.Button("Remove",buttonStyle,GUILayout.Width(75),GUILayout.Height(43)))Change(slot,"");
     if(GUILayout.Button("Reset",buttonStyle,GUILayout.Width(65),GUILayout.Height(43)))Change(slot,null,true);
     GUI.enabled=true;GUILayout.EndHorizontal();
    }
    if(visible.Length==0)GUILayout.Label("No matching actions. Load a character to discover gameplay controls.",textStyle);
    GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();
    GUILayout.BeginVertical(GUI.skin.box,GUILayout.Height(105));GUILayout.Label(status,textStyle);
    if(operation!=null||captureAt>0){if(GUILayout.Button("Cancel rebinding",buttonStyle)){CancelCapture();status="Previous binding kept.";}}
    if(candidate!=null)
    {
     var target=candidate;var conflicts=Conflicts(target,candidatePath);
     GUILayout.BeginHorizontal();
     if(GUILayout.Button("Keep both",buttonStyle)){Change(target,candidatePath);candidate=null;}
     GUI.enabled=conflicts.Length==1&&conflicts[0].Group==target.Group&&!conflicts[0].ReadOnly;
     if(GUILayout.Button("Swap bindings",buttonStyle)){SwapBindings(target,conflicts[0],candidatePath,target.Path);candidate=null;}
     GUI.enabled=true;
     if(GUILayout.Button("Cancel change",buttonStyle)){CancelCapture();status="Previous binding kept.";}
     GUILayout.EndHorizontal();
    }
    GUILayout.EndVertical();GUILayout.EndArea();
   }
   finally{GUI.enabled=true;GUI.matrix=saved;GUI.depth=oldDepth;}
  }
 }
}




