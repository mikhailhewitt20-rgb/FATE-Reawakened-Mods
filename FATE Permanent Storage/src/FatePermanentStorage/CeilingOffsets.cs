using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 [Serializable]
 public sealed class CeilingOffsetEntry
 {
  public int x;
  public int z;
  public float ox;
  public float oy;
  public float oz;
  // Uniform scale multiplier; zero means this older entry has no scale override.
  public float sx;
  public float sy;
  public float sz;
  public bool removed;
  public bool added;
 }

 [Serializable]
 public sealed class CeilingOffsetFile
 {
  public int version = 4;
  public List<CeilingOffsetEntry> tiles = new List<CeilingOffsetEntry>();
 }

 // Per-roof-tile position and scale overrides. Positions are deltas and scale values are
 // multipliers from each prefab's original scale, so both survive room rebuilds and style changes.
 internal static class CeilingOffsets
 {
  const float LegacyEdgeOffset = 1.54296875f;
  static readonly Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();
  static readonly Dictionary<string, Vector3> scaleOffsets = new Dictionary<string, Vector3>();
  static readonly HashSet<string> removedTiles = new HashSet<string>();
  static readonly HashSet<string> addedTiles = new HashSet<string>();
  static readonly HashSet<string> legacyOffsetKeys = new HashSet<string>();
  static bool loaded;
  static string pluginDir;

  public static string Key(int x, int z) { return x + "," + z; }

  public static void EnsureLoaded()
  {
   if (loaded) return;
   loaded = true;
   offsets.Clear();
   scaleOffsets.Clear();
   removedTiles.Clear();
   addedTiles.Clear();
   legacyOffsetKeys.Clear();
   // Baked defaults shipped with the mod (next to the DLL) are loaded first, then user edits from
   // the config folder override them so in-game positioning can be iterated and "baked" for release.
   LoadBakedDefaults();
   LoadFrom(UserPath());
   PermanentStoragePlugin.Log.LogInfo("Ceiling offsets loaded: " + offsets.Count + " tile override(s); baked path=" + BakedPath() + ".");
  }

  static void LoadBakedDefaults()
  {
   var paths = new List<string>();
   paths.Add(BakedPath());
   try
   {
    string pluginRoot = BepInEx.Paths.PluginPath;
    if (!string.IsNullOrEmpty(pluginRoot))
    {
     paths.Add(Path.Combine(pluginRoot, "FATE_PermanentStorage-FATE_PermanentStorage", "FATE_PermanentStorage", "ceiling_offsets.baked.json"));
     if (Directory.Exists(pluginRoot))
     {
      foreach (string candidate in Directory.GetFiles(pluginRoot, "ceiling_offsets.baked.json", SearchOption.AllDirectories))
      {
       if (candidate.IndexOf("FATE_PermanentStorage", StringComparison.OrdinalIgnoreCase) >= 0) paths.Add(candidate);
      }
     }
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not search plugin folders for the shipped roof bake: " + ex.Message);
   }

   var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach (string path in paths)
   {
    if (string.IsNullOrEmpty(path) || !seen.Add(path) || !File.Exists(path)) continue;
    int before = offsets.Count;
    LoadFrom(path);
    if (offsets.Count > before)
    {
     PermanentStoragePlugin.Log.LogInfo("Loaded shipped ceiling offsets from " + path + ".");
    }
   }
  }

  public static void Reload()
  {
   loaded = false;
   EnsureLoaded();
  }

  public static bool TryGet(int x, int z, out Vector3 offset)
  {
   EnsureLoaded();
   return offsets.TryGetValue(Key(x, z), out offset);
  }

  public static bool IsLegacyOffset(int x, int z)
  {
   EnsureLoaded();
   return legacyOffsetKeys.Contains(Key(x, z));
  }

  public static Vector3 NormalizeLegacyOffset(int x, int z, Vector3 offset)
  {
   float legacyX = x == -1 ? -LegacyEdgeOffset : (x == RoomBuilder.Cells ? LegacyEdgeOffset : 0f);
   float legacyZ = z == -1 ? -LegacyEdgeOffset : (z == RoomBuilder.Cells ? LegacyEdgeOffset : 0f);
   if (Mathf.Abs(legacyX) > 0.0001f && Mathf.Abs(offset.x - legacyX) < 0.05f) offset.x -= legacyX;
   if (Mathf.Abs(legacyZ) > 0.0001f && Mathf.Abs(offset.z - legacyZ) < 0.05f) offset.z -= legacyZ;
   return offset;
  }

  public static void Set(int x, int z, Vector3 offset)
  {
   EnsureLoaded();
   string key = Key(x, z);
   offsets[key] = offset;
   legacyOffsetKeys.Remove(key);
  }

  public static bool TryGetScale(int x, int z, out Vector3 multiplier)
  {
   EnsureLoaded();
   return scaleOffsets.TryGetValue(Key(x, z), out multiplier);
  }

  public static void SetScale(int x, int z, Vector3 multiplier)
  {
   EnsureLoaded();
   scaleOffsets[Key(x, z)] = multiplier;
  }

  public static void ClearPosition(int x, int z)
  {
   EnsureLoaded();
   string key = Key(x, z);
   offsets.Remove(key);
   legacyOffsetKeys.Remove(key);
  }

  public static void Clear(int x, int z)
  {
   EnsureLoaded();
   string key = Key(x, z);
   offsets.Remove(key);
   scaleOffsets.Remove(key);
   legacyOffsetKeys.Remove(key);
  }

  public static bool IsRemoved(int x, int z)
  {
   EnsureLoaded();
   return removedTiles.Contains(Key(x, z));
  }

  public static bool IsAdded(int x, int z)
  {
   EnsureLoaded();
   return addedTiles.Contains(Key(x, z));
  }

  public static void SetRemoved(int x, int z, bool removed)
  {
   EnsureLoaded();
   string key = Key(x, z);
   if (removed) { removedTiles.Add(key); addedTiles.Remove(key); }
   else removedTiles.Remove(key);
  }

  public static void SetAdded(int x, int z, bool added)
  {
   EnsureLoaded();
   string key = Key(x, z);
   if (added) { addedTiles.Add(key); removedTiles.Remove(key); }
   else addedTiles.Remove(key);
  }

  public static int Count
  {
   get
   {
    EnsureLoaded();
    var keys = new HashSet<string>(offsets.Keys);
    keys.UnionWith(scaleOffsets.Keys);
    keys.UnionWith(removedTiles);
    keys.UnionWith(addedTiles);
    return keys.Count;
   }
  }

  public static void SaveUser()
  {
   MigrateLegacyOffsets();
   string path = UserPath();
   if (SaveTo(path))
    PermanentStoragePlugin.Log.LogInfo("Ceiling tile overrides saved (" + Count + ") to " + path);
  }

  // Copies the current offsets next to the plugin DLL so they can be shipped as baked defaults in
  // the next release. Returns the path written, or null on failure.
  public static string BakeForRelease()
  {
   try
   {
    MigrateLegacyOffsets();
    if (!SaveTo(UserPath())) return null;
    string baked = BakedPath();
    if (!SaveTo(baked)) return null;
    PermanentStoragePlugin.Log.LogInfo("Ceiling offsets baked for release (" + Count + " tile record(s)) to " + baked + ". Include this file in the next package to ship these positions.");
    return baked;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not bake ceiling offsets: " + ex.Message);
    return null;
   }
  }

  static string UserPath()
  {
   string config = null;
   try { config = BepInEx.Paths.ConfigPath; } catch { }
   if (string.IsNullOrEmpty(config))
   {
    config = Path.Combine(Application.persistentDataPath, "BepInEx", "config");
   }
   return Path.Combine(config, "mikhailkillszombies.fate.permanentstorage.ceiling_offsets.json");
  }

  static string BakedPath()
  {
   return Path.Combine(PluginDirectory(), "ceiling_offsets.baked.json");
  }

  static string PluginDirectory()
  {
   if (pluginDir != null) return pluginDir;
   try
   {
    string loc = Assembly.GetExecutingAssembly().Location;
    if (!string.IsNullOrEmpty(loc))
    {
     pluginDir = Path.GetDirectoryName(loc);
    }
   }
   catch { }
   if (string.IsNullOrEmpty(pluginDir))
   {
    pluginDir = Path.Combine(Application.persistentDataPath, "BepInEx", "plugins");
   }
   return pluginDir;
  }

  static void LoadFrom(string path)
  {
   try
   {
    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
    string json = File.ReadAllText(path);
    int version = JsonInt(json, "version", 1);
    int records = 0;
    // JsonUtility silently returns an empty list for this shipped file on some Unity builds.
    // Parse the flat tile objects directly so the baked positions always apply.
    foreach (Match match in Regex.Matches(json, "\\{[^{}]*\\\"x\\\"\\s*:\\s*-?\\d+[^{}]*\\}"))
    {
     string tile = match.Value;
     int x = JsonInt(tile, "x", 0);
     int z = JsonInt(tile, "z", 0);
     float ox = JsonFloat(tile, "ox", 0f);
     float oy = JsonFloat(tile, "oy", 0f);
     float oz = JsonFloat(tile, "oz", 0f);
     float sx = JsonFloat(tile, "sx", 0f);
     float sy = JsonFloat(tile, "sy", 0f);
     float sz = JsonFloat(tile, "sz", 0f);
     bool removed = JsonBool(tile, "removed", false);
     bool added = JsonBool(tile, "added", false);
     string key = Key(x, z);
     records++;
     if (version >= 4) legacyOffsetKeys.Remove(key);
     else if (version == 3) legacyOffsetKeys.Add(key);
     if (version >= 3)
     {
      if (removed) removedTiles.Add(key); else removedTiles.Remove(key);
      if (added) addedTiles.Add(key); else addedTiles.Remove(key);
     }
     if (Mathf.Abs(ox) > 0.0001f || Mathf.Abs(oy) > 0.0001f || Mathf.Abs(oz) > 0.0001f)
     {
      offsets[key] = new Vector3(ox, oy, oz);
     }
     if (sx > 0f && sy > 0f && sz > 0f)
     {
      scaleOffsets[key] = new Vector3(sx, sy, sz);
     }
    }
    if (records > 0)
    {
     PermanentStoragePlugin.Log.LogInfo("Read " + records + " ceiling tile record(s) from " + path + ".");
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not read ceiling offsets from " + path + ": " + ex.Message);
   }
  }

  static int JsonInt(string json, string name, int fallback)
  {
   var match = Regex.Match(json, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(-?\\d+)");
   int value;
   return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
  }

  static float JsonFloat(string json, string name, float fallback)
  {
   var match = Regex.Match(json, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*([-+]?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][-+]?\\d+)?)");
   float value;
   return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
  }

  static bool JsonBool(string json, string name, bool fallback)
  {
   var match = Regex.Match(json, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
   return match.Success ? string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase) : fallback;
  }

  static bool SaveTo(string path)
  {
   try
   {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
     Directory.CreateDirectory(dir);
    }
    var keys = new HashSet<string>(offsets.Keys);
    keys.UnionWith(scaleOffsets.Keys);
    keys.UnionWith(removedTiles);
    keys.UnionWith(addedTiles);
    var entries = new List<CeilingOffsetEntry>();
    foreach (var key in keys)
    {
     var parts = key.Split(',');
     if (parts.Length != 2) continue;
     int x, z;
     if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out z)) continue;
     Vector3 offset, scale;
     offsets.TryGetValue(key, out offset);
     scaleOffsets.TryGetValue(key, out scale);
     entries.Add(new CeilingOffsetEntry
     {
      x = x, z = z,
      ox = offset.x, oy = offset.y, oz = offset.z,
      sx = scale.x, sy = scale.y, sz = scale.z,
      removed = removedTiles.Contains(key), added = addedTiles.Contains(key)
     });
    }
    entries.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
    var json = new StringBuilder("{\n  \"version\": 4,\n  \"tiles\": [");
    for (int i = 0; i < entries.Count; i++)
    {
     var entry = entries[i];
     json.Append(i == 0 ? "\n    {" : ",\n    {");
     json.Append("\"x\": ").Append(entry.x).Append(", \"z\": ").Append(entry.z);
     json.Append(", \"ox\": ").Append(JsonFloat(entry.ox));
     json.Append(", \"oy\": ").Append(JsonFloat(entry.oy));
     json.Append(", \"oz\": ").Append(JsonFloat(entry.oz));
     json.Append(", \"sx\": ").Append(JsonFloat(entry.sx));
     json.Append(", \"sy\": ").Append(JsonFloat(entry.sy));
     json.Append(", \"sz\": ").Append(JsonFloat(entry.sz));
     json.Append(", \"removed\": ").Append(entry.removed ? "true" : "false");
     json.Append(", \"added\": ").Append(entry.added ? "true" : "false").Append("}");
    }
    if (entries.Count > 0) json.Append('\n').Append("  ");
    json.Append("]\n}");
    File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
    PermanentStoragePlugin.Log.LogInfo("Wrote " + entries.Count + " ceiling tile record(s) to " + path + ".");
    return true;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not write ceiling offsets to " + path + ": " + ex.Message);
    return false;
   }
  }

  static void MigrateLegacyOffsets()
  {
   if (!loaded || legacyOffsetKeys.Count == 0) return;
   var keys = new List<string>(legacyOffsetKeys);
   foreach (string key in keys)
   {
    string[] parts = key.Split(',');
    int x, z;
    if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
        int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out z))
    {
     Vector3 value;
     if (offsets.TryGetValue(key, out value))
     {
      value = NormalizeLegacyOffset(x, z, value);
      if (value.sqrMagnitude < 0.0001f) offsets.Remove(key);
      else offsets[key] = value;
     }
    }
    legacyOffsetKeys.Remove(key);
   }
  }

  static string JsonFloat(float value)
  {
   if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
   return value.ToString("R", CultureInfo.InvariantCulture);
  }
 }
}
