using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Cleverous.VaultInventory;
using Tableflip;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // Owns the fixed room, the village door, the stable chest records and the save file.
 // The manager survives scene loads; the room itself does not, and is rebuilt from the same
 // template every time the Grove town loads.
 public sealed class StorageRoomManager : MonoBehaviour
 {
  // Legacy saves used eleven fixed chest records; only the first five ever had physical chests.
  public const int LegacyChestCount = 11;
  const int ChestCells = 7;
  const int MaxPlacedChests = 24;
  public const float RoomSize = RoomBuilder.Cells * 4f;
  const float RoomY = 300f;
  const float RoomXZ = 4000f;
  // The Grove is a fixed town prefab, and this is the open spot beside the fishing dock where the
  // entrance belongs (dock is at roughly 27.2, -0.6, 5.8). The ground/NavMesh snap decides Y.
  static readonly Vector3 GroveEntranceSpot = new Vector3(32.7f, 0.5f, 9.5f);

  public static StorageRoomManager Instance { get; private set; }

  public bool HasEntrance => doorGo != null;
  public bool HasRoom => room != null;
  public bool IsInsideRoom => inside;
  public bool IsRepositionMode => repositionMode;
  public bool IsMovingChest => movingChest != null;
  public Vector3 EntranceReturnPosition => entranceReturn;
  public Vector3 PlayerPosition => Player.PlayerChar != null ? Player.PlayerChar.transform.position : Vector3.zero;
  public Vector3 EntrancePosition => doorGo != null ? doorGo.transform.position : Vector3.zero;
  public Vector3 DockPosition => dockSpot;
  public string EntranceHouseName => houseName;
  public string EntranceText => doorGo != null ? doorGo.name + " @ " + doorGo.transform.position : "none";

  StorageRoomFile file;
  BuiltRoom room;
  GameObject doorGo;
  StorageChest openChest;
  readonly List<GameObject> hiddenStashes = new List<GameObject>();
  readonly List<ItemSaveData> overflow = new List<ItemSaveData>();
  Vector3 returnPosition;
  Vector3 entranceReturn;
  Vector3 dockSpot;
  string houseName = "none";
  bool inside;
  bool transitioning;
  int setupGeneration;
  bool preSaveHooked;
  int interactableLayer;
  int tickCount;
  int lastTickFrame = -1;
  bool groveReported;
  StorageKeeper keeper;
  GameObject placementPreview;
  bool placingChest;
  bool repositionMode;
  StorageChest movingChest;
  Vector3 movingChestPosition;
  Quaternion movingChestRotation;
  bool movingChestActive;
  Vector3 placementLocal;
  float placementRotation;
  float placementLift;
  bool placementValid;
  string placementHint = "";
  float placementHintUntil;
  TMP_Text gameHintText;
  string gameHintTextValue;
  float nextGameHintLookup;

  public static void EnsureRuntime()
  {
   if (Instance != null)
   {
    return;
   }
   var go = new GameObject("FATE_PermanentStorageManager_Runtime");
   go.AddComponent<StorageRoomManager>();
   PermanentStoragePlugin.Log.LogInfo("Runtime storage manager created during gameplay.");
  }

  void Awake()
  {
   Instance = this;
   PermanentStoragePlugin.Manager = this;
   DontDestroyOnLoad(gameObject);
   SceneManager.sceneLoaded += OnSceneLoaded;
   PermanentStoragePlugin.Log.LogInfo("Storage manager awake. Scene=" + SceneManager.GetActiveScene().name + ".");
  }

  void Start()
  {
   PermanentStoragePlugin.Log.LogInfo("Storage manager start. Scene=" + SceneManager.GetActiveScene().name +
    " town=" + SingletonBehaviour<Town>.InstanceExists() + " player=" + (Player.PlayerChar != null) + ".");
   // If the plugin was loaded after the current scene was already initialised, sceneLoaded
   // will not fire for it. This catches that case.
   if (SingletonBehaviour<Town>.InstanceExists())
   {
    int generation = ++setupGeneration;
    StartCoroutine(SetupTownWhenReady(generation));
   }
  }

  // Called every frame by PermanentStoragePlugin.Update. This does not depend on the
  // manager's own Update callback, which was not running in the mod-manager profile.
  public void PluginTick()
  {
   // Both the plugin Update fallback and the Harmony Player.Update patch can call this
   // in the same frame. Ignore the duplicate tick.
   if (lastTickFrame == Time.frameCount)
   {
    return;
   }
   lastTickFrame = Time.frameCount;
   tickCount++;
   if (tickCount % 300 == 0)
   {
    WallStyleMenu.EnsureKeeperModelRegistered();
   }
   var scene = SceneManager.GetActiveScene();
    if (tickCount % 30 == 0 && scene.IsValid() &&
        scene.name.IndexOf("Dungeon", StringComparison.OrdinalIgnoreCase) >= 0)
    {
     StorageDoorAudio.TryCache();
    }
   if (!groveReported && scene.IsValid() &&
       scene.name.IndexOf("Grove", StringComparison.OrdinalIgnoreCase) >= 0)
   {
    groveReported = true;
    if (interactableLayer == 0)
    {
     try
     {
      interactableLayer = FindInteractableLayer();
     }
     catch (Exception ex)
     {
      PermanentStoragePlugin.Log.LogWarning("Could not determine the interaction layer; using Default. " + ex.Message);
      interactableLayer = 0;
     }
    }
    PermanentStoragePlugin.Log.LogInfo("Plugin tick detected Grove scene '" + scene.name +
     "'. Player=" + (Player.PlayerChar != null) + " town=" + SingletonBehaviour<Town>.InstanceExists() +
     " layer=" + interactableLayer + ".");
   }

   if (tickCount % 300 == 0)
   {
    PermanentStoragePlugin.Log.LogInfo("Plugin tick alive. scene=" + (scene.IsValid() ? scene.name : "invalid") +
     " player=" + (Player.PlayerChar != null) + " room=" + (room != null) + " inside=" + inside +
     " tick=" + tickCount + ".");
   }
   // Styles edited directly in the config file are picked up without reopening the room.
   if (tickCount % 30 == 0)
   {
    RoomBuilder.SyncStyleFromConfig();
   }
   // The room is a player-only space. If a pet was already inside when the room was entered
   // (for example a save restored while standing in it, or a summon cast inside), put it back
   // outside instead of letting it follow the player through the storage door.
   if (inside && tickCount % 30 == 0)
   {
    KeepPetsOut();
   }
   // Mimic-body chest placement and chest repositioning (started by the Storage Keeper).
   UpdateChestPlacement();
   // Safety net: a save made while standing in the room leaves the player standing in it with the
   // room treated as "outside" (and, on a fresh load, the room not built at all). The room is a
   // separate space, so the player is put back inside it rather than dropped into the village.
   if (tickCount % 60 == 0 && !inside && !transitioning && Player.PlayerChar != null)
   {
    Vector3 playerPosition = Player.PlayerChar.transform.position;
    if (InRoomBounds(playerPosition))
    {
     ResumeInsideRoom();
    }
   }

   bool f10 = KeyState.Pressed(KeyCode.F10, "f10Key");
   if (f10)
   {
    LogLocation("F10");
   }
  }

  // F10 records the exact coordinates in the log, next to the entrance, so a bad placement can
  // be reported without reading numbers off the screen.
  void LogLocation(string source)
  {
   var player = Player.PlayerChar;
   Vector3 p = player != null ? player.transform.position : Vector3.zero;
   PermanentStoragePlugin.Log.LogInfo("Location (" + source + "): player=(" + Fmt(p) + "), entrance=" +
    (doorGo != null ? "(" + Fmt(doorGo.transform.position) + ")" : "none") + ", dock=(" + Fmt(dockSpot) +
    "), house=" + houseName + ", scene=" + SceneManager.GetActiveScene().name + ".");
  }

  static string Fmt(Vector3 v)
  {
   return v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00");
  }

  void OnDestroy()
  {
   PermanentStoragePlugin.Log.LogInfo("Storage manager destroyed. Scene=" + SceneManager.GetActiveScene().name + ".");
   SceneManager.sceneLoaded -= OnSceneLoaded;
   if (preSaveHooked)
   {
    SaveData.RemovePreSaveMainThreadTask(OnPreSave);
    preSaveHooked = false;
   }
   CleanupRoom();
   if (Instance == this)
   {
    Instance = null;
   }
  }

  void OnApplicationQuit()
  {
   SnapshotOpenChest();
   SaveNow();
  }

  void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
  {
   PermanentStoragePlugin.Log.LogInfo("Scene loaded: '" + scene.name + "' (" + mode + ").");
   groveReported = false;
   int generation = ++setupGeneration;
   StartCoroutine(SetupTownWhenReady(generation));
  }

  IEnumerator SetupTownWhenReady(int generation)
  {
   PermanentStoragePlugin.Log.LogInfo("Setup coroutine started for scene '" + SceneManager.GetActiveScene().name +
    "' generation " + generation + ".");
   // Additive loads (UI scenes, for example) must not tear down a room the player is using.
   // A real town reload destroys the old door with the old scene, so it fails this check.
   if (SingletonBehaviour<Town>.InstanceExists() && room != null && room.Root != null)
   {
    yield break;
   }
   CleanupRoom();
   int attempts = 0;
   while (attempts++ < 600)
   {
    if (generation != setupGeneration)
    {
     yield break;
    }
    if (SingletonBehaviour<Town>.InstanceExists() && Player.PlayerChar != null)
    {
     break;
    }
    yield return null;
   }
   if (generation != setupGeneration)
   {
    yield break;
   }
   if (!SingletonBehaviour<Town>.InstanceExists() || Player.PlayerChar == null)
   {
    PermanentStoragePlugin.Log.LogWarning("Permanent storage setup timed out waiting for the town and player.");
    yield break;
   }
   if (SingletonBehaviour<Town>.Instance.RealmId != RealmData.RealmId.Grove)
   {
    PermanentStoragePlugin.Log.LogInfo("Permanent storage is only created in the Grove; current town is " +
     SingletonBehaviour<Town>.Instance.RealmId + ".");
    yield break;
   }
   PermanentStoragePlugin.Log.LogInfo("Grove town detected. Creating the permanent storage entrance by the fishing dock.");
   try
   {
    interactableLayer = FindInteractableLayer();
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not determine the interaction layer; using Default. " + ex.Message);
    interactableLayer = 0;
   }
   PermanentStoragePlugin.Log.LogInfo("Storage entrance interaction layer: " + interactableLayer + ".");
   // Town props can still be spawning when Town.Instance and the player exist, so the dock gets
   // a few seconds to appear before the entrance falls back to the player's position.
   for (int i = 0; i < 300 && !HasDockCandidate(); i++)
   {
    yield return null;
   }
   try
   {
    CreateVillageDoor();
   }
   catch (Exception ex)
   {
         PermanentStoragePlugin.Log.LogError("Could not create the storage entrance: " + ex);
   }
   // A save made inside the room restores the player at the room's coordinates. Rebuild the room
   // around them so loading that save puts them back in the room instead of in empty space.
   ResumeInsideRoom();
  }

  void CreateFallbackEntrance()
  {
   try
   {
    var player = Player.PlayerChar;
    if (player == null)
    {
     PermanentStoragePlugin.Log.LogError("No player is available for the fallback storage entrance.");
     return;
    }
    Vector3 position = player.transform.position + player.transform.forward * 2.5f;
    position.y = GroundHeight(position, player.transform.position.y);
    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = "FATE_StorageEntrance_Fallback";
    go.layer = interactableLayer >= 0 ? interactableLayer : 0;
    go.transform.position = position + Vector3.up * 0.6f;
    go.transform.localScale = new Vector3(1.4f, 1.2f, 1.0f);
    var renderer = go.GetComponent<Renderer>();
    if (renderer != null)
    {
     renderer.sharedMaterial = GoldMaterial();
    }
    var entrance = go.AddComponent<StorageRoomDoor>();
    entrance.Init(this);
    doorGo = go;
    entranceReturn = position;
    returnPosition = position;
    StorageChestFactory.AddEntranceExtras(go);
    PermanentStoragePlugin.Log.LogWarning("Using the guaranteed fallback storage entrance at the player spawn: " +
     go.transform.position + " layer " + go.layer + ".");
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogError("Could not create the fallback storage entrance: " + ex);
   }
  }

  void EnsureRecords()
  {
   string slotKey = StoragePersistence.SlotKey();
   if (file == null || file.slotKey != slotKey)
   {
    if (file != null)
    {
     SnapshotOpenChest();
     SaveNow();
    }
    file = StoragePersistence.Load(slotKey);
    EnsurePreSaveHook();
    PermanentStoragePlugin.Log.LogInfo("Loaded storage room data for save " + slotKey +
     " with " + (file.chests != null ? file.chests.Count : 0) + " chest record(s).");
   }
   if (file.chests == null)
   {
    file.chests = new List<StorageChestRecord>();
   }
   var seen = new HashSet<string>();
   var cleaned = new List<StorageChestRecord>();
   foreach (var record in file.chests)
   {
    if (record == null)
    {
     continue;
    }
    if (record.items == null)
    {
     record.items = new List<ItemSaveData>();
    }
    if (string.IsNullOrEmpty(record.id))
    {
     record.id = NextChestId(cleaned);
    }
    if (!seen.Add(record.id))
    {
     PermanentStoragePlugin.Log.LogWarning("Dropping duplicate storage chest record " + record.id + ".");
     continue;
    }
    cleaned.Add(record);
   }
   file.chests = cleaned;
   NormalizeChestRotations();
  }

  void EnsurePreSaveHook()
  {
   if (preSaveHooked) return;
   try
   {
    SaveData.AddPreSaveMainThreadTask(OnPreSave);
    preSaveHooked = true;
    PermanentStoragePlugin.Log.LogInfo("Registered the storage snapshot with the game's pre-save hook.");
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not register the storage pre-save hook: " + ex.Message);
   }
  }

  // Older versions aimed every chest at the room centre before adding the player's rotation.
  // Remove that centre-facing component once so existing chests keep the rotation the player
  // actually chose, and store future rotations as absolute room-local yaw.
  void NormalizeChestRotations()
  {
   if (file == null || file.version >= 3)
   {
    return;
   }
   bool changed = false;
   foreach (var record in file.chests)
   {
    if (record == null || !record.placed)
    {
     continue;
    }
    float autoYaw = LegacyCenterFacingYaw(new Vector3(record.localX, 0f, record.localZ));
    record.rotationY = Mathf.Repeat(record.rotationY - autoYaw, 360f);
    changed = true;
   }
   file.version = 3;
   if (changed)
   {
    SaveNow();
    PermanentStoragePlugin.Log.LogInfo("Removed the old centre-facing rotation from placed storage chests.");
   }
  }

  string NextChestId(List<StorageChestRecord> records)
  {
   int highest = 0;
   foreach (var chest in records)
   {
    if (chest == null)
    {
     continue;
    }
    int separator = chest.id.LastIndexOf('_');
    int number;
    if (separator >= 0 && int.TryParse(chest.id.Substring(separator + 1), out number) && number > highest)
    {
     highest = number;
    }
   }
   return "storage_chest_" + (highest + 1).ToString("00");
  }

  // Saves from before 0.7.22 created eleven fixed records, five of which became physical chests
  // along the north wall. Empty records are dropped so an updated room starts empty; any record
  // that still holds items keeps them by becoming a placed chest at its old anchor.
  void MigrateLegacyChests()
  {
   if (file == null || room == null)
   {
    return;
   }
   bool changed = false;
   var kept = new List<StorageChestRecord>();
   foreach (var record in file.chests)
   {
    if (record == null)
    {
     changed = true;
     continue;
    }
    if (record.placed)
    {
     kept.Add(record);
     continue;
    }
    if (record.items != null && record.items.Count > 0)
    {
     Vector3 local;
     float yaw;
     LegacyChestPosition(record, out local, out yaw);
     record.placed = true;
     record.unlocked = true;
     record.unlockMethod = "legacy";
     record.localX = local.x;
     record.localY = local.y;
     record.localZ = local.z;
     record.rotationY = yaw;
     kept.Add(record);
     PermanentStoragePlugin.Log.LogInfo("Migrated legacy storage chest " + record.id + " with " +
      record.items.Count + " item(s) to room-local " + local + ".");
    }
    changed = true;
   }
   file.chests = kept;
   file.version = 3;
   if (changed)
   {
    SaveNow();
   }
  }

  void LegacyChestPosition(StorageChestRecord record, out Vector3 local, out float yaw)
  {
   int index = 0;
   int separator = record.id.LastIndexOf('_');
   int parsed;
   if (separator >= 0 && int.TryParse(record.id.Substring(separator + 1), out parsed) && parsed > 0)
   {
    index = parsed - 1;
   }
   if (index >= 0 && index < room.Chests.Count)
   {
    var anchor = room.Chests[index];
    local = anchor.LocalPosition;
    yaw = anchor.LocalRotation.eulerAngles.y;
    return;
   }
   int extra = Mathf.Max(0, index - room.Chests.Count);
   local = new Vector3(3f + (extra % 5) * 4.2f, 0f, 13f - (extra / 5) * 3.2f);
   yaw = 180f;
  }

  void SpawnPlacedChests()
  {
   if (room == null || room.Root == null || file == null)
   {
    return;
   }
   int count = 0;
   foreach (var record in file.chests)
   {
    if (record == null || !record.placed)
    {
     continue;
    }
    if (SpawnChest(record))
    {
     count++;
     WallStyleMenu.RegisterChestName(record);
    }
   }
   PermanentStoragePlugin.Log.LogInfo("Placed " + count + " player-placed storage chest(s) in the room.");
  }

  bool SpawnChest(StorageChestRecord record)
  {
   if (room == null || room.Root == null || record == null)
   {
    return false;
   }
   var local = new Vector3(record.localX, record.localY, record.localZ);
   if (local.x < 0.5f || local.z < 0.5f || local.x > RoomSize - 0.5f || local.z > RoomSize - 0.5f)
   {
    PermanentStoragePlugin.Log.LogWarning("Storage chest " + record.id + " has an invalid position " + local + "; it was not placed.");
    return false;
   }
   Vector3 position = room.Root.transform.TransformPoint(local);
   Quaternion rotation = room.Root.transform.rotation * Quaternion.Euler(0f, record.rotationY, 0f);
   StorageChestFactory.Create(this, record, position, rotation, interactableLayer, room.Root.transform);
   return true;
  }

  int PlacedChestCount()
  {
   int count = 0;
   if (file != null && file.chests != null)
   {
    foreach (var chest in file.chests)
    {
     if (chest != null && chest.placed)
     {
      count++;
     }
    }
   }
   return count;
  }

  internal static string GetChestMenuName(string chestId)
  {
   var manager = Instance;
   if (manager != null)
   {
    manager.EnsureRecords();
    var record = manager.file.chests.Find(chest => chest != null && chest.id == chestId);
    if (record != null && !string.IsNullOrWhiteSpace(record.customName)) return record.customName.Trim();
   }
   return StorageChest.DefaultChestName(new StorageChestRecord { id = chestId ?? string.Empty });
  }

  internal static void SetChestMenuName(string chestId, string value)
  {
   var manager = Instance;
   if (manager == null || string.IsNullOrEmpty(chestId)) return;
   manager.EnsureRecords();
   var record = manager.file.chests.Find(chest => chest != null && chest.id == chestId && chest.placed);
   if (record == null) return;
   string name = (value ?? string.Empty).Trim();
   if (name.Length > 24) name = name.Substring(0, 24).Trim();
   record.customName = name;
   if (manager.room != null && manager.room.Root != null)
   {
    foreach (var chest in manager.room.Root.GetComponentsInChildren<StorageChest>(true))
    {
     if (chest != null && chest.Record == record)
     {
      chest.RefreshVisual();
      break;
     }
    }
   }
   manager.SaveNow();
  }

  void CreateKeeper()
  {
   if (room == null || room.Root == null)
   {
    return;
   }
   // The town NPCs may have loaded after the menu module first registered; give the
   // model row a chance to appear now that the list can be built.
   StorageKeeperOptions.EnsureModels();
   WallStyleMenu.EnsureKeeperModelRegistered();
   Vector3 local = new Vector3(6.2f, 0.02f, 2.4f);
   Vector3 position = room.Root.transform.TransformPoint(local);
   try
   {
    string name = PermanentStoragePlugin.KeeperName != null && !string.IsNullOrWhiteSpace(PermanentStoragePlugin.KeeperName.Value)
     ? PermanentStoragePlugin.KeeperName.Value.Trim() : "Storage Keeper";
    int model = StorageKeeperOptions.Resolve(
     PermanentStoragePlugin.KeeperModelName != null ? PermanentStoragePlugin.KeeperModelName.Value : null,
     PermanentStoragePlugin.KeeperModel != null ? PermanentStoragePlugin.KeeperModel.Value : 0);
    keeper = StorageKeeperFactory.Create(this, position, room.Root.transform.rotation, interactableLayer, room.Root.transform, name, model);
   }
   catch (Exception ex)
   {
    keeper = null;
    PermanentStoragePlugin.Log.LogError("Could not create the Storage Keeper: " + ex);
   }
  }

  // Live changes from FATE Mod Menu. The name updates the prompt and the nameplate in place;
  // a model change rebuilds only the Keeper, leaving every chest and the room untouched.
  public void ApplyKeeperName(string displayName)
  {
   if (keeper == null)
   {
    CreateKeeper();
    return;
   }
   keeper.SetName(displayName);
  }

  public void ApplyKeeperModel(int modelIndex)
  {
   if (room == null || room.Root == null)
   {
    return;
   }
   if (keeper != null)
   {
    Object.Destroy(keeper.gameObject);
    keeper = null;
   }
   CreateKeeper();
  }

  string KeeperDisplayName()
  {
   return PermanentStoragePlugin.KeeperName != null && !string.IsNullOrWhiteSpace(PermanentStoragePlugin.KeeperName.Value)
    ? PermanentStoragePlugin.KeeperName.Value.Trim() : "Storage Keeper";
  }

  int FindInteractableLayer()
  {
   var interactables = Object.FindObjectsOfType<InteractableSceneObject>();
   foreach (var interactable in interactables)
   {
    if (interactable != null)
    {
     return interactable.gameObject.layer;
    }
   }
   int layer = LayerMask.NameToLayer("Interactable");
   return layer >= 0 ? layer : 0;
  }

  void CreateVillageDoor()
  {
   // The wall styles come from the Grove template, and the mod menu row needs them before the
   // player ever opens the room.
   RoomBuilder.InitStyles(RoomBuilder.FindGroveTemplate());
   WallStyleMenu.EnsureRegistered();
   if (doorGo != null)
   {
    Object.Destroy(doorGo);
    doorGo = null;
   }
   Vector3 dockPosition;
   if (!TryFindDock(out dockPosition))
   {
    var player = Player.PlayerChar;
    if (player == null)
    {
     PermanentStoragePlugin.Log.LogWarning("No fishing dock or player is available; the storage entrance was skipped.");
     return;
    }
    dockPosition = player.transform.position;
    PermanentStoragePlugin.Log.LogWarning("No fishing dock was found; placing the storage entrance beside the player instead.");
   }

   Transform house = FindHouseBehindDock(dockPosition);
   Vector3 doorPosition;
   Vector3 outward;
   if (house != null)
   {
    Bounds bounds = GetBounds(house);
    outward = dockPosition - bounds.center;
    outward.y = 0f;
    if (outward.sqrMagnitude < 0.01f)
    {
     outward = house.forward;
     outward.y = 0f;
    }
    outward.Normalize();
    // Put the chest a little out from the wall so its collider is in the open and its
    // interaction prompt is reachable without standing inside the house geometry.
    doorPosition = bounds.ClosestPoint(dockPosition) + outward * 1.75f;
    float ground = GroundHeight(doorPosition, bounds.min.y);
    doorPosition.y = ground + 0.05f;
    returnPosition = doorPosition + outward * 1.5f;
    returnPosition.y = ground + 0.05f;
    PermanentStoragePlugin.Log.LogInfo("Storage room entrance attached to the nearest house behind the fishing dock: " + house.name + ".");
   }
   else
   {
    // The Grove town always has both a dock and a house; this fallback keeps the mod usable
    // if a future build renames those objects.
    var town = SingletonBehaviour<Town>.Instance;
    Vector3 spawn = town != null && town.possibleSpawnPoints != null && town.possibleSpawnPoints.Length > 0
     ? town.possibleSpawnPoints[0].position
     : Player.PlayerChar.transform.position;
    outward = Vector3.forward;
    doorPosition = spawn + new Vector3(2f, 0.05f, 2f);
    returnPosition = spawn;
    PermanentStoragePlugin.Log.LogWarning("Could not identify the house behind the fishing dock; the storage entrance was placed near the town spawn.");
   }

   dockSpot = dockPosition;
   houseName = house != null ? house.name : "none";

   Vector3 manual = Vector3.zero;
   Vector3 offset;
   bool manualSet = PermanentStoragePlugin.EntrancePosition != null &&
                    TryParseVector(PermanentStoragePlugin.EntrancePosition.Value, out manual);
   if (!manualSet)
   {
    // The house-facing estimate lands on the porch line in this layout, so the known open spot
    // beside the dock is used instead. If a future town layout moves the ground out from under
    // it, the house-based spot stays as the fallback.
    if (NavMesh.SamplePosition(GroveEntranceSpot, out var walkable, 2f, NavMesh.AllAreas))
    {
     doorPosition = walkable.position;
     float fixedGround = GroundHeight(doorPosition, doorPosition.y);
     doorPosition.y = fixedGround + 0.05f;
     Vector3 returnSpot = doorPosition + outward * 1.5f;
     if (NavMesh.SamplePosition(returnSpot, out var back, 2f, NavMesh.AllAreas))
     {
      returnSpot = back.position;
     }
     returnPosition = returnSpot;
     returnPosition.y = doorPosition.y;
     PermanentStoragePlugin.Log.LogInfo("Using the known Grove entrance spot beside the dock: " +
      Fmt(doorPosition) + ".");
    }
    else
    {
     PermanentStoragePlugin.Log.LogWarning("The known Grove entrance spot is not on the town NavMesh; keeping the house-based spot at " +
      Fmt(doorPosition) + ".");
    }
   }

   // Entrance.ManualPosition wins when it parses; otherwise Offset nudges the chosen spot.
   if (manualSet)
   {
    doorPosition = manual;
    float manualGround = GroundHeight(doorPosition, doorPosition.y);
    doorPosition.y = manualGround + 0.05f;
    returnPosition = doorPosition + outward * 1.5f;
    returnPosition.y = doorPosition.y;
    PermanentStoragePlugin.Log.LogInfo("Using Entrance.ManualPosition " + Fmt(manual) +
     " (snapped to " + Fmt(doorPosition) + ").");
   }
   else if (PermanentStoragePlugin.EntranceOffset != null &&
            TryParseVector(PermanentStoragePlugin.EntranceOffset.Value, out offset))
   {
    doorPosition += offset;
    returnPosition += offset;
    PermanentStoragePlugin.Log.LogInfo("Entrance.Offset " + Fmt(offset) +
     " applied; entrance at " + Fmt(doorPosition) + ".");
   }

   doorGo = StorageChestFactory.CreateEntrance(this, doorPosition,
    Quaternion.LookRotation(outward, Vector3.up), interactableLayer, null).gameObject;
   entranceReturn = returnPosition;
   PermanentStoragePlugin.Log.LogInfo("Storage entrance placed at (" + Fmt(doorPosition) +
    ") on layer " + (doorGo != null ? doorGo.layer.ToString() : "none") +
    "); dock=(" + Fmt(dockPosition) + "), house=" + houseName +
    ", exit returns to (" + Fmt(entranceReturn) + ").");
  }

  static bool TryParseVector(string text, out Vector3 value)
  {
   value = Vector3.zero;
   if (string.IsNullOrWhiteSpace(text))
   {
    return false;
   }
   var parts = text.Split(',');
   if (parts.Length != 3)
   {
    return false;
   }
   float x, y, z;
   if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
       !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
       !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
   {
    return false;
   }
   value = new Vector3(x, y, z);
   return true;
  }

  static bool HasDockCandidate()
  {
   if (GameObject.Find("P_FishingDock_01") != null)
   {
    return true;
   }
   return Object.FindObjectsOfType<FishingSpotInteractable>().Length > 0;
  }

  // The dock is looked up by name first because it is part of the town prefab; the fishing
  // spots are the fallback for a build that renames the dock object.
  static bool TryFindDock(out Vector3 position)
  {
   var dock = GameObject.Find("P_FishingDock_01");
   if (dock == null)
   {
    foreach (var candidate in Object.FindObjectsOfType<Transform>())
    {
     if (candidate != null &&
         candidate.name.IndexOf("FishingDock", StringComparison.OrdinalIgnoreCase) >= 0)
     {
      dock = candidate.gameObject;
      break;
     }
    }
   }
   if (dock != null)
   {
    position = dock.transform.position;
    return true;
   }
   var spots = Object.FindObjectsOfType<FishingSpotInteractable>();
   if (spots != null && spots.Length > 0 && spots[0] != null)
   {
    position = spots[0].transform.position;
    return true;
   }
   position = Vector3.zero;
   return false;
  }

  Transform FindHouseBehindDock(Vector3 dockPosition)
  {
   Transform best = null;
   float bestDistance = float.MaxValue;
   var transforms = Object.FindObjectsOfType<Transform>();
   foreach (var candidate in transforms)
   {
    if (candidate == null || !candidate.name.StartsWith("P_House_", StringComparison.OrdinalIgnoreCase))
    {
     continue;
    }
    float distance = (GetBounds(candidate).center - dockPosition).sqrMagnitude;
    if (distance < bestDistance)
    {
     bestDistance = distance;
     best = candidate;
    }
   }
   return best;
  }

  static Bounds GetBounds(Transform target)
  {
   var renderers = target.GetComponentsInChildren<Renderer>();
   if (renderers.Length == 0)
   {
    return new Bounds(target.position, Vector3.one * 4f);
   }
   Bounds bounds = renderers[0].bounds;
   for (int i = 1; i < renderers.Length; i++)
   {
    bounds.Encapsulate(renderers[i].bounds);
   }
   return bounds;
  }

  static float GroundHeight(Vector3 position, float fallback)
  {
   int mask = LayerMask.GetMask("Player Walkable", "Default");
   if (mask == 0)
   {
    mask = ~0;
   }
   // Start just above the house's lowest point, not 12 m up: a tall roof would otherwise be
   // hit first and the door would sit on the roof.
   if (Physics.Raycast(new Vector3(position.x, fallback + 1.5f, position.z), Vector3.down,
       out var hit, 6f, mask))
   {
    return hit.point.y;
   }
   return fallback;
  }

  static Material doorMaterial;
  static Material goldMaterial;

  static Material DoorMaterial(Transform house)
  {
   if (house != null)
   {
    var renderer = house.GetComponentInChildren<Renderer>();
    if (renderer != null && renderer.sharedMaterial != null)
    {
     return renderer.sharedMaterial;
    }
   }
   if (doorMaterial == null)
   {
    doorMaterial = NewMaterial(new Color(0.38f, 0.23f, 0.12f, 1f));
   }
   return doorMaterial;
  }

  static Material GoldMaterial()
  {
   if (goldMaterial == null)
   {
    goldMaterial = NewMaterial(new Color(0.91f, 0.74f, 0.28f, 1f));
   }
   return goldMaterial;
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

  public void EnterRoom()
  {
   EnterRoom(default(Vector3), false);
  }

  // useReturnTo lets the Grove entrance send the player back to its own doorstep instead of
  // wherever the debug key happened to be pressed.
  public void EnterRoom(Vector3 returnTo, bool useReturnTo)
  {
   if (inside)
   {
    return;
   }
   PermanentStoragePlugin.Log.LogInfo("EnterRoom called. Scene=" + SceneManager.GetActiveScene().name + ".");
   if (Player.PlayerChar == null)
   {
    PermanentStoragePlugin.Log.LogWarning("EnterRoom was called before the player character exists.");
    return;
   }
   if (room == null)
   {
    BuildRoom();
   }
   if (room == null)
   {
    PermanentStoragePlugin.Log.LogError("The storage room could not be built.");
    return;
   }
   returnPosition = useReturnTo ? returnTo : Player.PlayerChar.transform.position;
   if (transitioning)
   {
    return;
   }
   transitioning = true;
   EnterRoomNow();
  }

  void EnterRoomNow()
  {
   // TeleportToPosition moves every pet, party member and ward with the player. The storage
   // room is player-only, so move just the player and leave the pets at the entrance.
   TeleportPlayerOnly(room.Spawn);
   inside = true;
   ApplyRoomAmbience();
   KeepPetsOut();
   transitioning = false;
   PermanentStoragePlugin.Log.LogInfo("Entered the storage room at " + room.Spawn + ".");
  }

  public void ExitRoom()
  {
   // Also allow the exit when a loaded save left the player standing in the room with the room
   // state cold, so the doorway is never a dead end.
   if (!inside && !(Player.PlayerChar != null && InRoomBounds(Player.PlayerChar.transform.position)))
   {
    return;
   }
   if (Player.PlayerChar == null)
   {
    return;
   }
   if (transitioning)
   {
    return;
   }
   ExitRoomNow(ResolveExitPosition());
  }

  void ExitRoomNow(Vector3 destination)
  {
   EndChestPlacement();
   repositionMode = false;
   HideChestHintUI();
   transitioning = true;
   Teleport(destination);
   inside = false;
   RestoreTownAmbience();
   transitioning = false;
   PermanentStoragePlugin.Log.LogInfo("Left the storage room and returned to " + destination + ".");
  }

  // The saved return point can be stale, empty, or inside the room itself (for example after
  // quitting while inside). Any of those would trap the player in the room, so it falls back to
  // the village entrance and then to the town spawn point.
  Vector3 ResolveExitPosition()
  {
   Vector3 destination = returnPosition;
   if (InRoomBounds(destination) || destination.sqrMagnitude < 0.01f)
   {
    destination = OutsideRoomDoor();
   }
   return destination;
  }

  // Any exit that lands inside the room would put the player straight back in it, so the
  // doorway outside the entrance is used instead when the saved return point is unusable.
  Vector3 OutsideRoomDoor()
  {
   if (doorGo != null && HasEntrance && !InRoomBounds(entranceReturn))
   {
    return entranceReturn;
   }
   if (doorGo != null && HasEntrance)
   {
    return doorGo.transform.position + doorGo.transform.forward * 2f;
   }
   Vector3 town = TownSpawn();
   return InRoomBounds(town) ? Player.PlayerChar.transform.position : town;
  }

  // The room sits at a fixed spot, 24 m across, so its footprint is known even before the room
  // is built. Used to recognise a player restored inside the room by a save.
  static bool InRoomBounds(Vector3 position)
  {
   const float roomSize = RoomBuilder.Cells * 4f;
   const float margin = 1.5f;
   return position.x >= RoomXZ - margin && position.x <= RoomXZ + roomSize + margin &&
          position.z >= RoomXZ - margin && position.z <= RoomXZ + roomSize + margin &&
          position.y >= RoomY - 2f && position.y <= RoomY + 12f;
  }

  // Puts the player back inside the room, building it first when a save restored them there.
  void ResumeInsideRoom()
  {
   if (inside || Player.PlayerChar == null)
   {
    return;
   }
   if (!InRoomBounds(Player.PlayerChar.transform.position))
   {
    return;
   }
   if (room == null || room.Root == null)
   {
    BuildRoom();
   }
   if (room == null || room.Root == null)
   {
    PermanentStoragePlugin.Log.LogWarning("Player was inside the storage room but it could not be rebuilt; moving them to the village entrance.");
    Teleport(doorGo != null && HasEntrance ? entranceReturn : TownSpawn());
    return;
   }
   returnPosition = doorGo != null && HasEntrance ? entranceReturn : TownSpawn();
   inside = true;
   ApplyRoomAmbience();
   TeleportPlayerOnly(room.Spawn);
   KeepPetsOut();
   PermanentStoragePlugin.Log.LogWarning("Player loaded inside the storage room; resuming inside it, exit returns to " + returnPosition + ".");
  }

  // Moves any pet that is standing in the storage room to the player's return point. This is
  // deliberately limited to pets: party members and wards keep their existing behaviour.
  void KeepPetsOut()
  {
   var player = Player.PlayerChar;
   if (player == null)
   {
    return;
   }
   int petCount = player.GetPetCount();
   for (int i = 0; i < petCount; i++)
   {
    var pet = player.GetPet(i);
    if (pet == null || pet.transform == null || !InRoomBounds(pet.transform.position))
    {
     continue;
    }
    Vector3 safe = PetWaitingSpot();
    if (InRoomBounds(safe))
    {
     continue;
    }
    pet.SetPositionProper(safe);
    PermanentStoragePlugin.Log.LogInfo("Moved pet " + pet.CharacterName + " out of the storage room to " + safe + ".");
   }
  }

  Vector3 PetWaitingSpot()
  {
   if (!InRoomBounds(returnPosition) && returnPosition.sqrMagnitude > 0.01f)
   {
    return returnPosition;
   }
   if (!InRoomBounds(entranceReturn) && entranceReturn.sqrMagnitude > 0.01f)
   {
    return entranceReturn;
   }
   return TownSpawn();
  }

  static Vector3 TownSpawn()
  {
   var town = SingletonBehaviour<Town>.InstanceExists() ? SingletonBehaviour<Town>.Instance : null;
   if (town != null && town.possibleSpawnPoints != null && town.possibleSpawnPoints.Length > 0)
   {
    return town.possibleSpawnPoints[0].position;
   }
   return Player.PlayerChar != null ? Player.PlayerChar.transform.position : Vector3.zero;
  }

  void BuildRoom()
  {
   try
   {
    if (interactableLayer == 0)
    {
     try
     {
      interactableLayer = FindInteractableLayer();
     }
     catch (Exception ex)
     {
      PermanentStoragePlugin.Log.LogWarning("Could not find an interaction layer; using Default. " + ex.Message);
     }
    }
    var origin = new Vector3(RoomXZ, RoomY, RoomXZ);
    EnsureRecords();
    PermanentStoragePlugin.Log.LogInfo("Building 3-cell dungeon room from existing Grove assets at " + origin + ".");
    room = RoomBuilder.Build(origin, interactableLayer, this);
    MigrateLegacyChests();
    SpawnPlacedChests();
    CreateKeeper();
    try
    {
     RoomBuilder.BakeNavMesh(room);
    }
    catch (Exception ex)
    {
     // A NavMesh failure must not discard the room that was just built.
     PermanentStoragePlugin.Log.LogWarning("Room NavMesh bake failed; using the room without a baked NavMesh. " + ex.Message);
    }
    PermanentStoragePlugin.Log.LogInfo("Storage room ready. Spawn=" + room.Spawn + ".");
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogError("Could not build the test room: " + ex);
    if (room != null && room.Root != null)
    {
     Object.Destroy(room.Root);
    }
    room = null;
   }
  }

  public void TalkToKeeper()
  {
   if (!inside || file == null)
   {
    return;
   }
   if (placingChest)
   {
    SetPlacementHint("Left-click to place or move the chest, or right-click/Esc to cancel.");
    return;
   }
   if (repositionMode)
   {
    SetPlacementHint("Reposition mode is active. Interact with a chest to move it, or press Esc to finish.");
    return;
   }
   bool hasMimic = MimicChestItem.HasOne();
   int placed = PlacedChestCount();
   bool canPlace = hasMimic && placed < MaxPlacedChests;
   bool canMove = placed > 0;
   if (!canPlace && !canMove)
   {
    string noChest = placed >= MaxPlacedChests
     ? "This room has no more space for chests."
     : "Bring me the body of a Mimic. They sometimes leave one behind when they fall.\n\nI will turn it into a storage chest for this room, free of charge.";
    ShowInfo(KeeperDisplayName().ToUpperInvariant() + "\n\n" + noChest);
    return;
   }
   if (!SingletonBehaviour<PromptUI>.InstanceExists())
   {
    return;
   }
   if (canPlace && canMove)
   {
    SingletonBehaviour<PromptUI>.Instance.ShowPromptRaw(
     KeeperDisplayName().ToUpperInvariant() + "\n\nThat Mimic body will make a fine chest. Place a new one, or move a chest you already placed?\n\nIt costs nothing; the body is the cost.",
     delegate (bool place)
     {
      if (place) BeginChestPlacement(); else BeginChestReposition();
     }, twoButtons: true, positiveOverride: "Place chest", negativeOverride: "Reposition");
    return;
   }
   if (canPlace)
   {
    SingletonBehaviour<PromptUI>.Instance.ShowPromptRaw(
     KeeperDisplayName().ToUpperInvariant() + "\n\nThat Mimic body will make a fine chest. Place it wherever you like in this room.\n\nIt costs nothing; the body is the cost.",
     delegate (bool place)
     {
      if (place) BeginChestPlacement();
     }, twoButtons: true, positiveOverride: "Place chest", negativeOverride: "Not now");
    return;
   }
   string reason = hasMimic
    ? "This room is full, but I can help you move a chest you already placed."
    : "Bring me the body of a Mimic for a new chest, or I can help you move one you already placed.";
   SingletonBehaviour<PromptUI>.Instance.ShowPromptRaw(
    KeeperDisplayName().ToUpperInvariant() + "\n\n" + reason,
    delegate (bool move)
    {
     if (move) BeginChestReposition();
    }, twoButtons: true, positiveOverride: "Reposition", negativeOverride: "Not now");
  }

  public void BeginChestPlacement()
  {
   if (placingChest || !inside || room == null || room.Root == null)
   {
    return;
   }
   if (!MimicChestItem.HasOne())
   {
    ShowInfo("You need a Mimic body to place a chest.\n\nKill Mimics in dungeons for a chance to find one.");
    return;
   }
   if (PlacedChestCount() >= MaxPlacedChests)
   {
    ShowInfo("This room has no more space for chests.");
    return;
   }
   repositionMode = false;
   var player = Player.PlayerChar;
   Vector3 start = player != null ? player.transform.position : room.Spawn;
   placementLocal = room.Root.transform.InverseTransformPoint(start);
   placementLocal.x = Mathf.Clamp(placementLocal.x, 1.6f, RoomSize - 1.6f);
   placementLocal.z = Mathf.Clamp(placementLocal.z, 2.6f, RoomSize - 1.6f);
   placementLocal.y = 0.02f;
   placementRotation = 0f;
   ChestRotationInput.EnsureRegistered();
   Vector3 world = room.Root.transform.TransformPoint(placementLocal);
   placementPreview = StorageChestFactory.CreatePlacementPreview(room.Root.transform, world, 0);
   placementLift = placementPreview != null ? placementPreview.transform.position.y - world.y : 0f;
   placementValid = false;
   placingChest = true;
   SetPlacementHint("Left-click to place the chest. R rotates it 90 degrees (rebindable in FATE Rebind). Right-click or Esc cancels.");
   PermanentStoragePlugin.Log.LogInfo("Chest placement started at room-local " + placementLocal + ".");
  }

  public void BeginChestReposition()
  {
   if (!inside || room == null || room.Root == null)
   {
    return;
   }
   if (placingChest)
   {
    CancelChestPlacement("Chest placement cancelled.");
   }
   if (PlacedChestCount() == 0)
   {
    ShowInfo("There are no placed chests to move.");
    return;
   }
   repositionMode = true;
   SetPlacementHint("Reposition mode: interact with a chest to pick it up. R rotates, Esc finishes.");
   PermanentStoragePlugin.Log.LogInfo("Chest reposition mode started.");
  }

  // Called by StorageChest.Interact while reposition mode is active. The selected chest is
  // hidden and replaced with the same translucent preview used for new chest placement.
  public bool TryBeginChestReposition(StorageChest chest)
  {
   if (!repositionMode || placingChest || chest == null || chest.Record == null ||
       room == null || room.Root == null)
   {
    return false;
   }
   movingChest = chest;
   movingChestPosition = chest.transform.position;
   movingChestRotation = chest.transform.rotation;
   movingChestActive = chest.gameObject.activeSelf;
   chest.gameObject.SetActive(false);
   placementLocal = new Vector3(chest.Record.localX, chest.Record.localY, chest.Record.localZ);
   placementRotation = Mathf.Repeat(chest.Record.rotationY, 360f);
   ChestRotationInput.EnsureRegistered();
   Vector3 world = room.Root.transform.TransformPoint(placementLocal);
   placementPreview = StorageChestFactory.CreatePlacementPreview(room.Root.transform, world, 0);
   placementLift = placementPreview != null ? placementPreview.transform.position.y - world.y : 0f;
   if (placementPreview != null)
   {
    placementPreview.transform.rotation = room.Root.transform.rotation * Quaternion.Euler(0f, placementRotation, 0f);
   }
   placementValid = IsPlacementValid(placementLocal, chest.Record);
   placingChest = true;
   SetPlacementHint("Move the chest with the mouse. Left-click to place it, R rotates, Esc cancels.");
   PermanentStoragePlugin.Log.LogInfo("Repositioning " + chest.Record.id + ".");
   return true;
  }

  void CommitChestReposition()
  {
   var chest = movingChest;
   if (chest == null || chest.Record == null || room == null || room.Root == null)
   {
    CancelChestPlacement("Chest move cancelled.");
    return;
   }
   var record = chest.Record;
   record.localX = placementLocal.x;
   record.localY = placementLocal.y;
   record.localZ = placementLocal.z;
   record.rotationY = Mathf.Repeat(placementRotation, 360f);
   chest.transform.SetPositionAndRotation(room.Root.transform.TransformPoint(placementLocal),
    room.Root.transform.rotation * Quaternion.Euler(0f, placementRotation, 0f));
   movingChest = null;
   chest.gameObject.SetActive(movingChestActive);
   SaveNow();
   EndChestPlacement();
   SetPlacementHint("Chest moved. Interact with another chest to move it, or press Esc to finish.");
   PermanentStoragePlugin.Log.LogInfo("Moved " + record.id + " to room-local " + placementLocal + ".");
  }

  public void ExitChestRepositionMode()
  {
   if (movingChest != null)
   {
    CancelChestPlacement("Chest move cancelled.");
   }
   repositionMode = false;
   SetPlacementHint("Reposition mode finished.");
   PermanentStoragePlugin.Log.LogInfo("Chest reposition mode finished.");
  }

  void UpdateChestPlacement()
  {
   if (repositionMode && !placingChest)
   {
    if (!inside || room == null || room.Root == null)
    {
     repositionMode = false;
     HideChestHintUI();
     return;
    }
    if (InputBlocker.Blocked) return;
    if (GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
    {
     ExitChestRepositionMode();
    }
    return;
   }
   if (!placingChest)
   {
    return;
   }
   if (!inside || room == null || room.Root == null || Player.PlayerChar == null)
   {
    CancelChestPlacement("Chest placement cancelled.");
    return;
   }
   if (movingChest == null && !MimicChestItem.HasOne())
   {
    CancelChestPlacement("You no longer have a Mimic body, so placement was cancelled.");
    return;
   }
   if (InputBlocker.Blocked)
   {
    return;
   }
   Camera camera = Camera.main;
   if (camera == null)
   {
    return;
   }
   Ray ray = camera.ScreenPointToRay(GetMousePosition());
   var plane = new Plane(Vector3.up, new Vector3(0f, room.Root.transform.position.y, 0f));
   float enter;
   if (plane.Raycast(ray, out enter))
   {
    Vector3 world = ray.GetPoint(enter);
    Vector3 local = room.Root.transform.InverseTransformPoint(world);
    local.x = Mathf.Clamp(local.x, 1.6f, RoomSize - 1.6f);
    local.z = Mathf.Clamp(local.z, 2.6f, RoomSize - 1.6f);
    local.y = 0.02f;
    placementLocal = local;
   }
   if (ChestRotationInput.WasPressedThisFrame())
   {
    placementRotation = Mathf.Repeat(placementRotation + 90f, 360f);
    SetPlacementHint("Chest rotated. Left-click to place it, or press R again for the next 90 degrees.");
   }
   placementValid = IsPlacementValid(placementLocal, movingChest != null ? movingChest.Record : null);
   if (placementPreview != null)
   {
    Vector3 world = room.Root.transform.TransformPoint(placementLocal);
    world.y += placementLift;
    placementPreview.transform.position = world;
    placementPreview.transform.rotation = room.Root.transform.rotation * Quaternion.Euler(0f, placementRotation, 0f);
    StorageChestFactory.SetPreviewValid(placementPreview, placementValid);
   }
   if (GetMouseButtonDown(0))
   {
    if (placementValid)
    {
     if (movingChest != null) CommitChestReposition(); else PlaceChestNow();
    }
    else
    {
     SetPlacementHint("Too close to the Keeper or another chest. Move the preview and click again.");
    }
   }
   if (GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
   {
    CancelChestPlacement(movingChest != null ? "Chest move cancelled." : "Chest placement cancelled.");
   }
  }

  bool IsPlacementValid(Vector3 local, StorageChestRecord ignore = null)
  {
   if (local.x < 1.6f || local.z < 2.6f || local.x > RoomSize - 1.6f || local.z > RoomSize - 1.6f)
   {
    return false;
   }
   if (keeper != null)
   {
    Vector3 keeperLocal = room.Root.transform.InverseTransformPoint(keeper.transform.position);
    float dx = local.x - keeperLocal.x;
    float dz = local.z - keeperLocal.z;
    if (dx * dx + dz * dz < 2.4f * 2.4f)
    {
     return false;
    }
   }
   if (file != null && file.chests != null)
   {
    foreach (var record in file.chests)
    {
     if (record == null || !record.placed || record == ignore)
     {
      continue;
     }
     float dx = local.x - record.localX;
     float dz = local.z - record.localZ;
     if (dx * dx + dz * dz < 2f * 2f)
     {
      return false;
     }
    }
   }
   return true;
  }

  // Only used to remove the old automatic center-facing rotation from existing saves.
  static float LegacyCenterFacingYaw(Vector3 local)
  {
   Vector3 toCenter = new Vector3(RoomSize * 0.5f - local.x, 0f, RoomSize * 0.5f - local.z);
   if (toCenter.sqrMagnitude < 0.01f)
   {
    return 180f;
   }
   return Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
  }

  void PlaceChestNow()
  {
   if (!placingChest || file == null || room == null)
   {
    return;
   }
   var record = new StorageChestRecord
   {
    id = NextChestId(file.chests),
    unlocked = true,
    unlockMethod = "mimic-placed",
    placed = true,
    localX = placementLocal.x,
    localY = placementLocal.y,
    localZ = placementLocal.z,
    rotationY = Mathf.Repeat(placementRotation, 360f)
   };
   file.chests.Add(record);
   bool spawned = SpawnChest(record);
   if (!spawned)
   {
    file.chests.Remove(record);
    CancelChestPlacement("The chest could not be built. Your Mimic body was not used.");
    SaveNow();
    return;
   }
   // Commit the chest record before consuming the only item that created it. If disk writing
   // fails, leave the Mimic item alone and remove the uncommitted visual.
   if (!SaveNow())
   {
    RemoveSpawnedChest(record);
    file.chests.Remove(record);
    CancelChestPlacement("The chest could not be saved. Your Mimic body was not used.");
    return;
   }
   if (!MimicChestItem.ConsumeOne())
   {
    RemoveSpawnedChest(record);
    file.chests.Remove(record);
    SaveNow();
    CancelChestPlacement("The Mimic body could not be consumed; no chest was placed.");
    return;
   }
   WallStyleMenu.RegisterChestName(record);
   EndChestPlacement();
   SetPlacementHint("Chest placed. Bring another Mimic body for more storage.");
   PermanentStoragePlugin.Log.LogInfo("Placed " + record.id + " at room-local " + placementLocal + " using one Mimic body.");
  }

  void RemoveSpawnedChest(StorageChestRecord record)
  {
   if (room == null || room.Root == null || record == null) return;
   foreach (var chest in room.Root.GetComponentsInChildren<StorageChest>(true))
   {
    if (chest != null && chest.Record == record)
    {
     Object.Destroy(chest.gameObject);
     return;
    }
  }
  }

  void EndChestPlacement()
  {
   placingChest = false;
   if (placementPreview != null)
   {
    Object.Destroy(placementPreview);
    placementPreview = null;
   }
   if (movingChest != null)
   {
    movingChest.transform.SetPositionAndRotation(movingChestPosition, movingChestRotation);
    movingChest.gameObject.SetActive(movingChestActive);
    movingChest = null;
   }
  }

  void CancelChestPlacement(string message)
  {
   if (!placingChest)
   {
    return;
   }
   EndChestPlacement();
   SetPlacementHint(message);
   PermanentStoragePlugin.Log.LogInfo(message);
  }

  void SetPlacementHint(string message)
  {
   placementHint = message;
   placementHintUntil = Time.time + 4f;
   MimicChestItem.ShowPlacementMessage(message);
  }

  // Use the game's own bottom gameplay-hint label instead of drawing a custom IMGUI panel.
  // Only this mod's text is replaced; the game's normal hints are restored when the mode ends.
  void UpdateChestHintUI()
  {
   if (!inside)
   {
    HideChestHintUI();
    return;
   }
   string hint;
   if (placingChest)
   {
    if (placementValid)
    {
     hint = (movingChest != null ? "Left Click Move Here" : "Left Click Place") +
      "   R Rotate   Right Click / Esc Cancel";
    }
    else
    {
     hint = "Move to a clear spot   R Rotate   Right Click / Esc Cancel";
    }
   }
   else if (!string.IsNullOrEmpty(placementHint) && Time.time <= placementHintUntil)
   {
    hint = placementHint;
   }
   else if (repositionMode)
   {
    hint = "Reposition mode   Interact with a chest to move it   Esc Finish";
   }
   else
   {
    HideChestHintUI();
    return;
   }
   SetChestHintUI(hint);
  }

  void SetChestHintUI(string text)
  {
   if (string.IsNullOrEmpty(text))
   {
    HideChestHintUI();
    return;
   }
   if (gameHintText == null && Time.unscaledTime >= nextGameHintLookup)
   {
    nextGameHintLookup = Time.unscaledTime + 2f;
    try
    {
     if (SingletonBehaviour<InputHintUI>.InstanceExists())
     {
      var field = typeof(InputHintUI).GetField("inputHintText",
       BindingFlags.Instance | BindingFlags.NonPublic);
      gameHintText = field != null ? field.GetValue(SingletonBehaviour<InputHintUI>.Instance) as TMP_Text : null;
     }
    }
    catch (Exception)
    {
     gameHintText = null;
    }
   }
   if (gameHintText != null)
   {
    gameHintText.text = text;
    gameHintTextValue = text;
   }
  }

  void HideChestHintUI()
  {
   if (gameHintText != null && !string.IsNullOrEmpty(gameHintTextValue) && gameHintText.text == gameHintTextValue)
   {
    gameHintText.text = string.Empty;
   }
   gameHintTextValue = null;
  }

  static Vector3 GetMousePosition()
  {
   // Prefer the new Input System mouse. Reading legacy Input first can return (0,0) without
   // throwing when the game is configured for the new Input System only.
   try
   {
    var type = FindInputType("UnityEngine.InputSystem.Mouse", "Unity.InputSystem");
    var current = type != null ? type.GetProperty("current", BindingFlags.Public | BindingFlags.Static) : null;
    var mouse = current != null ? current.GetValue(null) : null;
    var position = mouse != null ? mouse.GetType().GetProperty("position") : null;
    var value = position != null ? position.GetValue(mouse) : null;
    if (value != null)
    {
     float x = (float)value.GetType().GetProperty("x").GetValue(value);
     float y = (float)value.GetType().GetProperty("y").GetValue(value);
     return new Vector3(x, y, 0f);
    }
   }
   catch { }
   try { return Input.mousePosition; } catch { }
   return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
  }

  static bool GetMouseButtonDown(int button)
  {
   // Same order as the position above: the new Input System is authoritative when present.
   try
   {
    var type = FindInputType("UnityEngine.InputSystem.Mouse", "Unity.InputSystem");
    var current = type != null ? type.GetProperty("current", BindingFlags.Public | BindingFlags.Static) : null;
    var mouse = current != null ? current.GetValue(null) : null;
    if (mouse != null)
    {
     string controlName = button == 0 ? "leftButton" : button == 1 ? "rightButton" : "middleButton";
     var controlProperty = mouse.GetType().GetProperty(controlName);
     var control = controlProperty != null ? controlProperty.GetValue(mouse) : null;
     var pressed = control != null ? control.GetType().GetProperty("wasPressedThisFrame") : null;
     return pressed != null && (bool)pressed.GetValue(control);
    }
   }
   catch { }
   try { return Input.GetMouseButtonDown(button); } catch { return false; }
  }

  static Type FindInputType(string fullName, string assemblyName)
  {
   var type = Type.GetType(fullName + ", " + assemblyName);
   if (type != null) return type;
   foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
   {
    try { type = assembly.GetType(fullName, false); } catch { type = null; }
    if (type != null) return type;
   }
   return null;
  }

  void Teleport(Vector3 position)
  {
   var player = Player.PlayerChar;
   if (player == null)
   {
    return;
   }
   player.TeleportToPosition(position);
   var transform = player.transform;

   if (transform != null)
   {
    // Direct assignment is a fallback in case the game's teleport rejects a position
    // outside the current NavMesh (the test room is intentionally far away).
    transform.position = position;
    transform.rotation = Quaternion.identity;
   }
  }

  // Player-only movement for entering the storage room. TeleportToPosition would also move
  // every pet, party member and ward next to the player inside the room.
  void TeleportPlayerOnly(Vector3 position)
  {
   var player = Player.PlayerChar;
   if (player == null)
   {
    return;
   }
   player.SetPositionProper(position);
   var transform = player.transform;
   if (transform != null)
   {
    transform.position = position;
    transform.rotation = Quaternion.identity;
   }
  }

  void Transition(Action mid)
  {
   if (SingletonBehaviour<SystemOverlay>.InstanceExists())
   {
    SingletonBehaviour<SystemOverlay>.Instance.FadeToBlack(delegate
    {
     mid();
     SingletonBehaviour<SystemOverlay>.Instance.FadeToClear(null, ignoreIfAlreadyClear: false, 0.25f);
    }, 0.25f);
   }
   else
   {
    mid();
   }
  }

  void ApplyRoomAmbience()
  {
   if (SingletonBehaviour<AudioManager>.InstanceExists())
   {
    SingletonBehaviour<AudioManager>.Instance.PlayMusic(AudioData.AudioClipName.MusicTempleFate2, stopOthers: true);
   }
   if (SimpleSingletonBehaviour<PlayerUI>.InstanceExists())
   {
    SimpleSingletonBehaviour<PlayerUI>.Instance.SetLocationText("Permanent Storage Room");
   }
  }

  void RestoreTownAmbience()
  {
   if (SingletonBehaviour<AudioManager>.InstanceExists())
   {
    SingletonBehaviour<AudioManager>.Instance.PlayMusic(AudioData.AudioClipName.MusicTempleFate2, stopOthers: true);
   }
   if (SimpleSingletonBehaviour<PlayerUI>.InstanceExists())
   {
    string name = SingletonBehaviour<Town>.InstanceExists() ? SingletonBehaviour<Town>.Instance.TownName : "The Town of Grove";
    SimpleSingletonBehaviour<PlayerUI>.Instance.SetLocationText(name);
   }
  }

  public void ShowUnlockPrompt(StorageChest chest)
  {
   if (chest == null || chest.Record == null || chest.Record.unlocked)
   {
    return;
   }
   if (!SingletonBehaviour<PromptUI>.InstanceExists())
   {
    return;
   }
   int cost = PermanentStoragePlugin.GoldUnlockCost.Value;
   if (cost < 0)
   {
    cost = 0;
   }
   SingletonBehaviour<PromptUI>.Instance.ShowPromptRaw(
    "STORAGE CHEST - LOCKED\n\nYes: use 1 Mimic Chest to unlock this position.\nNo: purchase it for " + cost.ToString("N0") + " gold instead.",
    delegate (bool useMimic)
    {
     if (useMimic)
     {
      if (!MimicChestItem.HasOne())
      {
       ShowInfo("You do not have a Mimic Chest.\n\nKill Mimics in dungeons for a chance to find one, or pay gold instead.");
       return;
      }
      if (MimicChestItem.ConsumeOne())
      {
       UnlockChest(chest, "mimic");
      }
      else
      {
       ShowInfo("The Mimic Chest could not be consumed.");
      }
      return;
     }
     PromptForGold(chest, cost);
    }, twoButtons: true, positiveOverride: "Yes", negativeOverride: "No");
  }

  void PromptForGold(StorageChest chest, int cost)
  {
   SingletonBehaviour<PromptUI>.Instance.ShowPromptRaw(
    "Purchase this storage position for " + cost.ToString("N0") + " gold?\n\nYes purchases it. No cancels.",
    delegate (bool buy)
    {
     if (!buy)
     {
      return;
     }
     if (!SingletonBehaviour<PlayerStatus>.InstanceExists() ||
         !SingletonBehaviour<PlayerStatus>.Instance.CanAfford(cost))
     {
      ShowInfo("You do not have enough gold.");
      return;
     }
     SingletonBehaviour<PlayerStatus>.Instance.RemoveGold((uint)cost);
     UnlockChest(chest, "gold");
    }, twoButtons: true, positiveOverride: "Yes", negativeOverride: "No");
  }

  void ShowInfo(string body)
  {
   if (SingletonBehaviour<PromptUI>.InstanceExists())
   {
    SingletonBehaviour<PromptUI>.Instance.ShowPromptRaw(body, null, twoButtons: false, positiveOverride: "OK");
   }
  }

  void UnlockChest(StorageChest chest, string method)
  {
   if (chest == null || chest.Record == null || chest.Record.unlocked)
   {
    return;
   }
   chest.Record.unlocked = true;
   chest.Record.unlockMethod = method;
   chest.RefreshVisual();
   SaveNow();
   if (SingletonBehaviour<AudioManager>.InstanceExists())
   {
    SingletonBehaviour<AudioManager>.Instance.PlayAudioClip(AudioData.AudioClipName.ChestOpen, randomizePitch: true);
   }
   PermanentStoragePlugin.Log.LogInfo("Unlocked " + chest.Record.id + " using " + method + ".");
  }

  public void OpenStorage(StorageChest chest)
  {
   if (chest == null)
   {
    PermanentStoragePlugin.Log.LogWarning("Storage chest open was requested with no chest reference.");
    return;
   }
   if (openChest != null)
   {
    PermanentStoragePlugin.Log.LogWarning("Ignoring storage chest open while " + openChest.Record.id + " is already open.");
    return;
   }
   if (room == null || room.Proxy == null)
   {
    PermanentStoragePlugin.Log.LogError("Storage chest " + chest.Record.id + " has no room stash proxy.");
    return;
   }
   PermanentStoragePlugin.Log.LogInfo("Opening storage chest " + chest.Record.id + ".");
   StartCoroutine(OpenStorageRoutine(chest));
  }

  IEnumerator OpenStorageRoutine(StorageChest chest)
  {
   var proxy = room.Proxy;
   // The proxy is kept inactive so vanilla FindFirstObjectByType<Stash> calls never see it.
   // Activate it only for this storage session and let Stash.Start allocate its grid.
   proxy.gameObject.SetActive(true);
   for (int i = 0; i < 120 && proxy.GetItems() == null; i++)
   {
    yield return null;
   }
   if (proxy.GetItems() == null)
   {
    proxy.gameObject.SetActive(false);
    PermanentStoragePlugin.Log.LogWarning("The storage proxy did not initialize; the chest could not open.");
    yield break;
   }
   ClearProxy(proxy);
   overflow.Clear();
   SeedProxy(proxy, chest.Record);
   HideOtherStashes(proxy);
   openChest = chest;
   // Subscribe after Stash.Interact adds the stock handlers. Our snapshot then sees the proxy
   // after it has finished moving/removing an item, avoiding stale saves and duplicates.
   proxy.Interact();
   if (SimpleSingletonBehaviour<PlayerUI>.InstanceExists())
   {
    var playerUi = SimpleSingletonBehaviour<PlayerUI>.Instance;
    playerUi.onClosedTabs += OnStorageClosed;
    var inventory = playerUi.InventoryUI;
    if (inventory != null)
    {
     inventory.onAddItemToStash += OnStashChanged;
     inventory.onItemMovedInStash += OnStashMoved;
     inventory.onRemoveItemFromStash += OnStashRemovedFromProxy;
    }
   }
   PermanentStoragePlugin.Log.LogInfo("Storage chest " + chest.Record.id + " opened in the stock inventory UI with " +
    proxy.GetItems().Count + " item(s).");
  }

  void ClearProxy(Stash proxy)
  {
   var items = proxy.GetItems();
   if (items == null)
   {
    return;
   }
   foreach (var item in items.ToArray())
   {
    proxy.OnItemMovedFromStash(item);
   }
  }

  void SeedProxy(Stash proxy, StorageChestRecord record)
  {
   if (record == null || record.items == null)
   {
    return;
   }
   foreach (var saved in record.items.ToArray())
   {
    if (saved == null)
    {
     continue;
    }
    var data = SingletonBehaviour<FateDB>.Instance.AllItems.Find(a => a != null && a.displayName == saved.baseName);
    if (data == null)
    {
     PermanentStoragePlugin.Log.LogWarning("Stored item definition missing: " + saved.baseName + "; keeping it in the save.");
     overflow.Add(saved);
     continue;
    }
    var item = new Item(data);
    item.ApplyInstance(saved);
    int desired = saved.slotIndex >= 20 ? saved.slotIndex - 20 : -1;
    if (proxy.CanFitItem(item, out _, desired))
    {
     proxy.OnItemMovedToStash(item, desired);
    }
    else
    {
     overflow.Add(saved);
    }
   }
  }

  void OnStashChanged(Item item, int slot)
  {
   SnapshotOpenChest();
   SaveNow();
  }

  void OnStashMoved(Item item, int slot)
  {
   SnapshotOpenChest();
   SaveNow();
  }

  void OnStashRemovedFromProxy(Item item)
  {
   // OnInventoryInit is deliberately skipped for the proxy so it cannot load the town stash.
   // That also skips the stock removal subscription, so remove the item here before snapshotting.
   if (room != null && room.Proxy != null)
   {
    room.Proxy.OnItemMovedFromStash(item);
   }
   SnapshotOpenChest();
   SaveNow();
  }

  void OnStorageClosed()
  {
   string closedChest = openChest != null && openChest.Record != null ? openChest.Record.id : "unknown";
   DetachStorageEvents();
   SnapshotOpenChest();
   SaveNow();
   ShowHiddenStashes();
   if (room != null && room.Proxy != null)
   {
    room.Proxy.gameObject.SetActive(false);
   }
   openChest = null;
   PermanentStoragePlugin.Log.LogInfo("Storage chest " + closedChest + " closed and saved.");
  }

  void DetachStorageEvents()
  {
   if (SimpleSingletonBehaviour<PlayerUI>.InstanceExists())
   {
    SimpleSingletonBehaviour<PlayerUI>.Instance.onClosedTabs -= OnStorageClosed;
    var inventory = SimpleSingletonBehaviour<PlayerUI>.Instance.InventoryUI;
    if (inventory != null)
    {
     inventory.onAddItemToStash -= OnStashChanged;
     inventory.onItemMovedInStash -= OnStashMoved;
     inventory.onRemoveItemFromStash -= OnStashRemovedFromProxy;
    }
   }
  }

  void SnapshotOpenChest()
  {
   if (openChest == null || openChest.Record == null || room == null || room.Proxy == null)
   {
    return;
   }
   var items = room.Proxy.GetItems();
   if (items == null)
   {
    return;
   }
   var saved = new List<ItemSaveData>();
   foreach (var item in items)
   {
    if (item == null)
    {
     continue;
    }
    var entry = new ItemSaveData();
    item.FillInstance(entry);
    saved.Add(entry);
   }
   saved.AddRange(overflow);
   overflow.Clear();
   openChest.Record.items = saved;
  }

  void HideOtherStashes(Stash proxy)
  {
   hiddenStashes.Clear();
   var stashes = Object.FindObjectsOfType<Stash>(true);
   foreach (var stash in stashes)
   {
    if (stash == null || stash == proxy || !stash.gameObject.activeSelf)
    {
     continue;
    }
    hiddenStashes.Add(stash.gameObject);
    stash.gameObject.SetActive(false);
   }
  }

  void ShowHiddenStashes()
  {
   foreach (var go in hiddenStashes)
   {
    if (go != null)
    {
     go.SetActive(true);
    }
   }
   hiddenStashes.Clear();
  }

  void OnPreSave(FateSaveData saveData)
  {
   SnapshotOpenChest();
   SaveNow();
  }

  bool SaveNow()
  {
   return file != null && StoragePersistence.Save(file);
  }

  void CleanupRoom()
  {
   EndChestPlacement();
   repositionMode = false;
   HideChestHintUI();
   keeper = null;
   DetachStorageEvents();
   SnapshotOpenChest();
   openChest = null;
   ShowHiddenStashes();
   if (room != null && room.Root != null)
   {
    Object.Destroy(room.Root);
   }
   room = null;
   if (doorGo != null)
   {
    Object.Destroy(doorGo);
    doorGo = null;
   }
   inside = false;
   if (file != null)
   {
    SaveNow();
    file = null;
   }
   if (preSaveHooked)
   {
    SaveData.RemovePreSaveMainThreadTask(OnPreSave);
    preSaveHooked = false;
   }
  }
 }
}
