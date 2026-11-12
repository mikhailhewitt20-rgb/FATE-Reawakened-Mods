// Copyright (c) 2026 MikhailKillsZombies. MIT; see LICENSE.
// Quick spell wheel: hold the wheel key, flick the mouse, release. The slot is chosen by the
// direction of the movement rather than by where the cursor sits, nothing has to be clicked and
// the pointer is hidden for as long as the wheel is up. Releasing the wheel key activates the
// highlighted slot; a close for any other reason - a menu, death, a scene change - changes
// nothing. The wheel itself is left exactly as the game built it: no slot is moved, resized or
// added, and the mod adds no menu entry.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cleverous.Prodigy;
using Cleverous.Prodigy.Input;
using HarmonyLib;
using Tableflip;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MikhailKillsZombies.FateQuickSpell
{
 [BepInPlugin(Guid,"FATE Quick Spell","0.3.4")]
 public sealed class QuickSpellPlugin:BaseUnityPlugin
 {
  public const string Guid="mikhailkillszombies.fate.quickspell";
  // A new slot has to beat the highlighted one by this much before the highlight moves, so a
  // mouse held between two neighbours does not make the pick flicker.
  const float Hysteresis=.12f;
  // Travel fades to about a third within a quarter second: the pick follows the movement the
  // player is making now, not everything since the wheel opened.
  const float TravelDecay=.02f;
  // How long the wheel key can be down before the panel is expected to be up. The hold starts
  // either way, so a wheel that never activates cannot leave the mod dead.
  const float PanelGrace=.35f;

  static QuickSpellPlugin active;
  // Held statically: the game destroys the plugin object, and every hook still has to be able to
  // log and to put the driver back after a scene load.
  static ManualLogSource log;
  static Driver driver;
  Harmony harmony;
  ConfigEntry<bool> modEnabled;
  ConfigEntry<float> flick;
  ConfigEntry<bool> debug;
  bool stopped;
  float nextStepLog;
  // FATE Aiming, when it is installed, hands the game's cursor back whenever it suspends itself
  // for a menu - and an open wheel is exactly what its menu check sees. While a hold is up its
  // cursor handling stands down, and the cursor is put out of sight again every frame in case the
  // game re-enables its own arrow.
  static bool aimingJoined;
  static bool cursorGuarded;

  // One hold of the wheel. travel is the recent mouse movement; selected is the slot it points
  // at, or -1 when nothing has been picked yet.
  bool open;
  Vector2 travel;
  Vector2 lastPointer;
  bool pointerKnown;
  int selected=-1;
  // The wheel panel is found by the driver, and held for the life of the process: the game
  // destroys and rebuilds UI, so it is looked up again whenever the cached one goes away.
  SpellbookRadialUI panel;
  float holdingSince=-1f;
  bool wasHolding;
  int lastFrame=-1;

  // Pointer state saved for the hold and restored on release.
  bool cursorSaved,arrowWasActive,cursorVisible;
  CursorLockMode cursorLock;
  Vector2 pointerHome;

  static CursorManager cursorManager;
  static PropertyInfo virtualCursor;

  // spellSlots is private and serialized in the scene, so it is read through a typed field ref.
  static readonly AccessTools.FieldRef<SpellbookRadialUI,SpellSlot[]> Slots=
   AccessTools.FieldRefAccess<SpellbookRadialUI,SpellSlot[]>("spellSlots");

  void Awake()
  {
   active=this;
   log=Logger;
   modEnabled=Config.Bind("General","Enabled",true,
    "Pick spells by the direction of the mouse. Off leaves the game's own click-to-select wheel alone.");
   flick=Config.Bind("Wheel","FlickPixels",28f,
    "How far the mouse must travel, in pixels, before a direction is chosen. Lower picks a spell sooner; higher needs a more deliberate flick.");
   debug=Config.Bind("Wheel","Diagnostics",true,"Log each wheel hold and the spell it picks. Turn off once it works.");

   harmony=new Harmony(Guid);
   // The only hook: it tells the mod when the wheel closed for a reason other than the key, so a
   // hold cannot outlive the wheel it was picking from.
   Patch("OnTweenOutBegin",prefix:nameof(Closing));
   FreeWalking();
   JoinAiming();

   EnsureDriver();
   // A scene load can take the driver with it; Aiming needed the same treatment.
   SceneManager.sceneLoaded+=(scene,mode)=>EnsureDriver();
   Logger.LogInfo("FATE Quick Spell 0.3.4 ready; hold the spell wheel key, flick the mouse, release. With a controller the left stick still walks while the wheel is up.");
  }

  void Patch(string method,string prefix=null,string postfix=null)
  {
   try
   {
    var target=AccessTools.Method(typeof(SpellbookRadialUI),method,Type.EmptyTypes);
    if(target==null){Logger.LogWarning("SpellbookRadialUI."+method+" is missing; that part of the wheel is off.");return;}
    harmony.Patch(target,
     prefix==null?null:new HarmonyMethod(typeof(QuickSpellPlugin),prefix){priority=Priority.High},
     postfix==null?null:new HarmonyMethod(typeof(QuickSpellPlugin),postfix));
   }
   catch(Exception e) { Logger.LogWarning("Could not patch SpellbookRadialUI."+method+": "+e.Message); }
  }

  // FATE Aiming keeps the cursor hidden while it aims, but it gives the game's arrow back when it
  // suspends itself for a menu, and an open spell wheel reads as a menu to it: that is the pointer
  // that came back mid-hold. Its cursor calls are stood down for as long as the wheel is held and
  // picked up again on release. Without Aiming there is nothing to stand down, and the wheel hides
  // the cursor on its own as before.
  void JoinAiming()
  {
   try
   {
    var type=AccessTools.TypeByName("MikhailKillsZombies.FateAiming.AimingPlugin+Driver");
    if(type==null){Log("FATE Aiming is not installed; the wheel hides the cursor on its own.");return;}
    var names=new[]{"RestoreGameCursor","PlaceCursorRect","PinCursorRect","ForceCursorVisible"};
    var hooked=0;
    foreach(var name in names)
    {
     var target=AccessTools.Method(type,name);
     if(target==null)continue;
     harmony.Patch(target,new HarmonyMethod(typeof(QuickSpellPlugin),nameof(AimingCursor)){priority=Priority.High});
     hooked++;
    }
    aimingJoined=hooked>0;
    if(aimingJoined)Log("FATE Aiming found: its cursor handling stands down while the wheel is held ("+hooked+" hooks).");
    else Logger.LogWarning("FATE Aiming is loaded but its cursor methods moved; the wheel hides the cursor on its own.");
   }
   catch(Exception e) { Logger.LogWarning("Could not join FATE Aiming's cursor handling: "+e.Message); }
  }

  // Skipped calls, rather than undoing them afterwards, so the arrow never gets a frame in which
  // to appear.
  static bool AimingCursor(){return !cursorGuarded;}

  // The game destroys BepInEx's plugin object during startup, which stops its Update and
  // OnApplicationFocus. Per-frame work runs on our own persistent object instead.
  static void EnsureDriver()
  {
   if(driver!=null)return;
   var host=new GameObject("FATE.QuickSpellDriver");
   UnityEngine.Object.DontDestroyOnLoad(host);
   driver=host.AddComponent<Driver>();
   Log("driver attached.");
  }

  sealed class Driver:MonoBehaviour
  {
   // An exception in Update is only reported to Unity's log, which is switched off in this setup,
   // so a broken tick would look like a mod that does nothing at all.
   bool failed;

   void Start(){Log("driver running.");}

   void Update()
   {
    var p=active;
    if(ReferenceEquals(p,null)){Log("driver has no plugin instance.");enabled=false;return;}
    try { p.Tick(); }
    catch(Exception e)
    {
     if(failed)return;
     failed=true;
     Warn("driver tick failed: "+e);
    }
   }

   // After every Update in the frame, so the last word on the cursor is this mod's while a hold
   // is up, whoever else is trying to show it.
   void LateUpdate(){var p=active;if(!ReferenceEquals(p,null))p.Guard();}

   void OnApplicationFocus(bool focused){var p=active;if(!ReferenceEquals(p,null))p.Focus(focused);}
   void OnDestroy(){var p=active;if(!ReferenceEquals(p,null))p.Shutdown();}
  }

  internal static void Log(string message){log?.LogInfo(message);}
  internal static void Warn(string message){log?.LogWarning(message);}

  // The wheel closes because the hold key was released. Any other close - a menu, a death, a
  // scene change - is not a pick.
  static void Closing(SpellbookRadialUI __instance)
  {
   var p=active;if(ReferenceEquals(p,null))return;
   var broker=InputBroker.Instance;
   p.Finish(broker==null||!broker.HoldingSpellWheel);
  }

  // The open wheel is a blocking UI as far as the game is concerned, and a blocked frame makes the
  // broker drop the stick's movement, which is why a controller could not walk while picking. The
  // move vector is rebuilt after the frame has been parsed, and only movement: attacks, interaction
  // and camera zoom stay blocked, and a real menu, a pause or a locked movement keeps everything
  // frozen as usual.
  void FreeWalking()
  {
   Walk(typeof(InputBroker),"ParseFree",nameof(Walking));
   Walk(typeof(ProdigyPlayer),"LocalClientCollectInput",nameof(Collected));
  }

  void Walk(Type owner,string method,string postfix)
  {
   try
   {
    var target=AccessTools.Method(owner,method,Type.EmptyTypes);
    if(target==null){Logger.LogWarning(owner.Name+"."+method+" is missing; a controller cannot walk while the wheel is up.");return;}
    harmony.Patch(target,postfix:new HarmonyMethod(typeof(QuickSpellPlugin),postfix){priority=Priority.Low});
   }
   catch(Exception e) { Logger.LogWarning("Could not free movement while the wheel is up ("+owner.Name+"."+method+"): "+e.Message); }
  }

  // Why walking is not allowed right now, or null when it is. Only the wheel's own block is
  // ignored: a stacked menu, a pause, a locked movement, the mod being switched off and mouse
  // (click-to-move) mode all keep the player frozen exactly as the game intended.
  static string WalkBlocker(InputBroker broker)
  {
   if(broker==null)return "no input broker";
   if(!broker.HoldingSpellWheel)return "wheel key not held";
   // The stick only moves the player in free-move mode; click-to-move keeps its own movement.
   if(broker.LocalInputBehavior!=InputBroker.InputBehavior.Free)return "not stick movement";
   var p=active;
   if(ReferenceEquals(p,null))return "no plugin instance";
   if(!p.modEnabled.Value)return "mod off";
   if(Fate.isPaused)return "game paused";
   if(InputBlocker.AnyBlockingUiMenuIsOpen)return "a blocking menu is open";
   if(MovementLocked(broker))return "movement locked";
   var ui=p.Panel();
   if(ui==null)return "no wheel panel";
   if(!ui.isActiveAndEnabled&&!ui.gameObject.activeInHierarchy)return "wheel panel not up";
   return null;
  }

  static bool WheelKeepsYouWalking(InputBroker broker){return WalkBlocker(broker)==null;}

  // MoveRaw is read from the stick before the broker checks the block, so the direction is still
  // there to rebuild, exactly as the game's own ParseFree projects it. Look is left alone: the
  // right stick belongs to the wheel for picking while it is up.
  static void Walking(InputBroker __instance)
  {
   var broker=__instance;
   if(!WheelKeepsYouWalking(broker))return;
   var view=ProdigyCamera.GameCamera;
   if(view==null)return;
   var raw=broker.MoveRaw;
   if(raw.sqrMagnitude<.0001f){broker.Move=Vector3.zero;return;}
   broker.Move=Vector3.ProjectOnPlane(view.transform.TransformVector(new Vector3(raw.x,raw.y,0f)),Vector3.up);
  }

  // The broker's movement is not the movement the avatar walks on: the player copies the broker's
  // input into the input it hands to its agent every tick, and a blocked tick copies zeroes
  // instead. That is the other half of the freeze, so the movement part is put back into the copy;
  // every other action stays exactly as the block left it.
  static void Collected(ProdigyPlayer __instance)
  {
   var broker=InputBroker.Instance;
   if(broker==null||ReferenceEquals(__instance,null))return;
   var reason=WalkBlocker(broker);
   if(reason!=null){Say("wheel: walking stays frozen ("+reason+").");return;}
   if(!LocalPlayer(__instance,broker)){Say("wheel: walking skipped (not the local player).");return;}
   if(!CopyMovement(__instance,broker))return;
   Say("wheel: walking is free (stick "+broker.MoveRaw.magnitude.ToString("0.00")+", move "+broker.Move.magnitude.ToString("0.00")+", blockers="+InputBlocker.uiBlockers+").");
  }

  // The local player owns the agent the broker is following; the player reference is the fallback.
  static bool LocalPlayer(ProdigyPlayer player,InputBroker broker)
  {
   if(broker.TargetAgent!=null)return ReferenceEquals(player.RuntimeAvatar,broker.TargetAgent);
   if(broker.TargetPlayer!=null)return ReferenceEquals(player,broker.TargetPlayer);
   return false;
  }

  // At most one diagnostic a second, and only with Diagnostics on, so the log says either that
  // walking is free or exactly which check is still freezing it.
  static void Say(string message)
  {
   var p=active;
   if(ReferenceEquals(p,null)||!p.debug.Value||Time.unscaledTime<nextFreeLog)return;
   nextFreeLog=Time.unscaledTime+1f;
   Log(message);
  }


  static bool CopyMovement(ProdigyPlayer player,InputBroker broker)
  {
   if(currentInputMissing)return false;
   try
   {
    currentInput=currentInput??AccessTools.FieldRefAccess<ProdigyPlayer,Cleverous.Prodigy.PlayerInput>("m_currentInput");
    var input=currentInput(player);
    input.Move=broker.Move;
    input.MoveRaw=broker.MoveRaw;
    currentInput(player)=input;
    return true;
   }
   catch(Exception e)
   {
    currentInputMissing=true;
    Warn("player input is not reachable; walking while the wheel is up is off: "+e.Message);
    return false;
   }
  }

  static AccessTools.FieldRef<ProdigyPlayer,Cleverous.Prodigy.PlayerInput> currentInput;
  static bool currentInputMissing;
  static float nextFreeLog;

  // movementLocked also freezes the stick - a menu, a cutscene or a script can set it - and that
  // must keep winning. If the field ever moves, movement stays frozen rather than overriding it.
  static bool MovementLocked(InputBroker broker)
  {
   try
   {
    locked=locked??AccessTools.FieldRefAccess<InputBroker,bool>("movementLocked");
    return locked(broker);
   }
   catch { return true; }
  }

  static AccessTools.FieldRef<InputBroker,bool> locked;

  // A connected controller is the one thing that must keep the mouse half of the mod out of the
  // way: the game picks from the stick while the wheel is up, and a pointer has no business being
  // hidden or pinned for that. The game's own input behaviour is not a reliable test - it reports
  // Free as well as ClickToMove while playing with a mouse - so the devices themselves are asked.
  static bool MouseMode()
  {
   return Gamepad.current==null&&Joystick.current==null;
  }

  // Everything hangs off the wheel key, which the input broker reports every frame. The panel's
  // own Update is not used: it is the one thing that turned out not to run in game.
  void Tick()
  {
   var broker=InputBroker.Instance;
   var holding=broker!=null&&broker.HoldingSpellWheel;
   if(holding!=wasHolding)
   {
    wasHolding=holding;
    if(debug.Value)Log("wheel key "+(holding?"down":"up")+".");
   }
   if(!holding)
   {
    holdingSince=-1f;
    if(open)Finish(true);
    return;
   }
   if(!open)Begin();
   if(open)StepFrame();
  }

  static AccessTools.FieldRef<PlayerUI,SpellbookRadialUI> wheelField;

  // Found once and kept: the panel is a scene object that survives the whole run, so the lookup
  // only repeats if Unity destroys it.
  SpellbookRadialUI Panel()
  {
   if(panel!=null&&panel.gameObject!=null&&panel.gameObject.scene.IsValid())return panel;
   panel=null;
   try
   {
    var ui=PlayerUI.Instance;
    if(ui!=null)
    {
     wheelField=wheelField??AccessTools.FieldRefAccess<PlayerUI,SpellbookRadialUI>("spellWheelUI");
     panel=wheelField(ui);
    }
   }
   catch { }
   if(panel==null)
   {
    foreach(var item in Resources.FindObjectsOfTypeAll(typeof(SpellbookRadialUI)))
    {
     var ui=item as SpellbookRadialUI;
     if(ui==null||ui.gameObject==null||!ui.gameObject.scene.IsValid())continue;
     panel=ui;
     if(ui.isActiveAndEnabled)break;
    }
   }
   return panel;
  }

  void Begin()
  {
   if(!modEnabled.Value){if(debug.Value)Log("wheel key held with the mod switched off.");return;}
   if(!MouseMode())
   {
    if(holdingSince<0f)
    {
     holdingSince=Time.unscaledTime;
     if(debug.Value)Log("a controller is connected; the mod stays out of the wheel, but the left stick still walks.");
    }
    return;
   }
   var ui=Panel();
   if(ui==null)
   {
    if(holdingSince<0f)holdingSince=Time.unscaledTime;
    if(Time.unscaledTime-holdingSince<PanelGrace)return;
    Warn("wheel key held but no spell wheel panel was found; the mod stays out of it.");
    return;
   }
   var slots=Slots(ui);
   if(slots==null||slots.Length==0){Warn("wheel opened with no slots; nothing to pick.");return;}
   open=true;
   travel=Vector2.zero;
   selected=-1;
   HidePointer();
   var heldWheel=Wheel();
   if(debug.Value)Log("hold started: spells="+(heldWheel==null?0:heldWheel.Count)+", slots="+slots.Length+", panel "+(ui.isActiveAndEnabled?"up":"not up")+".");
   // Start on the spell that is already active, so a hold released without a flick changes
   // nothing at all.
   var player=Player.PlayerChar;
   var spell=player==null?null:player.GetActiveSpell;
   if(spell==null)return;
   var spells=player.ActiveSpellWheel;
   for(var i=0;i<slots.Length;i++)
   {
    if(slots[i]==null||!slots[i].gameObject.activeSelf||!ReferenceEquals(slots[i].ActiveSpell,spell))continue;
    if(spells!=null&&(i>=spells.Count||spells[i]==null))continue;
    Highlight(slots[i],i);
    return;
   }
  }

  // Runs every frame while the wheel key is down. The game's own wheel Update does nothing in
  // mouse mode, so there is no hover to fight over.
  void StepFrame()
  {
   // Both the driver and the panel hook can land here in the same frame; only the first counts,
   // or the same mouse movement would be counted twice.
   if(lastFrame==Time.frameCount)return;
   lastFrame=Time.frameCount;
   var ui=panel;
   if(ui==null)return;
   var mouse=Mouse.current;
   if(mouse==null)return;
   var point=mouse.position.ReadValue();
   if(!pointerKnown){lastPointer=point;pointerKnown=true;}
   var step=point-lastPointer;
   lastPointer=point;
   // The pointer is warped back to the centre every frame; a jump from that warp, or a window
   // move, is not the player aiming.
   var jump=Screen.width*.2f;
   if(step.sqrMagnitude>jump*jump)step=Vector2.zero;
   travel=travel*Mathf.Pow(TravelDecay,Time.unscaledDeltaTime)+step;
   // Keep the hidden pointer away from the window edges, where the operating system would stop
   // reporting movement, and away from any UI under it.
   PinPointer();
   var slots=Slots(ui);
   if(slots==null||slots.Length==0)return;
   if(debug.Value&&Time.unscaledTime>=nextStepLog)
   {
    nextStepLog=Time.unscaledTime+.5f;
    var list=Wheel();
    Log("wheel: travel="+travel.magnitude.ToString("0.0")+"px needs "+flick.Value.ToString("0.0")+", slots="+slots.Length+", spells="+(list==null?0:list.Count));
   }
   if(travel.magnitude<flick.Value)return;
   var direction=travel.normalized;
   var centre=Centre(slots);
   var spells=Wheel();
   var best=-1;
   var bestDot=-1f;
   for(var i=0;i<slots.Length;i++)
   {
    var slot=slots[i];
    // Empty slots cannot be selected, so aiming at one keeps the last pick. SetClear leaves a
    // slot's old spell behind, and the wheel's list is the only reliable answer for what is in
    // the ring this time.
    if(slot==null||slot.ActiveSpell==null||!slot.gameObject.activeSelf)continue;
    if(spells!=null&&(i>=spells.Count||spells[i]==null))continue;
    var offset=new Vector2(slot.transform.position.x,slot.transform.position.y)-centre;
    if(offset.sqrMagnitude<1f)continue;
    var dot=Vector2.Dot(offset.normalized,direction);
    if(dot<=bestDot)continue;
    bestDot=dot;
    best=i;
   }
   if(best<0||best==selected)return;
   // Stay on the current slot unless the new one is clearly more the direction being aimed at.
   if(selected>=0&&selected<slots.Length&&slots[selected]!=null)
   {
    var current=new Vector2(slots[selected].transform.position.x,slots[selected].transform.position.y)-centre;
    if(current.sqrMagnitude>=1f&&Vector2.Dot(current.normalized,direction)>=bestDot-Hysteresis)return;
   }
   Highlight(slots[best],best);
  }

  static List<SpellData> Wheel()
  {
   var player=Player.PlayerChar;
   return player==null?null:player.ActiveSpellWheel;
  }

  // The middle of the ring is the middle of the slots themselves, so the answer does not depend
  // on where the panel happens to sit on the canvas or how it is scaled.
  static Vector2 Centre(SpellSlot[] slots)
  {
   var sum=Vector2.zero;
   var count=0;
   for(var i=0;i<slots.Length;i++)
   {
    if(slots[i]==null||!slots[i].gameObject.activeSelf)continue;
    sum+=new Vector2(slots[i].transform.position.x,slots[i].transform.position.y);
    count++;
   }
   return count==0?sum:sum/count;
  }

  void Highlight(SpellSlot slot,int index)
  {
   selected=index;
   // The wheel's own selection: EventSystem selection lights the slot's toggles and clears the
   // previous one, exactly as hovering or the game pad would.
   try { UtilityTG.SetSelected(slot.gameObject); } catch { }
   if(debug.Value)Logger.LogInfo("Spell wheel: slot "+index+" ("+(slot.ActiveSpell==null?"empty":slot.ActiveSpell.spellName)+").");
  }

  // Release: the highlighted slot becomes the active spell, exactly as if it had been clicked,
  // and the pointer comes back before anything else can go wrong.
  void Finish(bool commit)
  {
   if(!open)return;
   open=false;
   pointerKnown=false;
   var index=selected;
   selected=-1;
   RestorePointer();
   if(!commit||index<0)return;
   var player=Player.PlayerChar;
   var spells=player==null?null:player.ActiveSpellWheel;
   if(player==null||spells==null||index>=spells.Count)return;
   var spell=spells[index];
   if(spell==null)return;
   try
   {
    if(player.SetActiveSpell(spell))Logger.LogInfo("Spell wheel picked "+spell.spellName+".");
    else Logger.LogInfo("Could not switch to "+spell.spellName+"; the active spell is unchanged.");
   }
   catch(Exception e) { Logger.LogWarning("Spell switch failed: "+e.Message); }
  }

  // Runs at the end of every frame of a hold. Aiming's late tick re-activates the game's cursor
  // rect and the game's CursorManager can do the same, so the arrow is put away again rather than
  // only once when the hold started.
  internal void Guard()
  {
   if(!open)return;
   try
   {
    if(Cursor.visible)Cursor.visible=false;
    var rect=CursorRect();
    if(rect!=null&&rect.gameObject.activeSelf)rect.gameObject.SetActive(false);
    if(Cursor.lockState==CursorLockMode.Locked)Cursor.lockState=CursorLockMode.Confined;
   }
   catch { }
  }

  void Focus(bool focused)
  {
   // Alt-tabbing away must never leave the game holding a hidden, pinned pointer.
   if(!focused&&open)Finish(false);
  }

  void Shutdown()
  {
   if(stopped)return;
   stopped=true;
   if(open)Finish(false);
   harmony?.UnpatchSelf();
  }

  // The game draws its own arrow, so hiding the operating-system pointer is not enough: the
  // game's cursor rect is switched off too, and the pointer is made warpable (Confined, not
  // Locked) and pinned to the middle each frame so the direction never depends on where the
  // pointer ended up.
  void HidePointer()
  {
   if(cursorSaved)return;
   cursorSaved=true;
   cursorGuarded=true;
   var mouse=Mouse.current;
   pointerHome=mouse==null?Vector2.zero:mouse.position.ReadValue();
   cursorVisible=Cursor.visible;
   cursorLock=Cursor.lockState;
   Cursor.visible=false;
   Cursor.lockState=CursorLockMode.Confined;
   arrowWasActive=false;
   var rect=CursorRect();
   if(rect!=null)
   {
    arrowWasActive=rect.gameObject.activeSelf;
    if(arrowWasActive)rect.gameObject.SetActive(false);
   }
   pointerKnown=false;
   PinPointer();
  }

  void PinPointer()
  {
   var mouse=Mouse.current;
   if(mouse==null)return;
   var centre=new Vector2(Mathf.Round(Screen.width*.5f),Mathf.Round(Screen.height*.5f));
   mouse.WarpCursorPosition(centre);
   lastPointer=centre;
   pointerKnown=true;
   Cursor.visible=false;
   if(Cursor.lockState==CursorLockMode.Locked)Cursor.lockState=CursorLockMode.Confined;
  }

  void RestorePointer()
  {
   if(!cursorSaved)return;
   cursorSaved=false;
   var rect=CursorRect();
   if(rect!=null&&arrowWasActive)
   {
    rect.gameObject.SetActive(true);
    // Put the arrow on the pointer in the same frame, instead of leaving it where the hold
    // pinned it for one frame.
    rect.position=new Vector3(pointerHome.x,pointerHome.y,0f);
   }
   arrowWasActive=false;
   var mouse=Mouse.current;
   if(mouse!=null)
   {
    // Put the pointer back where it was before the wheel, so the arrow never jumps.
    mouse.WarpCursorPosition(pointerHome);
    Cursor.visible=false;
    Cursor.lockState=CursorLockMode.Confined;
   }
   Cursor.visible=cursorVisible;
   Cursor.lockState=cursorLock==CursorLockMode.Locked?CursorLockMode.Confined:cursorLock;
   cursorGuarded=false;
  }

  // CursorManager is not a singleton in the game's code, so the live component is looked up and
  // its VirtualCursor rect is read through reflection.
  static RectTransform CursorRect()
  {
   try
   {
    virtualCursor=virtualCursor??typeof(CursorManager).GetProperty("VirtualCursor",BindingFlags.Instance|BindingFlags.Public);
    if(virtualCursor==null)return null;
    if(cursorManager==null||cursorManager.gameObject==null||!cursorManager.gameObject.scene.IsValid())
    {
     cursorManager=null;
     foreach(var item in Resources.FindObjectsOfTypeAll(typeof(CursorManager)))
     {
      var component=item as CursorManager;
      if(component==null||component.gameObject==null||!component.gameObject.scene.IsValid())continue;
      if(cursorManager==null)cursorManager=component;
      if(component.gameObject.activeInHierarchy){cursorManager=component;break;}
     }
    }
    return cursorManager==null?null:virtualCursor.GetValue(cursorManager) as RectTransform;
   }
   catch { return null; }
  }
 }
}
