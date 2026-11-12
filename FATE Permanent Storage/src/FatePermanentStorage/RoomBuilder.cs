using System;
using System.Collections.Generic;
using Tableflip;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace MikhailKillsZombies.FatePermanentStorage
{
 internal sealed class ChestAnchor
 {
  public Vector3 LocalPosition;
  public Quaternion LocalRotation;
 }

 internal sealed class BuiltRoom
 {
  public GameObject Root;
  public Vector3 Spawn;
  public GameObject ExitDoor;
  public List<ChestAnchor> Chests = new List<ChestAnchor>();
  public NavMeshSurface Surface;
  public Stash Proxy;
 }

 // One interchangeable wall look: a wall set from the dungeon template plus a prefab variant
 // inside that set. The Grove template usually stores several variants per wall type, and each
 // separate wall set is a different material set, so both give the player something to cycle.
 internal struct WallStyle
 {
  public TemplateData Template;
  public int Set;
  public int Variant;
  public string Name;
 }

 // One interchangeable floor look. Floor prefabs are independent of the wall set, so the
 // player can keep a stone floor under a wooden wall theme (or the other way round).
 internal struct FloorStyle
 {
  public TemplateData Template;
  public GameObject Prefab;
  public string Name;
 }

 // Builds the fixed room from the same prefab sets the Grove dungeon generator uses. The room
 // is deliberately not a generated dungeon: no MazeCreation, no seed, no monsters, no loot,
 // and no dungeon save data. The same cells are laid out in the same order every time.

 internal static class RoomBuilder
 {
  public const int Cells = 6;
  public const int CeilingTilePadding = 6;
  const float CeilingHeight = 3.2f;
  const float RoofEdgeOffset = 1.54296875f;
  const float ExitDoorLocalX = 3f;
  const float ExitDoorLocalZ = -1.5f;

  enum Quadrant
  {
   NW,
   NE,
   SW,
   SE
  }

  // Every wall type needed for a rectangular room: the four edge quadrants and the four corners.
  static readonly WallTypes[] QuadrantTypes =
  {
   WallTypes.NWHorizontal, WallTypes.NEHorizontal, WallTypes.SWHorizontal, WallTypes.SEHorizontal,
   WallTypes.NWVertical, WallTypes.NEVertical, WallTypes.SWVertical, WallTypes.SEVertical
  };

  static readonly List<WallStyle> styles = new List<WallStyle>();
  static readonly List<FloorStyle> floorStyles = new List<FloorStyle>();
  static GameObject wallsRoot;
  static GameObject doorwayRoot;
  static Vector3 wallOrigin;
  static float wallTile = 2f;
  static int wallDoorCell;
  static int wallStyleIndex;
  static int floorStyleIndex;
  static GameObject floorRoot;
  static GameObject roofRoot;
  static GameObject ceilingPrefab;
  static float ceilingTop;
  static float ceilingDelta;
  static GameObject roomRoot;
  static Vector3 wallShift;

  static readonly List<CeilingTileMarker> ceilingMarkers = new List<CeilingTileMarker>();
  static readonly List<WallTileMarker> wallMarkers = new List<WallTileMarker>();
  public static int CeilingTileCount => ceilingMarkers.Count;
  public static int WallTileCount => wallMarkers.Count;
  public static Vector3 WallOrigin => wallOrigin;
  public static float WallPlacementHeight => wallOrigin.y + CeilingHeight * 0.5f;
  public static float CeilingPlacementHeight => ceilingTop + ceilingDelta;
  public static IEnumerable<CeilingTileMarker> CeilingMarkers()
  {
   for (int i = ceilingMarkers.Count - 1; i >= 0; i--)
   {
    if (ceilingMarkers[i] == null) ceilingMarkers.RemoveAt(i);
    else yield return ceilingMarkers[i];
   }
  }

  public static IEnumerable<WallTileMarker> WallMarkers()
  {
   for (int i = wallMarkers.Count - 1; i >= 0; i--)
   {
    if (wallMarkers[i] == null) wallMarkers.RemoveAt(i);
    else yield return wallMarkers[i];
   }
  }

  internal static string WallLayoutStyleKey
  {
   get
   {
    if (styles.Count == 0) return "fallback";
    var style = styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)];
    string templateName = style.Template != null ? style.Template.templateName : "unknown";
    return templateName + "|set=" + style.Set + "|variant=" + style.Variant + "|" + style.Name;
   }
  }

  public static WallTileMarker FirstWallMarker()
  {
   foreach (var marker in WallMarkers()) return marker;
   return null;
  }

  // The room's collision shell sits this far inside the floor edge, so the visible walls are
  // aligned to the same line: the walls match the hitbox instead of standing a cell outside it.
  const float WallInset = 0.6f;
  static TemplateData floorTemplate;


  static int placedThisFill;

  public static int WallStyleCount => styles.Count;
  public static int WallStyleIndex => wallStyleIndex;
  public static int FloorStyleCount => floorStyles.Count;
  public static int FloorStyleIndex => floorStyleIndex;

  public static string FloorStyleName
  {
   get
   {
    if (floorStyles.Count == 0)
    {
     return "none";
    }
    return floorStyles[Mathf.Clamp(floorStyleIndex, 0, floorStyles.Count - 1)].Name;
   }
  }

  public static string WallStyleName
  {
   get
   {
    if (styles.Count == 0)
    {
     return "none";
    }
    return styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)].Name;
   }
  }

  public static TemplateData FindGroveTemplate()
  {
   if (!SingletonBehaviour<FateDB>.InstanceExists())
   {
    return null;
   }
   var templates = SingletonBehaviour<FateDB>.Instance.allTemplates;
   if (templates == null)
   {
    return null;
   }
   TemplateData fallback = null;
   foreach (var template in templates)
   {
    if (template == null || template.wallSets == null || template.wallSets.Count == 0)
    {
     continue;
    }
    if (template.realmId == RealmData.RealmId.Grove)
    {
     if (!string.IsNullOrEmpty(template.templateName) &&
         template.templateName.IndexOf("grove", StringComparison.OrdinalIgnoreCase) >= 0)
     {
      return template;
     }
     if (fallback == null)
     {
      fallback = template;
     }
    }
   }
   if (fallback != null)
   {
    return fallback;
   }
   foreach (var template in templates)
   {
    if (template != null && template.wallSets != null && template.wallSets.Count > 0)
    {
     return template;
    }
   }
   return null;
  }

  // Builds the list of interchangeable wall sets: every complete wall set of every dungeon
  // template whose tile size matches the Grove room, with the Grove's own sets first. A set is
  // only offered when every wall type the room uses has a prefab in it, so cycling can never
  // leave a hole in the room.
  public static void InitStyles(TemplateData preferred)
  {
   styles.Clear();
   floorStyles.Clear();
   if (!SingletonBehaviour<FateDB>.InstanceExists())
   {
    wallStyleIndex = 0;
    floorStyleIndex = 0;
    return;
   }
   var templates = SingletonBehaviour<FateDB>.Instance.allTemplates;
   if (templates == null)
   {
    wallStyleIndex = 0;
    floorStyleIndex = 0;
    return;
   }
   float tile = preferred != null && preferred.tileWidth > 0.1f ? preferred.tileWidth : 2f;
   var ordered = new List<TemplateData>();
   if (preferred != null)
   {
    ordered.Add(preferred);
   }
   foreach (var template in templates)
   {
    if (template == null || template == preferred || template.wallSets == null || template.wallSets.Count == 0)
    {
     continue;
    }
    // A wall prefab is authored for its own template's tile size; a set from a differently
    // scaled dungeon would not line up with this room's floor.
    if (template.tileWidth > 0.1f && Mathf.Abs(template.tileWidth - tile) > 0.05f)
    {
     continue;
    }
    ordered.Add(template);
   }
   // Floors do not need a complete wall set: a template with only a floor prefab is still a
   // valid texture source for this room.
   var floorOrdered = new List<TemplateData>();
   if (preferred != null)
   {
    floorOrdered.Add(preferred);
   }
   foreach (var template in templates)
   {
    if (template == null || template == preferred) continue;
    if (template.tileWidth > 0.1f && Mathf.Abs(template.tileWidth - tile) > 0.05f) continue;
    floorOrdered.Add(template);
   }
   var seen = new HashSet<GameObject>();
   foreach (var template in ordered)
   {
    if (template.wallSets == null)
    {
     continue;
    }
    for (int setIndex = 0; setIndex < template.wallSets.Count; setIndex++)
    {
     var set = template.wallSets[setIndex];
     if (set == null)
     {
      continue;
     }
     bool complete = true;
     foreach (var type in QuadrantTypes)
     {
      var walls = GetWalls(template, setIndex, type);
      if (walls == null || walls.Count == 0 || walls[0] == null)
      {
       complete = false;
       break;
      }
     }
     if (!complete)
     {
      continue;
     }
     if (!seen.Add(GetWalls(template, setIndex, WallTypes.NWHorizontal)[0]))
     {
      continue;
     }
     string label = string.IsNullOrEmpty(template.templateName) ? "Dungeon walls" : template.templateName;
     if (!string.IsNullOrEmpty(set.texture))
     {
      label += " - " + set.texture;
     }
     else if (template.wallSets.Count > 1)
     {
      label += " " + (setIndex + 1);
     }
     styles.Add(new WallStyle { Template = template, Set = setIndex, Variant = 0, Name = label });
    }
   }
   if (styles.Count == 0)
   {
    PermanentStoragePlugin.Log.LogWarning("No complete dungeon wall set was found; the room falls back to the wooden shell walls.");
   }
   // The dungeon generator always replaces a floor prefab's material with the template's
   // floorMaterials list. The old slider cycled prefabs from one template, so every entry
   // shared the same material and looked identical in the room. Offer one entry per distinct
   // floor material set instead, and apply that material to the room's floor tiles.
   var seenFloorMaterials = new HashSet<string>();
   foreach (var template in floorOrdered)
   {
    if (template == null || template.floorMaterials == null || template.floorMaterials.Count == 0) continue;
    GameObject floorPrefab = FindRoomFloorPrefab(template);
    if (floorPrefab == null) floorPrefab = FindRoomFloorPrefab(preferred);
    if (floorPrefab == null) continue;
    string materialKey = FloorMaterialKey(template.floorMaterials);
    if (!seenFloorMaterials.Add(materialKey)) continue;
    string label = string.IsNullOrEmpty(template.templateName) ? "Dungeon" : template.templateName;
    floorStyles.Add(new FloorStyle { Template = template, Prefab = floorPrefab, Name = label + " floor" });
   }
   // Older or unusual template sets may have no floor materials. Keep the prefab list as a
   // fallback so the slider still does something useful there.
   if (floorStyles.Count == 0)
   {
    var seenFloors = new HashSet<GameObject>();
    foreach (var template in floorOrdered)
    {
     if (template.floorPrefabs == null) continue;
     for (int floorIndex = 0; floorIndex < template.floorPrefabs.Count; floorIndex++)
     {
      var set = template.floorPrefabs[floorIndex];
      if (set.floorPrefab == null || !seenFloors.Add(set.floorPrefab)) continue;
      string label = string.IsNullOrEmpty(template.templateName) ? "Dungeon" : template.templateName;
      label += " floor";
      if (set.floorType != CellTypes.Room) label += " (" + set.floorType + ")";
      if (template.floorPrefabs.Count > 1) label += " " + (floorIndex + 1);
      floorStyles.Add(new FloorStyle { Template = template, Prefab = set.floorPrefab, Name = label });
     }
    }
   }
   if (floorStyles.Count == 0)
   {
    var fallbackFloor = FindRoomFloorPrefab(preferred);
    if (fallbackFloor != null)
    {
     string label = preferred != null && !string.IsNullOrEmpty(preferred.templateName) ? preferred.templateName : "Dungeon";
     floorStyles.Add(new FloorStyle { Template = preferred, Prefab = fallbackFloor, Name = label + " floor" });
    }
   }
   wallStyleIndex = ClampStyle(PermanentStoragePlugin.WallStyle != null ? PermanentStoragePlugin.WallStyle.Value : 0);
   floorStyleIndex = ClampFloor(PermanentStoragePlugin.FloorStyle != null ? PermanentStoragePlugin.FloorStyle.Value : 0);
   var names = new List<string>();
   foreach (var style in styles)
   {
    names.Add(style.Name);
   }
   PermanentStoragePlugin.Log.LogInfo("Storage room wall sets: " + styles.Count + " (" + string.Join(", ", names.ToArray()) +
    "); using \"" + WallStyleName + "\".");
   var floorNames = new List<string>();
   foreach (var style in floorStyles)
   {
    floorNames.Add(style.Name);
   }
   PermanentStoragePlugin.Log.LogInfo("Storage room floor sets: " + floorStyles.Count + " (" + string.Join(", ", floorNames.ToArray()) +
    "); using \"" + FloorStyleName + "\".");
  }

  static int ClampStyle(int index)
  {
   if (styles.Count == 0)
   {
    return 0;
   }
   if (index < 0)
   {
    index = 0;
   }
   if (index >= styles.Count)
   {
    index = styles.Count - 1;
   }
   return index;
  }

  static int ClampFloor(int index)
  {
   if (floorStyles.Count == 0)
   {
    return 0;
   }
   if (index < 0)
   {
    index = 0;
   }
   if (index >= floorStyles.Count)
   {
    index = floorStyles.Count - 1;
   }
   return index;
  }

  // Returns the index actually applied. Called by the mod menu slider and by the config poll.
  public static int SetWallStyle(int index)
  {
   if (styles.Count == 0)
   {
    return 0;
   }
   int clamped = ClampStyle(index);
   bool changed = clamped != wallStyleIndex;
   wallStyleIndex = clamped;
   if (changed)
   {
    PermanentStoragePlugin.Log.LogInfo("Storage room wall style set to \"" + WallStyleName + "\" (" + wallStyleIndex + "/" + (styles.Count - 1) + ").");
    RebuildWalls();
    // With no independent floor list, the old behaviour still applies: keep the floor in
    // step with the wall theme. The normal path leaves the chosen floor untouched.
    if (floorStyles.Count == 0) BuildFloor();
   }
   return clamped;
  }

  // Returns the index actually applied. The floor is deliberately independent of the wall
  // set; changing it rebuilds only the floor so the room does not flicker as a whole.
  public static int SetFloorStyle(int index)
  {
   if (floorStyles.Count == 0)
   {
    return 0;
   }
   int clamped = ClampFloor(index);
   bool changed = clamped != floorStyleIndex;
   floorStyleIndex = clamped;
   if (changed)
   {
    PermanentStoragePlugin.Log.LogInfo("Storage room floor style set to \"" + FloorStyleName + "\" (" + floorStyleIndex + "/" + (floorStyles.Count - 1) + ").");
    BuildFloor();
   }
   return clamped;
  }

  // Keeps the room in step with a value edited directly in the config file.
  public static void SyncStyleFromConfig()
  {
   if (styles.Count > 0 && PermanentStoragePlugin.WallStyle != null &&
       ClampStyle(PermanentStoragePlugin.WallStyle.Value) != wallStyleIndex)
   {
    SetWallStyle(PermanentStoragePlugin.WallStyle.Value);
   }
   if (floorStyles.Count > 0 && PermanentStoragePlugin.FloorStyle != null &&
       ClampFloor(PermanentStoragePlugin.FloorStyle.Value) != floorStyleIndex)
   {
    SetFloorStyle(PermanentStoragePlugin.FloorStyle.Value);
   }
  }

  // Floors and stairs follow the active wall theme so the room always uses one coherent tile set.
  static TemplateData CurrentWallTemplate()
  {
   return styles.Count == 0 ? floorTemplate : styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)].Template;
  }

  static GameObject FindRoomFloorPrefab(TemplateData template)
  {
   if (template == null)
   {
    return null;
   }
   if (template.newFloorPrefab != null)
   {
    return template.newFloorPrefab;
   }
   if (template.floorPrefabs != null)
   {
    foreach (var set in template.floorPrefabs)
    {
     if (set.floorType == CellTypes.Room && set.floorPrefab != null)
     {
      return set.floorPrefab;
     }
    }
   }
   if (template.floorDictionary == null)
   {
    try { template.UpdateFloorDictionary(); }
    catch (Exception ex) { PermanentStoragePlugin.Log.LogWarning("Could not build the selected theme floor dictionary: " + ex.Message); }
   }
   if (template.floorDictionary != null &&
       template.floorDictionary.TryGetValue(CellTypes.Room, out var pair) && pair != null &&
       pair.TryGetValue(false, out var floor) && floor != null)
   {
    return floor;
   }
   return null;
  }
  static void BuildFloor()
  {
   if (floorRoot == null)
   {
    return;
   }
   for (int i = floorRoot.transform.childCount - 1; i >= 0; i--)
   {
    Object.Destroy(floorRoot.transform.GetChild(i).gameObject);
   }
   FloorStyle style = floorStyles.Count > 0
    ? floorStyles[Mathf.Clamp(floorStyleIndex, 0, floorStyles.Count - 1)]
    : default(FloorStyle);
   GameObject prefab = floorStyles.Count > 0
    ? style.Prefab
    : FindRoomFloorPrefab(CurrentWallTemplate()) ?? FindRoomFloorPrefab(floorTemplate);
   for (int x = -1; x <= Cells; x++)
   {
    for (int z = -1; z <= Cells; z++)
    {
     if (prefab != null)
     {
      Vector3 cell = wallOrigin + new Vector3((x + 0.5f) * wallTile, 0f, (z + 0.5f) * wallTile);
      GameObject tile = Object.Instantiate(prefab, cell, Quaternion.identity, floorRoot.transform);
      if (floorStyles.Count > 0) ApplyFloorMaterials(style.Template, tile);
     }
    }
   }
   if (prefab == null)
   {
    float width = Cells * wallTile;
    CreateBox(floorRoot.transform, "FallbackFloor", wallOrigin + new Vector3(width * 0.5f, -0.15f, width * 0.5f),
     new Vector3(width + wallTile * 2f, 0.3f, width + wallTile * 2f), CreamMaterial(), false);
   }
  }

  // The storage exit uses the selected dungeon theme's real door prop. For this room the
  // south-facing NSDoor asset is placed at world (4003, 300, 3998.5), with the same base rotation
  // convention the dungeon generator applies to authored door props.
  static string FloorMaterialKey(List<Material> materials)
  {
   var parts = new List<string>();
   foreach (var material in materials)
   {
    parts.Add(material != null ? material.GetInstanceID().ToString() : "0");
   }
   return string.Join(",", parts.ToArray());
  }

  static void ApplyFloorMaterials(TemplateData template, GameObject tile)
  {
   if (template == null || template.floorMaterials == null || template.floorMaterials.Count == 0 || tile == null)
   {
    return;
   }
   foreach (var renderer in tile.GetComponentsInChildren<MeshRenderer>(true))
   {
    if (renderer != null)
    {
     renderer.SetMaterials(template.floorMaterials);
    }
   }
  }

  static void FillDoorway()
  {
   TemplateData styleTemplate = CurrentWallTemplate();
   PropData doorProp = FindDoorProp(styleTemplate, WallStyleName);
   if (doorwayRoot != null) Object.Destroy(doorwayRoot);
   if (doorProp == null || doorProp.modelPrefab == null)
   {
    PermanentStoragePlugin.Log.LogWarning("No themed NSDoor/EWDoor prop was found for '" + WallStyleName + "'; leaving the exit opening visible without a door model.");
    return;
   }

   Vector3 position = wallOrigin + new Vector3(ExitDoorLocalX, 0f, ExitDoorLocalZ);
   Quaternion rotation = Quaternion.Euler(0f, doorProp.baseRotation + 180f, 0f);
   doorwayRoot = new GameObject("FATE_StorageRoomThemedDoor");
   doorwayRoot.transform.SetParent(roomRoot != null ? roomRoot.transform : null, true);
   doorwayRoot.transform.SetPositionAndRotation(position, rotation);
   GameObject door = Object.Instantiate(doorProp.modelPrefab, position, rotation, doorwayRoot.transform);
   float variation = Mathf.Max(0f, doorProp.scaleVariation);
   if (variation > 0f) door.transform.localScale *= 1f + variation * 0.5f;
   StorageDoorAudio.TryCacheFromAsset(doorProp.modelPrefab);
   PermanentStoragePlugin.Log.LogInfo("Doorway: themed door prop '" + doorProp.propName + "' / '" +
    doorProp.modelPrefab.name + "' for wall theme '" + WallStyleName + "' placed at " + position + ".");
  }

  static PropData FindDoorProp(TemplateData preferred, string wallTheme)
  {
   var candidates = new List<TemplateData>();
   if (preferred != null) candidates.Add(preferred);
   if (floorTemplate != null && !candidates.Contains(floorTemplate)) candidates.Add(floorTemplate);
   try
   {
    var database = SingletonBehaviour<FateDB>.InstanceExists() ? SingletonBehaviour<FateDB>.Instance : null;
    var templates = database != null ? database.allTemplates : null;
    if (templates != null)
    {
     foreach (var template in templates)
      if (template != null && !candidates.Contains(template)) candidates.Add(template);
    }
   }
   catch { }

   PropData best = null;
   int bestScore = int.MinValue;
   string bestTemplate = "";
   foreach (var template in candidates)
   {
    if (template.props == null) continue;
    foreach (var prop in template.props)
    {
     if (prop == null || prop.modelPrefab == null || prop.locations == null) continue;
     bool northSouth = prop.locations.Contains(PropLocation.NSDoor);
     bool eastWest = prop.locations.Contains(PropLocation.EWDoor);
     if (!northSouth && !eastWest) continue;
     bool hasDoorController = prop.modelPrefab.GetComponentInChildren<Deftly.Door>(true) != null;
     int theme = ThemeScore(template.templateName, wallTheme, prop);
     int score = (northSouth ? 100 : 70) + (hasDoorController ? 60 : 0) + theme;
     if (template == preferred) score += 30;
     if (prop.isStairsUp || prop.isStairsDown) score -= 1000;
     if (score > bestScore)
     {
      best = prop;
      bestScore = score;
      bestTemplate = template.templateName;
     }
    }
   }
   if (best != null)
    PermanentStoragePlugin.Log.LogInfo("Selected themed door from template '" + bestTemplate + "' (score=" + bestScore + ").");
   return best;
  }

  static int ThemeScore(string templateName, string wallTheme, PropData prop)
  {
   if (string.IsNullOrEmpty(templateName) || prop == null || prop.modelPrefab == null) return 0;
   string assetName = ((prop.propName ?? "") + " " + prop.modelPrefab.name + " " + (prop.texturePath ?? "")).ToLowerInvariant();
   string[] ignored = { "dungeon", "template", "theme", "door", "prop", "fate", "the", "and", "realms", "ekbatan", "tiletextures", "triad0", "png" };
   string[] tokens = (templateName + " " + (wallTheme ?? "")).ToLowerInvariant().Split(new[] { ' ', '_', '-', '.', '/', '\\', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
   int score = 0;
   foreach (string token in tokens)
   {
    int numericToken;
    if (token.Length < 2 || int.TryParse(token, out numericToken) || Array.IndexOf(ignored, token) >= 0) continue;
    if (assetName.Contains(token)) score += token.Length + (token.Length >= 4 ? 2 : 1);
   }
   return score;
  }

  static void RebuildWalls()
  {
   if (wallsRoot == null)
   {
    return;
   }
   for (int i = wallsRoot.transform.childCount - 1; i >= 0; i--)
   {
    Object.Destroy(wallsRoot.transform.GetChild(i).gameObject);
   }
   FillWalls();
   BuildCeilingSurround();
   FillDoorway();
  }

  // Dungeons roof every non-room cell, and on the map edge they add two more cells outward, so
  // their ceiling reads as an upper floor that runs off into the dark around a room. The roof is
  // built after the walls: it uses the chosen wall set's own ceiling prefab (so the texture
  // matches the walls) and sits on the measured wall tops instead of a guessed height. Any saved
  // per-tile offsets (from the in-game roof editor) are applied as deltas from the computed base.
  static void BuildCeilingSurround()
  {
   ceilingMarkers.Clear();
   if (roofRoot == null)
   {
    roofRoot = new GameObject("CeilingSurround");
    roofRoot.transform.SetParent(roomRoot != null ? roomRoot.transform : null, true);
    roofRoot.transform.position = wallOrigin;
    roofRoot.transform.rotation = Quaternion.identity;
   }
   for (int i = roofRoot.transform.childCount - 1; i >= 0; i--)
   {
    Object.Destroy(roofRoot.transform.GetChild(i).gameObject);
   }
   var styleTemplate = styles.Count > 0 ? styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)].Template : null;
   GameObject roof = FindCeilingPrefab(styleTemplate);
   if (roof == null)
   {
    roof = FindCeilingPrefab(floorTemplate);
   }
   if (roof == null)
   {
    PermanentStoragePlugin.Log.LogWarning("No ceiling prefab was found; the room has no ceiling surround.");
    return;
   }
   CeilingOffsets.EnsureLoaded();
   float top = WallTop();
   Vector3 first = wallOrigin + wallShift + new Vector3(-2.5f * wallTile, top, -2.5f * wallTile);
   var probe = Object.Instantiate(roof, first, Quaternion.identity, roofRoot.transform);
   float delta = 0f;
   var probeRenderer = probe.GetComponentInChildren<Renderer>();
   if (probeRenderer != null)
   {
    delta = top - probeRenderer.bounds.min.y;
   }
   Object.Destroy(probe);
   ceilingPrefab = roof;
   ceilingTop = top;
   ceilingDelta = delta;
   int placed = 0;
   int overridden = 0;
   for (int x = -CeilingTilePadding; x < Cells + CeilingTilePadding; x++)
   {
    for (int z = -CeilingTilePadding; z < Cells + CeilingTilePadding; z++)
    {
     bool defaultTile = IsDefaultCeilingCell(x, z);
     if (CeilingOffsets.IsRemoved(x, z) || (!defaultTile && !CeilingOffsets.IsAdded(x, z))) continue;
     var marker = CreateCeilingTile(x, z);
     if (marker == null) continue;
     placed++;
     if (HasCustomCeilingOffset(x, z) || CeilingOffsets.TryGetScale(x, z, out _) || !defaultTile) overridden++;
    }
   }
   PermanentStoragePlugin.Log.LogInfo("Ceiling surround: '" + roof.name + "' " + placed + " grid tile(s) at wall-top height " +
    top.ToString("0.00") + (overridden > 0 ? ", " + overridden + " with baked/editor offsets" : "") + ".");
  }

  static bool IsDefaultCeilingCell(int x, int z)
  {
   return !(x >= 0 && x < Cells && z >= 0 && z < Cells);
  }

  static CeilingTileMarker CreateCeilingTile(int x, int z)
  {
   if (ceilingPrefab == null || roofRoot == null) return null;
   Vector3 cell = wallOrigin + wallShift + new Vector3((x + 0.5f) * wallTile, ceilingTop, (z + 0.5f) * wallTile);
   Vector3 basePos = cell + Vector3.up * ceilingDelta + DefaultCeilingEdgeOffset(x, z);
   Vector3 offset = SavedCeilingAdjustment(x, z);
   var tile = Object.Instantiate(ceilingPrefab, basePos + offset,
    Quaternion.identity, roofRoot.transform);
   var marker = tile.AddComponent<CeilingTileMarker>();
   marker.GridX = x;
   marker.GridZ = z;
   marker.BaseWorldPosition = basePos;
   marker.BaseLocalScale = tile.transform.localScale;
   Vector3 scale;
   if (CeilingOffsets.TryGetScale(x, z, out scale))
   {
    tile.transform.localScale = Vector3.Scale(marker.BaseLocalScale, scale);
   }
   ceilingMarkers.Add(marker);
   return marker;
  }

  // Shift each straight inner roof edge outward by the requested 1.54 m. The four corner
  // cells move half that distance in both directions, so they overlap the two adjacent edge
  // strips without adding a second, unselectable tile at each corner.
  static Vector3 DefaultCeilingEdgeOffset(int x, int z)
  {
   bool edgeX = x == -1 || x == Cells;
   bool edgeZ = z == -1 || z == Cells;
   bool corner = edgeX && edgeZ;
   float amount = corner ? RoofEdgeOffset * 0.5f : RoofEdgeOffset;
   bool sideX = edgeX && (corner || (z >= 0 && z < Cells));
   bool sideZ = edgeZ && (corner || (x >= 0 && x < Cells));
   float ox = sideX ? (x < 0 ? -amount : amount) : 0f;
   float oz = sideZ ? (z < 0 ? -amount : amount) : 0f;
   return new Vector3(ox, 0f, oz);
  }

  static Vector3 SavedCeilingAdjustment(int x, int z)
  {
   Vector3 saved;
   if (!CeilingOffsets.TryGet(x, z, out saved)) return Vector3.zero;
   return CeilingOffsets.IsLegacyOffset(x, z) ? CeilingOffsets.NormalizeLegacyOffset(x, z, saved) : saved;
  }

  static bool HasCustomCeilingOffset(int x, int z)
  {
   return SavedCeilingAdjustment(x, z).sqrMagnitude > 0.0001f;
  }

  public static CeilingTileMarker InsertCeilingTileAt(Vector3 worldPosition)
  {
   if (ceilingPrefab == null || roofRoot == null || wallTile <= 0f) return null;
   int x = Mathf.RoundToInt((worldPosition.x - wallOrigin.x - wallShift.x) / wallTile - 0.5f);
   int z = Mathf.RoundToInt((worldPosition.z - wallOrigin.z - wallShift.z) / wallTile - 0.5f);
   if (x < -CeilingTilePadding || x >= Cells + CeilingTilePadding ||
       z < -CeilingTilePadding || z >= Cells + CeilingTilePadding) return null;
   foreach (var existing in CeilingMarkers())
   {
    if (existing.GridX == x && existing.GridZ == z) return existing;
   }
   if (IsDefaultCeilingCell(x, z))
   {
    // A missing default cell should only be restorable when it was explicitly deleted.
    if (!CeilingOffsets.IsRemoved(x, z)) return null;
    CeilingOffsets.SetRemoved(x, z, false);
   }
   else
   {
    CeilingOffsets.SetAdded(x, z, true);
   }
   CeilingOffsets.Clear(x, z);
   var marker = CreateCeilingTile(x, z);
   CeilingOffsets.SaveUser();
   return marker;
  }

  public static void DeleteCeilingTile(CeilingTileMarker marker)
  {
   if (marker == null) return;
   int x = marker.GridX;
   int z = marker.GridZ;
   if (IsDefaultCeilingCell(x, z)) CeilingOffsets.SetRemoved(x, z, true);
   else CeilingOffsets.SetAdded(x, z, false);
   CeilingOffsets.Clear(x, z);
   ceilingMarkers.Remove(marker);
   Object.Destroy(marker.gameObject);
   CeilingOffsets.SaveUser();
  }

  static float WallTop()
  {
   float top = wallOrigin.y + CeilingHeight;
   if (wallsRoot == null)
   {
    return top;
   }
   bool found = false;
   foreach (var renderer in wallsRoot.GetComponentsInChildren<Renderer>())
   {
    if (renderer == null || renderer is TrailRenderer || renderer is LineRenderer)
    {
     continue;
    }
    if (!found)
    {
     top = renderer.bounds.max.y;
     found = true;
    }
    else
    {
     top = Mathf.Max(top, renderer.bounds.max.y);
    }
   }
   return top;
  }

  public static BuiltRoom Build(Vector3 origin, int interactableLayer, StorageRoomManager manager)
  {
   var built = new BuiltRoom();
   var root = new GameObject("FATE_PermanentStorageRoom");
   root.transform.position = origin;
   root.transform.rotation = Quaternion.identity;
   built.Root = root;
   roomRoot = root;

   TemplateData template = FindGroveTemplate();
   if (template == null)
   {
    PermanentStoragePlugin.Log.LogWarning("No Grove dungeon template was found; the storage room will use a simple fallback shell.");
   }
   else
   {
    PermanentStoragePlugin.Log.LogInfo("Building storage room from dungeon template '" + template.templateName + "'.");
   }
   InitStyles(template);

   float tile = template != null && template.tileWidth > 0.1f ? template.tileWidth : 2f;
   float width = Cells * tile;

   // Floor. It runs one cell past the room on every side so it reaches the ring walls instead of
   // stopping short of them and showing the drop underneath.
   wallOrigin = origin;
   wallTile = tile;
   wallDoorCell = Mathf.Clamp(Mathf.FloorToInt(ExitDoorLocalX / tile), 0, Cells - 1);
   floorTemplate = template;
   floorRoot = new GameObject("Floor");
   floorRoot.transform.SetParent(root.transform, true);
   floorRoot.transform.position = origin;
   floorRoot.transform.rotation = Quaternion.identity;

   BuildFloor();
   // A second, invisible floor collider makes sure the player can never fall through a prefab
   // whose collider is missing or one-sided.
   float floorExtent = width + tile * 2f;
   CreateBox(root.transform, "FloorSafetyCollider", origin + new Vector3(width * 0.5f, -0.15f, width * 0.5f),
    new Vector3(floorExtent, 0.3f, floorExtent), null, true);

   // The ceiling surround is built after the walls, so it can sit on the wall tops and use the
   // ceiling tiles of the chosen wall set (see BuildCeilingSurround).

   wallOrigin = origin;
   wallTile = tile;
   if (template != null && styles.Count > 0)
   {
    var container = new GameObject("Walls");
    container.transform.SetParent(root.transform, true);
    container.transform.position = origin;
    container.transform.rotation = Quaternion.identity;
    wallsRoot = container;
    FillWalls();
   }
   else
   {
    wallsRoot = null;
    BuildFallbackWalls(root.transform, origin, width);
   }
   BuildCeilingSurround();
   FillDoorway();

   // Collision shell. The visual wall prefabs are reused; these thin colliders guarantee the
   // room is closed even if a particular wall prefab has no collider.
   float half = width * 0.5f;
   float centerY = CeilingHeight * 0.5f;
   CreateBox(root.transform, "NorthCollider", origin + new Vector3(half, centerY, width + 0.15f),
    new Vector3(width, CeilingHeight, 0.3f), null, true);
   BuildSouthColliderWithDoorway(root.transform, origin, width, centerY);
   CreateBox(root.transform, "WestCollider", origin + new Vector3(-0.15f, centerY, half),
    new Vector3(0.3f, CeilingHeight, width), null, true);
   CreateBox(root.transform, "EastCollider", origin + new Vector3(width + 0.15f, centerY, half),
    new Vector3(0.3f, CeilingHeight, width), null, true);

   // Warm interior lighting. The room is large enough that the corners alone leave a dark
   // middle, so the quarter points and the centre are lit as well.
   AddLight(root.transform, origin + new Vector3(2.5f, 2.7f, 2.5f), 9f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width - 2.5f, 2.7f, 2.5f), 9f, 1.5f);
   AddLight(root.transform, origin + new Vector3(2.5f, 2.7f, width - 2.5f), 9f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width - 2.5f, 2.7f, width - 2.5f), 9f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width * 0.25f, 2.7f, width * 0.5f), 10f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width * 0.75f, 2.7f, width * 0.5f), 10f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width * 0.5f, 2.7f, width * 0.25f), 10f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width * 0.5f, 2.7f, width * 0.75f), 10f, 1.5f);
   AddLight(root.transform, origin + new Vector3(width * 0.5f, 2.8f, width * 0.5f), 14f, 1.8f);

   // Keep the interaction trigger just inside the themed door at X=4003, Z=3998.5.
   Vector3 exitPos = origin + new Vector3(ExitDoorLocalX, 1.1f, ExitDoorLocalZ + 0.55f);
   var exit = new GameObject("FATE_StorageRoomExit");
   exit.transform.SetParent(root.transform, true);
   exit.transform.position = exitPos;
   exit.transform.rotation = Quaternion.identity;
   var exitCollider = exit.AddComponent<BoxCollider>();
   exitCollider.size = new Vector3(1.8f, 2.2f, 0.7f);
   exitCollider.center = Vector3.zero;
   exit.layer = interactableLayer;
   var exitDoor = exit.AddComponent<StorageExitDoor>();
   exitDoor.Init(manager);
   built.ExitDoor = exit;

   // Spawn just inside the south doorway, in line with the themed door and exit trigger.
   built.Spawn = origin + new Vector3(ExitDoorLocalX, 0.15f, 1.5f);

   // The proxy is the game's own Stash component with no renderer and no collider. It is kept
   // in the room so the stock storage grid can be used for whichever chest is open. Its save
   // side effects are suppressed by the patches in StorageStashPatches.
   var proxyGo = new GameObject("FATE_StorageStashProxy");
   proxyGo.transform.SetParent(root.transform, false);
   proxyGo.transform.localPosition = Vector3.zero;
   proxyGo.AddComponent<StorageStashProxy>();
   built.Proxy = proxyGo.AddComponent<Stash>();
   // Keep the proxy out of every FindFirstObjectByType<Stash> lookup until one of this mod's
   // chests is actually open. Otherwise the town stash UI could pick the proxy instead of the
   // real town stash.
   proxyGo.SetActive(false);

   // Put the surface on the room root so CollectObjects.Children picks up every floor, wall,
   // ceiling, door and chest that is parented under the room.
   var surface = root.AddComponent<NavMeshSurface>();
   surface.collectObjects = CollectObjects.Children;
   surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
   surface.layerMask = ~0;
   built.Surface = surface;

   // Legacy chest anchors. Chests are now placed by the player, so these positions are only used
   // to keep items from saves made before 0.7.22. The old row was centred on the room's X midline
   // and 6 m in from the north wall, one chest every 4 m; the 24 m room fits five at that spacing.
   const float ChestSpacing = 4f;
   const float ChestWallGap = 6f;
   const float ChestMargin = 1.5f;
   int chestHalf = Mathf.FloorToInt((width * 0.5f - ChestMargin) / ChestSpacing);
   chestHalf = Mathf.Min(chestHalf, (StorageRoomManager.LegacyChestCount - 1) / 2);
   for (int i = -chestHalf; i <= chestHalf; i++)
   {
    AddAnchor(built, tile, width * 0.5f + i * ChestSpacing, width - ChestWallGap, 180f);
   }

   return built;
  }

  static void AddAnchor(BuiltRoom built, float tile, float localX, float localZ, float yaw)
  {
   built.Chests.Add(new ChestAnchor
   {
    LocalPosition = new Vector3(localX, 0f, localZ),
    LocalRotation = Quaternion.Euler(0f, yaw, 0f)
   });
  }

  static GameObject FindFloorPrefab(TemplateData template)
  {
   if (template == null)
   {
    return null;
   }
   if (template.floorPrefabs != null)
   {
    foreach (var set in template.floorPrefabs)
    {
     if (set.floorPrefab != null && set.floorType == CellTypes.Room)
     {
      return set.floorPrefab;
     }
    }
    foreach (var set in template.floorPrefabs)
    {
     if (set.floorPrefab != null)
     {
      return set.floorPrefab;
     }
    }
   }
   if (template.floorDictionary != null &&
       template.floorDictionary.TryGetValue(CellTypes.Room, out var pair) && pair != null)
   {
    if (pair.TryGetValue(false, out var floor) && floor != null)
    {
     return floor;
    }
   }
   return null;
  }

  static GameObject FindCeilingPrefab(TemplateData template)
  {
   if (template == null || template.ceilingTiles == null)
   {
    return null;
   }
   foreach (var tile in template.ceilingTiles)
   {
    if (tile != null && tile.tilePrefab != null)
    {
     return tile.tilePrefab;
    }
   }
   return null;
  }

  static List<GameObject> GetWalls(TemplateData template, int setIndex, WallTypes type)
  {
   if (template == null || template.wallSets == null || template.wallSets.Count == 0)
   {
    return null;
   }
   if (setIndex < 0 || setIndex >= template.wallSets.Count)
   {
    setIndex = 0;
   }
   var set = template.wallSets[setIndex];
   if (set == null || set.wallTypes == null)
   {
    return null;
   }
   int index = (int)type;
   if (index < 0 || index >= set.wallTypes.Count)
   {
    return null;
   }
   var data = set.wallTypes[index];
   return data != null ? data.walls : null;
  }

  // Lays walls exactly the way the dungeon generator does. Walls are not placed on the room's
  // own cells: a dungeon builds them on the ring of "cliff" cells around a room, comparing each
  // cell's height with its neighbours. Our room is a flat block at height 0 and everything around
  // it is height 1, which is what the generator uses for out-of-bounds cells too. Each ring cell
  // then gets whichever of its four quadrants faces the lower room floor, with the generator's
  // own corner, corner-join, inner-corner, horizontal and vertical cases. That also produces the
  // room's corners from the diagonal ring cells instead of a single hand-placed corner piece.
  static void FillWalls()
  {
   if (wallsRoot == null)
   {
    return;
   }
   wallMarkers.Clear();
   placedThisFill = 0;
   for (int x = -1; x <= Cells; x++)
   {
    for (int z = -1; z <= Cells; z++)
    {
     if (IsRoomCell(x, z))
     {
      continue;
     }
     Vector3 cell = wallOrigin + new Vector3((x + 0.5f) * wallTile, 0f, (z + 0.5f) * wallTile);
     // Dungeon cell coordinates: X grows with +x, Y grows towards -z.
     int cellX = x;
     int cellY = -(z + 1);
     PlaceQuadrant(cell, cellX, cellY, Quadrant.NW);
     PlaceQuadrant(cell, cellX, cellY, Quadrant.NE);
     PlaceQuadrant(cell, cellX, cellY, Quadrant.SW);
     PlaceQuadrant(cell, cellX, cellY, Quadrant.SE);
    }
   }
   AlignWallsToRoom();
   if (placedThisFill == 0)
   {
    // Never leave the room open: fall back to the straightforward edge construction.
    PermanentStoragePlugin.Log.LogWarning("The procedural wall ring placed nothing; using the edge construction instead.");
    BuildEdgeWalls();
    PermanentStoragePlugin.Log.LogInfo("Edge walls: " + placedThisFill + " piece(s) placed.");
   }
   ApplyWallLayout();
   PermanentStoragePlugin.Log.LogInfo("Wall ring: " + placedThisFill + " piece(s) placed with set \"" + WallStyleName + "\".");
  }

  // Slides the whole wall ring so its inner faces sit exactly on the room's hitbox line. The
  // ring is generated on the surrounding cells, which leaves it a little outside the floor; the
  // offset is measured from the pieces themselves, so it works for every tile set.
  static void AlignWallsToRoom()
  {
   wallShift = Vector3.zero;
   if (wallsRoot == null)
   {
    return;
   }
   float width = Cells * wallTile;
   float centreX = wallOrigin.x + width * 0.5f;
   float centreZ = wallOrigin.z + width * 0.5f;
   float westInner = float.NegativeInfinity;
   float eastInner = float.PositiveInfinity;
   float southInner = float.NegativeInfinity;
   float northInner = float.PositiveInfinity;
   bool found = false;
   foreach (var renderer in wallsRoot.GetComponentsInChildren<Renderer>())
   {
    if (renderer == null || renderer is TrailRenderer || renderer is LineRenderer)
    {
     continue;
    }
    var bounds = renderer.bounds;
    var centre = bounds.center;
    if (centre.x < centreX)
    {
     westInner = Mathf.Max(westInner, bounds.max.x);
    }
    else
    {
     eastInner = Mathf.Min(eastInner, bounds.min.x);
    }
    if (centre.z < centreZ)
    {
     southInner = Mathf.Max(southInner, bounds.max.z);
    }
    else
    {
     northInner = Mathf.Min(northInner, bounds.min.z);
    }
    found = true;
   }
   if (!found)
   {
    return;
   }
   float targetMinX = wallOrigin.x + WallInset;
   float targetMaxX = wallOrigin.x + width - WallInset;
   float targetMinZ = wallOrigin.z + WallInset;
   float targetMaxZ = wallOrigin.z + width - WallInset;
    if (eastInner - westInner < width * 0.5f || northInner - southInner < width * 0.5f)
    {
     PermanentStoragePlugin.Log.LogWarning("Wall bounds did not span the room; skipping automatic alignment to avoid pulling the wall ring into the centre.");
     return;
    }
   float deltaX = ((targetMinX - westInner) + (targetMaxX - eastInner)) * 0.5f;
   float deltaZ = ((targetMinZ - southInner) + (targetMaxZ - northInner)) * 0.5f;
   wallShift = new Vector3(deltaX, 0f, deltaZ);
   wallsRoot.transform.position += wallShift;
   PermanentStoragePlugin.Log.LogInfo("Walls aligned to the room: inner x " + (westInner + deltaX).ToString("0.00") +
    ".." + (eastInner + deltaX).ToString("0.00") + ", inner z " + (southInner + deltaZ).ToString("0.00") + ".." +
    (northInner + deltaZ).ToString("0.00") + " (target " + targetMinX.ToString("0.00") + ".." +
    targetMaxX.ToString("0.00") + ").");
  }

  // Fallback ring: one straight piece per room cell on each outward edge, placed on the room
  // cells themselves. It is what a hand-made room box looks like, and it is only used when the
  // height-based ring produced nothing.
  static void BuildEdgeWalls()
  {
   for (int x = 0; x < Cells; x++)
   {
    for (int z = 0; z < Cells; z++)
    {
     Vector3 cell = wallOrigin + new Vector3((x + 0.5f) * wallTile, 0f, (z + 0.5f) * wallTile);
     if (z == Cells - 1)
     {
      PlaceWallAt(cell, WallTypes.NWHorizontal, "edge:" + x + ":" + z + ":NW");
      PlaceWallAt(cell, WallTypes.NEHorizontal, "edge:" + x + ":" + z + ":NE");
     }
     if (z == 0)
     {
       if (x != wallDoorCell)
       {
        PlaceWallAt(cell, WallTypes.SWHorizontal, "edge:" + x + ":" + z + ":SW");
        PlaceWallAt(cell, WallTypes.SEHorizontal, "edge:" + x + ":" + z + ":SE");
       }
     }
     if (x == 0)
     {
      PlaceWallAt(cell, WallTypes.NWVertical, "edge:" + x + ":" + z + ":NWV");
      PlaceWallAt(cell, WallTypes.SWVertical, "edge:" + x + ":" + z + ":SWV");
     }
     if (x == Cells - 1)
     {
      PlaceWallAt(cell, WallTypes.NEVertical, "edge:" + x + ":" + z + ":NEV");
      PlaceWallAt(cell, WallTypes.SEVertical, "edge:" + x + ":" + z + ":SEV");
     }
    }
   }
  }

  static bool IsRoomCell(int x, int z)
  {
   return x >= 0 && x < Cells && z >= 0 && z < Cells;
  }

  // Height field: 0 on the room floor, 1 everywhere else, matching Dungeon.CellHeight.
  static int Height(int cellX, int cellY)
  {
   return IsRoomCell(cellX, -(cellY + 1)) ? 0 : 1;
  }

  static void PlaceQuadrant(Vector3 cell, int cellX, int cellY, Quadrant quadrant)
  {
   // The south outer cell at x=wallDoorCell contributes the two horizontal wall halves that
   // face into the room. Leave that tile-width opening for the authored door prop.
   if (cellX == wallDoorCell && cellY == 0 &&
       (quadrant == Quadrant.NW || quadrant == Quadrant.NE)) return;
   int self = Height(cellX, cellY);
   int north = Height(cellX, cellY - 1);
   int south = Height(cellX, cellY + 1);
   int west = Height(cellX - 1, cellY);
   int east = Height(cellX + 1, cellY);
   WallTypes type;
   switch (quadrant)
   {
    case Quadrant.NW:
     type = Classify(self, north, west, Height(cellX - 1, cellY - 1), quadrant);
     break;
    case Quadrant.NE:
     type = Classify(self, north, east, Height(cellX + 1, cellY - 1), quadrant);
     break;
    case Quadrant.SW:
     type = Classify(self, south, west, Height(cellX - 1, cellY + 1), quadrant);
     break;
    default:
     type = Classify(self, south, east, Height(cellX + 1, cellY + 1), quadrant);
     break;
   }
   if (type == WallTypes.Open)
   {
    return;
   }
   PlaceWallAt(cell, type, "ring:" + cellX + ":" + cellY + ":" + quadrant);
  }

  // Dungeon.WallTypeNW and its three siblings, generalised over the quadrant.
  static WallTypes Classify(int self, int sideA, int sideB, int diagonal, Quadrant quadrant)
  {
   bool aLower = sideA < self;
   bool bLower = sideB < self;
   bool aEqual = sideA == self;
   bool bEqual = sideB == self;
   bool dLower = diagonal < self;
   bool dEqual = diagonal == self;
   if (aLower && bLower && dEqual)
   {
    return CornerJoin(quadrant);
   }
   if (aLower && bLower)
   {
    return Corner(quadrant);
   }
   if (aEqual && bEqual && dLower)
   {
    return InnerCorner(quadrant);
   }
   if (aEqual && bEqual && dEqual)
   {
    // The generator skips solid walls; they are the tops of unbroken ground.
    return Solid(quadrant);
   }
   if (aLower && bEqual)
   {
    return Horizontal(quadrant);
   }
   if (aEqual && bLower)
   {
    return Vertical(quadrant);
   }
   return WallTypes.Open;
  }

  static WallTypes Horizontal(Quadrant quadrant)
  {
   switch (quadrant)
   {
    case Quadrant.NW: return WallTypes.NWHorizontal;
    case Quadrant.NE: return WallTypes.NEHorizontal;
    case Quadrant.SW: return WallTypes.SWHorizontal;
    default: return WallTypes.SEHorizontal;
   }
  }

  static WallTypes Vertical(Quadrant quadrant)
  {
   switch (quadrant)
   {
    case Quadrant.NW: return WallTypes.NWVertical;
    case Quadrant.NE: return WallTypes.NEVertical;
    case Quadrant.SW: return WallTypes.SWVertical;
    default: return WallTypes.SEVertical;
   }
  }

  static WallTypes Corner(Quadrant quadrant)
  {
   switch (quadrant)
   {
    case Quadrant.NW: return WallTypes.NWCorner;
    case Quadrant.NE: return WallTypes.NECorner;
    case Quadrant.SW: return WallTypes.SWCorner;
    default: return WallTypes.SECorner;
   }
  }

  static WallTypes CornerJoin(Quadrant quadrant)
  {
   switch (quadrant)
   {
    case Quadrant.NW: return WallTypes.NWCornerJoin;
    case Quadrant.NE: return WallTypes.NECornerJoin;
    case Quadrant.SW: return WallTypes.SWCornerJoin;
    default: return WallTypes.SECornerJoin;
   }
  }

  static WallTypes InnerCorner(Quadrant quadrant)
  {
   switch (quadrant)
   {
    case Quadrant.NW: return WallTypes.NWInnerCorner;
    case Quadrant.NE: return WallTypes.NEInnerCorner;
    case Quadrant.SW: return WallTypes.SWInnerCorner;
    default: return WallTypes.SEInnerCorner;
   }
  }

  static WallTypes Solid(Quadrant quadrant)
  {
   switch (quadrant)
   {
    case Quadrant.NW: return WallTypes.NWSolid;
    case Quadrant.NE: return WallTypes.NESolid;
    case Quadrant.SW: return WallTypes.SWSolid;
    default: return WallTypes.SESolid;
   }
  }

  static bool PlaceWallAt(Vector3 cell, WallTypes type, string id)
  {
   if (styles.Count == 0 || wallsRoot == null)
   {
    return false;
   }
   var style = styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)];
   var prefabs = GetWalls(style.Template, style.Set, type);
   if (prefabs == null || prefabs.Count == 0)
   {
    return false;
   }
   var prefab = prefabs[style.Variant % prefabs.Count];
   if (prefab == null)
   {
    return false;
   }
   // No extra flip: the piece keeps the rotation the game's generator gives it, which faces the
   // textured side into the room.
   var instance = Object.Instantiate(prefab, cell + prefab.transform.position, Quaternion.identity, wallsRoot.transform);
   var marker = instance.GetComponent<WallTileMarker>();
   if (marker == null) marker = instance.AddComponent<WallTileMarker>();
   marker.Id = id;
   marker.StyleKey = WallLayoutStyleKey;
   marker.Type = (int)type;
   marker.Added = false;
   marker.SourcePrefab = prefab;
   marker.BaseWorldPosition = instance.transform.position;
   marker.BaseWorldRotation = instance.transform.rotation;
   wallMarkers.Add(marker);
   placedThisFill++;
   return true;
  }

  static void ApplyWallLayout()
  {
   WallLayout.EnsureLoaded();
   string styleKey = WallLayoutStyleKey;
   var liveIds = new HashSet<string>();
   for (int i = wallMarkers.Count - 1; i >= 0; i--)
   {
    var marker = wallMarkers[i];
    if (marker == null) { wallMarkers.RemoveAt(i); continue; }
    marker.BaseWorldPosition = marker.transform.position;
    marker.BaseWorldRotation = marker.transform.rotation;
    liveIds.Add(marker.Id);
    ApplyWallLayoutToMarker(marker);
   }

   foreach (var entry in WallLayout.ForStyle(styleKey))
   {
    if (entry == null || !entry.added || entry.removed || liveIds.Contains(entry.id)) continue;
    if (SpawnAddedWall(entry) != null) liveIds.Add(entry.id);
   }
  }

  static void ApplyWallLayoutToMarker(WallTileMarker marker)
  {
   if (marker == null) return;
   var entry = WallLayout.Get(marker.StyleKey, marker.Id);
   if (entry == null || entry.reset) return;
   if (entry.removed)
   {
    wallMarkers.Remove(marker);
    marker.gameObject.SetActive(false);
    Object.Destroy(marker.gameObject);
    return;
   }
   marker.transform.position = wallOrigin + new Vector3(entry.x, entry.y, entry.z);
   marker.transform.rotation = Quaternion.Euler(entry.rx, entry.ry, entry.rz);
  }

  static WallTileMarker SpawnAddedWall(WallLayoutEntry entry)
  {
   if (entry == null || wallsRoot == null || styles.Count == 0) return null;
   var style = styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)];
   var prefabs = GetWalls(style.Template, style.Set, (WallTypes)entry.type);
   if (prefabs == null || prefabs.Count == 0) return null;
   var prefab = prefabs[style.Variant % prefabs.Count];
   if (prefab == null) return null;
   Vector3 position = wallOrigin + new Vector3(entry.x, entry.y, entry.z);
   Quaternion rotation = Quaternion.Euler(entry.rx, entry.ry, entry.rz);
   var instance = Object.Instantiate(prefab, position, rotation, wallsRoot.transform);
   var marker = instance.GetComponent<WallTileMarker>();
   if (marker == null) marker = instance.AddComponent<WallTileMarker>();
   marker.Id = entry.id;
   marker.StyleKey = entry.style;
   marker.Type = entry.type;
   marker.Added = true;
   marker.SourcePrefab = prefab;
   marker.BaseWorldPosition = position;
   marker.BaseWorldRotation = rotation;
   wallMarkers.Add(marker);
   return marker;
  }

  public static WallTileMarker InsertWallAt(WallTileMarker source, Vector3 position)
  {
   if (source == null || source.SourcePrefab == null || wallsRoot == null) return null;
   var instance = Object.Instantiate(source.SourcePrefab, position, source.transform.rotation, wallsRoot.transform);
   instance.transform.localScale = source.transform.localScale;
   var marker = instance.GetComponent<WallTileMarker>();
   if (marker == null) marker = instance.AddComponent<WallTileMarker>();
   marker.Id = "added:" + System.Guid.NewGuid().ToString("N");
   marker.StyleKey = WallLayoutStyleKey;
   marker.Type = source.Type;
   marker.Added = true;
   marker.SourcePrefab = source.SourcePrefab;
   marker.BaseWorldPosition = instance.transform.position;
   marker.BaseWorldRotation = instance.transform.rotation;
   wallMarkers.Add(marker);
   WallLayout.SaveMarker(marker, true, false);
   WallLayout.SaveUser();
   return marker;
  }

  public static int FirstWallType() { return CycleWallType(-1, 1); }

  public static int CycleWallType(int current, int direction)
  {
   if (styles.Count == 0) return -1;
   var values = (WallTypes[])Enum.GetValues(typeof(WallTypes));
   if (values == null || values.Length == 0) return -1;
   int index = Array.IndexOf(values, (WallTypes)current);
   for (int attempt = 0; attempt < values.Length; attempt++)
   {
    index = (index + (direction >= 0 ? 1 : values.Length - 1)) % values.Length;
    if (values[index] == WallTypes.Open) continue;
    var style = styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)];
    var prefabs = GetWalls(style.Template, style.Set, values[index]);
    if (prefabs != null && prefabs.Count > 0 && prefabs[style.Variant % prefabs.Count] != null)
     return (int)values[index];
   }
   return -1;
  }

  public static string WallTypeName(int type)
  {
   return Enum.IsDefined(typeof(WallTypes), type) ? ((WallTypes)type).ToString() : "none";
  }

  public static WallTileMarker InsertWallTypeAt(int type, Vector3 position, Quaternion rotation)
  {
   if (wallsRoot == null || styles.Count == 0 || !Enum.IsDefined(typeof(WallTypes), type) || (WallTypes)type == WallTypes.Open) return null;
   var style = styles[Mathf.Clamp(wallStyleIndex, 0, styles.Count - 1)];
   var prefabs = GetWalls(style.Template, style.Set, (WallTypes)type);
   if (prefabs == null || prefabs.Count == 0) return null;
   var prefab = prefabs[style.Variant % prefabs.Count];
   if (prefab == null) return null;
   var instance = Object.Instantiate(prefab, position + prefab.transform.position, rotation, wallsRoot.transform);
   var marker = instance.GetComponent<WallTileMarker>();
   if (marker == null) marker = instance.AddComponent<WallTileMarker>();
   marker.Id = "added:" + Guid.NewGuid().ToString("N");
   marker.StyleKey = WallLayoutStyleKey;
   marker.Type = type;
   marker.Added = true;
   marker.SourcePrefab = prefab;
   marker.BaseWorldPosition = instance.transform.position;
   marker.BaseWorldRotation = instance.transform.rotation;
   wallMarkers.Add(marker);
   WallLayout.SaveMarker(marker, true, false);
   WallLayout.SaveUser();
   return marker;
  }

  public static void DeleteWall(WallTileMarker marker)
  {
   if (marker == null) return;
   WallLayout.SaveMarker(marker, marker.Added, true);
   WallLayout.SaveUser();
   wallMarkers.Remove(marker);
   marker.gameObject.SetActive(false);
   Object.Destroy(marker.gameObject);
  }

   static void BuildFallbackWalls(Transform root, Vector3 origin, float width)
  {
   float half = width * 0.5f;
   var wood = WoodMaterial();
   CreateBox(root, "FallbackNorth", origin + new Vector3(half, 1.6f, width + 0.2f),
    new Vector3(width, 3.2f, 0.4f), wood, true);
   CreateBox(root, "FallbackWest", origin + new Vector3(-0.2f, 1.6f, half),
    new Vector3(0.4f, 3.2f, width), wood, true);
   CreateBox(root, "FallbackEast", origin + new Vector3(width + 0.2f, 1.6f, half),
    new Vector3(0.4f, 3.2f, width), wood, true);
   float openingWidth = Mathf.Min(wallTile, width);
   float openingStart = Mathf.Clamp(ExitDoorLocalX - openingWidth * 0.5f, 0f, width);
   float openingEnd = Mathf.Clamp(ExitDoorLocalX + openingWidth * 0.5f, 0f, width);
   if (openingStart > 0.01f)
    CreateBox(root, "FallbackSouthLeft", origin + new Vector3(openingStart * 0.5f, 1.6f, -0.2f),
     new Vector3(openingStart, 3.2f, 0.4f), wood, true);
   if (openingEnd < width - 0.01f)
    CreateBox(root, "FallbackSouthRight", origin + new Vector3((openingEnd + width) * 0.5f, 1.6f, -0.2f),
     new Vector3(width - openingEnd, 3.2f, 0.4f), wood, true);
  }

  static void BuildSouthColliderWithDoorway(Transform parent, Vector3 origin, float width, float centerY)
  {
   float openingWidth = Mathf.Min(wallTile, width);
   float openingStart = Mathf.Clamp(ExitDoorLocalX - openingWidth * 0.5f, 0f, width);
   float openingEnd = Mathf.Clamp(ExitDoorLocalX + openingWidth * 0.5f, 0f, width);
   if (openingStart > 0.01f)
    CreateBox(parent, "SouthColliderLeft", origin + new Vector3(openingStart * 0.5f, centerY, -0.15f),
     new Vector3(openingStart, CeilingHeight, 0.3f), null, true);
   if (openingEnd < width - 0.01f)
    CreateBox(parent, "SouthColliderRight", origin + new Vector3((openingEnd + width) * 0.5f, centerY, -0.15f),
     new Vector3(width - openingEnd, CeilingHeight, 0.3f), null, true);
  }

  static void AddLight(Transform parent, Vector3 position, float range, float intensity)
  {
   var go = new GameObject("StorageLight");
   go.transform.SetParent(parent, true);
   go.transform.position = position;
   var light = go.AddComponent<Light>();
   light.type = LightType.Point;
   light.range = range;
   light.intensity = intensity;
   light.color = new Color(1f, 0.92f, 0.78f);
   light.shadows = LightShadows.Soft;
  }

  static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
  {
   var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
   go.name = name;
   go.transform.SetParent(parent, true);
   go.transform.position = position;
   go.transform.localScale = scale;
   var col = go.GetComponent<Collider>();
   if (!collider && col != null)
   {
    Object.Destroy(col);
   }
   var renderer = go.GetComponent<Renderer>();
   if (renderer != null)
   {
    if (material != null)
    {
     renderer.sharedMaterial = material;
    }
    else
    {
     renderer.enabled = false;
    }
   }
   return go;
  }

  static Material cream;
  static Material wood;

  static Material CreamMaterial()
  {
   if (cream == null)
   {
    cream = NewMaterial(new Color(0.82f, 0.76f, 0.63f, 1f));
   }
   return cream;
  }

  static Material WoodMaterial()
  {
   if (wood == null)
   {
    wood = NewMaterial(new Color(0.45f, 0.29f, 0.15f, 1f));
   }
   return wood;
  }

  static Material NewMaterial(Color color)
  {
   Shader shader = Shader.Find("Standard");
   if (shader == null)
   {
    shader = Shader.Find("Diffuse");
   }
   var material = shader != null ? new Material(shader) : null;
   if (material != null && material.HasProperty("_Color"))
   {
    material.color = color;
   }
   return material;
  }

  static bool TrySampleRoomSpawn(BuiltRoom room, Vector3 floorSpawn, float distance, out NavMeshHit hit)
  {
   if (NavMesh.SamplePosition(floorSpawn, out hit, distance, -1) &&
       hit.position.y <= floorSpawn.y + 1f)
   {
    return true;
   }
   // A bake that only produced the ceiling must not teleport the player onto the roof.
   hit = default(NavMeshHit);
   return false;
  }

  public static void BakeNavMesh(BuiltRoom room)
  {
   if (room == null || room.Surface == null)
   {
    return;
   }
   try
   {
    // Newly created colliders and transforms are not guaranteed to be visible to the
    // NavMesh collector until physics has synchronized them.
    Physics.SyncTransforms();

    // Bake with the same agent type the player's NavMeshAgent uses. A surface built with
    // the default agent type can produce a navmesh the player cannot use.
    var agent = Player.PlayerChar != null ? Player.PlayerChar.GetComponentInChildren<NavMeshAgent>(true) : null;
    int agentTypeID = agent != null ? agent.agentTypeID : 0;
    room.Surface.agentTypeID = agentTypeID;
    var settings = NavMesh.GetSettingsByID(agentTypeID);
    PermanentStoragePlugin.Log.LogInfo("Baking room NavMesh with agentTypeID=" + agentTypeID +
     " (player agent=" + (agent != null) + ", height=" + settings.agentHeight +
     ", radius=" + settings.agentRadius + ").");

    Vector3 floorSpawn = room.Spawn;
    room.Surface.BuildNavMesh();
    var triangulation = NavMesh.CalculateTriangulation();
    Vector3 min = Vector3.positiveInfinity;
    Vector3 max = Vector3.negativeInfinity;
    foreach (var vertex in triangulation.vertices)
    {
     min = Vector3.Min(min, vertex);
     max = Vector3.Max(max, vertex);
    }
    PermanentStoragePlugin.Log.LogInfo("Room NavMesh baked triangles=" + (triangulation.indices.Length / 3) +
     " vertices=" + triangulation.vertices.Length + " bounds=" + min + ".." + max + ".");
    if (TrySampleRoomSpawn(room, floorSpawn, 3f, out var hit))
    {
     room.Spawn = hit.position;
     PermanentStoragePlugin.Log.LogInfo("Room NavMesh walkable at spawn: " + room.Spawn + ".");
    }
    else
    {
     Physics.SyncTransforms();
     room.Surface.BuildNavMesh();
     if (TrySampleRoomSpawn(room, floorSpawn, 4f, out hit))
     {
      room.Spawn = hit.position;
      PermanentStoragePlugin.Log.LogInfo("Room NavMesh became walkable after a second bake: " + room.Spawn + ".");
     }
     else
     {
      room.Spawn = floorSpawn;
      PermanentStoragePlugin.Log.LogWarning("Storage room navmesh has no walkable point at the spawn; movement may not work.");
     }
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not build the storage room navmesh: " + ex.Message);
   }
  }
 }
}
