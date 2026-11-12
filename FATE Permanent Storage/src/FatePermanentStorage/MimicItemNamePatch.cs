using HarmonyLib;
using Tableflip;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // The Mimic Chest item is created at runtime, so the shipped localization table has no term
 // for it. I2Loca normally falls back to the term itself; this postfix guarantees a readable
 // name in every language if it returns an empty string instead.
 [HarmonyPatch(typeof(Item), "get_ItemDisplayName")]
 internal static class MimicItemNamePatch
 {
  [HarmonyPostfix]
  private static void Postfix(Item __instance, ref string __result)
  {
   if (__instance != null && __instance.itemBaseName == MimicChestItem.ItemName &&
       string.IsNullOrEmpty(__result))
   {
    __result = MimicChestItem.ItemName;
   }
  }
 }
}
