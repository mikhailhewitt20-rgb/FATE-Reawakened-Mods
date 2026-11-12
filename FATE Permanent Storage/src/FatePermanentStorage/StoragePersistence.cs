using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Tableflip;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // One physical storage position. The id is stable forever, so a chest keeps its contents
 // even if the room is rebuilt, the player leaves, or the game is restarted.
 [Serializable]
 public sealed class StorageChestRecord
 {
  public string id = "";
  public bool unlocked;
  public string unlockMethod = "";
  public string customName = "";
  public List<ItemSaveData> items = new List<ItemSaveData>();
  // Chests are no longer built into the room. A record is only physical once the player has
  // spent a Mimic body to place it, and the position is stored relative to the room root so it
  // stays put if the room is ever moved.
  public bool placed;
  public float localX;
  public float localY;
  public float localZ;
  public float rotationY;
 }

 [Serializable]
 public sealed class StorageRoomFile
 {
  public int version = 1;
  public string slotKey = "";
  public List<StorageChestRecord> chests = new List<StorageChestRecord>();
 }

 // JsonUtility was dropping StorageChestRecord records because they contain ItemSaveData lists.
 // This flat DTO keeps record fields primitive and stores each game's item-save object as JSON text.
 [DataContract]
 public sealed class StorageRoomJson
 {
  [DataMember(Name = "version", Order = 0)]
  public int version;
  [DataMember(Name = "slotKey", Order = 1)]
  public string slotKey;
  [DataMember(Name = "records", Order = 2)]
  public StorageChestJson[] records;
 }

 [DataContract]
 public sealed class StorageChestJson
 {
  [DataMember(Name = "id", Order = 0)]
  public string id;
  [DataMember(Name = "unlocked", Order = 1)]
  public bool unlocked;
  [DataMember(Name = "unlockMethod", Order = 2)]
  public string unlockMethod;
  [DataMember(Name = "customName", Order = 3)]
  public string customName;
  [DataMember(Name = "itemsJson", Order = 4)]
  public string[] itemsJson;
  [DataMember(Name = "placed", Order = 5)]
  public bool placed;
  [DataMember(Name = "localX", Order = 6)]
  public float localX;
  [DataMember(Name = "localY", Order = 7)]
  public float localY;
  [DataMember(Name = "localZ", Order = 8)]
  public float localZ;
  [DataMember(Name = "rotationY", Order = 9)]
  public float rotationY;
 }

 internal static class StoragePersistence
 {
  const string FilePrefix = "FatePermanentStorage_";   // The game's own save container already has a stable per-character id. Using it means a
  const int BackupGenerations = 4;
   // second character never sees the first character's room, and two save slots cannot collide.
   // The game version is deliberately not part of the key: a patch should not hide stored items.
  public static string SlotKey()
  {
   try
   {
    var slot = SaveData.CurrentSaveSlot;
    if (slot != null && slot.uniqueId != 0)
    {
     return slot.uniqueId.ToString();
    }
   }
   catch (Exception)
   {
    // SaveData is not ready during startup; use a deterministic fallback until it is.
   }
   return "noslot";
  }

  static string FilePath(string slotKey)
  {
   return Path.Combine(Application.persistentDataPath, FilePrefix + slotKey + ".json");
  }

  static string BackupPath(string path, int generation)
  {
   return generation == 1 ? path + ".bak" : path + ".bak." + generation.ToString();
  }

  static StorageRoomFile ReadCandidate(string path, string slotKey)
  {
   if (!File.Exists(path)) return null;
   try
   {
    string json = File.ReadAllText(path);
    StorageRoomJson saved = null;
    try
    {
     saved = FromJson(json);
    }
    catch (Exception)
    {
     // Try the prior Unity JsonUtility format below.
    }
    StorageRoomFile candidate;
    if (saved != null && saved.records != null)
    {
     if (!string.IsNullOrEmpty(saved.slotKey) && saved.slotKey != slotKey) return null;
     candidate = new StorageRoomFile
     {
      version = saved.version,
      slotKey = slotKey,
      chests = new List<StorageChestRecord>()
     };
     foreach (var stored in saved.records)
     {
      if (stored == null) continue;
      var chest = new StorageChestRecord
      {
       id = stored.id ?? "",
       unlocked = stored.unlocked,
       unlockMethod = stored.unlockMethod ?? "",
       customName = stored.customName ?? "",
       placed = stored.placed,
       localX = stored.localX,
       localY = stored.localY,
       localZ = stored.localZ,
       rotationY = stored.rotationY,
       items = new List<ItemSaveData>()
      };
      if (stored.itemsJson != null)
      {
       foreach (string itemJson in stored.itemsJson)
       {
        if (string.IsNullOrEmpty(itemJson))
        {
         throw new InvalidDataException("A stored item record was empty.");
        }
        ItemSaveData item = JsonUtility.FromJson<ItemSaveData>(itemJson);
        if (item == null) throw new InvalidDataException("A stored item record could not be decoded.");
        chest.items.Add(item);
       }
      }
      candidate.chests.Add(chest);
     }
    }
    else
    {
     // Accept the original format so existing character saves migrate without losing contents.
     candidate = JsonUtility.FromJson<StorageRoomFile>(json);
     if (candidate == null || (!string.IsNullOrEmpty(candidate.slotKey) && candidate.slotKey != slotKey)) return null;
    }
    candidate.slotKey = slotKey;
    if (candidate.chests == null)
    {
     candidate.chests = new List<StorageChestRecord>();
    }
    foreach (var chest in candidate.chests)
    {
     if (chest != null && chest.items == null) chest.items = new List<ItemSaveData>();
    }
    return candidate;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not read storage save candidate " + path + ": " + ex.Message);
    return null;
   }
  }

  public static StorageRoomFile Load(string slotKey)
  {
   string path = FilePath(slotKey);
   StorageRoomFile primary = ReadCandidate(path, slotKey);
   StorageRoomFile best = primary;
   int bestCount = primary != null && primary.chests != null ? primary.chests.Count : -1;
   for (int generation = 1; generation <= BackupGenerations; generation++)
   {
    var backup = ReadCandidate(BackupPath(path, generation), slotKey);
    int count = backup != null && backup.chests != null ? backup.chests.Count : -1;
    if (count > bestCount)
    {
     best = backup;
     bestCount = count;
    }
   }
   if (best == null) return new StorageRoomFile { slotKey = slotKey };
   if (best != primary)
   {
    PermanentStoragePlugin.Log.LogWarning("Recovered " + bestCount + " storage chest record(s) from a prior save generation.");
   }
   return best;
  }

  public static bool Save(StorageRoomFile file)
  {
   if (file == null)
   {
    return false;
   }
   if (string.IsNullOrEmpty(file.slotKey) || file.slotKey == "noslot")
   {
    PermanentStoragePlugin.Log.LogWarning("Skipping chest save because no unique character save slot is active.");
    return false;
   }
   try
   {
    string path = FilePath(file.slotKey);
    StorageRoomFile lastGood = ReadCandidate(path, file.slotKey);
    int lastGoodCount = lastGood != null && lastGood.chests != null ? lastGood.chests.Count : -1;
    string lastGoodPath = path;
    for (int generation = 1; generation <= BackupGenerations; generation++)
    {
     var backup = ReadCandidate(BackupPath(path, generation), file.slotKey);
     int count = backup != null && backup.chests != null ? backup.chests.Count : -1;
     if (count > lastGoodCount)
     {
      lastGood = backup;
      lastGoodCount = count;
      lastGoodPath = BackupPath(path, generation);
     }
    }
    if (lastGoodCount > 0)
    {
     if (file.chests == null || file.chests.Count == 0)
     {
      PermanentStoragePlugin.Log.LogWarning("Rejected an empty storage save to protect " + lastGoodCount +
       " existing chest record(s). Recovery source: " + lastGoodPath);
      file.chests = lastGood.chests;
     }
     else
     {
      foreach (var oldRecord in lastGood.chests)
      {
       if (oldRecord == null) continue;
       bool found = file.chests.Exists(current => current != null && current.id == oldRecord.id);
       if (!found) file.chests.Add(oldRecord);
      }
     }
    }
    string json = ToJson(file);
    string temporary = path + ".tmp";
    File.WriteAllText(temporary, json, new UTF8Encoding(false));
    var verified = ReadCandidate(temporary, file.slotKey);
    if (verified == null || verified.chests.Count != file.chests.Count)
    {
     PermanentStoragePlugin.Log.LogError("Storage save verification failed (expected " +
      (file.chests != null ? file.chests.Count : 0) + ", read " +
      (verified != null && verified.chests != null ? verified.chests.Count : -1) +
      " chest records); preserving the previous save.");
     return false;
    }
    foreach (var chest in file.chests)
    {
     var savedChest = chest != null ? verified.chests.Find(saved => saved != null && saved.id == chest.id) : null;
     if (savedChest == null)
     {
      PermanentStoragePlugin.Log.LogError("Storage save verification found a missing chest id; preserving the previous save.");
      return false;
     }
     int expectedItems = chest.items != null ? chest.items.Count : 0;
     int savedItems = savedChest.items != null ? savedChest.items.Count : 0;
     if (expectedItems != savedItems)
     {
      PermanentStoragePlugin.Log.LogError("Storage save verification found " + savedItems + " of " + expectedItems +
       " items in chest " + chest.id + "; preserving the previous save.");
      return false;
     }
     for (int itemIndex = 0; itemIndex < expectedItems; itemIndex++)
     {
      if (chest.items[itemIndex] == null || savedChest.items[itemIndex] == null ||
          chest.items[itemIndex].baseName != savedChest.items[itemIndex].baseName)
      {
       PermanentStoragePlugin.Log.LogError("Storage save verification found a damaged item record in chest " +
        chest.id + "; preserving the previous save.");
       return false;
      }
     }
    }
    if (File.Exists(path))
    {
     if (lastGoodCount > 0)
     {
      for (int generation = BackupGenerations; generation >= 2; generation--)
      {
       string previous = BackupPath(path, generation - 1);
       if (File.Exists(previous)) File.Copy(previous, BackupPath(path, generation), true);
      }
      if (ReadCandidate(path, file.slotKey) != null && ReadCandidate(path, file.slotKey).chests.Count > 0)
      {
       File.Copy(path, BackupPath(path, 1), true);
      }
     }
     File.Replace(temporary, path, null);
    }
    else
    {
     File.Move(temporary, path);
    }
    PermanentStoragePlugin.Log.LogInfo("Saved " + (file.chests != null ? file.chests.Count : 0) +
     " storage chest record(s) to " + path + ".");
    return true;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not write storage room save: " + ex.Message);
    return false;
   }
  }

  static string ToJson(StorageRoomFile file)
  {
   var saved = new StorageRoomJson
   {
    version = 4,
    slotKey = file.slotKey,
    records = new StorageChestJson[file.chests != null ? file.chests.Count : 0]
   };
   for (int i = 0; i < saved.records.Length; i++)
   {
    var chest = file.chests[i];
    if (chest == null) throw new InvalidDataException("A storage chest record was null.");
    var items = chest.items ?? new List<ItemSaveData>();
    var stored = new StorageChestJson
    {
     id = chest.id ?? "",
     unlocked = chest.unlocked,
     unlockMethod = chest.unlockMethod ?? "",
     customName = chest.customName ?? "",
     itemsJson = new string[items.Count],
     placed = chest.placed,
     localX = chest.localX,
     localY = chest.localY,
     localZ = chest.localZ,
     rotationY = chest.rotationY
    };
    for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
    {
     if (items[itemIndex] == null) throw new InvalidDataException("A stored item record was null.");
     stored.itemsJson[itemIndex] = JsonUtility.ToJson(items[itemIndex]);
     if (string.IsNullOrEmpty(stored.itemsJson[itemIndex]))
     {
      throw new InvalidDataException("A stored item could not be serialized.");
     }
    }
    saved.records[i] = stored;
   }
   var serializer = new DataContractJsonSerializer(typeof(StorageRoomJson));
   using (var stream = new MemoryStream())
   {
    serializer.WriteObject(stream, saved);
    return Encoding.UTF8.GetString(stream.ToArray());
   }
  }

  static StorageRoomJson FromJson(string json)
  {
   var serializer = new DataContractJsonSerializer(typeof(StorageRoomJson));
   using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
   {
    return serializer.ReadObject(stream) as StorageRoomJson;
   }
  }
 }
}
