using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;

namespace FateMusicPlayer
{
    // The two small native windows: one to pick a song file, one to choose where an
    // exported pack goes. Both run on their own STA thread because Unity's main thread
    // is not an apartment thread and WinForms dialogs need one.
    internal static class Dialogs
    {
        internal static string PickAudioFile()
        {
            string result = null;
            Run(() =>
            {
                using (var dialog = new System.Windows.Forms.OpenFileDialog())
                {
                    dialog.Title = "Add a song";
                    dialog.Filter = "Audio files (*.ogg;*.wav;*.mp3;*.aiff)|*.ogg;*.wav;*.mp3;*.aiff|All files (*.*)|*.*";
                    dialog.CheckFileExists = true;
                    dialog.Multiselect = false;
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) result = dialog.FileName;
                }
            });
            return result;
        }

        internal static string PickExportFile(string suggested)
        {
            string result = null;
            Run(() =>
            {
                using (var dialog = new System.Windows.Forms.SaveFileDialog())
                {
                    dialog.Title = "Export song pack";
                    dialog.Filter = "Thunderstore package (*.zip)|*.zip";
                    dialog.FileName = suggested;
                    dialog.OverwritePrompt = true;
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) result = dialog.FileName;
                }
            });
            return result;
        }

        private static void Run(Action work)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { work(); }
                catch (Exception error) { failure = error; }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null && FateMusicPlayerPlugin.LogSource != null)
                FateMusicPlayerPlugin.LogSource.LogError("The file window could not open: " + failure.Message);
        }
    }

    // Writes a Thunderstore-ready zip whose only payload is a song pack folder. The
    // pack inside is exactly what the player scans at startup, so an export can be
    // installed by anyone (including the author) to get the same categories back.
    internal static class PackExporter
    {
        internal static string Export()
        {
            // The live rows are what the menu shows and what the player hears: the saved
            // add-song entries still hold the values from the moment the file was picked,
            // so tuning the loop timer and then exporting used to ship the old numbers.
            var songs = Library.Songs.Where(song => song.GameClip == null
                && string.IsNullOrEmpty(song.PackId)
                && !string.IsNullOrEmpty(song.FilePath)).ToList();
            if (songs.Count == 0)
            {
                Message("There are no songs of your own to export yet. Use ADD SONG under a category first.");
                return null;
            }
            var path = Dialogs.PickExportFile("FATE Music Pack.zip");
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var packId = Library.Sanitize(Path.GetFileNameWithoutExtension(path));
                if (string.IsNullOrEmpty(packId)) packId = "SongPack";
                var written = WritePack(path, packId, songs);
                Message("Exported " + written + " song(s) to " + path + ". Requires "
                    + ModId + "-" + FateMusicPlayerPlugin.Version + ".");
                return path;
            }
            catch (Exception error)
            {
                if (FateMusicPlayerPlugin.LogSource != null) FateMusicPlayerPlugin.LogSource.LogError("Export failed: " + error);
                Message("Export failed: " + error.Message);
                return null;
            }
        }

        // Returns how many songs made it into the package. A song whose file has been
        // moved is left out of pack.json as well, so an installed pack never advertises a
        // track it cannot play (that entry used to be listed and then skipped silently).
        private static int WritePack(string zipPath, string packId, List<Song> songs)
        {
            var packRoot = "BepInEx/plugins/FateMusicPlayer/packs/" + packId + "/";
            var entries = new List<KeyValuePair<Song, string>>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skipped = 0;
            foreach (var song in songs)
            {
                if (!File.Exists(song.FilePath))
                {
                    skipped++;
                    if (FateMusicPlayerPlugin.LogSource != null)
                        FateMusicPlayerPlugin.LogSource.LogWarning("Not exporting " + song.Title + ": " + song.FilePath + " is no longer there.");
                    continue;
                }
                // Two picks named the same (a.ogg in two folders) must not collapse into
                // one entry, which silently dropped a song from the package.
                var name = Path.GetFileName(song.FilePath);
                var unique = name;
                var index = 2;
                while (!names.Add(unique))
                {
                    unique = Path.GetFileNameWithoutExtension(name) + "-" + index + Path.GetExtension(name);
                    index++;
                }
                entries.Add(new KeyValuePair<Song, string>(song, unique));
            }
            if (entries.Count == 0) throw new IOException("none of the song files are still on disk");

            var buffer = new byte[81920];
            using (var stream = File.Create(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "manifest.json", BuildManifest(packId, entries.Count));
                WriteEntry(archive, "README.md", BuildReadme(packId, entries));
                WriteEntry(archive, packRoot + "pack.json", BuildPackJson(packId, entries));
                foreach (var entry in entries)
                {
                    var target = archive.CreateEntry(packRoot + entry.Value, System.IO.Compression.CompressionLevel.Optimal);
                    using (var output = target.Open())
                    using (var source = File.OpenRead(entry.Key.FilePath))
                    {
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, read);
                    }
                }
            }
            Verify(zipPath, packRoot);
            if (skipped > 0) Message("Skipped " + skipped + " song(s) whose file is no longer on disk.");
            return entries.Count;
        }

        // A package that was written but cannot be read back is worse than a clear
        // failure, so the finished zip is opened again and its required entries checked.
        private static void Verify(string zipPath, string packRoot)
        {
            var required = new[] { "manifest.json", "README.md", packRoot + "pack.json" };
            using (var archive = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read))
            {
                foreach (var name in required)
                    if (archive.GetEntry(name) == null) throw new InvalidDataException("the package is missing " + name);
                // A pack whose manifest does not name the mod installs to nothing: the
                // manager has no reason to bring the player along, so the songs are on disk
                // with no code that reads them. Check the finished zip for it too.
                using (var reader = new StreamReader(archive.GetEntry("manifest.json").Open()))
                {
                    var manifest = reader.ReadToEnd();
                    if (manifest.IndexOf(ModId + "-", StringComparison.Ordinal) < 0)
                        throw new InvalidDataException("the package does not depend on " + ModId);
                    // Thunderstore refuses a name with a dash, a dot or a space in it, and
                    // the whole package is rejected rather than renamed.
                    var name = ManifestName(manifest);
                    if (!IsThunderstoreName(name))
                        throw new InvalidDataException("Thunderstore cannot accept the package name \"" + name + "\"");
                }
            }
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content);
        }

        // Only the songs that are in the package are described, and every value comes from
        // the row the player tuned, so a pack sounds the way its author heard it.
        private static string BuildPackJson(string packId, List<KeyValuePair<Song, string>> songs)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"id\": \"").Append(Json(packId)).Append("\",\n");
            json.Append("  \"name\": \"").Append(Json(packId)).Append("\",\n");
            json.Append("  \"author\": \"\",\n");
            json.Append("  \"version\": \"1.0.0\",\n");
            json.Append("  \"songs\": [\n");
            for (var i = 0; i < songs.Count; i++)
            {
                var song = songs[i].Key;
                json.Append("    {\n");
                json.Append("      \"title\": \"").Append(Json(string.IsNullOrEmpty(song.Title) ? songs[i].Value : song.Title)).Append("\",\n");
                json.Append("      \"category\": \"").Append(Json(song.Category)).Append("\",\n");
                json.Append("      \"bucket\": \"").Append(Json(song.BucketName)).Append("\",\n");
                json.Append("      \"file\": \"").Append(Json(songs[i].Value)).Append("\",\n");
                json.Append("      \"loop\": ").Append(song.Loop ? "true" : "false").Append(",\n");
                json.Append("      \"volume\": ").Append(Mathf.Clamp01(song.Volume).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
                // The loop timer travels with the song so a pack sounds the way its
                // author tuned it, and the person installing it can still adjust it.
                json.Append("      \"loopSeconds\": ").Append(Mathf.Clamp(song.LoopSeconds, 0f, Library.MaxLoopSeconds).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append("\n");
                json.Append("    }").Append(i == songs.Count - 1 ? "\n" : ",\n");
            }
            json.Append("  ]\n}\n");
            return json.ToString();
        }

        // Thunderstore identifies a package as Author-Name-Version, and a pack is useless
        // without the mod that can read it. The player's own manifest already pulls in
        // BepInEx and FATE Mod Menu, and mod managers follow those transitively, so the
        // pack names the player and the player's version it was exported with.
        private const string Author = "MikhailKillsZombies";
        private const string ModName = "FATE_MusicPlayer";
        private const string ModId = Author + "-" + ModName;

        private static string BuildManifest(string packId, int count)
        {
            return "{\n" +
                   "  \"name\": \"" + Json(PackageName(packId)) + "\",\n" +
                   "  \"version_number\": \"1.0.0\",\n" +
                   "  \"website_url\": \"\",\n" +
                   "  \"description\": \"Song pack for FATE Music Player: " + count + " track(s).\",\n" +
                   "  \"dependencies\": [\n" +
                   "    \"" + ModId + "-" + FateMusicPlayerPlugin.Version + "\"\n" +
                   "  ]\n" +
                   "}\n";
        }

        // Thunderstore names are letters, digits and underscores only, so the file name
        // offers no shortcut: "FATE Music Pack v1.2.zip" goes up as FATE_Music_Pack_v1_2.
        // The pack folder keeps the file's own sanitized name, and nobody but the
        // manifest has to follow this rule.
        private static string PackageName(string packId)
        {
            var builder = new StringBuilder(packId == null ? 0 : packId.Length);
            foreach (var character in packId ?? "SongPack")
                builder.Append(IsNameCharacter(character) ? character : '_');
            var name = builder.ToString().Trim('_');
            if (name.Length > 64) name = name.Substring(0, 64);
            return name.Length < 3 ? "SongPack" : name;
        }

        private static bool IsNameCharacter(char character)
        {
            return (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9') || character == '_';
        }

        private static bool IsThunderstoreName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 128) return false;
            foreach (var character in name) if (!IsNameCharacter(character)) return false;
            return true;
        }

        private static string ManifestName(string manifest)
        {
            const string key = "\"name\": \"";
            var start = manifest.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return null;
            start += key.Length;
            var end = manifest.IndexOf('"', start);
            return end < 0 ? null : manifest.Substring(start, end - start);
        }

        private static string BuildReadme(string packId, List<KeyValuePair<Song, string>> songs)
        {
            var text = new StringBuilder();
            text.Append("# ").Append(packId).Append("\n\n");
            text.Append("A song pack for **FATE Music Player**. Install it with r2modman or Thunderstore Mod Manager.\n\n");
            text.Append("It declares " + ModId + "-" + FateMusicPlayerPlugin.Version + " as a dependency, so the player is installed with it.\n\n");
            text.Append("Its songs appear in their own categories and play in the location slots they were exported for. ");
            text.Append("It adds rows only; it never changes the songs that already exist, and removing this package removes exactly these tracks again.\n\n");
            text.Append("## Songs\n\n");
            foreach (var entry in songs)
                text.Append("- ").Append(string.IsNullOrEmpty(entry.Key.Title) ? entry.Value : entry.Key.Title).Append("\n");
            return text.ToString();
        }

        private static string Json(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        internal static void Message(string text)
        {
            if (FateMusicPlayerPlugin.LogSource != null) FateMusicPlayerPlugin.LogSource.LogInfo(text);
            FateMusicPlayerPlugin.Toast(text);
        }
    }
}
