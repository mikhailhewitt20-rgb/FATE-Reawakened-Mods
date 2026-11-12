using System;
using System.Reflection;
using BepInEx;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace ZombieGhost.ModMenu
{
 [BepInPlugin(Guid,Name,Version)]
 // Rebind is not consumed yet, so a hard requirement would only stop this screen
 // from loading when the installed Rebind is older. The Thunderstore manifest
 // still lists it so mod managers install both together.
 [BepInDependency("zombieghost.rebind",BepInDependency.DependencyFlags.SoftDependency)]
 public sealed class ModMenuPlugin : BaseUnityPlugin
 {
  public const string Guid="zombieghost.modmenu",Name="FATE Mod Menu",Version="0.4.4";
  internal const string ButtonName="Mod Settings (FATE Mod Menu)";
  // One pause-menu row, matching the native button height the content rect assumes.
  private const float PauseRowHeight=66f;
  // Everything lives in statics on purpose: this game destroys the loader's plugin
  // object moments after it is created, so nothing may depend on the component
  // surviving. The Harmony patches stay installed and keep reading this state.
  private static ManualLogSource log;
  private static ModMenuScreen screen;
  private static Component pause;
  private static Button pauseButton;
  private static RectTransform pauseContent;
  private static Vector2 pauseContentSize;
  private static Type pauseType;
  private static MethodInfo menuButton;
  private static MethodInfo setPause;
  private static bool dirty;
  private static bool hotkeyOpened;
  private static bool pausedByUs;
  private static bool cursorShown;
  private static bool warnedNoPause;
  private static bool legacyInput;
  private static int lastPollFrame=-1;
  // Mods use this to stop their own input handling while the screen is up.
  internal static bool BlocksInput=>screen!=null;
  internal static void RequestRefresh(){dirty=true;}
  internal static void Log(string message){if(log!=null)log.LogInfo(message);}
  private Harmony harmony;

  private void Awake()
  {
   log=Logger;
   harmony=new Harmony(Guid);
   // The game may run with the legacy input backend disabled; probe once instead
   // of taking an exception every frame in the hotkey check.
   try{Input.GetKeyDown(KeyCode.None);legacyInput=true;}catch{legacyInput=false;}
   pauseType=AccessTools.TypeByName("Tableflip.PauseUI");
   if(pauseType==null){Log("MOD_MENU_NO_TYPE: Tableflip.PauseUI was not found; only the F10 screen will work.");return;}
   menuButton=AccessTools.Method(pauseType,"OnMenuButton");
   if(menuButton==null)Log("MOD_MENU_NO_MENU_METHOD: PauseUI.OnMenuButton was not found; F10 opens the screen on its own.");
   else harmony.Patch(menuButton,prefix:new HarmonyMethod(typeof(ModMenuPlugin),nameof(BeforeMenuButton)),
    postfix:new HarmonyMethod(typeof(ModMenuPlugin),nameof(AfterMenuButton)));
   // Awake runs when the pause UI is built, so the row exists before it is ever
   // opened; OnMenuButton re-fits it every time the window comes up.
   var awake=AccessTools.Method(pauseType,"Awake");
   var start=AccessTools.Method(pauseType,"Start");
   if(awake!=null)harmony.Patch(awake,postfix:new HarmonyMethod(typeof(ModMenuPlugin),nameof(AfterPauseAwake)));
   else if(start!=null)harmony.Patch(start,postfix:new HarmonyMethod(typeof(ModMenuPlugin),nameof(AfterPauseAwake)));
   else Log("MOD_MENU_NO_START: PauseUI.Awake and Start were not found; the row is only added when the pause menu opens.");
   // The game's own input tick is the frame loop. It is a game method, so it keeps
   // running no matter what happens to this plugin's object.
   var broker=AccessTools.TypeByName("Cleverous.Prodigy.Input.InputBroker");
   var tick=broker==null?null:AccessTools.Method(broker,"ManagedTick");
   if(tick!=null)harmony.Patch(tick,postfix:new HarmonyMethod(typeof(ModMenuPlugin),nameof(AfterInputTick)));
   else Log("MOD_MENU_NO_TICK: InputBroker.ManagedTick was not found; F10 will not be read.");
   Log("FATE Mod Menu ready: "+ModMenuApi.Modules.Count+" module(s) registered.");
  }

  // The pause key closes one layer at a time: our screen first, then the pause menu.
  private static bool BeforeMenuButton()
  {
   if(screen==null)return true;
   CloseScreen();
   return false;
  }
  private static void AfterMenuButton(object __instance){Log("MOD_MENU_PAUSE_TOGGLED");InstallButton(__instance);}
  private static void AfterPauseAwake(object __instance){Log("MOD_MENU_PAUSE_AWAKE");InstallButton(__instance);}
  private static void AfterInputTick(){Poll();}

  // One frame of work, called from the game's input tick.
  private static void Poll()
  {
   if(Time.frameCount==lastPollFrame)return;
   lastPollFrame=Time.frameCount;
   if(screen==null)
   {
    if(HotkeyPressed())ToggleFromHotkey();
    return;
   }
   // A screen opened from the pause menu closes with the menu; one opened with
   // the hotkey has no pause menu to follow.
   if(!hotkeyOpened&&pause!=null&&!pause.gameObject.activeInHierarchy){CloseScreen();return;}
   if(dirty){dirty=false;screen.Invalidate();}
   screen.Tick();
  }

  // F10 works anywhere in a loaded game, which is how the screen is tested
  // without going through the pause menu.
  private static bool HotkeyPressed()
  {
   var keyboard=Keyboard.current;
   if(keyboard!=null&&keyboard.f10Key.wasPressedThisFrame)return true;
   return legacyInput&&Input.GetKeyDown(KeyCode.F10);
  }

  private static void ToggleFromHotkey()
  {
   Log("MOD_MENU_HOTKEY screen="+(screen!=null)+" pause="+(pause!=null)+" pauseActive="+(pause!=null&&pause.gameObject.activeInHierarchy));
   if(screen!=null){CloseScreen();return;}
   if(pause==null||!pause.gameObject.activeInHierarchy)
   {
    // Going through the real pause menu keeps the pause, the cursor and the
    // button focus native; the screen then simply sits on top of it.
    if(OpenPauseMenu()){hotkeyOpened=false;OpenScreen();return;}
    OpenStandalone();
    return;
   }
   hotkeyOpened=false;
   OpenScreen();
  }

  // Opens the native pause menu without toggling it shut if it is already up.
  private static bool OpenPauseMenu()
  {
   if(menuButton==null){Log("MOD_MENU_HOTKEY_NO_METHOD: no OnMenuButton to open the pause menu with.");return false;}
   try
   {
    var target=FindPauseInstance();
    if(target==null){Log("MOD_MENU_HOTKEY_NO_PAUSE: no PauseUI instance in the loaded scene.");return false;}
    if(!target.gameObject.activeSelf)menuButton.Invoke(target,Array.Empty<object>());
    pause=target;
    return pause.gameObject.activeInHierarchy;
   }
   catch(Exception ex){Log("MOD_MENU_HOTKEY_FAILED: "+ex);return false;}
  }

  private static Component FindPauseInstance()
  {
   if(pauseType==null)return null;
   foreach(var candidate in Resources.FindObjectsOfTypeAll(pauseType))
   {
    var behaviour=candidate as Component;
    if(behaviour!=null&&behaviour.gameObject.scene.IsValid())return behaviour;
   }
   if(!warnedNoPause){warnedNoPause=true;Log("MOD_MENU_NO_PAUSE_INSTANCE: no Tableflip.PauseUI is loaded yet.");}
   return null;
  }

  // No pause menu in this scene: hold the game still and show the cursor ourselves.
  private static void OpenStandalone()
  {
   var canvas=FindScreenCanvas();
   if(canvas==null){Log("MOD_MENU_NO_CANVAS: the settings screen needs a canvas and none was active.");return;}
   try
   {
    screen=ModMenuScreen.Create(canvas);
    hotkeyOpened=true;
    dirty=false;
    SetGamePaused(true);pausedByUs=true;
    cursorShown=Cursor.lockState!=CursorLockMode.None;
    Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    Log("MOD_MENU_OPENED_HOTKEY canvas="+canvas.name+" modules="+ModMenuApi.Modules.Count);
   }
   catch(Exception ex){screen=null;hotkeyOpened=false;OpenFailed(ex);}
  }

  // Highest root canvas in the loaded scene, so the screen covers the HUD. The
  // game may render its UI through a camera, so an overlay canvas is preferred
  // but not required.
  private static Transform FindScreenCanvas()
  {
   Transform overlay=null,camera=null;
   int overlayOrder=int.MinValue,cameraOrder=int.MinValue;
   foreach(var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
   {
    if(canvas==null||!canvas.isRootCanvas||!canvas.gameObject.activeInHierarchy)continue;
    if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
    {
     if(canvas.sortingOrder>=overlayOrder){overlayOrder=canvas.sortingOrder;overlay=canvas.transform;}
    }
    else if(canvas.renderMode==RenderMode.ScreenSpaceCamera)
    {
     if(canvas.sortingOrder>=cameraOrder){cameraOrder=canvas.sortingOrder;camera=canvas.transform;}
    }
   }
   return overlay!=null?overlay:camera;
  }

  private static void OpenFailed(Exception ex){if(log!=null)log.LogError("MOD_MENU_OPEN_FAILED: "+ex);}

  private static void SetGamePaused(bool paused)
  {
   try
   {
    if(setPause==null)
    {
     var fate=AccessTools.TypeByName("Tableflip.Fate");
     setPause=fate==null?null:AccessTools.Method(fate,"SetPause",new[]{typeof(bool)});
    }
    if(setPause!=null)setPause.Invoke(null,new object[]{paused});
   }
   catch(Exception){/* pausing is a convenience, never a reason to fail */}
  }

  // Adds the button after Options, cloning the native one so the art matches.
  private static void InstallButton(object value)
  {
   if(!(value is Component pauseUi)){Log("MOD_MENU_SKIP: pause value was not a component.");return;}
   var type=pauseUi.GetType();
   var options=AccessTools.Field(type,"optionsButton")?.GetValue(pauseUi) as Button;
   if(options==null||options.transform.parent==null){Log("MOD_MENU_SKIP: optionsButton or its parent was not found.");return;}
   pause=pauseUi;
   if(pauseButton==null||pauseButton.transform.parent!=options.transform.parent)
   {
    try
    {
     var clone=(GameObject)UnityEngine.Object.Instantiate(options.gameObject,options.transform.parent,false);
     clone.name=ButtonName;
     // Drop the localization component so our label is not overwritten on language refresh.
     foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
      if(behaviour!=null&&behaviour.GetType().Name=="Localize")UnityEngine.Object.Destroy(behaviour);
     var label=clone.GetComponentInChildren<TMP_Text>(true);
     if(label!=null)label.text="Mod Settings";
     var button=clone.GetComponent<Button>();
     if(button==null){UnityEngine.Object.Destroy(clone);return;}
     button.interactable=true;
     button.onClick.RemoveAllListeners();
     button.onClick.AddListener(OpenScreen);
     clone.transform.SetSiblingIndex(options.transform.GetSiblingIndex()+1);
     pauseButton=button;
    }
    catch(Exception ex){if(log!=null)log.LogError("MOD_MENU_CLONE_FAILED: "+ex);return;}
   }
   // The pause window's content rect is a fixed size, so make room for one more
   // row. It is re-applied on every visit, always from the native height, so the
   // row is never clipped and the window is never grown twice.
   if(options.transform.parent.parent is RectTransform content)
   {
    if(content!=pauseContent){pauseContent=content;pauseContentSize=content.sizeDelta;}
    content.sizeDelta=new Vector2(pauseContentSize.x,pauseContentSize.y+PauseRowHeight);
    Log("MOD_MENU_BUTTON_INSTALLED parent="+options.transform.parent.name+" rows="+options.transform.parent.childCount+
     " native="+pauseContentSize+" content="+content.sizeDelta+" active="+pauseButton.gameObject.activeSelf);
   }
   else Log("MOD_MENU_SKIP: the button container has no grandparent RectTransform; the new row may be clipped.");
   Chain(pauseButton,type,pauseUi);
  }
  // Explicit up/down so the controller lands on the new button in the right place.
  private static void Chain(Button added,Type type,Component pauseUi)
  {
   var saveQuit=AccessTools.Field(type,"saveQuitButton")?.GetValue(pauseUi) as Button;
   var options=AccessTools.Field(type,"optionsButton")?.GetValue(pauseUi) as Button;
   var resume=AccessTools.Field(type,"resumeButton")?.GetValue(pauseUi) as Button;
   if(saveQuit==null||options==null||resume==null||added==null)return;
   var order=new[]{saveQuit,options,added,resume};
   for(int i=0;i<order.Length;i++)
   {
    var navigation=order[i].navigation;
    navigation.mode=Navigation.Mode.Explicit;
    navigation.selectOnUp=order[(i+order.Length-1)%order.Length];
    navigation.selectOnDown=order[(i+1)%order.Length];
    order[i].navigation=navigation;
   }
  }

  internal static void OpenScreen()
  {
   if(screen!=null||pause==null)return;
   var canvas=FindCanvas(pause.transform);
   if(canvas==null){Log("MOD_MENU_NO_PAUSE_CANVAS: no canvas was found for the pause menu.");return;}
   try
   {
    screen=ModMenuScreen.Create(canvas);
    dirty=false;
    Log("MOD_MENU_OPENED canvas="+canvas.name+" modules="+ModMenuApi.Modules.Count);
   }
   catch(Exception ex){screen=null;OpenFailed(ex);}
  }
  internal static void CloseScreen()
  {
   if(screen!=null)
   {
    var closing=screen;
    screen=null;
    closing.gameObject.SetActive(false);
    UnityEngine.Object.Destroy(closing.gameObject);
   }
   hotkeyOpened=false;
   if(pausedByUs){pausedByUs=false;SetGamePaused(false);}
   if(cursorShown){cursorShown=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
   if(pauseButton!=null&&pauseButton.gameObject.activeInHierarchy&&EventSystem.current!=null)
    EventSystem.current.SetSelectedGameObject(pauseButton.gameObject);
  }
  private static Transform FindCanvas(Transform start)
  {
   for(var current=start;current!=null;current=current.parent)
   {
    var canvas=current.GetComponent<Canvas>();
    if(canvas!=null&&canvas.isRootCanvas)return current;
   }
   return null;
  }
  // The component is destroyed by the game shortly after loading, and the patches
  // must stay for the session, so the only work here is forgetting the instance.
  private void OnDestroy(){Log("MOD_MENU_PLUGIN_OBJECT_DESTROYED (screen and patches continue)");}
 }
}
