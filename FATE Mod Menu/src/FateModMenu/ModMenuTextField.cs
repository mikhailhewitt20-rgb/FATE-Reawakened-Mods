using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZombieGhost.ModMenu
{
 // A single-line value you can type into, such as a pet or NPC name. The keyboard is
 // read from the Input System because the game never enables the legacy input manager,
 // and TMP_InputField depends on it: this keeps the field usable in FATE.
 internal sealed class ModMenuTextField : MonoBehaviour, IPointerClickHandler
 {
  // While a field is open it owns Escape and the list must not rebuild under it.
  internal static bool Editing { get; private set; }

  private TextMeshProUGUI caption;
  private Func<string> read;
  private Func<string, bool> apply;
  private Action<string> report;
  private int maxLength;
  private bool editing;
  private string buffer = string.Empty;
  private float nextClick;

  internal static void Attach(RectTransform host, TextMeshProUGUI caption, Func<string> read,
   Func<string, bool> apply, Action<string> report, int maxLength)
  {
   var field = host.gameObject.AddComponent<ModMenuTextField>();
   field.caption = caption;
   field.read = read;
   field.apply = apply;
   field.report = report;
   field.maxLength = Mathf.Clamp(maxLength, 1, 64);
   field.Show();
  }

  public void OnPointerClick(PointerEventData eventData)
  {
   // The click that opens the field must not also submit it.
   if (Time.unscaledTime < nextClick) return;
   nextClick = Time.unscaledTime + .2f;
   if (editing) Commit();
   else Begin();
  }

  private void Begin()
  {
   buffer = read != null ? read() : string.Empty;
   editing = true;
   Editing = true;
   var keyboard = Keyboard.current;
   if (keyboard != null) keyboard.onTextInput += Type;
   Show();
   Say("Type a name, then press Enter. Escape cancels.");
  }

  private void Commit()
  {
   var value = (buffer ?? string.Empty).Trim();
   if (value.Length == 0)
   {
    Say("A name is required. Escape cancels.");
    return;
   }
   if (apply != null && apply(value))
   {
    Finish();
    Say("Set to " + (read != null ? read() : value) + ".");
    return;
   }
   Say("That value could not be applied.");
  }

  private void Cancel()
  {
   Finish();
   Say("Name unchanged.");
  }

  private void Finish()
  {
   editing = false;
   Editing = false;
   var keyboard = Keyboard.current;
   if (keyboard != null) keyboard.onTextInput -= Type;
   Show();
   ModMenuPlugin.RequestRefresh();
  }

  private void Type(char character)
  {
   if (!editing || char.IsControl(character)) return;
   if (buffer.Length >= maxLength) return;
   buffer += character;
   Show();
  }

  private void Update()
  {
   if (!editing) return;
   var keyboard = Keyboard.current;
   if (keyboard == null) return;
   if (keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
   if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) { Commit(); return; }
   if (keyboard.backspaceKey.wasPressedThisFrame)
   {
    if (buffer.Length > 0) buffer = buffer.Substring(0, buffer.Length - 1);
    Show();
    return;
   }
   // TMP_InputField's own paste path needs the legacy input manager, so the
   // clipboard is read here instead, on the keys people actually press.
   if ((keyboard.ctrlKey.isPressed || keyboard.leftCtrlKey.isPressed) && keyboard.vKey.wasPressedThisFrame)
    Paste();
  }

  private void Paste()
  {
   string clipboard;
   try { clipboard = GUIUtility.systemCopyBuffer; }
   catch { clipboard = null; }
   if (string.IsNullOrWhiteSpace(clipboard))
   {
    Say("Clipboard is empty.");
    return;
   }
   var value = clipboard.Replace('\r', ' ').Replace('\n', ' ').Trim();
   if (value.Length > maxLength) value = value.Substring(0, maxLength);
   buffer = value;
   Show();
  }

  private void Show()
  {
   if (caption == null) return;
   if (editing)
   {
    caption.text = buffer + "|";
    caption.color = ModMenuTheme.Gold;
    return;
   }
   caption.text = read != null ? read() : string.Empty;
   caption.color = ModMenuTheme.Text;
  }

  private void Say(string message)
  {
   if (report != null) report(message);
  }

  private void OnDestroy()
  {
   if (!editing) return;
   editing = false;
   Editing = false;
   var keyboard = Keyboard.current;
   if (keyboard != null) keyboard.onTextInput -= Type;
  }
 }
}
