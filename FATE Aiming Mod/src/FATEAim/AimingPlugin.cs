// Copyright (c) 2026 MikhailKillsZombies. MIT; see LICENSE.
// Target-mode aiming layered on PartGhost's FATE WASD (partghost.fate.wasd) and
// FATE Camera Turn (partghost.fate.cameraturn). Both are hard requirements. The
// reticle's look is registered with FATE Mod Menu, and the aim key with the
// optional FATE Rebind; neither menu is required for aiming to work.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Cleverous.Prodigy;
using Cleverous.Prodigy.Input;
using Cleverous.VaultInventory;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace MikhailKillsZombies.FateAiming
{
 [BepInPlugin(Guid,"FATE Aiming","0.5.6")]
 [BepInDependency("partghost.fate.wasd")]
 [BepInDependency("partghost.fate.cameraturn")]
 // Both menus are optional and are resolved by reflection; neither is required.
 [BepInDependency("zombieghost.rebind",BepInDependency.DependencyFlags.SoftDependency)]
 [BepInDependency("zombieghost.modmenu",BepInDependency.DependencyFlags.SoftDependency)]
 public sealed class AimingPlugin:BaseUnityPlugin
 {
  public const string Guid="mikhailkillszombies.fate.aiming";
  const float MaxRange=100f;
  // Ring thickness is a share of the radius, in steps this fine so 1 fills it.
  const int RingSteps=100;
  static AimingPlugin active;

  Harmony harmony;
  InputAction toggle,attack,interact;
  object provider;
  ConfigEntry<bool> modEnabled,hold,separate,clickToWalk,invertVertical,bindingMigrated,debug;
  ConfigEntry<string> binding,attackBinding,interactBinding;
  ConfigEntry<float> yawSpeed,verticalSpeed,reticleSize,reticleRotation,reticleOpacity,ringOpacity,ringThickness,ringScale;
  ConfigEntry<float> crosshairRed,crosshairGreen,crosshairBlue,ringRed,ringGreen,ringBlue;
  ConfigEntry<bool> ringThicknessMigrated;

  // armed = requested by toggle/hold; applied = cursor and camera are ours right now.
  bool armed,applied,restoring;   float yaw,aimUp,lastX,lastY,lastDx,lastDy;
   int settle;CursorLockMode lastLock;
  float nextReport;
  int camHits,clickHits,locHits,moveGates;
  float reticleX,reticleY=float.NaN;
  InteractableSceneObject hover;
  bool selfAttack;
  // True only while ParseClickToMove is placing the walk a click asked for.
  bool clickMove;
  // Seconds left of keeping the game's cursor rect on the pointer after a release.
  float releasePin;
  // Set once a controller has been seen; see ControllerConnected.
  static bool controllerSeen;
  static Texture2D pixel,ringTexture;
  static int ringTextureThickness;
  Driver driver;

  static readonly FieldInfo DamageableField=AccessTools.Field(typeof(InputBroker),"damageableTarget");
  static readonly FieldInfo CharacterTargetField=AccessTools.Field(typeof(InputBroker),"characterTarget");
  static readonly FieldInfo HoveredField=AccessTools.Field(typeof(InputBroker),"hoveredInteractable");
  static readonly MethodInfo LookDestination=AccessTools.Method(typeof(InputBroker),"SetLookDestination");

  static readonly AccessTools.FieldRef<InputBroker,LayerMask> NavigationLayers=
   AccessTools.FieldRefAccess<InputBroker,LayerMask>("NavigationLayers");
  static readonly AccessTools.FieldRef<ProdigyCamera,Quaternion> OriginalRotation=
   AccessTools.FieldRefAccess<ProdigyCamera,Quaternion>("originalRotation");
  static readonly AccessTools.FieldRef<ProdigyCamera,float> OriginalYaw=
   AccessTools.FieldRefAccess<ProdigyCamera,float>("originalYRotation");
  static readonly AccessTools.FieldRef<Tableflip.MiniMapIconUI,Quaternion> SavedRotation=
   AccessTools.FieldRefAccess<Tableflip.MiniMapIconUI,Quaternion>("savedRotation");
  // The only way a click moves the player; dropping it is what keeps clicks still.
  static readonly MethodInfo MoveDestination=AccessTools.Method(typeof(InputBroker),"SetMoveDestination",new[]{typeof(Vector3)});

  // FATE Camera Turn owns its own yaw; hand it over so toggling is seamless.
  static Type turnType;
  static FieldInfo turnActive,turnYaw;

  // The game's virtual cursor, restored through its own state machine.
  static Type cursorType;
  static PropertyInfo cursorInstance,cursorVirtual,cursorCurrent;
  // CursorManager has no singleton property; the live component is cached here.
  static object cursorManagerInstance;
  static MethodInfo cursorVisible,cursorUpdateState;

  // Optional Rebind integration, resolved lazily because load order is not guaranteed.
  static Type rebindType;
  static PropertyInfo rebindBlocked;
  static float nextRebindProbe;
  static bool rebindBlockedNow;
  float nextRebindRegister;
  // Optional FATE Mod Menu module: the reticle gets its own screen there.
  object menuModule;
  float nextMenuRegister;
  static Type modMenuType;
  static PropertyInfo modMenuBlocked;
  static float nextModMenuProbe;
  static bool modMenuBlockedNow;

  void Awake()
  {
   modEnabled=Config.Bind("Aiming","Enabled",true,"Enable target aiming mode. Requires FATE WASD and FATE Camera Turn.");
   if(!modEnabled.Value)return;
   active=this;
   hold=Config.Bind("Aiming","HoldToAim",false,"Checked: hold the key to aim. Unchecked: the key toggles aiming on and off.");
   binding=Config.Bind("Aiming","Binding","<Mouse>/middleButton","Aim key when FATE Rebind is not installed. FATE Rebind exposes it as 'Toggle aiming mode'.");
   // The default moved from Left Alt to the middle mouse button. Existing configs already
   // store Left Alt, so migrate once and remember that it was done.
   bindingMigrated=Config.Bind("Internal","BindingMigrated",false,"Internal marker. Leave checked: it records that the default aim key was moved to the middle mouse button.");
   if(!bindingMigrated.Value)
   {
    if(binding.Value=="<Keyboard>/leftAlt")binding.Value="<Mouse>/middleButton";
    bindingMigrated.Value=true;
   }
   yawSpeed=Config.Bind("Aiming","MouseSensitivity",.25f,new ConfigDescription("Camera degrees per pixel of horizontal mouse movement.",new AcceptableValueRange<float>(.02f,3f)));
   verticalSpeed=Config.Bind("Aiming","VerticalSensitivity",.6f,new ConfigDescription("Reticle pixels per pixel of vertical mouse movement.",new AcceptableValueRange<float>(.1f,3f)));
   reticleSize=Config.Bind("Aiming","ReticleSize",1f,new ConfigDescription("Reticle scale.",new AcceptableValueRange<float>(.5f,3f)));
   reticleRotation=Config.Bind("Reticle","Rotation",0f,new ConfigDescription("Crosshair rotation in degrees.",new AcceptableValueRange<float>(-180f,180f)));
   reticleOpacity=Config.Bind("Reticle","Opacity",1f,new ConfigDescription("Crosshair opacity.",new AcceptableValueRange<float>(.05f,1f)));
   ringOpacity=Config.Bind("Reticle","RingOpacity",.6f,new ConfigDescription("Outer ring opacity. Set to 0 to hide the ring.",new AcceptableValueRange<float>(0f,1f)));
   // A share of the radius rather than pixels, so 1 is a solid disc instead of a ring.
   ringThickness=Config.Bind("Reticle","RingThickness",.1f,
    "Outer ring thickness as a share of the ring radius, 0.01 to 1. 1 fills the circle completely.");
   ringThicknessMigrated=Config.Bind("Internal","RingThicknessMigrated",false,
    "Internal marker. Leave checked: it records that RingThickness was converted from pixels to a share of the radius.");
   if(!ringThicknessMigrated.Value)
   {
    // Older configs stored 1-10 pixels; a fifth of that keeps the same thin look.
    if(ringThickness.Value>1f)ringThickness.Value=Mathf.Clamp(ringThickness.Value/20f,.01f,1f);
    ringThicknessMigrated.Value=true;
   }
   crosshairRed=Config.Bind("Reticle","CrosshairRed",1f,Amount("Red amount of the crosshair, 0 to 1."));
   crosshairGreen=Config.Bind("Reticle","CrosshairGreen",.86f,Amount("Green amount of the crosshair, 0 to 1."));
   crosshairBlue=Config.Bind("Reticle","CrosshairBlue",.55f,Amount("Blue amount of the crosshair, 0 to 1."));
   ringRed=Config.Bind("Reticle","RingRed",1f,Amount("Red amount of the circle, 0 to 1."));
   ringGreen=Config.Bind("Reticle","RingGreen",.86f,Amount("Green amount of the circle, 0 to 1."));
   ringBlue=Config.Bind("Reticle","RingBlue",.55f,Amount("Blue amount of the circle, 0 to 1."));
   ringScale=Config.Bind("Reticle","RingScale",1.6f,new ConfigDescription("Outer ring size relative to the crosshair.",new AcceptableValueRange<float>(1f,4f)));
   invertVertical=Config.Bind("Aiming","InvertVertical",false,"Checked: moving the mouse up lowers the reticle (inverted aim). Unchecked: moving the mouse up raises it.");
   separate=Config.Bind("Aiming","SeparateActionButtons",false,"Checked: Attack and Interact use their own buttons. Unchecked: the game's single context-sensitive click does both.");
   attackBinding=Config.Bind("Aiming","AttackBinding","<Mouse>/leftButton","Attack button used when actions are separated. FATE Rebind overrides it when installed.");
   interactBinding=Config.Bind("Aiming","InteractBinding","<Keyboard>/e","Interact button used when actions are separated. FATE Rebind overrides it when installed.");
   clickToWalk=Config.Bind("Controls","ClickToWalk",false,
    "Checked: the mouse may walk the player, as in the unmodded game. Unchecked: clicks still select, attack and interact, but never move the player. "+
    "The walk a click asks for is dropped before it reaches the player, so clicking the hotbar, the spell wheel or any menu leaves movement alone.");
   debug=Config.Bind("Aiming","Diagnostics",true,"Log aiming state to the BepInEx log every couple of seconds and on every aim-key press. Turn off once aiming works.");

   toggle=new InputAction("aim.toggle",InputActionType.Button,binding.Value);
   toggle.Enable();
   if(separate.Value)
   {
    attack=new InputAction("aim.attack",InputActionType.Button,attackBinding.Value);
    interact=new InputAction("aim.interact",InputActionType.Button,interactBinding.Value);
    attack.Enable();interact.Enable();
   }

   harmony=new Harmony(Guid);
   // The per-frame driver comes first: without it nothing below would ever run, and one
   // unavailable patch used to abort the whole mod. Each patch now stands on its own, so a
   // changed game method only turns off the feature that needs it.
   EnsureDriver();
   Patch(AccessTools.Method(typeof(ProdigyCamera),"UpdateCameraRotation"),
    prefix:nameof(Camera),priority:Priority.High);
   Patch(AccessTools.Method(typeof(Tableflip.MiniMapIconUI),"LateUpdate"),
    postfix:nameof(Minimap),priority:Priority.Low);
   Patch(AccessTools.Method(typeof(InputBroker),"GetMouseLocation"),
    postfix:nameof(Location));
   // A click still selects, interacts and attacks; only the walk it asks for is dropped. The
   // window opens at the lowest priority so every other ParseClickToMove prefix has run first:
   // FATE WASD moves the player from one of its own, and that move must never be taken for a
   // click.
   Patch(AccessTools.Method(typeof(InputBroker),"ParseClickToMove"),
    prefix:nameof(BeforeClickMove),postfix:nameof(AfterClickMove),priority:Priority.Low);
   Patch(MoveDestination,prefix:nameof(GateMoveDestination),priority:Priority.High);
   // While aiming, the game's cursor belongs to us. If the game asks for it back,
   // a menu or UI has taken over, so aiming yields immediately.
   Patch(AccessTools.Method(typeof(Tableflip.CursorManager),"SetCursorVisible"),
    prefix:nameof(CursorRequest));
   // Stop the game moving its cursor rect to the pointer while aiming.
   Patch(AccessTools.Method(typeof(Tableflip.CursorManager),"InputTick"),
    prefix:nameof(SkipCursorTick));
   if(separate.Value)
   {
    Patch(AccessTools.Method(typeof(InputBroker),"UpdateClickToMoveTargets"),postfix:nameof(Targets));
    Patch(AccessTools.Method(typeof(Tableflip.Player),"StartAttack",Type.EmptyTypes),
     prefix:nameof(GateAttack));
   }
   EnsureRebind();
   EnsureModMenu();
   SceneManager.sceneLoaded+=OnSceneLoaded;
   Logger.LogInfo("FATE Aiming 0.5.6 ready; WASD and Camera Turn required and present."+
    (ControllerConnected()?" A controller is connected, so aiming stays off until it is unplugged.":""));
  }

  // One patch, reported on its own, so a single missing game method cannot take the rest of
  // the mod down with it.
  void Patch(MethodInfo target,string prefix=null,string postfix=null,int priority=Priority.Normal)
  {
   if(target==null){Logger.LogWarning("A game method FATE Aiming uses is missing; one feature is off.");return;}
   try {
    harmony.Patch(target,
     prefix==null?null:new HarmonyMethod(typeof(AimingPlugin),prefix){priority=priority},
     postfix==null?null:new HarmonyMethod(typeof(AimingPlugin),postfix){priority=priority});
   } catch(Exception e) { Logger.LogWarning("Could not patch "+target.DeclaringType.Name+"."+target.Name+": "+e.Message); }
  }

  // The game destroys BepInEx's plugin object during startup, which stops its Update and OnGUI.
  // All per-frame work therefore runs on our own persistent object instead.
  void EnsureDriver()
  {
   if(driver!=null)return;
   var host=new GameObject("FATE.AimDriver");
   UnityEngine.Object.DontDestroyOnLoad(host);
   driver=host.AddComponent<Driver>();
   Logger.LogInfo("Aiming driver attached; per-frame update running.");
  }

  void OnSceneLoaded(Scene scene,LoadSceneMode mode)
  {
   // A new scene usually means a loading screen, menu or floor change; never carry a
   // hidden cursor and a warped pointer across it.
   if(applied)Stop();
   armed=false;
   EnsureDriver();
  }

  sealed class Driver:MonoBehaviour
  {
   void Update(){var p=active;if(!ReferenceEquals(p,null))p.Tick();}
   void LateUpdate(){var p=active;if(!ReferenceEquals(p,null))p.LateTick();}
   void OnGUI(){var p=active;if(!ReferenceEquals(p,null))p.Draw();}
   void OnApplicationFocus(bool focused){var p=active;if(!ReferenceEquals(p,null))p.Focus(focused);}
  }

  // Optional integration: no Rebind DLL is shipped or required.
  void EnsureRebind()
  {
   if(provider!=null||Time.unscaledTime<nextRebindRegister)return;
   nextRebindRegister=Time.unscaledTime+2f;
   RegisterRebind();
  }

  void RegisterRebind()
  {
   var api=FindType("ZombieGhost.Rebind.RebindApi");
   if(api==null)return;
   try {
    var register=api.GetMethod("RegisterProvider",new[]{typeof(string),typeof(string)});
    if(register==null)return;
    provider=register.Invoke(null,new object[]{Guid,"FATE Aiming"});
    var action=provider.GetType().GetMethod("RegisterAction");
    action?.Invoke(provider,new object[]{"aim.toggle",toggle,"Toggle aiming mode","Aiming",null});
    // The reticle's look now lives in FATE Mod Menu's own screen (see RegisterModMenu);
    // Rebind keeps the key bindings alone.
    if(separate.Value)
    {
     action?.Invoke(provider,new object[]{"aim.attack",attack,"Attack (aiming)","Combat",null});
     action?.Invoke(provider,new object[]{"aim.interact",interact,"Interact (aiming)","Combat",null});
    }
    Logger.LogInfo("FATE Aiming registered its aim key with FATE Rebind.");
   } catch(Exception e) { Logger.LogWarning("FATE Rebind registration skipped: "+e.Message);provider=null;nextRebindRegister=Time.unscaledTime+30f; }
  }

  // Optional FATE Mod Menu integration: reticle settings get their own screen there
  // instead of sharing Rebind's key list. Retried because load order is not guaranteed.
  void EnsureModMenu()
  {
   if(menuModule!=null||Time.unscaledTime<nextMenuRegister)return;
   nextMenuRegister=Time.unscaledTime+2f;
   RegisterModMenu();
  }

  void RegisterModMenu()
  {
   var api=FindType("ZombieGhost.ModMenu.ModMenuApi");
   if(api==null)return;
   try {
    var register=api.GetMethod("RegisterModule",new[]{typeof(string),typeof(string)});
    if(register==null)return;
    menuModule=register.Invoke(null,new object[]{Guid,"FATE Aiming"});
    var type=menuModule.GetType();
    var addToggle=type.GetMethod("RegisterToggle");
    if(addToggle!=null)
    {
     addToggle.Invoke(menuModule,new object[]{"aim.invert-vertical","Invert up/down aim",new Func<bool>(()=>invertVertical.Value),new Action<bool>(value=>invertVertical.Value=value),"Aim"});
     addToggle.Invoke(menuModule,new object[]{"aim.hold","Hold the key to aim",new Func<bool>(()=>hold.Value),new Action<bool>(value=>hold.Value=value),"Aim"});
    }
    var addSlider=type.GetMethod("RegisterSlider");
    if(addSlider==null)Logger.LogWarning("Installed FATE Mod Menu has no RegisterSlider: update it to show the reticle sliders.");
    else
    {
     Action<string,string,Func<float>,Action<float>,float,float,float,string> slider=(id,label,get,set,min,max,step,category)=>{
      try { addSlider.Invoke(menuModule,new object[]{id,label,get,set,min,max,step,category,null}); }
      catch(Exception e) { Logger.LogWarning("FATE Mod Menu could not add the "+id+" slider: "+e.Message); }
     };
     slider("reticle.rotation","Rotation",()=>reticleRotation.Value,value=>reticleRotation.Value=value,-180f,180f,1f,"Crosshair");
     slider("reticle.scale","Crosshair scale",()=>reticleSize.Value,value=>reticleSize.Value=value,.5f,3f,.05f,"Crosshair");
     slider("reticle.opacity","Crosshair opacity",()=>reticleOpacity.Value,value=>reticleOpacity.Value=value,.05f,1f,.01f,"Crosshair");
     slider("ring.opacity","Circle opacity",()=>ringOpacity.Value,value=>ringOpacity.Value=value,0f,1f,.01f,"Circle");
     slider("ring.thickness","Circle thickness",()=>ringThickness.Value,value=>ringThickness.Value=value,.01f,1f,.01f,"Circle");
     slider("ring.scale","Circle size",()=>ringScale.Value,value=>ringScale.Value=value,1f,4f,.05f,"Circle");
    }
    var addColour=type.GetMethod("RegisterColor");
    if(addColour==null)Logger.LogWarning("Installed FATE Mod Menu has no RegisterColor: update it to show the reticle colours.");
    else
    {
     addColour.Invoke(menuModule,new object[]{"reticle.crosshair-colour","Crosshair colour",new Func<Color>(CrosshairColour),new Action<Color>(SetCrosshairColour),"Crosshair"});
     addColour.Invoke(menuModule,new object[]{"ring.colour","Circle colour",new Func<Color>(RingColour),new Action<Color>(SetRingColour),"Circle"});
    }
    var addPreview=type.GetMethod("RegisterPreview");
    if(addPreview!=null)
     addPreview.Invoke(menuModule,new object[]{"reticle.preview","What it looks like",new Func<Texture2D>(PreviewTexture),"Crosshair",170f});
    Logger.LogInfo("FATE Aiming registered its reticle settings with FATE Mod Menu.");
   } catch(Exception e) { Logger.LogWarning("FATE Mod Menu registration skipped: "+e.Message);menuModule=null;nextMenuRegister=Time.unscaledTime+30f; }
  }

  static ConfigDescription Amount(string description)
   =>new ConfigDescription(description,new AcceptableValueRange<float>(0f,1f));

  static Color Colour(ConfigEntry<float> red,ConfigEntry<float> green,ConfigEntry<float> blue)
   =>new Color(Mathf.Clamp01(red.Value),Mathf.Clamp01(green.Value),Mathf.Clamp01(blue.Value),1f);

  static Color CrosshairColour()
  {
   var p=active;
   if(ReferenceEquals(p,null))return Color.white;
   return Colour(p.crosshairRed,p.crosshairGreen,p.crosshairBlue);
  }

  static Color RingColour()
  {
   var p=active;
   if(ReferenceEquals(p,null))return Color.white;
   return Colour(p.ringRed,p.ringGreen,p.ringBlue);
  }

  static void SetCrosshairColour(Color value)
  {
   var p=active;
   if(ReferenceEquals(p,null))return;
   p.crosshairRed.Value=value.r;p.crosshairGreen.Value=value.g;p.crosshairBlue.Value=value.b;
  }

  static void SetRingColour(Color value)
  {
   var p=active;
   if(ReferenceEquals(p,null))return;
   p.ringRed.Value=value.r;p.ringGreen.Value=value.g;p.ringBlue.Value=value.b;
  }

  // The Mod Menu preview is this reticle, drawn from the same values as the real one.
  static Texture2D PreviewTexture()
  {
   var p=active;
   if(ReferenceEquals(p,null))return null;
   var key=p.crosshairRed.Value+"/"+p.crosshairGreen.Value+"/"+p.crosshairBlue.Value+"|"+
    p.reticleOpacity.Value+"|"+p.reticleSize.Value+"|"+p.reticleRotation.Value+"|"+
    p.ringRed.Value+"/"+p.ringGreen.Value+"/"+p.ringBlue.Value+"|"+p.ringOpacity.Value+"|"+
    p.ringThickness.Value+"|"+p.ringScale.Value;
   return ReticlePreview.Render(key,p.reticleRotation.Value,p.reticleSize.Value,CrosshairColour(),p.reticleOpacity.Value,
    p.ringScale.Value,RingColour(),p.ringOpacity.Value,p.ringThickness.Value);
  }

  void Tick()
  {
   if(!modEnabled.Value){if(applied)Stop();return;}
   EnsureRebind();
   EnsureModMenu();
   // A connected controller takes the whole mod out of the picture before anything else runs.
   var controller=ControllerConnected();
   NoticeController(controller);
   if(controller)armed=false;
   var blocked=Blocked();
   // Never re-enable our action while Rebind is capturing a key for it.
   if(!blocked&&!toggle.enabled)toggle.Enable();
   var press=!blocked&&toggle.WasPressedThisFrame();
   if(debug.Value)Report(press,blocked);
   if(!hold.Value&&press)armed=!armed;
   // Losing focus must never leave the mouse warped into the game window.
   var want=Application.isFocused&&!blocked&&(hold.Value?toggle.IsPressed():armed);
   if(want&&!applied){Start();Logger.LogInfo("Aiming on.");}
   else if(!want&&applied){Stop();Logger.LogInfo("Aiming off.");}
   if(applied)
   {
    if(separate.Value)ActionPresses();
    HideGameCursor();
   }
  }

  // A blocking menu, a rebind capture, death or a connected controller suspends aiming
  // without losing the toggle.
  static bool Blocked()=>BlockReason()!="none";

  // A connected controller switches off every feature of this mod: the aim key, the reticle,
  // the hidden cursor and the mouse-turned camera would all fight the pad, which has its own
  // cursor and camera controls. It is the connection that counts, not whether the pad has been
  // used, so a controller left plugged in keeps aiming off - and unplugging it makes the mod
  // available again, with no restart.
  static bool ControllerConnected()
  {
   var pad=Gamepad.current;
   if(pad!=null&&pad.added)return true;
   // Some controllers present themselves as a plain joystick rather than a gamepad.
   var stick=Joystick.current;
   return stick!=null&&stick.added;
  }

  // Says so once when the controller comes or goes, and drops the toggle so unplugging a pad
  // does not silently resume aiming the player never asked for.
  void NoticeController(bool connected)
  {
   if(connected==controllerSeen)return;
   controllerSeen=connected;
   if(connected)Logger.LogInfo("A controller is connected: FATE Aiming is off until it is disconnected.");
   else Logger.LogInfo("Controller disconnected: FATE Aiming is available again.");
  }

  static string BlockReason()
  {
   if(RebindBlocking())return "rebind";
   if(ModMenuBlocking())return "modmenu";
   if(Tableflip.InputBlocker.AnyBlockingUiMenuIsOpen)return "menu";
   if(ControllerConnected())return "controller";
   if(Tableflip.InventoryUI.IsDraggingItem)return "dragging";
   var player=Tableflip.Player.PlayerChar;
   if(player!=null&&player.MyVitals!=null&&player.MyVitals.IsDead)return "dead";
   var broker=InputBroker.Instance;
   if(broker!=null&&broker.HoldingSpellWheel)return "spellwheel";
   return "none";
  }

  // The settings screen keeps the mouse for itself, so aiming waits while it is open.
  // AccessTools.TypeByName writes a warning every time a type is missing, and the optional
  // integrations are asked about on a timer. With FATE Rebind or FATE Mod Menu simply not
  // installed, that filled the log with the same warning several times a second, so a plain
  // miss is answered quietly here instead. A miss is not cached: a partner mod that loads
  // later is still picked up on the next probe.
  static readonly Dictionary<string,Type> foundTypes=new Dictionary<string,Type>(StringComparer.Ordinal);

  static Type FindType(string name)
  {
   Type found;
   if(foundTypes.TryGetValue(name,out found))return found;
   foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
   {
    try { found=assembly.GetType(name,false); }
    catch { found=null; }
    if(found==null)continue;
    foundTypes[name]=found;
    return found;
   }
   return null;
  }

  // The current value is read every frame; only the reflection lookup is throttled. Caching the
  // answer is what made the cursor take a moment to come back when a menu opened - the mod could
  // sit behind a stale "not blocked" for a quarter of a second.
  static bool ModMenuBlocking()
  {
   if(modMenuBlocked==null)
   {
    if(Time.unscaledTime<nextModMenuProbe)return false;
    nextModMenuProbe=Time.unscaledTime+2f;
    modMenuType=modMenuType??FindType("ZombieGhost.ModMenu.ModMenuApi");
    modMenuBlocked=modMenuType?.GetProperty("IsInputBlocked",BindingFlags.Static|BindingFlags.Public);
    if(modMenuBlocked==null)return false;
   }
   try { modMenuBlockedNow=(bool)modMenuBlocked.GetValue(null); } catch { modMenuBlockedNow=false; }
   return modMenuBlockedNow;
  }

  static bool RebindBlocking()
  {
   if(rebindBlocked==null)
   {
    if(Time.unscaledTime<nextRebindProbe)return false;
    nextRebindProbe=Time.unscaledTime+2f;
    rebindType=rebindType??FindType("ZombieGhost.Rebind.RebindApi");
    rebindBlocked=rebindType?.GetProperty("IsInputBlocked",BindingFlags.Static|BindingFlags.Public);
    if(rebindBlocked==null)return false;
   }
   try { rebindBlockedNow=(bool)rebindBlocked.GetValue(null); } catch { rebindBlockedNow=false; }
   return rebindBlockedNow;
  }

  // Explains what the aim key is doing, so a failing test is diagnosable from the log alone.
  void Report(bool press,bool blocked)
  {
   if(press)Logger.LogInfo("aim key pressed: blocked="+blocked+" reason="+BlockReason()+" armed="+armed+" applied="+applied);
   if(Time.unscaledTime<nextReport)return;
   nextReport=Time.unscaledTime+2f;
   var keyboard=Keyboard.current;var mouse=Mouse.current;var broker=InputBroker.Instance;
   var path=toggle.bindings.Count>0?toggle.bindings[0].effectivePath:"none";
   var control=toggle.activeControl==null?"none":toggle.activeControl.displayName;
   var cursorTypeLoaded=cursorType??FindType("Tableflip.CursorManager");
   var manager=cursorTypeLoaded==null?null:ManagerInstance(cursorTypeLoaded);
   var numMode=manager==null?null:CurrentCursor(manager);
   Logger.LogInfo("aim state: applied="+applied+" armed="+armed+" hold="+hold.Value+
    " toggleEnabled="+toggle.enabled+" bound="+path+" active="+control+
    " focus="+Application.isFocused+
    " blocked="+blocked+" reason="+BlockReason()+" rebindBlocked="+rebindBlockedNow+" modMenuBlocked="+modMenuBlockedNow+" clickToWalk="+clickToWalk.Value+" invertVertical="+invertVertical.Value+" moveGates="+moveGates+
    " controller="+ControllerConnected()+
    " mode="+(broker==null?"no-broker":broker.ActiveInputMode+"/"+broker.LocalInputBehavior)+" cursorMode="+(numMode==null?"none":numMode.ToString())+
    " cam="+camHits+" click="+clickHits+" loc="+locHits+
    " cursor="+(mouse==null?"no-mouse":mouse.position.ReadValue().ToString())+
    " lock="+Cursor.lockState+" yaw="+yaw.ToString("0.0")+" aimUp="+aimUp.ToString("0.0")+
    " dx="+lastDx.ToString("0.0")+" dy="+lastDy.ToString("0.0")+
    " screen="+Screen.width+"x"+Screen.height+
    " turnYaw="+ReadTurnYaw().ToString("0.0"));
  }

  void Start()
  {
   applied=true;aimUp=0;
   reticleX=Screen.width*.5f;reticleY=Screen.height*.5f;
   yaw=ReadTurnYaw();
   var mouse=Mouse.current;
   if(mouse!=null)
   {
    var warp=MouseWarpPoint();
    mouse.WarpCursorPosition(warp);
    lastX=warp.x;lastY=warp.y;
   }
   EnterHiddenCursorState();
   HideGameCursor();
   // The pointer was free a moment ago; give the Input System a few frames to stop
   // reporting the distance it travelled before its movement turns the camera again.
   settle=3;lastLock=Cursor.lockState;
  }

  void Stop()
  {
   if(!applied)return;
   // Release the camera/cursor first so the cursor-restore path is not gated by our own patches.
   applied=false;
   RestoreGameCursor();
  }

  // A menu asked for the cursor while aiming: hand it back without losing the toggle.
  void ReleaseForUi()
  {
   if(!applied)return;
   applied=false;
   RestoreGameCursor();
   Logger.LogInfo("Aiming suspended for a menu; cursor returned.");
  }

  void Focus(bool focused)
  {
   if(!focused)
   {
    if(applied){Stop();Logger.LogInfo("Aiming cancelled: window lost focus.");}
    // Do not silently resume aiming after an alt-tab: the user gets a normal cursor
    // back and presses the aim key when they are ready.
    armed=false;
    // The game's own focus handler shows the pointer while unfocused; do not hide it again.
    Cursor.visible=true;
    return;
   }
   // Tick resumes toggle aiming once the window is focused again.
  }

  // Horizontal movement turns the camera; vertical movement moves the reticle upward.
  // reticleY is a bottom-left Unity screen coordinate; GUI and Input System use top-left.
  void Track(Mouse mouse)
  {
   // Taking the pointer back - a closed menu, a fresh aim key press, a relock - comes with one
   // frame whose delta is the whole distance the free cursor travelled while aiming was not in
   // charge, and applying it flings the camera round by that much. Settle instead: pin the
   // pointer, follow it and drop the movement that happened on somebody else's watch.
   var pos=mouse.position.ReadValue();
   if(Cursor.lockState!=lastLock){lastLock=Cursor.lockState;settle=3;}
   if(settle>0)
   {
    settle--;
    var pin=MouseWarpPoint();
    mouse.WarpCursorPosition(pin);
    lastX=pin.x;lastY=pin.y;lastDx=0;lastDy=0;
    return;
   }
   float dx=pos.x-lastX,dy=pos.y-lastY;
   if(Cursor.lockState==CursorLockMode.Locked){var raw=mouse.delta.ReadValue();dx=raw.x;dy=raw.y;}
   // Focus changes and window jumps must not fling the camera.
   if(Mathf.Abs(dx)>Screen.width*.5f||Mathf.Abs(dy)>Screen.height*.5f){dx=0;dy=0;}
   lastDx=dx;lastDy=dy;
   yaw=Mathf.Repeat(yaw+dx*yawSpeed.Value+180f,360f)-180f;
   var ceiling=Screen.height*.46f;
   // Unity pointer Y is bottom-left: moving the mouse up is a positive delta and raises
   // the reticle. It never drops below centre. InvertVertical mirrors this on request.
   var vertical=dy*(invertVertical.Value?-1f:1f);
   aimUp=Mathf.Clamp(aimUp+vertical*verticalSpeed.Value,0f,ceiling);
   reticleX=Screen.width*.5f;reticleY=Screen.height*.5f+aimUp;
   // A locked pointer ignores warping and is already pinned, so absolute tracking is meaningless.
   if(Cursor.lockState!=CursorLockMode.Locked)
   {
    var warp=MouseWarpPoint();
    mouse.WarpCursorPosition(warp);
    // Remember the point the cursor actually reached, otherwise sub-pixel rounding
    // is read back as movement and the camera slowly drifts on its own.
    lastX=warp.x;lastY=warp.y;
   }
  }

  // Mouse.position and WarpCursorPosition both use Unity screen coordinates (bottom-left),
  // the same space as reticleY and ScreenPointToRay. Do not flip Y here.
  Vector2 MouseWarpPoint()
  {
   var x=Mathf.Clamp(Mathf.Round(reticleX),0f,Mathf.Max(0,Screen.width-1));
   var y=Mathf.Clamp(Mathf.Round(reticleY),0f,Mathf.Max(0,Screen.height-1));
   return new Vector2(x,y);
  }

  // Separate buttons: our own actions drive attack and interaction while aiming.
  void ActionPresses()
  {
   var broker=InputBroker.Instance;
   if(broker==null)return;
   if(interact!=null&&interact.WasPressedThisFrame())InteractWith(broker);
   if(attack!=null&&attack.WasPressedThisFrame())AttackTarget(broker);
  }

  void InteractWith(InputBroker broker)
  {
   var target=hover;
   var player=Tableflip.Player.PlayerChar;
   if(target==null||player==null||!target.CanInteract)return;
   if(Vector3.Distance(player.transform.position,target.GetCenterPosition())>=target.InteractRange)return;
   target.Interact(player);
  }

  void AttackTarget(InputBroker broker)
  {
   var target=DamageableField?.GetValue(broker) as Component;
   var player=Tableflip.Player.PlayerChar;
   if(target==null||player==null)return;
   if(!player.InAttackRange(target.gameObject))return;
   var character=CharacterTargetField?.GetValue(broker) as Tableflip.Character;
   if(character!=null)player.SetTarget(character,true,true);
   LookDestination?.Invoke(broker,new object[]{target.transform.position});
   selfAttack=true;
   try { player.SelectAttack();player.StartAttack(); } finally { selfAttack=false; }
  }

  // Keep the hovered interactable for our own button, and keep the native click out of it.
  static void Targets(InputBroker __instance)
  {
   var p=active;
   if(ReferenceEquals(p,null)||!p.applied||!p.separate.Value)return;
   p.hover=HoveredField?.GetValue(__instance) as InteractableSceneObject;
   var mouse=Mouse.current;
   if(p.hover!=null&&mouse!=null&&mouse.leftButton.wasPressedThisFrame)HoveredField.SetValue(__instance,null);
  }

  // With separated actions the native click may never start an attack; the mod's own button does.
  static bool GateAttack()
  {
   var p=active;
   if(ReferenceEquals(p,null)||!p.applied||!p.separate.Value)return true;
   return p.selfAttack;
  }

  static bool Camera(ProdigyCamera __instance)
  {
   var p=active;
   if(ReferenceEquals(p,null))return true;
   p.camHits++;
   if(!p.applied||!Application.isFocused)return true;
   var mouse=Mouse.current;
   if(mouse==null)return true;
   p.Track(mouse);
   // FATE Camera Turn owns this method and always writes the rotation. Rather than fight it
   // for the camera, hand it our yaw and let its proven write do the turning.
   WriteTurnYaw(p.yaw);
   return true;
  }

  // The game asks for its cursor back while aiming: a menu or UI needs the mouse.
  static void CursorRequest(ref bool __0)
  {
   var p=active;
   if(ReferenceEquals(p,null)||!p.applied||p.restoring)return;
   if(__0)p.ReleaseForUi();
   else HideGameCursor();
  }

  static bool SkipCursorTick()
  {
   var p=active;
   return ReferenceEquals(p,null)||!p.applied;
  }

  // Re-hide after the game's own cursor pass has run this frame. Once the cursor is back,
  // keep it on the pointer until the game's own pass takes over, so it can be used to select
  // something in the very first frame it reappears.
  void LateTick()
  {
   if(applied){HideGameCursor();return;}
   if(releasePin<=0f)return;
   releasePin-=Time.unscaledDeltaTime;
   PinCursorRect();
  }

  static void PinCursorRect()
  {
   var mouse=Mouse.current;
   var type=cursorType??FindType("Tableflip.CursorManager");
   if(mouse==null||type==null)return;
   cursorType=type;
   var instance=ManagerInstance(type);
   if(instance==null)return;
   PlaceCursorRect(instance,mouse.position.ReadValue());
  }

  // Runs after FATE Camera Turn's own minimap pass so the compass matches the aimed camera.
  static void Minimap(Tableflip.MiniMapIconUI __instance)
  {
   var p=active;
   if(ReferenceEquals(p,null)||!p.applied||ProdigyCamera.GameCamera==null)return;
   var saved=SavedRotation(__instance);
   if(__instance.isCamera){__instance.transform.rotation=Quaternion.AngleAxis(p.yaw,Vector3.up)*saved;return;}
   var up=saved*Vector3.up;
   var angle=Mathf.Atan2(up.x,up.z)*Mathf.Rad2Deg;
   __instance.transform.rotation=Quaternion.AngleAxis(ProdigyCamera.GameCamera.transform.eulerAngles.y-angle,Vector3.up)*saved;
  }

  // Spells and other callers that ask for the cursor's world point follow the reticle.
  static void Location(InputBroker __instance,ref Vector3 __result)
  {
   var p=active;
   if(ReferenceEquals(p,null))return;
   p.locHits++;
   if(!p.applied)return;
   var camera=ProdigyCamera.GameCamera;
   if(camera==null)return;
   var ray=camera.ScreenPointToRay(new Vector3(p.reticleX,p.reticleY,0f));
   __result=Physics.Raycast(ray,out var hit,MaxRange,NavigationLayers(__instance)) ? hit.point : ray.GetPoint(MaxRange);
  }

  // ParseClickToMove decides where a click should take the player and applies it through
  // SetMoveDestination. With ClickToWalk off a click's destination is simply dropped, which is
  // what keeps clicking from walking. Nothing about the player's own movement state is touched,
  // so a click the interface owns - a hotbar slot, the spell wheel, anything in an open menu -
  // never reaches this path and can no longer plant the player mid-stride while they are walking.
  // FATE WASD moves the player through this same method from its own ParseClickToMove prefix, so
  // the window below is only open after every other prefix has run, and the gate also lets a call
  // through when no mouse button is down. Keyboard movement is never affected.
  static void BeforeClickMove()
  {
   var p=active;
   if(ReferenceEquals(p,null))return;
   p.clickHits++;
   p.clickMove=true;
  }

  static void AfterClickMove()
  {
   var p=active;
   if(!ReferenceEquals(p,null))p.clickMove=false;
  }

  static bool GateMoveDestination()
  {
   var p=active;
   if(ReferenceEquals(p,null)||p.clickToWalk.Value||!p.clickMove)return true;
   // Only a click is dropped. A held movement key is not a click, so FATE WASD keeps walking; on
   // a frame where the mouse button is also down its one destination is skipped and the next
   // frame puts it straight back.
   var mouse=Mouse.current;
   if(mouse==null||!mouse.leftButton.isPressed)return true;
   p.moveGates++;
   return false;
  }

  static float ReadTurnYaw()
  {
   try {
    turnType=turnType??FindType("PartGhost.FateControls.CameraTurnPlugin");
    turnActive=turnActive??turnType?.GetField("active",BindingFlags.Static|BindingFlags.NonPublic);
    turnYaw=turnYaw??turnType?.GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic);
    var instance=turnActive?.GetValue(null);
    if(instance!=null&&turnYaw!=null)return (float)turnYaw.GetValue(instance);
   } catch { }
   return 0f;
  }

  static void WriteTurnYaw(float value)
  {
   try {
    var instance=turnActive?.GetValue(null);
    if(instance!=null&&turnYaw!=null)turnYaw.SetValue(instance,value);
   } catch { }
  }

  // CursorManager is not a singleton in the game's code (the get_Instance calls inside it belong
  // to InputBroker), so locate the live component in the loaded scene and cache it.
  static object ManagerInstance(Type type)
  {
   var cached=cursorManagerInstance as Component;
   try {
    if(cached!=null&&cached.gameObject!=null&&cached.gameObject.scene.IsValid())return cached;
   } catch { }
   cursorManagerInstance=null;
   try {
    cursorInstance=cursorInstance??type.GetProperty("Instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
    var direct=cursorInstance?.GetValue(null);
    if(direct!=null){cursorManagerInstance=direct;return direct;}
   } catch { }
   try {
    Component fallback=null;
    foreach(var item in Resources.FindObjectsOfTypeAll(type))
    {
     var component=item as Component;
     if(component==null||component.gameObject==null||!component.gameObject.scene.IsValid())continue;
     if(component.gameObject.activeInHierarchy){cursorManagerInstance=component;return component;}
     fallback=fallback??component;
    }
    if(fallback!=null){cursorManagerInstance=fallback;return fallback;}
   } catch { }
   return null;
  }

  static object CurrentCursor(object manager)
  {
   try {
    cursorCurrent=cursorCurrent??manager.GetType().GetProperty("CurrentCursor",BindingFlags.Instance|BindingFlags.Public);
    return cursorCurrent?.GetValue(manager);
   } catch { return null; }
  }

  // Enter the same state the game uses for a controller: UpdateCursorState(2) deactivates the
  // game's arrow, tells the broker the virtual cursor is off and hides the OS pointer. It also
  // locks the pointer, which the game's own click ray cannot use, so we switch back to Confined
  // and keep warping to the reticle.
  static void EnterHiddenCursorState()
  {
   try {
    var type=cursorType??FindType("Tableflip.CursorManager");
    if(type!=null)
    {
     cursorType=type;
     var instance=ManagerInstance(type);
     cursorUpdateState=cursorUpdateState??type.GetMethod("UpdateCursorState",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
     if(instance!=null&&cursorUpdateState!=null)
     {
      var parameter=cursorUpdateState.GetParameters()[0];
      var mode=parameter.ParameterType.IsEnum
       ?Enum.ToObject(parameter.ParameterType,2)
       :Convert.ChangeType(2,parameter.ParameterType);
      cursorUpdateState.Invoke(instance,new[]{mode});
     }
     if(!ReferenceEquals(active,null))active.Logger.LogInfo("Hidden cursor state: manager="+(instance!=null)+" updateState="+(cursorUpdateState!=null)+" lock="+Cursor.lockState);
    }
   } catch(Exception e) { if(!ReferenceEquals(active,null))active.Logger.LogWarning("Could not enter the game's hidden cursor state: "+e.Message); }
   // UpdateCursorState(2) locks the pointer, but the game's click code reads Mouse.position
   // directly. Keep the pointer hidden yet warpable (Confined) so every code path still sees
   // the reticle; the arrow itself is already deactivated above.
   Cursor.visible=false;
   Cursor.lockState=CursorLockMode.Confined;
  }

  // Hide only the game's virtual cursor rect; do not touch the OS cursor state every frame.
  static void HideGameCursor()
  {
   // Re-assert the OS state even if the game's manager cannot be found this frame.
   // Confined keeps the pointer warpable, so the game's raw Mouse.position reads land on
   // the reticle; visible=false keeps it hidden and the game arrow is deactivated below.
   Cursor.visible=false;
   Cursor.lockState=CursorLockMode.Confined;
   var type=cursorType??FindType("Tableflip.CursorManager");
   if(type==null)return;
   cursorType=type;
   var instance=ManagerInstance(type);
   if(instance==null)return;
   try {
    cursorVirtual=cursorVirtual??type.GetProperty("VirtualCursor",BindingFlags.Instance|BindingFlags.Public);
    var rect=cursorVirtual?.GetValue(instance) as RectTransform;
    if(rect!=null&&rect.gameObject.activeSelf)rect.gameObject.SetActive(false);
   } catch { }
  }

  // Give the cursor back through CursorManager's own state machine, then park the OS
  // pointer on the reticle so the game's rect (which follows the pointer) cannot be
  // restored off-screen after a warp, an alt-tab or a resolution change.
  void RestoreGameCursor()
  {
   // Hold the arrow on the pointer for a few frames; see LateTick.
   releasePin=.25f;
   // Unity screen coordinates are bottom-left; Mouse.position and warping share this space.
   var targetToUse=new Vector2(
    Mathf.Clamp(reticleX,1f,Mathf.Max(1,Screen.width-2)),
    Mathf.Clamp(reticleY,1f,Mathf.Max(1,Screen.height-2)));
   // A locked pointer is pinned to the centre and cannot be warped, so unlock it first;
   // CursorManager then puts it back in its normal Confined mouse state below.
   if(Cursor.lockState==CursorLockMode.Locked)
   {
    Cursor.lockState=CursorLockMode.Confined;
    Cursor.visible=false;
   }
   var mouse=Mouse.current;
   if(mouse!=null)
   {
    mouse.WarpCursorPosition(targetToUse);
    lastX=targetToUse.x;lastY=targetToUse.y;
   }
   var type=cursorType??FindType("Tableflip.CursorManager");
   if(type==null)
   {
    Cursor.visible=false;
    Cursor.lockState=CursorLockMode.Confined;
    return;
   }
   cursorType=type;
   var instance=ManagerInstance(type);
   if(instance==null)return;
   restoring=true;
   try {
    cursorUpdateState=cursorUpdateState??type.GetMethod("UpdateCursorState",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    if(cursorUpdateState!=null)
    {
     var parameter=cursorUpdateState.GetParameters()[0];
     var mode=parameter.ParameterType.IsEnum
      ?Enum.ToObject(parameter.ParameterType,1)
      :Convert.ChangeType(1,parameter.ParameterType);
     cursorUpdateState.Invoke(instance,new[]{mode});
     PlaceCursorRect(instance,targetToUse);
    }
    else
    {
     cursorCurrent=cursorCurrent??type.GetProperty("CurrentCursor",BindingFlags.Instance|BindingFlags.Public);
     cursorCurrent?.SetValue(instance,Enum.ToObject(cursorCurrent.PropertyType,1));
     cursorVisible=cursorVisible??type.GetMethod("SetCursorVisible",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
     cursorVisible?.Invoke(instance,new object[]{true});
     Cursor.visible=false;
     Cursor.lockState=CursorLockMode.Confined;
     PlaceCursorRect(instance,targetToUse);
    }
   } catch(Exception e)
   {
    Logger.LogWarning("Cursor state restore failed, applying direct fallback: "+e.Message);
    ForceCursorVisible(type,instance);
   }
   finally { restoring=false; }
  }

  // Put the game's cursor rect on the pointer immediately. InputTick does this on its next
  // pass, but a stale or off-screen rect for even one frame reads as a missing cursor.
  static void PlaceCursorRect(object instance,Vector2 point)
  {
   try {
    var broker=InputBroker.Instance;
    // Gamepad users get the virtual cursor, which the game positions itself.
    if(broker!=null&&(int)broker.LocalInputBehavior!=1)return;
    if(cursorType==null)return;
    cursorVirtual=cursorVirtual??cursorType.GetProperty("VirtualCursor",BindingFlags.Instance|BindingFlags.Public);
    var rect=cursorVirtual?.GetValue(instance) as RectTransform;
    if(rect==null)return;
    rect.gameObject.SetActive(true);
    rect.position=new Vector3(point.x,point.y,0f);
   } catch { }
  }

  // Last-resort restore: show the game's rect and give the OS pointer back its normal state.
  static void ForceCursorVisible(Type type,object instance)
  {
   try {
    cursorVisible=cursorVisible??type.GetMethod("SetCursorVisible",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    cursorVisible?.Invoke(instance,new object[]{true});
   } catch { }
   try {
    cursorVirtual=cursorVirtual??type.GetProperty("VirtualCursor",BindingFlags.Instance|BindingFlags.Public);
    var rect=cursorVirtual?.GetValue(instance) as RectTransform;
    if(rect!=null)rect.gameObject.SetActive(true);
   } catch { }
   Cursor.visible=false;
   Cursor.lockState=CursorLockMode.Confined;
  }

  void Draw()
  {
   if(!applied)return;
   // Late guard: OnGUI runs after every Update/LateUpdate in the frame, so nothing that
   // re-enables the pointer late can leave it on screen this frame.
   if(Cursor.visible||Cursor.lockState!=CursorLockMode.Confined){Cursor.visible=false;Cursor.lockState=CursorLockMode.Confined;}
   var current=Event.current;
   if(current==null||current.type!=EventType.Repaint)return;
   if(pixel==null){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();}
   var scale=reticleSize.Value;
   var arm=12f*scale;
   var gap=arm*.4f;
   var thick=Mathf.Max(3f,Mathf.Round(arm/4f));
   var cx=reticleX;
   var cy=Screen.height-reticleY;
   var old=GUI.color;
   var oldMatrix=GUI.matrix;
   var rotation=reticleRotation.Value;
   if(Mathf.Abs(rotation)>.01f)GUIUtility.RotateAroundPivot(rotation,new Vector2(cx,cy));
   var opacity=Mathf.Clamp01(reticleOpacity.Value);
   GUI.color=new Color(0f,0f,0f,.6f*opacity);Arms(cx,cy,arm+2f,gap,thick+2f);
   var crosshair=CrosshairColour();
   GUI.color=new Color(crosshair.r,crosshair.g,crosshair.b,.95f*opacity);Arms(cx,cy,arm,gap,thick);
   GUI.matrix=oldMatrix;
   DrawRing(cx,cy,scale);
   GUI.color=old;
  }

  // The circle around the crosshair. Thickness is a share of its radius, so the
  // top of the range closes the hole and leaves a solid disc.
  void DrawRing(float cx,float cy,float scale)
  {
   var alpha=Mathf.Clamp01(ringOpacity.Value);
   if(alpha<=.001f)return;
   int steps=Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(ringThickness.Value,.01f,1f)*RingSteps),1,RingSteps);
   if(ringTexture==null||ringTextureThickness!=steps)
   {
    if(ringTexture!=null)UnityEngine.Object.Destroy(ringTexture);
    ringTexture=MakeRing(steps);
    ringTextureThickness=steps;
   }
   var radius=12f*scale*ringScale.Value;
   var half=radius+1f;
   var ringColour=RingColour();
   GUI.color=new Color(ringColour.r,ringColour.g,ringColour.b,alpha);
   GUI.DrawTexture(new Rect(cx-half,cy-half,half*2f,half*2f),ringTexture);
  }

  // Built once per thickness step; the texture carries the antialiased coverage,
  // GUI.color the tint. At RingSteps the inner radius reaches zero and it is a disc.
  static Texture2D MakeRing(int steps)
  {
   const int size=128;
   var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
   texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
   float center=(size-1)*.5f;
   float outer=center-1f;
   float inner=outer*(1f-steps/(float)RingSteps);
   var pixels=new Color32[size*size];
   for(int y=0;y<size;y++)
    for(int x=0;x<size;x++)
    {
     float dx=x-center,dy=y-center;
     float distance=Mathf.Sqrt(dx*dx+dy*dy);
     float coverage=Mathf.Clamp01(outer-distance+.5f)*(inner<=0f?1f:Mathf.Clamp01(distance-inner+.5f));
     pixels[y*size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(coverage*255f));
    }
   texture.SetPixels32(pixels);
   texture.Apply();
   return texture;
  }

  static void Arms(float cx,float cy,float arm,float gap,float thick)
  {
   Bar(cx-gap-arm,cy-thick*.5f,arm,thick);
   Bar(cx+gap,cy-thick*.5f,arm,thick);
   Bar(cx-thick*.5f,cy-gap-arm,thick,arm);
   Bar(cx-thick*.5f,cy+gap,thick,arm);
   Bar(cx-thick*.5f,cy-thick*.5f,thick,thick);
  }

  static void Bar(float x,float y,float width,float height)
  {GUI.DrawTexture(new Rect(x,y,width,height),pixel);}

  void Shutdown()
  {
   SceneManager.sceneLoaded-=OnSceneLoaded;
   if(driver!=null){Destroy(driver.gameObject);driver=null;}
   if(applied)Stop();
   (provider as IDisposable)?.Dispose();provider=null;
   (menuModule as IDisposable)?.Dispose();menuModule=null;
   toggle?.Dispose();toggle=null;
   attack?.Dispose();attack=null;interact?.Dispose();interact=null;
   active=null;
  }

  // The game destroys BepInEx's Unity object during startup; managed patch state survives.
  void OnApplicationQuit(){harmony?.UnpatchSelf();Shutdown();}
 }
}
