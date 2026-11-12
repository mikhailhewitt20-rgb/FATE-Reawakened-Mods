using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Tableflip;
using UnityEngine;
using UnityEngine.AI;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // The game's items are ScriptableObjects created at build time. A mod cannot add one to the
 // shipped asset list, so this creates a real ItemData at runtime, appends it to FateDB's item
 // array, gives it a runtime sprite and a matching world-drop prefab, and lets the normal item,
 // inventory, pickup and save code do the rest.
 internal static class MimicChestItem
 {
  public const string ItemName = "Mimic Chest";
  public const string RefId = "MimicChest";
  public const string IconName = "mimicchest";

  public static ItemData Data { get; private set; }

  public static void ShowPlacementMessage(string message)
  {
   if (string.IsNullOrWhiteSpace(message)) return;
   try
   {
    var tooltips = UnityEngine.Object.FindObjectsOfType<TooltipUI>(true);
    if (tooltips != null && tooltips.Length > 0 && tooltips[0] != null)
    {
     tooltips[0].ShowTooltip("Chest Placement", message);
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log?.LogWarning("Could not show the chest placement message in the item tooltip panel: " + ex.Message);
   }
  }

  static bool registered;
  static Sprite icon;
  static GameObject worldDropPrefab;
  static readonly object DropRolledMarker = new object();

  // A Mimic only rolls once, even if Die is reached more than once during its death sequence.
  static readonly ConditionalWeakTable<Monster, object> DropRolled =
   new ConditionalWeakTable<Monster, object>();

  public static void EnsureRegistered()
  {
   if (registered && Data != null)
   {
    return;
   }
   if (!SingletonBehaviour<FateDB>.InstanceExists())
   {
    return;
   }
   var db = SingletonBehaviour<FateDB>.Instance;
   var allItemsField = AccessTools.FieldRefAccess<FateDB, ItemData[]>("allItems");
   var versionField = AccessTools.FieldRefAccess<FateDB, int>("allItemsGameVersionIndex");
   ItemData[] items = allItemsField(db);
   if (items == null)
   {
    return;
   }
   foreach (var item in items)
   {
    if (item != null && item.displayName == ItemName)
    {
     Data = item;
     registered = true;
     return;
    }
   }

   ItemData template = FindTemplate(items);
   ItemData data;
   if (template != null)
   {
    data = UnityEngine.Object.Instantiate(template);
   }
   else
   {
    data = ScriptableObject.CreateInstance<ItemData>();
   }
   data.name = "Item_MimicChest";
   data.displayName = ItemName;
   data.refId = RefId;
   data.description = "A strange chest recovered from a Mimic. Bring it to your storage room to unlock an additional storage chest.";
   data.itemType = ItemType.Book;
   data.heroType = HeroType.None;
   data.rarity = 1;
   data.fishingRarity = 0;
   data.minimumDepth = 0;
   data.maximumDepth = 0;
   data.value = 0;
   data.merchantMinimum = 0;
   data.merchantMaximum = 0;
   data.itemTarget = ItemTarget.None;
   data.takeable = true;
   data.autoPickup = false;
   data.forcePickupHold = false;
   data.usable = false;
   data.destructible = false;
   data.uses = -9999;
   data.sockets = 0;
   data.reserved = false;
   data.identified = true;
   data.notPurchasable = true;
   data.notSellable = true;
   data.artifact = false;
   data.expansion = false;
   data.collision = false;
   data.hasShadow = true;
   // The chest is a bulky two-by-two object in the inventory grid, so it reads as furniture
   // rather than as a small trinket and takes four slots.
   data.preCalcSize = new Vector2Int(2, 2);
   data.iconPath = "icons/" + IconName + ".png:0";
   var iconNameField = AccessTools.Field(typeof(ItemData), "iconName");
   if (iconNameField != null)
   {
    iconNameField.SetValue(data, IconName);
   }

   EnsureIconSprite(db);
   EnsureWorldDropPrefab(db);

   var list = new List<ItemData>(items);
   list.Add(data);
   allItemsField(db) = list.ToArray();
   // Force the per-game-version cache to rebuild so the new item is visible everywhere.
   versionField(db) = -1;
   Data = data;
   registered = true;
   PermanentStoragePlugin.Log.LogInfo("Registered Mimic Chest item.");
  }

  static ItemData FindTemplate(ItemData[] items)
  {
   ItemData generic = null;
   foreach (var item in items)
   {
    if (item == null)
    {
     continue;
    }
    if (item.itemType == ItemType.ChestKey)
    {
     return item;
    }
    if (generic == null && item.itemType == ItemType.Book && !string.IsNullOrEmpty(item.iconPath))
    {
     generic = item;
    }
   }
   if (generic != null)
   {
    return generic;
   }
   foreach (var item in items)
   {
    if (item != null && !string.IsNullOrEmpty(item.iconPath))
    {
     return item;
    }
   }
   return null;
  }

  static void EnsureIconSprite(FateDB db)
  {
   if (db.spriteStash != null)
   {
    foreach (var sprite in db.spriteStash)
    {
     if (sprite != null && sprite.name == IconName)
     {
      return;
     }
    }
   }
   if (icon == null)
   {
    icon = BuildIcon();
   }
   var sprites = new List<Sprite>();
   if (db.spriteStash != null)
   {
    sprites.AddRange(db.spriteStash);
   }
   sprites.Add(icon);
   db.spriteStash = sprites.ToArray();
  }

  static Sprite BuildIcon()
  {
   try
   {
    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
     "MikhailKillsZombies.FatePermanentStorage.Assets.mimicchest.png"))
    {
     if (stream != null)
     {
      byte[] png = new byte[stream.Length];
      stream.Read(png, 0, png.Length);
      var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
      if (tex.LoadImage(png, markNonReadable: false))
      {
       tex.name = IconName;
       var importedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 256f);
       importedSprite.name = IconName;
       return importedSprite;
      }
    }
   }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log?.LogWarning("Could not load the embedded Mimic Chest icon: " + ex.Message);
   }

   const int size = 32;
   var fallback = new Texture2D(size, size, TextureFormat.RGBA32, false);
   fallback.name = IconName;
   var clear = new Color32(0, 0, 0, 0);
   var wood = new Color32(112, 70, 34, 255);
   var woodLight = new Color32(148, 96, 48, 255);
   var dark = new Color32(48, 31, 18, 255);
   var gold = new Color32(232, 190, 72, 255);
   for (int y = 0; y < size; y++)
   {
    for (int x = 0; x < size; x++)
    {
     fallback.SetPixel(x, y, clear);
    }
   }
   for (int y = 8; y < 24; y++)
    for (int x = 5; x < 27; x++)
     fallback.SetPixel(x, y, (x == 5 || x == 26 || y == 8 || y == 23) ? dark : wood);
   for (int y = 22; y < 27; y++)
    for (int x = 6; x < 26; x++)
     fallback.SetPixel(x, y, (y == 26) ? dark : woodLight);
   // gold bands and lock
   for (int x = 8; x < 24; x++)
   {
    fallback.SetPixel(x, 18, gold);
   }
   for (int y = 12; y < 22; y++)
   {
    fallback.SetPixel(16, y, gold);
   }
   for (int y = 13; y < 17; y++)
   {
    for (int x = 14; x < 19; x++)
    {
     fallback.SetPixel(x, y, gold);
    }
   }
   fallback.SetPixel(16, 15, dark);
   fallback.Apply(false, false);
   var sprite = Sprite.Create(fallback, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
   sprite.name = IconName;
   return sprite;
  }

  // The Mimic's own body is the world model. The supplied chest art is only the inventory icon;
  // no chest sprite is added to the world drop. RollDrop reparents the dead Mimic under this
  // prefab, so picking the item up destroys the body with it.
  static GameObject EnsureWorldDropPrefab(FateDB db)
  {
   if (db.itemPrefabs != null)
   {
    foreach (var prefab in db.itemPrefabs)
    {
     if (prefab != null && prefab.name.Replace("P_", "").Trim().ToLowerInvariant() == "mimic chest")
     {
      EnsureDropTracking(prefab);
      EnsureMimicCorpse(prefab);
      worldDropPrefab = prefab;
      return prefab;
     }
    }
   }
   var root = new GameObject("P_Mimic Chest");
   root.transform.position = new Vector3(9000f, -9000f, 9000f);
    EnsureDropTracking(root);
   EnsureMimicCorpse(root);
   var list = new List<GameObject>();
   if (db.itemPrefabs != null)
   {
    list.AddRange(db.itemPrefabs);
   }
   list.Add(root);
   db.itemPrefabs = list.ToArray();
   worldDropPrefab = root;
   return root;
  }

  static void EnsureDropTracking(GameObject root)
  {
   if (root == null || root.GetComponent<MimicChestDropVisual>() != null)
   {
    return;
   }
   root.AddComponent<MimicChestDropVisual>();
  }

  // A Mimic Chest dropped from the inventory uses a Mimic model stand-in. The death path replaces
  // it with the actual corpse so the death pose is preserved.
  static void EnsureMimicCorpse(GameObject root)
  {
   if (root == null) return;
   var visual = root.GetComponent<MimicChestDropVisual>();
   if (visual == null) return;
   Transform existing = root.transform.Find("MimicCorpse");
   if (existing != null)
   {
    visual.SetStandInCorpse(existing);
    return;
   }
   GameObject prefab = FindMimicPrefab();
   if (prefab == null)
   {
    PermanentStoragePlugin.Log.LogWarning("No Mimic model was found for the Mimic Chest drop visual.");
    return;
   }
   GameObject body;
   try
   {
    body = UnityEngine.Object.Instantiate(prefab, root.transform, false);
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not clone the Mimic model for the chest drop: " + ex.Message);
    return;
   }
   body.name = "MimicCorpse";
   body.SetActive(false);
   body.transform.localPosition = Vector3.zero;
   body.transform.localRotation = Quaternion.identity;
   DisarmMimicVisual(body);
   body.SetActive(true);
   visual.SetStandInCorpse(body.transform);
   PermanentStoragePlugin.Log.LogInfo("Mimic Chest drop now carries a Mimic corpse visual.");
  }

  static GameObject FindMimicPrefab()
  {
   if (SingletonBehaviour<FateDB>.InstanceExists())
   {
    var db = SingletonBehaviour<FateDB>.Instance;
    if (db.allMonsters != null)
    {
     foreach (var data in db.allMonsters)
     {
      if (data != null && data.family == Family.Mimic && data.modelPrefab != null)
      {
       return data.modelPrefab;
      }
     }
    }
   }
   foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
   {
    if (go == null) continue;
    if (go.name == "Monster_Mimic" || go.name == "P_Mimic") return go;
   }
   return Resources.Load<GameObject>("Monster_Mimic");
  }

  static void DisarmMimicVisual(GameObject body)
  {
   foreach (var behaviour in body.GetComponentsInChildren<MonoBehaviour>(true))
   {
    if (behaviour != null) behaviour.enabled = false;
   }
   foreach (var agent in body.GetComponentsInChildren<NavMeshAgent>(true))
   {
    if (agent != null) agent.enabled = false;
   }
   foreach (var animator in body.GetComponentsInChildren<Animator>(true))
   {
    if (animator != null) animator.applyRootMotion = false;
   }
   foreach (var collider in body.GetComponentsInChildren<Collider>(true))
   {
    if (collider != null) UnityEngine.Object.Destroy(collider);
   }
   foreach (var rigidbody in body.GetComponentsInChildren<Rigidbody>(true))
   {
    if (rigidbody != null) UnityEngine.Object.Destroy(rigidbody);
   }
   foreach (var canvas in body.GetComponentsInChildren<Canvas>(true))
   {
    if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
   }
   foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
   {
    if (renderer != null) renderer.enabled = true;
   }
  }

  internal static GameObject GetWorldDropPrefab()
  {
   if (worldDropPrefab != null)
   {
    return worldDropPrefab;
   }
   if (!SingletonBehaviour<FateDB>.InstanceExists())
   {
    return null;
   }
   return EnsureWorldDropPrefab(SingletonBehaviour<FateDB>.Instance);
  }

  public static bool HasOne()
  {
   var player = Player.PlayerChar;
   if (player == null || player.inventory == null)
   {
    return false;
   }
   foreach (var item in player.inventory)
   {
    if (item != null && (item.refId == RefId || item.itemBaseName == ItemName))
    {
     return true;
    }
   }
   return false;
  }

  public static bool ConsumeOne()
  {
   var player = Player.PlayerChar;
   if (player == null || player.inventory == null)
   {
    return false;
   }
   Item found = null;
   foreach (var item in player.inventory)
   {
    if (item != null && (item.refId == RefId || item.itemBaseName == ItemName))
    {
     found = item;
     break;
    }
   }
   if (found == null)
   {
    return false;
   }
   player.RemoveItemFromInventory(found, RemoveItemReason.Consumed);
   return true;
  }

  public static bool RollDrop(Monster monster)
  {
   // IsMimic() is the game's disguise-state helper and can be true for a monster whose
   // underlying prefab is not actually a Mimic. Require the spawned object identity too, so
   // ordinary enemies such as Devils cannot produce a Mimic Chest/corpse drop.
   if (monster == null || monster.loadedAsDead || !monster.IsMimic() ||
       monster.name.IndexOf("mimic", StringComparison.OrdinalIgnoreCase) < 0)
   {
    return false;
   }
   if (DropRolled.TryGetValue(monster, out _))
   {
    return false;
   }
   DropRolled.Add(monster, DropRolledMarker);
   if (LevelGenerator.currentInstance == null)
   {
    PermanentStoragePlugin.Log.LogWarning("Mimic death hook ran without an active dungeon level; no Mimic Chest was dropped.");
    return false;
   }
   int chance = PermanentStoragePlugin.MimicChestDropChance.Value;
   if (chance <= 0 || UnityEngine.Random.Range(0f, 100f) >= chance)
   {
    PermanentStoragePlugin.Log.LogInfo("Mimic Chest drop roll failed at " + chance + "% chance.");
    return false;
   }
   EnsureRegistered();
   if (Data == null)
   {
    PermanentStoragePlugin.Log.LogWarning("Mimic died but the Mimic Chest item was not registered.");
    return false;
   }
   try
   {
    var item = new Item(Data);
    // Drop in place: the corpse is the visual, so the item must not bounce away from it.
    var dropped = item.Drop(monster.transform.position, skipSoundAndDropTween: true);
    if (dropped == null)
    {
     PermanentStoragePlugin.Log.LogWarning("Mimic Chest drop roll succeeded, but the game did not create a dropped item object.");
     return false;
    }
    var visual = dropped.GetComponentInChildren<MimicChestDropVisual>(true);
    if (visual != null)
    {
     visual.AttachCorpse(monster.transform);
    }
    else
    {
     PermanentStoragePlugin.Log.LogWarning("The Mimic Chest dropped without its corpse indicator prefab; the item is still lootable.");
    }
    PermanentStoragePlugin.Log.LogInfo("Mimic dropped a Mimic Chest on its corpse.");
    return true;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogError("Mimic Chest drop failed after a successful roll: " + ex);
    return false;
   }
  }
 }

 // Item hovers use the game's own stats/tooltip panel. Replace only this item's rows with its
 // instructions, using the same title + description overload the panel uses for other UI tips.
 [HarmonyPatch(typeof(TooltipUI), "ShowTooltip", new Type[] { typeof(Item), typeof(bool), typeof(Character) })]
 internal static class MimicChestTooltipPatch
 {
  [HarmonyPostfix]
  private static void Postfix(TooltipUI __instance, Item targetItem)
  {
   if (targetItem == null || (targetItem.refId != MimicChestItem.RefId && targetItem.itemBaseName != MimicChestItem.ItemName))
   {
    return;
   }
   __instance.ShowTooltip(MimicChestItem.ItemName,
    "Take this Mimic Chest to the Storage Keeper in the Permanent Storage Room.\n\n" +
    "Talk to the Keeper and choose Place chest, then left-click to place it. R rotates it; right-click or Esc cancels. " +
    "The Mimic Chest is consumed when placement succeeds.");
  }
 }

 // The Mimic Chest item prefab carries a Mimic model stand-in for world drops. When a Mimic dies,
 // its actual corpse is re-parented under the dropped item, so picking up the item removes the body.
 internal sealed class MimicChestDropVisual : MonoBehaviour
 {
  Transform corpse;
  Transform standInCorpse;

  public void SetStandInCorpse(Transform body)
  {
   standInCorpse = body;
   if (body != null)
   {
    corpse = body;
   }
  }

  public void AttachCorpse(Transform body)
  {
   if (body == null)
   {
    return;
   }
   if (standInCorpse != null)
   {
    UnityEngine.Object.Destroy(standInCorpse.gameObject);
    standInCorpse = null;
   }
   corpse = body;
   // The corpse is decoration only. Its colliders must not steal the interaction sphere from the
   // dropped item, and its agent and any overhead status canvas are no longer meaningful.
   foreach (var collider in body.GetComponentsInChildren<Collider>(true))
   {
    if (collider != null)
    {
     collider.enabled = false;
    }
   }
   foreach (var agent in body.GetComponentsInChildren<NavMeshAgent>(true))
   {
    if (agent != null)
    {
     agent.enabled = false;
    }
   }
   foreach (var canvas in body.GetComponentsInChildren<Canvas>(true))
   {
    if (canvas != null)
    {
     canvas.enabled = false;
    }
   }
   body.SetParent(transform, false);
   body.localPosition = Vector3.zero;
   body.localRotation = Quaternion.identity;
   PermanentStoragePlugin.Log.LogInfo("Mimic corpse attached to the dropped Mimic Chest.");
  }
 }

 // A postfix is ideal here: the game has already run its ordinary death, gold and loot path,
 // and the special item is added afterwards without replacing any existing drop.
 [HarmonyPatch(typeof(Monster), "Die")]
 internal static class MimicDeathPatch
 {
  [HarmonyPostfix]
  private static void Postfix(Monster __instance)
  {
   try
   {
    MimicChestItem.RollDrop(__instance);
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogError("Mimic death hook failed: " + ex);
   }
  }
 }

 // The item must exist before saved inventories are rebuilt. The AllItems getter is the one
 // funnel every inventory loader goes through, so registering there is both early and safe.
 [HarmonyPatch(typeof(FateDB), "get_AllItems")]
 internal static class FateDbItemsPatch
 {
  [HarmonyPrefix]
  private static void Prefix()
  {
   MimicChestItem.EnsureRegistered();
  }
 }

 // The stock prefab selector maps Book items to P_Book, so merely adding P_Mimic Chest to
 // FateDB.itemPrefabs is not enough. Keep the custom item visually distinct in the world.
 [HarmonyPatch(typeof(Item), "GetItemPrefab")]
 internal static class MimicItemPrefabPatch
 {
  [HarmonyPrefix]
  private static bool Prefix(Item __instance, ref GameObject __result)
  {
   if (__instance == null || (__instance.refId != MimicChestItem.RefId && __instance.itemBaseName != MimicChestItem.ItemName))
   {
    return true;
   }
   GameObject prefab = MimicChestItem.GetWorldDropPrefab();
   if (prefab != null)
   {
    __result = prefab;
    return false;
   }
   return true;
  }
 }
}
