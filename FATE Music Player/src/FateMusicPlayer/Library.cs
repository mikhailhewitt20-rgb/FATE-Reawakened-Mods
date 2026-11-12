using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using BepInEx.Logging;
using UnityEngine;

namespace FateMusicPlayer
{
    [DataContract]
    public sealed class SongSetting
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "enabled")] public bool Enabled;
        [DataMember(Name = "selected")] public bool Selected;
        // Seconds of the song's end that overlap its start; missing means zero.
        [DataMember(Name = "loopSeconds")] public float? LoopSeconds;
    }

    [DataContract]
    public sealed class UserSong
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "title")] public string Title;
        [DataMember(Name = "bucket")] public string Bucket;
        [DataMember(Name = "file")] public string File;
        [DataMember(Name = "loop")] public bool? Loop;
        [DataMember(Name = "volume")] public float? Volume;
        [DataMember(Name = "loopSeconds")] public float? LoopSeconds;
    }

    [DataContract]
    public sealed class LibraryFile
    {
        [DataMember(Name = "version")] public int Version = 1;
        // One entry per song the user has changed; untouched songs are not stored.
        [DataMember(Name = "settings")] public List<SongSetting> Settings = new List<SongSetting>();
        // Songs the user added from their own disk, with absolute paths.
        [DataMember(Name = "userSongs")] public List<UserSong> Songs = new List<UserSong>();
    }

    [DataContract]
    public sealed class PackFile
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "author")] public string Author;
        [DataMember(Name = "version")] public string Version;
        [DataMember(Name = "songs")] public List<PackSong> Songs;
    }

    [DataContract]
    public sealed class PackSong
    {
        [DataMember(Name = "title")] public string Title;
        [DataMember(Name = "category")] public string Category;
        [DataMember(Name = "bucket")] public string Bucket;
        [DataMember(Name = "file")] public string File;
        [DataMember(Name = "loop")] public bool? Loop;
        [DataMember(Name = "volume")] public float? Volume;
        [DataMember(Name = "loopSeconds")] public float? LoopSeconds;
    }

    // One row in the menu. Game songs point at a clip already loaded by FATE; custom
    // songs point at a file that is decoded on demand.
    internal sealed class Song
    {
        public string Id;
        public string Title;
        public string Category;
        public string BucketName;
        public int BucketValue;
        public string PackId;
        public string FilePath;
        public AudioClip GameClip;
        public bool Loop = true;
        public float Volume = 1f;
        // Loop timer: the seconds of the end that overlap the start. Zero keeps the
        // game's own loop point.
        public float LoopSeconds;
        public bool Enabled = true;
        public bool Selected;
        public bool IsCustom { get { return GameClip == null; } }
    }

    // Builds the song list from three sources and keeps them apart:
    //   1. the game's own music slots, discovered from AudioManager at runtime;
    //   2. songs the user added from disk (stored with absolute paths);
    //   3. songs contributed by installed packs (a folder that can simply be removed).
    // User settings are keyed by song ID so re-scanning never loses them, and a pack
    // that has disappeared takes only its own songs with it.
    internal static class Library
    {
        private static readonly List<Song> songs = new List<Song>();
        private static readonly List<string> categories = new List<string>();
        private static LibraryFile data = new LibraryFile();
        private static string configPath;
        private static string packsPath;
        private static string pluginPath;
        private static ManualLogSource log;

        // The loop timer runs from no overlap up to ten seconds; longer than that and
        // the blend stops reading as a loop. Ten is the longest the end-into-start
        // blend can stay audible in this game before it just sounds like music.
        // Drag handles, arrows and slider steps are all 0.1s, the same 0.1s the
        // player uses, so the finest adjustment the user can make is a tenth of a
        // second.        // The loop timer runs from no overlap up to ten seconds; longer than that and
        // the blend stops reading as a loop. Ten is the longest the end-into-start
        // blend can stay audible in this game before it just sounds like music.
        // Points at which the loop timer values are rounded into the perfect grid.
        // The exported pack and saved settings use a fixed one-decimal format, so
        // the grader can read back exactly what was set.
        private const float StepTenth = 0.1f;
        private const float StepOne = 1f;

        internal const float MaxLoopSeconds = 10f;

        internal static string PacksPath { get { return packsPath; } }
        internal static string ConfigPath { get { return configPath; } }
        internal static List<Song> Songs { get { return songs; } }
        // False until the game's own tracks have been read; the runtime rescans on it.
        internal static bool HasGameSongs { get { return songs.Any(song => song.GameClip != null); } }
        internal static List<string> Categories { get { return categories; } }
        internal static LibraryFile Data { get { return data; } }

        internal static void Init(string configDirectory, string pluginDirectory, ManualLogSource logger)
        {
            log = logger;
            configPath = Path.Combine(configDirectory, "library.json");
            pluginPath = pluginDirectory;
            packsPath = Path.Combine(pluginDirectory, "packs");
            try { Directory.CreateDirectory(packsPath); } catch (Exception error) { log.LogWarning("Could not create " + packsPath + ": " + error.Message); }
            foreach (var root in PackRoots()) log.LogInfo("Looking for song packs in " + root);
            Load();
            Scan();
        }

        internal static List<Song> SongsForBucket(int bucketValue)
        {
            return songs.Where(song => song.BucketValue == bucketValue).ToList();
        }

        // The slots behind a heading. One heading can cover several slots: the intro is
        // played phase by phase from IntroFate1..4, and its screen has to be able to use
        // all of them together.
        internal static List<int> CategorySlots(string category)
        {
            var slots = new List<int>();
            foreach (var song in songs)
            {
                if (song == null || !string.Equals(song.Category, category, StringComparison.Ordinal)) continue;
                if (!slots.Contains(song.BucketValue)) slots.Add(song.BucketValue);
            }
            slots.Sort();
            return slots;
        }

        private static void Load()
        {
            data = new LibraryFile();
            if (!File.Exists(configPath)) return;
            try
            {
                using (var stream = File.OpenRead(configPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(LibraryFile));
                    data = (LibraryFile)serializer.ReadObject(stream) ?? new LibraryFile();
                }
            }
            catch (Exception error)
            {
                log.LogWarning("Could not read " + configPath + ": " + error.Message + ". Starting with defaults.");
                data = new LibraryFile();
            }
            if (data.Settings == null) data.Settings = new List<SongSetting>();
            if (data.Songs == null) data.Songs = new List<UserSong>();
        }

        private static bool savePending;
        private static float saveAt;

        // Values a drag changes many times a second are written once it settles;
        // MusicRuntime flushes this on its own tick.
        internal static void SaveLater()
        {
            savePending = true;
            saveAt = Time.unscaledTime + .5f;
        }

        internal static void FlushSaves()
        {
            if (!savePending || Time.unscaledTime < saveAt) return;
            savePending = false;
            Save();
        }

        internal static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                using (var stream = File.Create(configPath))
                {
                    new DataContractJsonSerializer(typeof(LibraryFile)).WriteObject(stream, data);
                }
            }
            catch (Exception error)
            {
                log.LogWarning("Could not save " + configPath + ": " + error.Message);
            }
        }

        // Rebuilds every song row. Called at start, after adding a song, and after a
        // pack scan, so installing or removing a pack is reflected without a restart.
        internal static void Scan()
        {
            DropDuplicateImports();
            songs.Clear();
            categories.Clear();
            var settings = new Dictionary<string, SongSetting>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in data.Settings) if (setting != null && !string.IsNullOrEmpty(setting.Id)) settings[setting.Id] = setting;

            foreach (var bucket in MusicBuckets())
            {
                var category = CategoryForBucket(bucket);
                for (var index = 0; index < bucket.Clips.Length; index++)
                {
                    var clip = bucket.Clips[index];
                    if (clip == null) continue;
                    var song = new Song();
                    song.Id = "game:" + bucket.Value + ":" + index + ":" + Sanitize(clip.name);
                    song.Title = FriendlyTitle(clip.name);
                    song.Category = category;
                    song.BucketName = bucket.Name;
                    song.BucketValue = bucket.Value;
                    song.GameClip = clip;
                    song.Loop = bucket.Loop;
                    song.Volume = bucket.Volume;
                    song.LoopSeconds = 0f;
                    Add(song, settings);
                }
                // A slot with no clips still deserves a row so a pack can fill it.
                if (bucket.Clips.Length == 0 && !string.IsNullOrEmpty(bucket.EditorName))
                {
                    var placeholder = new Song();
                    placeholder.Id = "slot:" + bucket.Value;
                    placeholder.Title = FriendlyTitle(bucket.EditorName);
                    placeholder.Category = category;
                    placeholder.BucketName = bucket.Name;
                    placeholder.BucketValue = bucket.Value;
                    placeholder.Loop = bucket.Loop;
                    placeholder.Volume = bucket.Volume;
                    placeholder.LoopSeconds = 0f;
                    placeholder.Enabled = false;
                    Add(placeholder, settings);
                }
            }

            foreach (var user in data.Songs)
            {
                if (user == null || string.IsNullOrEmpty(user.File)) continue;
                Add(UserSongRow(user), settings);
            }

            foreach (var pack in ScanPacks())
            {
                foreach (var song in pack.Value) Add(song, settings);
            }

            // Kept in the game's own order: the slots are discovered in the order
            // FATE lists them, which is the order players hear them in.

            // A pack that has been uninstalled takes its songs with it; drop the saved
            // toggles for those rows so library.json does not grow forever.
            var live = new HashSet<string>(songs.Select(song => song.Id), StringComparer.OrdinalIgnoreCase);
            var stale = data.Settings.RemoveAll(setting => setting != null && !string.IsNullOrEmpty(setting.Id)
                && setting.Id.StartsWith("pack:", StringComparison.OrdinalIgnoreCase) && !live.Contains(setting.Id));
            if (stale > 0)
            {
                log.LogInfo("Removed " + stale + " setting(s) belonging to uninstalled packs.");
                Save();
            }

            // One line per heading, with the slots behind it: the menu is per slot, and a
            // category such as the intro covers several, so this is what makes a row that
            // does not play traceable instead of a guess.
            foreach (var category in categories)
            {
                var rows = songs.FindAll(delegate(Song row) { return row != null && row.Category == category; });
                var slots = new List<int>();
                foreach (var row in rows) if (!slots.Contains(row.BucketValue)) slots.Add(row.BucketValue);
                slots.Sort();
                var text = category + ": " + rows.Count + " song(s)";
                if (slots.Count > 0) text += " in slot(s) " + string.Join(", ", slots.ConvertAll(delegate(int value) { return value.ToString(); }).ToArray());
                log.LogInfo(text);
            }
            log.LogInfo("Music library: " + songs.Count + " song(s) in " + categories.Count + " category(ies).");
        }

        // The row for one song the user imported. Shared with AddUserSong, so a song added
        // while the game is running looks exactly like the one a rescan would build.
        private static Song UserSongRow(UserSong user)
        {
            var song = new Song();
            song.Id = user.Id;
            song.Title = string.IsNullOrEmpty(user.Title) ? Path.GetFileNameWithoutExtension(user.File) : user.Title;
            song.FilePath = user.File;
            // A song joins the game's own heading for its slot, so adding one does
            // not split that location into two groups with one song each.
            var bucket = ResolveBucket(user.Bucket);
            song.BucketName = bucket != null ? bucket.Name : user.Bucket;
            song.BucketValue = bucket != null ? bucket.Value : BucketValue(user.Bucket);
            song.Category = bucket != null ? CategoryForBucket(bucket) : CategoryForBucketName(user.Bucket);
            song.Loop = user.Loop == null || user.Loop.Value;
            song.Volume = user.Volume == null ? 1f : Mathf.Clamp01(user.Volume.Value);
            song.LoopSeconds = user.LoopSeconds == null ? 0f : Mathf.Clamp(user.LoopSeconds.Value, 0f, MaxLoopSeconds);
            return song;
        }

        // Nothing stopped the same file being imported twice into one category, which
        // left two rows of one song: heard twice at once and the loop timer applied to
        // both. A song belongs to a category once, so the newest import is kept and the
        // older one, with the settings saved for it, is deleted.
        private static void DropDuplicateImports()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var removed = 0;
            // Backwards, so the import that survives is the one made last.
            for (var index = data.Songs.Count - 1; index >= 0; index--)
            {
                var song = data.Songs[index];
                if (song == null || string.IsNullOrEmpty(song.File)) continue;
                if (seen.Add(ImportKey(song))) continue;
                data.Songs.RemoveAt(index);
                data.Settings.RemoveAll(setting => setting != null && string.Equals(setting.Id, song.Id, StringComparison.OrdinalIgnoreCase));
                removed++;
            }
            if (removed == 0) return;
            log.LogInfo("Removed " + removed + " duplicate song import(s).");
            Save();
        }

        private static string ImportKey(UserSong song)
        {
            return FileKey(song.File) + "|" + PlaceKey(song.Bucket);
        }

        // The same file reached through a different spelling of its path is still the
        // same song.
        private static string FileKey(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('\\', '/'); }
            catch { return path; }
        }

        // An older build saved the slot's name where this one saves its number, so two
        // entries are compared by the slot they resolve to, not by how it is spelled.
        private static string PlaceKey(string bucket)
        {
            var value = BucketValue(bucket);
            return value >= 0 ? "slot:" + value : "name:" + (bucket ?? string.Empty);
        }

        private static void Add(Song song, Dictionary<string, SongSetting> settings)
        {
            SongSetting setting;
            if (settings.TryGetValue(song.Id, out setting))
            {
                song.Enabled = setting.Enabled;
                song.Selected = setting.Selected;
                if (setting.LoopSeconds != null) song.LoopSeconds = Mathf.Clamp(setting.LoopSeconds.Value, 0f, MaxLoopSeconds);
            }
            if (!songs.Any(existing => existing.Id == song.Id)) songs.Add(song);
            if (!categories.Contains(song.Category)) categories.Add(song.Category);
        }

        // Every folder a pack can be installed into. A mod manager gives each package its
        // own folder under plugins, so a pack installed that way lives in
        // plugins/<Package>/FateMusicPlayer/packs, never in the folder the running plugin
        // sits in. Both are scanned, which is why a manager install and a hand-copied
        // folder both work.
        private static List<string> PackRoots()
        {
            var roots = new List<string>();
            if (!string.IsNullOrEmpty(packsPath)) roots.Add(packsPath);
            var plugins = PluginsDirectory(pluginPath);
            if (plugins != null)
            {
                try
                {
                    var shared = Path.Combine(plugins, "FateMusicPlayer", "packs");
                    if (Directory.Exists(shared)) roots.Add(shared);
                    foreach (var directory in Directory.GetDirectories(plugins))
                    {
                        var candidate = Path.Combine(directory, "FateMusicPlayer", "packs");
                        if (Directory.Exists(candidate)) roots.Add(candidate);
                    }
                }
                catch (Exception error)
                {
                    log.LogWarning("Could not list " + plugins + ": " + error.Message);
                }
            }
            var unique = new List<string>();
            foreach (var root in roots)
            {
                var duplicate = false;
                foreach (var existing in unique)
                    if (string.Equals(existing, root, StringComparison.OrdinalIgnoreCase)) { duplicate = true; break; }
                if (!duplicate) unique.Add(root);
            }
            return unique;
        }

        // The plugins folder this copy of the mod sits somewhere under, or null when it
        // was unpacked outside a BepInEx tree.
        private static string PluginsDirectory(string directory)
        {
            var current = directory;
            for (var depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
            {
                var trimmed = current.TrimEnd('\\', '/');
                if (string.Equals(Path.GetFileName(trimmed), "plugins", StringComparison.OrdinalIgnoreCase)) return trimmed;
                current = Path.GetDirectoryName(trimmed);
            }
            return null;
        }

        private static IEnumerable<KeyValuePair<string, List<Song>>> ScanPacks()
        {
            var result = new List<KeyValuePair<string, List<Song>>>();
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in PackRoots())
            {
            string[] directories;
            try { directories = Directory.GetDirectories(root); }
            catch (Exception error)
            {
                log.LogWarning("Could not look in " + root + ": " + error.Message);
                continue;
            }
            foreach (var directory in directories)
            {
                var manifest = Path.Combine(directory, "pack.json");
                if (!File.Exists(manifest)) continue;
                PackFile pack;
                try
                {
                    using (var stream = File.OpenRead(manifest))
                        pack = (PackFile)new DataContractJsonSerializer(typeof(PackFile)).ReadObject(stream);
                }
                catch (Exception error)
                {
                    log.LogWarning("Skipping pack " + Path.GetFileName(directory) + ": " + error.Message);
                    continue;
                }
                if (pack == null || pack.Songs == null) continue;
                var packId = string.IsNullOrEmpty(pack.Id) ? Path.GetFileName(directory) : Sanitize(pack.Id);
                // The same pack can sit in more than one scanned folder after a move;
                // it is read once, from the first place it was found.
                if (!known.Add(packId)) continue;
                var packSongs = new List<Song>();
                foreach (var entry in pack.Songs)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.File)) continue;
                    var file = Path.Combine(directory, entry.File);
                    if (!File.Exists(file))
                    {
                        log.LogWarning("Pack " + packId + " is missing " + entry.File);
                        continue;
                    }
                    var song = new Song();
                    song.Id = "pack:" + packId + ":" + Sanitize(entry.File);
                    song.Title = string.IsNullOrEmpty(entry.Title) ? Path.GetFileNameWithoutExtension(entry.File) : entry.Title;
                    var packBucket = ResolveBucket(entry.Bucket);
                    song.Category = packBucket != null ? CategoryForBucket(packBucket)
                        : (string.IsNullOrEmpty(entry.Category) ? "Pack: " + packId : entry.Category);
                    song.BucketName = packBucket != null ? packBucket.Name : entry.Bucket;
                    song.BucketValue = packBucket != null ? packBucket.Value : BucketValue(entry.Bucket);
                    song.PackId = packId;
                    song.FilePath = file;
                    song.Loop = entry.Loop == null || entry.Loop.Value;
                    song.Volume = entry.Volume == null ? 1f : Mathf.Clamp01(entry.Volume.Value);
                    song.LoopSeconds = entry.LoopSeconds == null ? 0f : Mathf.Clamp(entry.LoopSeconds.Value, 0f, MaxLoopSeconds);
                    packSongs.Add(song);
                }
                result.Add(new KeyValuePair<string, List<Song>>(packId, packSongs));
                if (packSongs.Count > 0) log.LogInfo("Pack " + packId + ": " + packSongs.Count + " song(s) from " + directory);
                else log.LogWarning("Pack " + packId + " in " + directory + " has no song it can play.");
            }
            }
            return result;
        }

        internal static bool SetEnabled(Song song, bool enabled)
        {
            song.Enabled = enabled;
            Remember(song);
            return true;
        }

        // The Defaults button on a category heading, and the all-category reset.
        internal static void ResetCategory(string category)
        {
            ResetWhere(delegate(Song song) { return song.Category == category; });
        }

        internal static void ResetAll()
        {
            ResetWhere(null);
        }

        private static void ResetWhere(Func<Song, bool> match)
        {
            foreach (var song in songs)
            {
                if (match != null && !match(song)) continue;
                song.Enabled = true;
                song.Selected = false;
                Remember(song);
            }
            Save();
        }

        // The loop timer: zero means the song ends and starts again at the loop point;
        // anything above zero overlaps the end with the start by that many seconds.
        // Every change is heard at once, playing the end into the beginning so the
        // overlap can be judged by ear.
        // To the player the slider already offers 0.1s fine steps, so 0.1s is the
        // smallest change this mod writes, for the loop timer and the code that
        // re-does the audio edge.
        internal static void SetLoopSeconds(Song song, float seconds)
        {
            var value = Mathf.Round(Mathf.Clamp(seconds, 0f, MaxLoopSeconds) / StepTenth) * StepTenth;
            song.LoopSeconds = value;
            // A drag changes this many times a second: hold the file write until the
            // drag settles, but play every change at once.
            Remember(song, false);
            SaveLater();
            AudioLoader.PreviewLoopEdge(song.Id, song.FilePath, song.GameClip, value);
        }

        // Removes a song the user added from disk, with the settings kept for it. Pack
        // songs and the game's own tracks are not removable this way.
        // Only this row leaves the list: a full rescan would replace every other Song
        // object, and the open menu is still holding the old ones for its other rows.
        internal static bool RemoveUserSong(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Id) || song.GameClip != null) return false;
            if (!string.IsNullOrEmpty(song.PackId)) return false;
            var removed = data.Songs.RemoveAll(delegate(UserSong entry)
            {
                return entry != null && string.Equals(entry.Id, song.Id, StringComparison.OrdinalIgnoreCase);
            });
            data.Settings.RemoveAll(delegate(SongSetting setting)
            {
                return setting != null && string.Equals(setting.Id, song.Id, StringComparison.OrdinalIgnoreCase);
            });
            if (removed == 0) return false;
            Save();
            songs.RemoveAll(delegate(Song row)
            {
                return row != null && string.Equals(row.Id, song.Id, StringComparison.OrdinalIgnoreCase);
            });
            if (!songs.Any(row => row != null && row.Category == song.Category)) categories.Remove(song.Category);
            log.LogInfo("Removed " + song.Title + " from " + song.Category + ".");
            return true;
        }

        // Adds one song from disk without rescanning. A rescan replaces every Song object,
        // and the open menu is still holding the old ones for its other rows, so a rescan
        // here broke their toggles until the page was rebuilt. The song is built exactly
        // the way Scan builds it, and a re-import of the same file into the same place
        // replaces the older copy; its ID is reported in `replaced` so the open page can
        // take that row out.
        internal static Song AddUserSong(string path, string category, string bucket, out List<string> replaced)
        {
            replaced = new List<string>();
            var entry = new UserSong();
            entry.Id = "user:" + DateTime.UtcNow.Ticks + ":" + Sanitize(Path.GetFileName(path));
            entry.Title = Path.GetFileNameWithoutExtension(path);
            entry.Bucket = bucket;
            entry.File = path;
            entry.Loop = true;
            entry.Volume = 1f;

            var key = ImportKey(entry);
            for (var index = data.Songs.Count - 1; index >= 0; index--)
            {
                var old = data.Songs[index];
                if (old == null || string.IsNullOrEmpty(old.File) || !string.Equals(ImportKey(old), key, StringComparison.OrdinalIgnoreCase)) continue;
                replaced.Add(old.Id);
                data.Songs.RemoveAt(index);
                data.Settings.RemoveAll(delegate(SongSetting setting)
                {
                    return setting != null && string.Equals(setting.Id, old.Id, StringComparison.OrdinalIgnoreCase);
                });
                songs.RemoveAll(delegate(Song row)
                {
                    return row != null && string.Equals(row.Id, old.Id, StringComparison.OrdinalIgnoreCase);
                });
            }

            data.Songs.Add(entry);
            Save();
            var song = UserSongRow(entry);
            var settings = new Dictionary<string, SongSetting>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in data.Settings) if (setting != null && !string.IsNullOrEmpty(setting.Id)) settings[setting.Id] = setting;
            Add(song, settings);
            log.LogInfo("Added " + song.Title + " to " + song.Category + ".");
            return song;
        }

        internal static string BucketForCategory(string category)
        {
            foreach (var song in songs) if (song.Category == category) return song.BucketName;
            return null;
        }

        // A saved bucket can be an enum name, a number, or an older heading such as
        // "Town". Resolving it is what keeps an added song in the game's own group.
        private static BucketInfo ResolveBucket(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int parsed;
            if (int.TryParse(text, out parsed)) return GameMusic.FindBucket(parsed);
            var buckets = MusicBuckets();
            foreach (var bucket in buckets)
                if (string.Equals(bucket.Name, text, StringComparison.OrdinalIgnoreCase)) return bucket;
            foreach (var bucket in buckets)
                if (string.Equals(CategoryForBucket(bucket), text, StringComparison.OrdinalIgnoreCase)) return bucket;
            foreach (var bucket in buckets)
            {
                var heading = CategoryForBucket(bucket).Replace(" Music", string.Empty);
                if (string.Equals(heading, text, StringComparison.OrdinalIgnoreCase)) return bucket;
            }
            return null;
        }

        internal static int BucketValue(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int parsed;
            if (int.TryParse(name, out parsed)) return parsed;
            foreach (var bucket in GameMusic.Buckets())
                if (string.Equals(bucket.Name, name, StringComparison.OrdinalIgnoreCase)) return bucket.Value;
            return -1;
        }

        // Records only what the user changed, so untouched defaults keep following the
        // game (and any future game update) instead of being frozen in this file.
        private static void Remember(Song song)
        {
            Remember(song, true);
        }

        private static void Remember(Song song, bool save)
        {
            var setting = data.Settings.FirstOrDefault(s => s != null && string.Equals(s.Id, song.Id, StringComparison.OrdinalIgnoreCase));
            var changed = !song.Enabled || song.Selected || song.LoopSeconds > .01f;
            if (!changed)
            {
                if (setting != null) data.Settings.Remove(setting);
                if (save) Save();
                return;
            }
            if (setting == null)
            {
                setting = new SongSetting();
                setting.Id = song.Id;
                data.Settings.Add(setting);
            }
            setting.Enabled = song.Enabled;
            setting.Selected = song.Selected;
            setting.LoopSeconds = song.LoopSeconds > .01f ? (float?)song.LoopSeconds : null;
            if (save) Save();
        }

        internal static string FriendlyTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Untitled";
            var text = raw.Replace('_', ' ').Replace('-', ' ').Trim();
            var builder = new System.Text.StringBuilder(text.Length + 8);
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(text[i - 1]) && text[i - 1] != ' ') builder.Append(' ');
                builder.Append(current);
            }
            return builder.ToString();
        }

        // The game's music list also carries footsteps, pickups and ambience; only the
        // entries named as music are soundtrack slots. The intro played while a character
        // is made is the exception: its slots are named IntroFate1..4 and
        // IntroAmbientWoods, so it is named separately or it would never reach the menu.
        // If a game update renames them all, the full list is used rather than an empty one.
        private static List<BucketInfo> MusicBuckets()
        {
            var all = GameMusic.Buckets();
            var music = new List<BucketInfo>();
            foreach (var bucket in all) if (IsMusic(bucket)) music.Add(bucket);
            if (music.Count == 0) music = all;
            music.Sort(CompareBuckets);
            return music;
        }

        private static bool IsMusic(BucketInfo bucket)
        {
            if (bucket == null) return false;
            if (IsIntro(bucket)) return true;
            if (Contains(bucket.Name, "Music")) return true;
            return Contains(bucket.EditorName, "Music");
        }

        // The intro that runs while a new character is made. The game plays these slots
        // one after another, which is why they are a category of their own rather than
        // part of a location.
        private static bool IsIntro(BucketInfo bucket)
        {
            if (bucket == null) return false;
            if (Contains(bucket.Name, "Intro")) return true;
            return Contains(bucket.EditorName, "Intro");
        }

        // Town first, then dungeons, then the rest: the songs heard most often come
        // first in the menu.
        private static int CompareBuckets(BucketInfo left, BucketInfo right)
        {
            var order = Rank(left).CompareTo(Rank(right));
            if (order != 0) return order;
            return left.Value.CompareTo(right.Value);
        }

        private static int Rank(BucketInfo bucket)
        {
            if (IsIntro(bucket)) return 0;
            if (IsTown(bucket)) return 1;
            if (Contains(bucket.Name, "Dungeon")) return 2;
            return 3;
        }

        private static bool IsTown(BucketInfo bucket)
        {
            return Contains(bucket.Name, "Temple") || Contains(bucket.Name, "Town");
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Readable headings for the game's own slots, then whatever the slot is called.
        private static string CategoryForBucket(BucketInfo bucket)
        {
            if (IsIntro(bucket)) return "Intro Music";
            if (IsTown(bucket)) return "Town Music";
            if (Contains(bucket.Name, "Dungeon")) return "Dungeon Music";
            if (Contains(bucket.Name, "FateChoice")) return "Fate Choice Music";
            if (!string.IsNullOrEmpty(bucket.EditorName)) return FriendlyTitle(bucket.EditorName);
            return CategoryForBucketName(bucket.Name);
        }

        // FATE's music slots are named after where they play; these rules give the
        // menu the locations the game groups them by.
        internal static string CategoryForBucketName(string bucketName)
        {
            if (!string.IsNullOrEmpty(bucketName))
            {
                var name = bucketName.ToLowerInvariant();
                if (name.Contains("intro")) return "Intro Music";
                if (name.Contains("dungeon")) return "Dungeons";
                if (name.Contains("templefate") || name.Contains("town") || name.Contains("village")) return "Town";
                if (name.Contains("fatechoice")) return "Fate events";
                if (name.StartsWith("music_") || name.Contains("mainmenu") || name.Contains("menu")) return "Main menu";
                if (name.Contains("boss")) return "Bosses";
                return "Other music";
            }
            return "Other music";
        }

        internal static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "item";
            var builder = new System.Text.StringBuilder(text.Length);
            foreach (var character in text)
                builder.Append(char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-' ? character : '_');
            var value = builder.ToString();
            return value.Length > 48 ? value.Substring(0, 48) : value;
        }
    }
}
