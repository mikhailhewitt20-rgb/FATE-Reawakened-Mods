using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 [Serializable]
 public sealed class WallLayoutEntry
 {
  public string style;
  public string id;
  public int type;
  public float x;
  public float y;
  public float z;
  public float rx;
  public float ry;
  public float rz;
  public bool added;
  public bool removed;
  public bool reset;
 }

 [Serializable]
 public sealed class WallLayoutFile
 {
  public int version = 1;
  public List<WallLayoutEntry> walls = new List<WallLayoutEntry>();
 }

 // Per-wall-piece layout overrides. Styles are part of the key so an edit to one tileset
 // never moves or removes pieces from another tileset.
 internal static class WallLayout
 {
  static readonly Dictionary<string, WallLayoutEntry> entries = new Dictionary<string, WallLayoutEntry>();
  static bool loaded;
  static string pluginDir;

  public static void EnsureLoaded()
  {
   if (loaded) return;
   loaded = true;
   entries.Clear();
   LoadFrom(BakedPath());
   LoadFrom(UserPath());
   PermanentStoragePlugin.Log.LogInfo("Wall layout loaded: " + entries.Count + " customized wall piece(s).");
  }

  public static WallLayoutEntry Get(string style, string id)
  {
   EnsureLoaded();
   WallLayoutEntry entry;
   return entries.TryGetValue(Key(style, id), out entry) ? entry : null;
  }

  public static List<WallLayoutEntry> ForStyle(string style)
  {
   EnsureLoaded();
   var result = new List<WallLayoutEntry>();
   foreach (var entry in entries.Values)
   {
    if (entry != null && string.Equals(entry.style, style, StringComparison.Ordinal)) result.Add(entry);
   }
   return result;
  }

  public static int Count
  {
   get { EnsureLoaded(); return entries.Count; }
  }

  public static void SaveMarker(WallTileMarker marker, bool added, bool removed)
  {
   if (marker == null || string.IsNullOrEmpty(marker.Id) || string.IsNullOrEmpty(marker.StyleKey)) return;
   EnsureLoaded();
   Vector3 relative = marker.transform.position - RoomBuilder.WallOrigin;
   Vector3 rotation = marker.transform.eulerAngles;
   var entry = new WallLayoutEntry
   {
    style = marker.StyleKey,
    id = marker.Id,
    type = marker.Type,
    x = relative.x,
    y = relative.y,
    z = relative.z,
    rx = rotation.x,
    ry = rotation.y,
    rz = rotation.z,
    added = added,
    removed = removed
   };
   entries[Key(entry.style, entry.id)] = entry;
  }

  public static void ResetMarker(WallTileMarker marker)
  {
   if (marker == null) return;
   EnsureLoaded();
   entries[Key(marker.StyleKey, marker.Id)] = new WallLayoutEntry
   {
    style = marker.StyleKey,
    id = marker.Id,
    type = marker.Type,
    reset = true
   };
  }

  public static void SaveUser()
  {
   string path = UserPath();
   if (SaveTo(path)) PermanentStoragePlugin.Log.LogInfo("Wall layout saved (" + Count + ") to " + path + ".");
  }

  public static string BakeForRelease()
  {
   try
   {
    if (!SaveTo(UserPath())) return null;
    string baked = BakedPath();
    if (!SaveTo(baked)) return null;
    PermanentStoragePlugin.Log.LogInfo("Wall layout baked for release (" + Count + " record(s)) to " + baked + ".");
    return baked;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not bake wall layout: " + ex.Message);
    return null;
   }
  }

  static string Key(string style, string id) { return (style ?? "") + "\n" + (id ?? ""); }

  static string UserPath()
  {
   string config = null;
   try { config = BepInEx.Paths.ConfigPath; } catch { }
   if (string.IsNullOrEmpty(config)) config = Path.Combine(Application.persistentDataPath, "BepInEx", "config");
   return Path.Combine(config, "mikhailkillszombies.fate.permanentstorage.wall_layout.json");
  }

  static string BakedPath() { return Path.Combine(PluginDirectory(), "wall_layout.baked.json"); }

  static string PluginDirectory()
  {
   if (pluginDir != null) return pluginDir;
   try
   {
    string location = Assembly.GetExecutingAssembly().Location;
    if (!string.IsNullOrEmpty(location)) pluginDir = Path.GetDirectoryName(location);
   }
   catch { }
   if (string.IsNullOrEmpty(pluginDir)) pluginDir = Path.Combine(Application.persistentDataPath, "BepInEx", "plugins");
   return pluginDir;
  }

  static void LoadFrom(string path)
  {
   try
   {
    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
    var file = JsonUtility.FromJson<WallLayoutFile>(File.ReadAllText(path));
    if (file == null || file.walls == null) return;
    foreach (var entry in file.walls)
    {
     if (entry == null || string.IsNullOrEmpty(entry.style) || string.IsNullOrEmpty(entry.id)) continue;
     entries[Key(entry.style, entry.id)] = entry;
    }
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not read wall layout from " + path + ": " + ex.Message);
   }
  }

  static bool SaveTo(string path)
  {
   try
   {
    string directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
    var file = new WallLayoutFile();
    file.walls.AddRange(entries.Values);
    file.walls.Sort((a, b) =>
    {
     int style = string.CompareOrdinal(a.style, b.style);
     return style != 0 ? style : string.CompareOrdinal(a.id, b.id);
    });
    var json = new StringBuilder();
    json.Append("{\n  \"version\": 1,\n  \"walls\": [");
    for (int i = 0; i < file.walls.Count; i++)
    {
     var entry = file.walls[i];
     json.Append(i == 0 ? "\n    {" : ",\n    {");
     json.Append("\"style\": \"").Append(JsonString(entry.style)).Append("\"");
     json.Append(", \"id\": \"").Append(JsonString(entry.id)).Append("\"");
     json.Append(", \"type\": ").Append(entry.type);
     json.Append(", \"x\": ").Append(JsonFloat(entry.x));
     json.Append(", \"y\": ").Append(JsonFloat(entry.y));
     json.Append(", \"z\": ").Append(JsonFloat(entry.z));
     json.Append(", \"rx\": ").Append(JsonFloat(entry.rx));
     json.Append(", \"ry\": ").Append(JsonFloat(entry.ry));
     json.Append(", \"rz\": ").Append(JsonFloat(entry.rz));
     json.Append(", \"added\": ").Append(entry.added ? "true" : "false");
     json.Append(", \"removed\": ").Append(entry.removed ? "true" : "false");
     json.Append(", \"reset\": ").Append(entry.reset ? "true" : "false").Append("}");
    }
    if (file.walls.Count > 0) json.Append('\n').Append("  ");
    json.Append("]\n}");
    File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
    PermanentStoragePlugin.Log.LogInfo("Wrote " + file.walls.Count + " wall layout record(s) to " + path + ".");
    return true;
   }
   catch (Exception ex)
   {
    PermanentStoragePlugin.Log.LogWarning("Could not write wall layout to " + path + ": " + ex.Message);
    return false;
   }
  }

  static string JsonFloat(float value)
  {
   if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
   return value.ToString("R", CultureInfo.InvariantCulture);
  }

  static string JsonString(string value)
  {
   if (string.IsNullOrEmpty(value)) return "";
   return value.Replace("\\", "\\\\").Replace("\"", "\\\"")
    .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
  }
 }
}
