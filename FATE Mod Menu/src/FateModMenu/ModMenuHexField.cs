using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZombieGhost.ModMenu
{
 // A colour code you can read, type into and paste into. The keyboard is read from
 // the Input System because the game never enables the legacy input manager, and
 // TMP_InputField depends on it: this keeps the field usable in FATE.
 internal sealed class ModMenuHexField : MonoBehaviour, IPointerClickHandler
 {
  // While a field is open it owns Escape and the list must not rebuild under it.
  internal static bool Editing { get; private set; }

  private TextMeshProUGUI caption;
  private Func<string> read;
  private Func<string, bool> apply;
  private Action<string> report;
  private bool editing;
  private string buffer = string.Empty;
  private float nextClick;

  internal static void Attach(RectTransform host, TextMeshProUGUI caption, Func<string> read,
   Func<string, bool> apply, Action<string> report)
  {
   var field = host.gameObject.AddComponent<ModMenuHexField>();
   field.caption = caption;
   field.read = read;
   field.apply = apply;
   field.report = report;
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
   buffer = read != null ? read() : "#FFFFFF";
   editing = true;
   Editing = true;
   var keyboard = Keyboard.current;
   if (keyboard != null) keyboard.onTextInput += Type;
   Show();
   Say("Type a code or press Ctrl+V, then Enter. Escape cancels.");
  }

  private void Commit()
  {
   var value = Clean(buffer);
   Finish();
   if (value == null)
   {
    Say("Not a colour code: " + (string.IsNullOrEmpty(buffer) ? "nothing" : buffer) + ".");
    return;
   }
   if (apply != null && apply(value)) Say("Colour set to " + (read != null ? read() : value) + ".");
   else Say("That colour could not be applied.");
  }

  private void Cancel()
  {
   Finish();
   Say("Colour unchanged.");
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
   if (!editing) return;
   if (character == '#')
   {
    buffer = "#";
    Show();
    return;
   }
   if (!IsHex(character)) return;
   if (buffer.Length >= 7) return;
   buffer += char.ToLowerInvariant(character);
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
    Say("Clipboard is empty. Copy a code such as " + (read != null ? read() : "#B76063") + ".");
    return;
   }
   var value = Clean(clipboard);
   if (value == null)
   {
    Say("The clipboard does not hold a colour code.");
    return;
   }
   buffer = value;
   Show();
   Commit();
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

  // "#B76063", "B76063" or the short "#B76"; anything else is rejected.
  internal static string Clean(string text)
  {
   if (string.IsNullOrWhiteSpace(text)) return null;
   var value = text.Trim();
   if (value.StartsWith("#")) value = value.Substring(1);
   if (value.Length != 3 && value.Length != 6) return null;
   foreach (var character in value)
    if (!IsHex(character)) return null;
   return "#" + value.ToLowerInvariant();
  }

  private static bool IsHex(char character) =>
   (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F');

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
