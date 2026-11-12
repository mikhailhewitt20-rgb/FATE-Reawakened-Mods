using System;
using System.Collections.Generic;
using Cleverous.VaultInventory;
using Tableflip;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MikhailKillsZombies.FatePermanentStorage
{
 public sealed class StorageChest : InteractableSceneObject
 {
  public StorageChestRecord Record { get; private set; }
  StorageRoomManager manager;
  GameObject lockVisual;

  public void Init(StorageRoomManager owner, StorageChestRecord record)
  {
   manager = owner;
   Record = record;
   InteractRange = 2.4f;
   InteractionPriority = 3;
   RefreshVisual();
  }

  public void RefreshVisual()
  {
   bool unlocked = Record != null && Record.unlocked;
   string chestName = ChestName(Record);
   InteractableName = unlocked ? chestName : chestName + " (Locked)";
   foreach (var label in GetComponentsInChildren<TextMeshPro>(true))
   {
    if (label != null && label.gameObject.name == "StorageChestNameplate")
    {
     label.text = chestName;
     break;
    }
   }
   if (unlocked)
   {
    if (lockVisual != null)
    {
     Object.Destroy(lockVisual);
     lockVisual = null;
    }
   }
   else if (lockVisual == null)
   {
    lockVisual = CreateLock();
   }
  }

  internal static string ChestName(StorageChestRecord record)
  {
   if (record != null && !string.IsNullOrWhiteSpace(record.customName)) return record.customName.Trim();
   return DefaultChestName(record);
  }

  internal static string DefaultChestName(StorageChestRecord record)
  {
   if (record == null || string.IsNullOrEmpty(record.id)) return "Storage Chest";
   int separator = record.id.LastIndexOf('_');
   string number = separator >= 0 && separator + 1 < record.id.Length
    ? record.id.Substring(separator + 1)
    : record.id;
   int index;
   return int.TryParse(number, out index) ? "Chest " + index.ToString() : "Storage Chest";
  }

  GameObject CreateLock()
  {
   var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
   go.name = "Locked";
   var collider = go.GetComponent<Collider>();
   if (collider != null)
   {
    Object.Destroy(collider);
   }
   go.transform.SetParent(transform, false);
   go.transform.localPosition = new Vector3(0f, 0.62f, 0.52f);
   go.transform.localScale = new Vector3(0.28f, 0.38f, 0.1f);
   var renderer = go.GetComponent<Renderer>();
   if (renderer != null)
   {
    renderer.sharedMaterial = GoldMaterial();
   }
   return go;
  }

  public override void Interact(Character interactor = null)
  {
   if (manager != null && manager.TryBeginChestReposition(this))
   {
    return;
   }
   PermanentStoragePlugin.Log.LogInfo("Storage chest interaction selected: " + ChestName(Record) + ".");
   if (Record == null || manager == null)
   {
    return;
   }
   if (!Record.unlocked)
   {
    manager.ShowUnlockPrompt(this);
    return;
   }
   manager.OpenStorage(this);
  }

  static Material gold;

  static Material GoldMaterial()
  {
   if (gold != null)
   {
    return gold;
   }
   Shader shader = Shader.Find("Standard");
   if (shader == null)
   {
    shader = Shader.Find("Diffuse");
   }
   if (shader != null)
   {
    gold = new Material(shader);
    if (gold.HasProperty("_Color"))
    {
     gold.color = new Color(0.91f, 0.74f, 0.28f, 1f);
    }
    if (gold.HasProperty("_EmissionColor"))
    {
     gold.EnableKeyword("_EMISSION");
     gold.SetColor("_EmissionColor", new Color(0.25f, 0.18f, 0.03f));
    }
   }
   return gold;
  }
 }

 // The stock chest prefab's TMP label is disabled when stripping its vanilla stash behavior.
 // Add a clean label of our own and billboard it toward the camera like the game's name tags.
 internal sealed class StorageChestNameplate : MonoBehaviour
 {
  void LateUpdate()
  {
   if (Camera.main == null) return;
   Vector3 towardCamera = Camera.main.transform.position - transform.position;
   if (towardCamera.sqrMagnitude > 0.0001f)
   {
    // 3D TMP text renders its front along local -Z in this game; point that face at the camera.
    transform.rotation = Quaternion.LookRotation(-towardCamera, Vector3.up);
   }
  }
 }

 public static class StorageChestFactory
 {
  public static StorageChest Create(StorageRoomManager manager, StorageChestRecord record,
   Vector3 position, Quaternion rotation, int layer, Transform parent)
  {
   GameObject go = BuildChestVisual("FATE_StorageChest_" + record.id, position, rotation, layer, parent);
   AddNameplate(go, StorageChest.ChestName(record));
   AddBox(go, new Vector3(1.2f, 1.1f, 0.9f), new Vector3(0f, 0.55f, 0f));
   var chest = go.AddComponent<StorageChest>();
   chest.Init(manager, record);
   go.SetActive(true);
   return chest;
  }

  // A translucent clone of the chest used while the player is choosing a spot. It carries no
  // collider and no StorageChest component, so it cannot be opened or selected.
  public static GameObject CreatePlacementPreview(Transform parent, Vector3 position, int layer)
  {
   GameObject go = BuildChestVisual("FATE_StorageChestPreview", position, Quaternion.identity, layer, parent);
   SetPreviewValid(go, true);
   go.SetActive(true);
   return go;
  }

  public static void SetPreviewValid(GameObject preview, bool valid)
  {
   if (preview == null) return;
   Material material = GhostMaterial(valid);
   if (material == null) return;
   foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
   {
    if (renderer != null)
    {
     renderer.sharedMaterial = material;
    }
   }
  }

  static Material ghostValid;
  static Material ghostInvalid;

  static Material GhostMaterial(bool valid)
  {
   if (valid && ghostValid != null) return ghostValid;
   if (!valid && ghostInvalid != null) return ghostInvalid;
   Shader shader = Shader.Find("Standard");
   if (shader == null) shader = Shader.Find("Sprites/Default");
   if (shader == null) return null;
   var material = new Material(shader);
   if (material.HasProperty("_Color"))
   {
    material.color = valid ? new Color(0.28f, 1f, 0.45f, 0.48f) : new Color(1f, 0.32f, 0.26f, 0.48f);
   }
   if (shader.name == "Standard")
   {
    material.SetFloat("_Mode", 3f);
    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
    material.SetInt("_ZWrite", 0);
    material.DisableKeyword("_ALPHATEST_ON");
    material.EnableKeyword("_ALPHABLEND_ON");
    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    material.renderQueue = 3000;
   }
   if (valid) ghostValid = material;
   else ghostInvalid = material;
   return material;
  }

  static TMP_FontAsset nameplateFont;

  internal static TextMeshPro AddNameplate(GameObject target, string text)
  {
   if (target == null) return null;
   if (nameplateFont == null) nameplateFont = TMP_Settings.defaultFontAsset;
   var labelObject = new GameObject("StorageChestNameplate");
   labelObject.transform.SetParent(target.transform, true);
   Bounds bounds = default(Bounds);
   bool found = false;
   foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
   {
    if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
    if (renderer is TrailRenderer || renderer is LineRenderer || renderer.GetType().Name == "ParticleSystemRenderer") continue;
    if (!found) { bounds = renderer.bounds; found = true; }
    else bounds.Encapsulate(renderer.bounds);
   }
   Vector3 anchor = found ? new Vector3(bounds.center.x, bounds.max.y + 0.32f, bounds.center.z)
    : target.transform.position + Vector3.up * 1.35f;
   labelObject.transform.position = anchor;
   var label = labelObject.AddComponent<TextMeshPro>();
   label.text = text;
   label.alignment = TextAlignmentOptions.Center;
   label.fontSize = 2.5f;
   label.fontStyle = FontStyles.Bold;
   if (nameplateFont != null) label.font = nameplateFont;
   label.textWrappingMode = TextWrappingModes.NoWrap;
   label.overflowMode = TextOverflowModes.Overflow;
   label.color = new Color(1f, 0.91f, 0.68f, 1f);
   // TMP's outline setter dereferences its generated font material. Some game startup paths
   // create this label before that material exists, so the outline is an optional enhancement.
   if (label.font != null && label.fontMaterial != null)
   {
    label.outlineColor = new Color(0.12f, 0.075f, 0.035f, 1f);
    label.outlineWidth = 0.18f;
   }
   label.rectTransform.sizeDelta = new Vector2(3.2f, 0.42f);
   var mesh = label.GetComponent<MeshRenderer>();
   if (mesh != null)
   {
    mesh.sortingOrder = 20;
    mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    mesh.receiveShadows = false;
   }
   var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
   frame.name = "StorageChestNameplateFrame";
   Object.Destroy(frame.GetComponent<Collider>());
   frame.transform.SetParent(labelObject.transform, false);
   frame.transform.localPosition = new Vector3(0f, 0f, 0.065f);
   frame.transform.localRotation = Quaternion.identity;
   frame.transform.localScale = new Vector3(2.4f, 0.56f, 0.035f);
   var frameRenderer = frame.GetComponent<Renderer>();
   if (frameRenderer != null)
   {
    frameRenderer.sharedMaterial = NameplateGoldMaterial();
    frameRenderer.sortingOrder = 18;
    frameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    frameRenderer.receiveShadows = false;
   }
   var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
   banner.name = "StorageChestNameplateBanner";
   Object.Destroy(banner.GetComponent<Collider>());
   banner.transform.SetParent(labelObject.transform, false);
   banner.transform.localPosition = new Vector3(0f, 0f, 0.02f);
   banner.transform.localRotation = Quaternion.identity;
   banner.transform.localScale = new Vector3(2.25f, 0.42f, 0.035f);
   var bannerRenderer = banner.GetComponent<Renderer>();
   if (bannerRenderer != null)
   {
    bannerRenderer.sharedMaterial = NameplateBannerMaterial();
    bannerRenderer.sortingOrder = 19;
    bannerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    bannerRenderer.receiveShadows = false;
   }
   labelObject.AddComponent<StorageChestNameplate>();
   return label;
  }

  static Material nameplateBanner;
  static Material nameplateGold;

  static Material NameplateGoldMaterial()
  {
   if (nameplateGold != null) return nameplateGold;
   Shader shader = Shader.Find("Unlit/Color");
   if (shader == null) shader = Shader.Find("Sprites/Default");
   if (shader == null) shader = Shader.Find("Standard");
   if (shader != null)
   {
    nameplateGold = new Material(shader);
    if (nameplateGold.HasProperty("_Color"))
     nameplateGold.color = new Color(0.57f, 0.36f, 0.12f, 1f);
   }
   return nameplateGold;
  }

  static Material NameplateBannerMaterial()
  {
   if (nameplateBanner != null) return nameplateBanner;
   Shader shader = Shader.Find("Unlit/Color");
   if (shader == null) shader = Shader.Find("Sprites/Default");
   if (shader == null) shader = Shader.Find("Standard");
   if (shader != null)
   {
    nameplateBanner = new Material(shader);
    if (nameplateBanner.HasProperty("_Color"))
     nameplateBanner.color = new Color(0.09f, 0.055f, 0.028f, 0.94f);
   }
   return nameplateBanner;
  }

  // The town entrance deliberately copies the game's own dungeon entrance object instead of
  // inventing a collider/layer setup. That object is already configured for the player's
  // InteractableSelector, so cloning it and replacing only the scene-change component keeps
  // the stock interaction behaviour. The original is stripped before our component is added.
  public static StorageRoomDoor CreateEntrance(StorageRoomManager manager, Vector3 position,
   Quaternion rotation, int fallbackLayer, Transform parent)
  {
   GameObject go = null;
   try
   {
    GameObject template = FindEntranceTemplate();
    if (template != null)
    {
     go = Object.Instantiate(template);
     go.name = "FATE_StorageEntrance";
     go.SetActive(false);
     StripEntranceComponents(go);
     go.transform.SetParent(parent, true);
     go.transform.position = position;
     go.transform.rotation = rotation;
     // The clone must be on the layer the interaction scan uses, and only the root should
     // carry the hit box: the template's children can carry dungeon-only colliders.
     go.layer = fallbackLayer;
     foreach (var childCollider in go.GetComponentsInChildren<Collider>(true))
     {
      if (childCollider != null && childCollider.gameObject != go)
      {
       childCollider.enabled = false;
      }
     }
     var rootCollider = go.GetComponent<Collider>();
     if (rootCollider == null)
     {
      AddEntranceCollider(go);
     }
     else
     {
      rootCollider.enabled = true;
     }
     PermanentStoragePlugin.Log.LogInfo("Using the game's " + template.name + " as the storage entrance template on layer " + go.layer + ".");
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogError("Could not copy the game's dungeon entrance: " + ex);
    if (go != null)
    {
     Object.DestroyImmediate(go);
    }
    go = null;
   }
   if (go == null)
   {
    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = "FATE_StorageEntrance";
    go.transform.localScale = new Vector3(1.4f, 1.2f, 1.0f);
    go.layer = fallbackLayer;
    go.transform.SetParent(parent, true);
    go.transform.position = position + Vector3.up * 0.6f;
    go.transform.rotation = rotation;
    var fallbackRenderer = go.GetComponent<Renderer>();
    if (fallbackRenderer != null)
    {
     fallbackRenderer.sharedMaterial = FallbackWood();
    }
   }
   var entrance = go.AddComponent<StorageRoomDoor>();
   entrance.Init(manager);
   AddEntranceExtras(go);
   go.SetActive(true);
   return entrance;
  }

  static GameObject FindEntranceTemplate()
  {
   GameObject inactive = null;
   foreach (var door in Object.FindObjectsOfType<EnterDungeon>(true))
   {
    if (door == null)
    {
     continue;
    }
    if (door.gameObject.activeInHierarchy)
    {
     return door.gameObject;
    }
    if (inactive == null)
    {
     inactive = door.gameObject;
    }
   }
   foreach (var portal in Object.FindObjectsOfType<EnterPortal>(true))
   {
    if (portal == null)
    {
     continue;
    }
    if (portal.gameObject.activeInHierarchy)
    {
     return portal.gameObject;
    }
    if (inactive == null)
    {
     inactive = portal.gameObject;
    }
   }
   if (inactive != null)
   {
    return inactive;
   }
   if (SingletonBehaviour<FateDB>.InstanceExists() &&
       SingletonBehaviour<FateDB>.Instance.portalPrefab != null)
   {
    return SingletonBehaviour<FateDB>.Instance.portalPrefab;
   }
   return null;
  }

  static void StripEntranceComponents(GameObject go)
  {
   // Tooltips depend on EnterPortal/EnterDungeon, so remove them first.
   foreach (var tooltip in go.GetComponentsInChildren<InteractableTooltipBase>(true))
   {
    Object.DestroyImmediate(tooltip);
   }
   foreach (var interactable in go.GetComponentsInChildren<InteractableSceneObject>(true))
   {
    Object.DestroyImmediate(interactable);
   }
  }

  static void AddEntranceCollider(GameObject go)
  {
   var box = go.AddComponent<BoxCollider>();
   bool hasBounds = false;
   Bounds bounds = default(Bounds);
   foreach (var renderer in go.GetComponentsInChildren<Renderer>())
   {
    if (renderer is TrailRenderer || renderer is LineRenderer ||
        renderer.GetType().Name == "ParticleSystemRenderer")
    {
     continue;
    }
    if (!hasBounds)
    {
     bounds = renderer.bounds;
     hasBounds = true;
    }
    else
    {
     bounds.Encapsulate(renderer.bounds);
    }
   }
   if (hasBounds && bounds.size.sqrMagnitude > 0.01f)
   {
    // A dungeon portal can be much larger than a town door; clamp the hit box so the prompt stays
    // reachable from the doorway instead of covering the whole house.
    box.center = go.transform.InverseTransformPoint(bounds.center);
    box.size = new Vector3(Mathf.Clamp(bounds.size.x, 1f, 4f), Mathf.Clamp(bounds.size.y, 1.5f, 4f),
     Mathf.Clamp(bounds.size.z, 1f, 4f));
   }
   else
   {
    box.center = new Vector3(0f, 0.9f, 0f);
    box.size = new Vector3(1.5f, 1.8f, 1.2f);
   }
  }

  public static void AddEntranceExtras(GameObject go)
  {
   try
   {
    AddWorldBeacon(go);
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not add the storage entrance beacon: " + ex.Message);
   }
   try
   {
    bool hasIcon = false;
    foreach (var icon in go.GetComponentsInChildren<MiniMapIconUI>(true))
    {
     var existingSprite = icon != null ? icon.GetComponent<SpriteRenderer>() : null;
     if (icon != null && icon.gameObject.activeSelf && existingSprite != null &&
         existingSprite.sprite != null)
     {
      hasIcon = true;
      existingSprite.transform.localScale = Vector3.one * 3.8f;
      break;
     }
    }
    if (!hasIcon)
    {
     AddMinimapMarker(go);
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not add the storage entrance minimap marker: " + ex.Message);
   }
  }

  static void AddWorldBeacon(GameObject go)
  {
   var lightGo = new GameObject("StorageEntranceLight");
   lightGo.transform.SetParent(go.transform, false);
   lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
   var light = lightGo.AddComponent<Light>();
   light.type = LightType.Point;
   light.range = 7f;
   light.intensity = 1.4f;
   light.color = new Color(1f, 0.8f, 0.35f);
   light.shadows = LightShadows.None;

   var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   beam.name = "StorageEntranceBeacon";
   var beamCollider = beam.GetComponent<Collider>();
   if (beamCollider != null)
   {
    Object.Destroy(beamCollider);
   }
   beam.transform.SetParent(go.transform, false);
   beam.transform.localPosition = new Vector3(0f, 3f, 0f);
   beam.transform.localScale = new Vector3(0.09f, 3f, 0.09f);
   var beamRenderer = beam.GetComponent<Renderer>();
   if (beamRenderer != null)
   {
    beamRenderer.sharedMaterial = BeaconMaterial();
    beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    beamRenderer.receiveShadows = false;
   }

   // A floating gold diamond above the beam makes the entrance readable even when the
   // minimap is closed or unavailable in town.
   var diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
   diamond.name = "StorageEntranceDiamond";
   var diamondCollider = diamond.GetComponent<Collider>();
   if (diamondCollider != null)
   {
    Object.Destroy(diamondCollider);
   }
   diamond.transform.SetParent(go.transform, false);
   diamond.transform.localPosition = new Vector3(0f, 2.6f, 0f);
   diamond.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
   diamond.transform.rotation = Quaternion.Euler(45f, 45f, 45f);
   var diamondRenderer = diamond.GetComponent<Renderer>();
   if (diamondRenderer != null)
   {
    diamondRenderer.sharedMaterial = BeaconMaterial();
    diamondRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    diamondRenderer.receiveShadows = false;
   }
  }

  static void AddMinimapMarker(GameObject go)
  {
   int minimapLayer = LayerMask.NameToLayer("Minimap");
   if (minimapLayer < 0)
   {
    PermanentStoragePlugin.Log.LogWarning("Minimap layer is missing; no minimap marker can be added.");
    return;
   }
   var iconGo = new GameObject("_MiniMapIcon");
   iconGo.layer = minimapLayer;
   iconGo.transform.SetParent(go.transform, false);
   iconGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
   iconGo.transform.localScale = Vector3.one * 3.8f;
   iconGo.transform.rotation = Quaternion.Euler(90f, 0f, 45f);
   var spriteRenderer = iconGo.AddComponent<SpriteRenderer>();
   spriteRenderer.sprite = MarkerSprite();
   spriteRenderer.sortingOrder = 10;
   iconGo.AddComponent<MiniMapIconUI>();
   PermanentStoragePlugin.Log.LogInfo("Storage entrance minimap marker added on layer " + minimapLayer + ".");
  }

  static Sprite markerSprite;

  // The minimap icon is drawn here rather than loaded from the game: a small chest, so the
  // marker reads as "storage" instead of the old abstract diamond.
  static Sprite MarkerSprite()
  {
   if (markerSprite != null)
   {
    return markerSprite;
   }
   const int size = 32;
   var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
   tex.name = "storage_chest_marker";
   tex.filterMode = FilterMode.Point;
   var clear = new Color32(0, 0, 0, 0);
   var outline = new Color32(34, 21, 6, 255);
   var lidWood = new Color32(158, 104, 48, 255);
   var bodyWood = new Color32(104, 63, 26, 255);
   var gold = new Color32(255, 201, 74, 255);
   var goldDark = new Color32(168, 116, 24, 255);
   for (int y = 0; y < size; y++)
   {
    for (int x = 0; x < size; x++)
    {
     tex.SetPixel(x, y, clear);
    }
   }
   // Feet.
   FillRect(tex, 6, 5, 9, 7, outline);
   FillRect(tex, 22, 5, 25, 7, outline);
   // Lid, then the body below it.
   FillRect(tex, 4, 17, 27, 27, outline);
   FillRect(tex, 5, 18, 26, 26, lidWood);
   FillRect(tex, 5, 20, 26, 21, gold);
   FillRect(tex, 4, 9, 27, 17, outline);
   FillRect(tex, 5, 10, 26, 16, bodyWood);
   // Vertical gold bands and the lock plate, so it still reads at minimap zoom.
   FillRect(tex, 8, 10, 9, 26, gold);
   FillRect(tex, 22, 10, 23, 26, gold);
   FillRect(tex, 14, 13, 17, 16, gold);
   FillRect(tex, 15, 13, 16, 14, goldDark);
   tex.Apply(false, false);
   markerSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
   markerSprite.name = "storage_chest_marker";
   return markerSprite;
  }

  static void FillRect(Texture2D tex, int xMin, int yMin, int xMax, int yMax, Color32 color)
  {
   for (int y = yMin; y <= yMax; y++)
   {
    for (int x = xMin; x <= xMax; x++)
    {
     if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
     {
      tex.SetPixel(x, y, color);
     }
    }
   }
  }

  static Material beaconMaterial;

  static Material BeaconMaterial()
  {
   if (beaconMaterial != null)
   {
    return beaconMaterial;
   }
   Shader shader = Shader.Find("Standard");
   if (shader == null)
   {
    shader = Shader.Find("Diffuse");
   }
   if (shader != null)
   {
    beaconMaterial = new Material(shader);
    if (beaconMaterial.HasProperty("_Color"))
    {
     beaconMaterial.color = new Color(1f, 0.78f, 0.25f, 0.55f);
    }
    if (beaconMaterial.HasProperty("_EmissionColor"))
    {
     beaconMaterial.EnableKeyword("_EMISSION");
     beaconMaterial.SetColor("_EmissionColor", new Color(1f, 0.65f, 0.12f));
    }
   }
   return beaconMaterial;
  }

  static GameObject BuildChestVisual(string name, Vector3 position, Quaternion rotation,
   int layer, Transform parent)
  {
   GameObject prefab = FindChestPrefab();
   GameObject go;
   if (prefab != null)
   {
    go = Object.Instantiate(prefab);
    go.SetActive(false);
    StripContainer(go);
   }
   else
   {
    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = "StorageChestFallback";
    go.transform.localScale = new Vector3(1.1f, 1.0f, 0.8f);
    var renderer = go.GetComponent<Renderer>();
    if (renderer != null)
    {
     renderer.sharedMaterial = FallbackWood();
    }
    var old = go.GetComponent<Collider>();
    if (old != null)
    {
     Object.Destroy(old);
    }
   }
   go.name = name;
   go.transform.SetParent(parent, true);
   go.transform.position = position;
   go.transform.rotation = rotation;
   go.layer = layer;

   // The different container prefabs do not all share the same pivot. Drop the visual onto
   // the floor using its renderer bounds so a chest is never half-buried or floating.
   Renderer mainRenderer = null;
   Bounds bounds = default(Bounds);
   foreach (var renderer in go.GetComponentsInChildren<Renderer>())
   {
    if (renderer is TrailRenderer || renderer is LineRenderer ||
        renderer.GetType().Name == "ParticleSystemRenderer")
    {
     continue;
    }
    if (mainRenderer == null)
    {
     mainRenderer = renderer;
     bounds = renderer.bounds;
    }
    else
    {
     bounds.Encapsulate(renderer.bounds);
    }
   }
   if (mainRenderer != null)
   {
    go.transform.position += Vector3.up * (position.y - bounds.min.y);
   }
   return go;
  }

  static void AddBox(GameObject go, Vector3 size, Vector3 center)
  {
   var box = go.AddComponent<BoxCollider>();
   box.size = size;
   box.center = center;
  }

  static GameObject FindChestPrefab()
  {
   if (!SingletonBehaviour<FateDB>.InstanceExists())
   {
    return null;
   }
   var prefabs = SingletonBehaviour<FateDB>.Instance.containerPrefabs;
   if (prefabs == null)
   {
    return null;
   }
   GameObject fallback = null;
   foreach (var prefab in prefabs)
   {
    if (prefab == null)
    {
     continue;
    }
    string name = prefab.name.ToLowerInvariant();
    if (name.Contains("chestsmall"))
    {
     return prefab;
    }
    if (fallback == null && name.Contains("chest"))
    {
     fallback = prefab;
    }
   }
   return fallback;
  }

  static void StripContainer(GameObject instance)
  {
   // The prefab's tooltip has RequireComponent<ContainerInteractable>. Remove dependants first
   // and immediately, or Unity refuses to remove the vanilla interactable and the selector
   // returns it instead of our StorageChest component.
   foreach (var tooltip in instance.GetComponentsInChildren<InteractableTooltipBase>(true))
   {
    if (tooltip != null) Object.DestroyImmediate(tooltip);
   }
   foreach (var canvas in instance.GetComponentsInChildren<InteractableTooltipCanvas>(true))
   {
    if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
   }
   foreach (var component in instance.GetComponentsInChildren<ContainerInteractable>(true))
   {
    if (component != null) Object.DestroyImmediate(component);
   }
   foreach (var component in instance.GetComponentsInChildren<ContainerDestroyable>(true))
   {
    if (component != null) Object.DestroyImmediate(component);
   }
   foreach (var component in instance.GetComponentsInChildren<Stash>(true))
   {
    if (component != null) Object.DestroyImmediate(component);
   }
   foreach (var component in instance.GetComponentsInChildren<Rigidbody>(true))
   {
    if (component != null) Object.DestroyImmediate(component);
   }
   foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
   {
    if (collider != null) Object.DestroyImmediate(collider);
   }
   // The stock container prefab carries a world-space TMP label defaulted to "NEW TEXT".
   // It is not part of the chest visual and must not be copied into the storage room.
   foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
   {
    if (text != null)
    {
     text.gameObject.SetActive(false);
    }
   }
  }

  static Material fallbackWood;

  static Material FallbackWood()
  {
   if (fallbackWood != null)
   {
    return fallbackWood;
   }
   Shader shader = Shader.Find("Standard");
   if (shader == null)
   {
    shader = Shader.Find("Diffuse");
   }
   if (shader != null)
   {
    fallbackWood = new Material(shader);
    if (fallbackWood.HasProperty("_Color"))
    {
     fallbackWood.color = new Color(0.42f, 0.26f, 0.13f, 1f);
    }
   }
   return fallbackWood;
  }
 }
}
