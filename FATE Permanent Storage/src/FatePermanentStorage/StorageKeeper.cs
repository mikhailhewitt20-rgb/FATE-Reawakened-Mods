using System;
using System.Collections.Generic;
using Cleverous.VaultInventory;
using Tableflip;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // The room's only occupant. It is deliberately not a real town NPC: the visual is a clone with
 // every gameplay script disabled, so it cannot wander, talk over quests, be attacked, or save
 // state. It exists to explain the Mimic body rule and to start placement.
 public sealed class StorageKeeper : InteractableSceneObject
 {
  StorageRoomManager manager;
  public TextMeshPro Nameplate;

  public void Init(StorageRoomManager owner, string displayName)
  {
   manager = owner;
   SetName(displayName);
   InteractRange = 3f;
   InteractionPriority = 6;
  }

  public void SetName(string displayName)
  {
   string clean = string.IsNullOrWhiteSpace(displayName) ? "Storage Keeper" : displayName.Trim();
   InteractableName = clean;
   if (Nameplate == null) return;
   Nameplate.text = clean;
   var rect = Nameplate.rectTransform;
   if (rect != null)
   {
    rect.sizeDelta = new Vector2(Mathf.Max(3.2f, clean.Length * 0.18f + 0.6f), 0.42f);
   }
  }

  public override void Interact(Character interactor = null)
  {
   if (manager != null)
   {
    manager.TalkToKeeper();
   }
  }
 }

 // Every NPC model the town has loaded, plus the party and merchant prefabs. This is the
 // list behind the Storage Keeper model slider: indices are stable for the session and the
 // chosen model is stored by name in the config, so a scene reload can re-find it.
 internal static class StorageKeeperOptions
 {
  public const string DefaultModelName = "DreyaTheHealer";
  static readonly List<GameObject> prefabs = new List<GameObject>();
  static readonly List<string> names = new List<string>();
  static bool built;
  static bool loggedModels;
  static bool healerLookupDone;
  static bool extrasAdded;
  static float nextModelScan;
  static Dictionary<string, GameObject> resourceObjects;
  const float ModelScanInterval = 15f;

  public static int Count
  {
   get { return prefabs.Count; }
  }

  public static int Clamp(int index)
  {
   if (prefabs.Count == 0) return 0;
   return Mathf.Clamp(index, 0, prefabs.Count - 1);
  }

  public static string Name(int index)
  {
   if (prefabs.Count == 0) return "none";
   return names[Mathf.Clamp(index, 0, names.Count - 1)];
  }

  public static int IndexOf(string name)
  {
   return FindIndex(name);
  }

  static int FindIndex(string name)
  {
   if (string.IsNullOrWhiteSpace(name)) return -1;
   string wanted = NormalizedName(name);
   for (int i = 0; i < names.Count; i++)
   {
    if (NormalizedName(names[i]) == wanted) return i;
   }
   return -1;
  }

  // The config stores the character name, not only a list index, so the Keeper keeps the
  // same model when the town loads late or the model list gains or loses an NPC.
  public static int Resolve(string name, int fallback)
  {
   int byName = IndexOf(name);
   return byName >= 0 ? byName : Clamp(fallback);
  }

  public static GameObject Prefab(int index)
  {
   if (prefabs.Count == 0) return null;
   GameObject prefab = prefabs[Mathf.Clamp(index, 0, prefabs.Count - 1)];
   // A scene reload destroys the NPC instances the list was built from. Rebuild once so
   // the Keeper does not silently fall back to a different model after returning to town.
   if (prefab == null)
   {
    built = false;
    nextModelScan = 0f;
    EnsureModels();
    if (prefabs.Count == 0) return null;
    prefab = prefabs[Mathf.Clamp(index, 0, prefabs.Count - 1)];
   }
   return prefab;
  }

  public static void EnsureModels()
  {
   // Keep discovery out of the Mod Menu's getter path. It asks for slider values
   // every frame; a Unity object scan there caused visible stalls while the menu
   // listed settings.
   if (built) return;
   if (Time.unscaledTime < nextModelScan) return;
   nextModelScan = Time.unscaledTime + ModelScanInterval;
   prefabs.Clear();
   names.Clear();
   try
   {
    // Scene NPCs first, so the default model is one the player already sees in town
    // rather than an arbitrary prefab asset from the project.
    AddSceneNpcs();
    var found = Resources.FindObjectsOfTypeAll<NPC>();
    foreach (var npc in found)
    {
     if (npc == null || npc.gameObject == null || npc.gameObject.scene.IsValid()) continue;
     if (npc.GetComponentInParent<StorageKeeper>() != null) continue;
     Add(npc.gameObject, NpcLabel(npc));
    }
    AddHealerFallback();
    AddExtraModels();
    if (SingletonBehaviour<FateDB>.InstanceExists())
    {
     var database = SingletonBehaviour<FateDB>.Instance;
     Add(database.partyMemberPrefab, "Party Member");
     if (database.merchantPrefabs != null)
     {
      foreach (var merchant in database.merchantPrefabs)
      {
       if (merchant != null) Add(merchant.merchantPrefab, merchant.merchantPrefab != null ? merchant.merchantPrefab.name : null);
      }
     }
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not build the Storage Keeper model list: " + ex.Message);
   }
   // If the town has not loaded yet, leave the list open so the next call can find the NPCs.
    built = prefabs.Count > 0;
   if (built && !loggedModels)
   {
    loggedModels = true;
    PermanentStoragePlugin.Log.LogInfo("Storage Keeper models: " + prefabs.Count + " (" + string.Join(", ", names.ToArray()) + ").");
   }
  }

  static void AddSceneNpcs()
  {
   try
   {
    foreach (var npc in Resources.FindObjectsOfTypeAll<NPC>())
    {
     if (npc == null || npc.gameObject == null || !npc.gameObject.scene.IsValid()) continue;
     if (npc.GetComponentInParent<StorageKeeper>() != null) continue;
     Add(npc.gameObject, NpcLabel(npc));
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not add scene NPC models to the Keeper list: " + ex.Message);
   }
  }

  // The healer prefab is a Resources asset rather than a FateDB field, and the town may
  // not have instantiated her when the menu is first built. Load the prefab directly so
  // the default model is still Dreya instead of falling back to the first merchant.
  static void AddHealerFallback()
  {
   if (FindIndex(DefaultModelName) >= 0)
   {
    healerLookupDone = true;
    return;
   }
    if (healerLookupDone) return;
   try
   {
     var loaded = LoadNpcAsset("DreyatheHealer");
    if (loaded != null && loaded.GetComponentInChildren<Renderer>(true) != null)
    {
     Add(loaded, "Dreya the Healer");
     healerLookupDone = FindIndex(DefaultModelName) >= 0;
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not load the Dreya the Healer model: " + ex.Message);
   }
  }

  struct ExtraModel
  {
   public string Asset;
   public string Label;
   public ExtraModel(string asset, string label)
   {
    Asset = asset;
    Label = label;
   }
  }

  // The scene only keeps the current town's NPCs alive, and several women are Resources
  // prefabs rather than FateDB merchant entries. Load the useful ones explicitly so the
  // model slider includes the healer, magic sellers and the other female characters.
  static readonly ExtraModel[] ExtraModels =
  {
   new ExtraModel("DreyatheHealer", "Dreya the Healer"),
   new ExtraModel("AsiyatheHealer", "Asiya the Healer"),
   new ExtraModel("JintheSeer", "Jinth the Seer"),
   new ExtraModel("KiratheJeweler", "Kira the Jeweler"),
   new ExtraModel("HikkotheSalesman", "Hikko the Salesman"),
   new ExtraModel("SakinathePriestess", "Sakina the Priestess"),
   new ExtraModel("MadameYaga", "Madame Yaga"),
   new ExtraModel("AmiliatheCunning", "Amilia the Cunning"),
   new ExtraModel("Berova", "Berova"),
   new ExtraModel("Calliope", "Calliope"),
   new ExtraModel("Ariadne", "Ariadne"),
   new ExtraModel("Selene", "Selene"),
   new ExtraModel("Antheia", "Antheia"),
   new ExtraModel("Hecate", "Hecate"),
   new ExtraModel("Iris", "Iris"),
   new ExtraModel("Helene", "Helene"),
   new ExtraModel("Mandisa", "Mandisa"),
   new ExtraModel("Sharifa", "Sharifa"),
   new ExtraModel("Shezequa", "Shezequa"),
   new ExtraModel("Shantag", "Shantag"),
   new ExtraModel("Akilah", "Akilah"),
   new ExtraModel("Alethe", "Alethe"),
   new ExtraModel("NatarktheEnchanter", "Natark the Enchanter"),
   new ExtraModel("CrythostheEnchanter", "Crythos the Enchanter"),
   new ExtraModel("EquintartheSage", "Equintar the Sage"),
   new ExtraModel("ShaztheIdentifier", "Shaz the Identifier")
  };

  static void AddExtraModels()
  {
    if (extrasAdded) return;
   bool allFound = true;
   foreach (var model in ExtraModels)
   {
    if (FindIndex(model.Label) >= 0) continue;
    GameObject prefab = LoadNpcAsset(model.Asset);
    if (prefab != null && prefab.GetComponentInChildren<Renderer>(true) != null)
    {
     Add(prefab, model.Label);
    }
    if (FindIndex(model.Label) < 0) allFound = false;
   }
   extrasAdded = allFound;
  }

  static GameObject LoadNpcAsset(string asset)
  {
   string[] paths = { "NPC_" + asset, asset, "NPCs/NPC_" + asset, "NPCs/" + asset };
   foreach (string path in paths)
   {
    try
    {
     var loaded = Resources.Load<GameObject>(path);
     if (loaded != null && loaded.GetComponentInChildren<Renderer>(true) != null) return loaded;
    }
    catch (Exception)
    {
     // Try the next known Resources path.
    }
   }
    try
    {
     EnsureResourceObjectIndex();
     string wanted = NormalizedName(asset);
     if (resourceObjects.TryGetValue(wanted, out var exact) && exact != null &&
         exact.GetComponentInChildren<Renderer>(true) != null) return exact;
     foreach (var pair in resourceObjects)
     {
      if ((pair.Key.StartsWith(wanted, StringComparison.Ordinal) || pair.Key.Contains(wanted)) &&
          pair.Value != null && pair.Value.GetComponentInChildren<Renderer>(true) != null)
      {
       return pair.Value;
      }
     }
    }
   catch (Exception)
   {
    // A partially loaded Resources bundle must not stop the other models.
   }
   return null;
  }

  static void EnsureResourceObjectIndex()
  {
   if (resourceObjects != null) return;
   var indexed = new Dictionary<string, GameObject>(StringComparer.Ordinal);
   foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
   {
    if (go == null) continue;
    string key = NormalizedName(go.name);
    if (key.Length > 0 && !indexed.ContainsKey(key)) indexed.Add(key, go);
   }
   resourceObjects = indexed;
  }

  static string NormalizedName(string name)
  {
   if (string.IsNullOrWhiteSpace(name)) return string.Empty;
   var builder = new System.Text.StringBuilder(name.Length);
   foreach (char c in name)
   {
    if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
   }
   string value = builder.ToString();
   if (value.StartsWith("npc", StringComparison.Ordinal)) value = value.Substring(3);
   if (value.StartsWith("p", StringComparison.Ordinal)) value = value.Substring(1);
   return value;
  }

  static void Add(GameObject prefab, string label)
  {
   if (prefab == null) return;
   if (prefab.GetComponentInParent<StorageKeeper>() != null) return;
   string name = CleanName(label, prefab.name);
   if (name.IndexOf("StorageKeeper", StringComparison.OrdinalIgnoreCase) >= 0) return;
   if (prefab.GetComponentInChildren<Renderer>(true) == null) return;
   float size = RendererSize(prefab);
   if (size > 6f) return;
   foreach (var existing in names)
   {
    if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return;
   }
   prefabs.Add(prefab);
   names.Add(name);
  }

  static string NpcLabel(NPC npc)
  {
   if (npc != null)
   {
    var voice = npc.GetComponent<NpcVoiceLines>();
    if (voice != null && voice.thisNpc == NpcGreetings.DreyatheHealer) return "Dreya the Healer";
   }
   string name = npc != null ? npc.CharacterName : null;
   if (string.IsNullOrWhiteSpace(name) && npc != null && npc.gameObject != null) name = npc.gameObject.name;
   return name;
  }

  static string CleanName(string label, string fallback)
  {
   string value = string.IsNullOrWhiteSpace(label) ? fallback : label;
   if (string.IsNullOrWhiteSpace(value)) value = "NPC";
   value = value.Replace("(Clone)", string.Empty).Trim();
   return value.Length == 0 ? "NPC" : value;
  }

  static float RendererSize(GameObject go)
  {
   var renderers = go.GetComponentsInChildren<Renderer>(true);
   if (renderers == null || renderers.Length == 0) return 0f;
   Bounds bounds = renderers[0].bounds;
   for (int i = 1; i < renderers.Length; i++)
   {
    bounds.Encapsulate(renderers[i].bounds);
   }
   return bounds.size.magnitude;
  }
 }

 internal static class StorageKeeperFactory
 {
  public static StorageKeeper Create(StorageRoomManager manager, Vector3 position,
   Quaternion rotation, int layer, Transform parent, string displayName, int modelIndex)
  {
   GameObject go = BuildVisual(position, rotation, layer, parent, modelIndex);
   go.name = "FATE_StorageKeeper";
   var box = go.AddComponent<BoxCollider>();
   // Solid rather than a trigger: the interaction scan can be configured not to hit triggers,
   // and an NPC standing still should block the player anyway.
   box.isTrigger = false;
   box.center = new Vector3(0f, 1.1f, 0f);
   box.size = new Vector3(1.2f, 2.2f, 1.2f);
   var keeper = go.AddComponent<StorageKeeper>();
   keeper.Init(manager, displayName);
   // Activate before measuring so renderer bounds and the nameplate anchor are real, then snap
   // the clone's feet to the room floor in case its pivot is not at the feet.
   go.SetActive(true);
   DropToFloor(go, position.y);
   keeper.Nameplate = StorageChestFactory.AddNameplate(go, string.IsNullOrWhiteSpace(displayName) ? "Storage Keeper" : displayName.Trim());
   PermanentStoragePlugin.Log.LogInfo("Storage Keeper created at " + go.transform.position + " using model " + modelIndex + " (" + StorageKeeperOptions.Name(modelIndex) + ").");
   return keeper;
  }

  static GameObject BuildVisual(Vector3 position, Quaternion rotation, int layer, Transform parent, int modelIndex)
  {
   GameObject template = StorageKeeperOptions.Prefab(modelIndex);
   if (template == null && StorageKeeperOptions.Count == 0) template = FindTemplate();
   if (template != null)
   {
    PermanentStoragePlugin.Log.LogInfo("Storage Keeper visual source: " + template.name + " for option " + modelIndex + ".");
   }
   GameObject go = null;
   if (template != null)
   {
    try
    {
     go = Object.Instantiate(template);
     go.SetActive(false);
     Disarm(go);
    }
    catch (Exception ex)
    {
     PermanentStoragePlugin.Log.LogWarning("Could not clone a visual for the Storage Keeper: " + ex.Message);
     if (go != null)
     {
      Object.DestroyImmediate(go);
     }
     go = null;
    }
   }
   if (go == null)
   {
    go = BuildFallbackVisual();
    go.SetActive(false);
   }
   go.transform.SetParent(parent, true);
   go.transform.position = position;
   go.transform.rotation = rotation;
   go.layer = layer;
   return go;
  }

  static GameObject FindTemplate()
  {
   // A town NPC is the most fitting source: it already wears the game's clothes and uses its
   // humanoid rig. The clone's scripts are all disabled before it can take a turn, and the NPC
   // class itself only becomes a town member when the town calls Init, which never happens here.
   foreach (var npc in Object.FindObjectsOfType<NPC>(true))
   {
    if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy)
    {
     continue;
    }
    if (npc.gameObject.name.IndexOf("StorageKeeper", StringComparison.OrdinalIgnoreCase) >= 0)
    {
     continue;
    }
    if (npc.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
    {
     continue;
    }
    if (RendererSize(npc.gameObject) > 6f)
    {
     continue;
    }
    return npc.gameObject;
   }
   if (SingletonBehaviour<FateDB>.InstanceExists())
   {
    GameObject party = SingletonBehaviour<FateDB>.Instance.partyMemberPrefab;
    if (party != null && party.GetComponentInChildren<Renderer>(true) != null)
    {
     return party;
    }
   }
   return null;
  }

  static float RendererSize(GameObject go)
  {
   var renderers = go.GetComponentsInChildren<Renderer>(true);
   if (renderers == null || renderers.Length == 0)
   {
    return 0f;
   }
   Bounds bounds = renderers[0].bounds;
   for (int i = 1; i < renderers.Length; i++)
   {
    bounds.Encapsulate(renderers[i].bounds);
   }
   return bounds.size.magnitude;
  }

  // The clone is decoration. Disable every gameplay script first, then remove the things that
  // make it a character in the world: navigation, physics, hit boxes, floating labels and the
  // original interaction component.
  static void Disarm(GameObject go)
  {
   var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
   foreach (var behaviour in behaviours)
   {
    if (behaviour != null)
    {
     behaviour.enabled = false;
    }
   }
   foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true))
   {
    if (agent != null)
    {
     agent.enabled = false;
    }
   }
   foreach (var animator in go.GetComponentsInChildren<Animator>(true))
   {
    if (animator != null)
    {
     animator.applyRootMotion = false;
    }
   }
   foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
   {
    if (body != null)
    {
     Object.Destroy(body);
    }
   }
   foreach (var collider in go.GetComponentsInChildren<Collider>(true))
   {
    if (collider != null)
    {
     Object.Destroy(collider);
    }
   }
   foreach (var canvas in go.GetComponentsInChildren<Canvas>(true))
   {
    if (canvas == null)
    {
     continue;
    }
    if (canvas.gameObject == go)
    {
     canvas.enabled = false;
    }
    else
    {
     Object.Destroy(canvas.gameObject);
    }
   }
   foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
   {
    if (text != null)
    {
     Object.Destroy(text);
    }
   }
   foreach (var interactable in go.GetComponentsInChildren<InteractableSceneObject>(true))
   {
    if (interactable != null)
    {
     Object.Destroy(interactable);
    }
   }
   // Quest markers, minimap icons, health bars and the town nameplate belong to the original NPC.
   foreach (var child in go.GetComponentsInChildren<Transform>(true))
   {
    if (child == null || child == go.transform)
    {
     continue;
    }
    string name = child.name.ToLowerInvariant();
    if (name.Contains("quest") || name.Contains("marker") || name.Contains("minimap") ||
        name.Contains("healthbar") || name.Contains("nameplate"))
    {
     child.gameObject.SetActive(false);
    }
   }
  }

  static void DropToFloor(GameObject go, float floorY)
  {
   Renderer main = null;
   Bounds bounds = default(Bounds);
   foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
   {
    if (renderer == null || !renderer.gameObject.activeInHierarchy || renderer is TrailRenderer ||
        renderer is LineRenderer || renderer.GetType().Name == "ParticleSystemRenderer")
    {
     continue;
    }
    if (main == null)
    {
     main = renderer;
     bounds = renderer.bounds;
    }
    else
    {
     bounds.Encapsulate(renderer.bounds);
    }
   }
   if (main != null && bounds.size.sqrMagnitude > 0.01f)
   {
    float shift = floorY - bounds.min.y;
    // A renderer whose bounds have not been populated yet reports the world origin; that would
    // throw the keeper hundreds of metres up. Only trust a shift close to the model.
    if (Mathf.Abs(shift) < 6f)
    {
     go.transform.position += Vector3.up * shift;
    }
   }
  }

  static Material robe;
  static Material trim;
  static Material skin;

  static GameObject BuildFallbackVisual()
  {
   var root = new GameObject("StorageKeeperFallback");
   var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
   body.name = "Robe";
   Object.Destroy(body.GetComponent<Collider>());
   body.transform.SetParent(root.transform, false);
   body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
   body.transform.localScale = new Vector3(0.62f, 0.95f, 0.62f);
   SetMaterial(body, ref robe, new Color(0.17f, 0.2f, 0.32f, 1f));
   var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   head.name = "Head";
   Object.Destroy(head.GetComponent<Collider>());
   head.transform.SetParent(root.transform, false);
   head.transform.localPosition = new Vector3(0f, 1.92f, 0f);
   head.transform.localScale = Vector3.one * 0.42f;
   SetMaterial(head, ref skin, new Color(0.72f, 0.56f, 0.42f, 1f));
   var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   hood.name = "Hood";
   Object.Destroy(hood.GetComponent<Collider>());
   hood.transform.SetParent(root.transform, false);
   hood.transform.localPosition = new Vector3(0f, 2.02f, -0.02f);
   hood.transform.localScale = new Vector3(0.56f, 0.5f, 0.56f);
   SetMaterial(hood, ref robe, new Color(0.17f, 0.2f, 0.32f, 1f));
   var trimRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   trimRing.name = "Belt";
   Object.Destroy(trimRing.GetComponent<Collider>());
   trimRing.transform.SetParent(root.transform, false);
   trimRing.transform.localPosition = new Vector3(0f, 1.02f, 0f);
   trimRing.transform.localScale = new Vector3(0.66f, 0.05f, 0.66f);
   SetMaterial(trimRing, ref trim, new Color(0.86f, 0.67f, 0.24f, 1f));
   return root;
  }

  static void SetMaterial(GameObject go, ref Material cache, Color color)
  {
   if (cache == null)
   {
    Shader shader = Shader.Find("Standard");
    if (shader == null) shader = Shader.Find("Diffuse");
    if (shader != null)
    {
     cache = new Material(shader);
     if (cache.HasProperty("_Color"))
     {
      cache.color = color;
     }
    }
   }
   var renderer = go.GetComponent<Renderer>();
   if (renderer != null)
   {
    renderer.sharedMaterial = cache;
   }
  }
 }
}
