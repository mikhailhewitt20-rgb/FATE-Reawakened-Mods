using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FateMusicPlayer
{
    // Registers the player with FATE Mod Menu through its reflection API. The menu is
    // optional: if it is missing this does nothing and the rest of the mod still runs.
    // Two layouts are supported, chosen at runtime:
    //   API v3 (menu 0.4.0+): one row per song — play, title, on/off and the loop timer
    //                         on that same row, with a cross for songs you added.
    //   API v2 (older menus): three rows per song, because that API has no media row.
    internal static class ModMenuBridge
    {
        // The menu caps a module at 256 settings.
        private const int MenuCap = 256;
        private const int PacksRows = 3;

        private static object module;
        private static float nextAttempt;
        private static float rebuildAt = -1f;
        private static float missingSince = -1f;
        private static bool warnedMissing;
        private static bool warnedLegacy;
        private static bool warnedNoRows;
        private static ManualLogSource log;

        // Kept from the last registration so a song added while the menu is open can get
        // its row on the live module. Registering the module again disposes the old one,
        // and the menu only draws settings for a module that is still alive.
        private static MethodInfo trackRow;
        private static MethodInfo removeRow;
        private static MethodInfo simpleToggle;
        private static MethodInfo simpleAction;
        private static MethodInfo simpleSlider;

        internal static void Init(ManualLogSource logger)
        {
            log = logger;
        }

        internal static void Tick()
        {
            if (module == null)
            {
                if (Time.unscaledTime < nextAttempt) return;
                nextAttempt = Time.unscaledTime + 2f;
                Register();
                return;
            }
            if (rebuildAt > 0f && Time.unscaledTime >= rebuildAt)
            {
                rebuildAt = -1f;
                Register();
            }
        }

        internal static void RebuildSoon()
        {
            rebuildAt = Time.unscaledTime + .25f;
        }

        private static void Register()
        {
            try
            {
                var api = AccessTools.TypeByName("ZombieGhost.ModMenu.ModMenuApi");
                var register = api == null ? null : api.GetMethod("RegisterModule", new[] { typeof(string), typeof(string) });
                if (api == null || register == null)
                {
                    // The menu may load after this mod, so keep retrying quietly and only
                    // say something once it is clear it is not there at all.
                    if (missingSince < 0f) missingSince = Time.unscaledTime;
                    else if (!warnedMissing && Time.unscaledTime - missingSince > 10f)
                    {
                        warnedMissing = true;
                        if (log != null) log.LogWarning("FATE Mod Menu API was not found ("
                            + (api == null ? "ZombieGhost.ModMenu.ModMenuApi type" : "RegisterModule method")
                            + "); the player has no settings screen. Install FATE_ModMenu to configure it.");
                    }
                    return;
                }
                Dispose();
                module = register.Invoke(null, new object[] { FateMusicPlayerPlugin.Guid, "FATE Music Player" });
                if (module == null) return;
                // Handles left over from a disposed module must never add a row later.
                trackRow = removeRow = simpleToggle = simpleAction = simpleSlider = null;
                var type = module.GetType();
                var note = type.GetMethod("RegisterNote");
                var track = type.GetMethod("RegisterTrack");
                var wide = type.GetMethod("RegisterWideAction");
                if (track != null && wide != null)
                    RegisterTrackRows(type, track, wide, note, api);
                else
                    RegisterSimpleRows(type, note, api);

                missingSince = -1f;
                warnedMissing = false;
            }
            catch (Exception error)
            {
                if (log != null) log.LogWarning("Could not register with FATE Mod Menu: " + error.Message);
                module = null;
            }
        }

        // API v3: one row per song, with the loop timer on that row.
        private static void RegisterTrackRows(Type type, MethodInfo track, MethodInfo wide, MethodInfo note, Type api)
        {
            var categoryReset = type.GetMethod("SetCategoryResetCallback");
            var allReset = type.GetMethod("SetResetCallback");
            var removeAction = type.GetMethod("RegisterTrackRemoveAction");
            // Remembered for AppendSongRow, so ADD SONG can reach the live module.
            trackRow = track;
            removeRow = removeAction;
            simpleToggle = simpleAction = simpleSlider = null;
            if (categoryReset != null) categoryReset.Invoke(module, new object[] { new Action<string>(Library.ResetCategory) });
            if (allReset != null) allReset.Invoke(module, new object[] { new Action(Library.ResetAll) });

            var songs = Library.Songs;
            var categories = songs.Select(song => song.Category).Distinct().ToList();
            var budget = MenuCap - PacksRows - categories.Count;
            var shown = 0;
            foreach (var category in categories)
            {
                var label = category;
                foreach (var song in songs.Where(s => s.Category == label))
                {
                    if (shown >= budget) break;
                    shown++;
                    RegisterSongRow(track, removeAction, song);
                }
                wide.Invoke(module, new object[] {
                    Id(label, "add"), "ADD SONG", new Action(() => AddSong(label)), label });
            }
            RegisterPackRows(wide, note, shown, budget, songs.Count);
            if (log != null)
                log.LogInfo("Registered with FATE Mod Menu: " + shown + " song row(s) in " + categories.Count + " category(ies).");
        }

        // API v2 and older: the same settings, drawn with the rows that API has.
        private static void RegisterSimpleRows(Type type, MethodInfo note, Type api)
        {
            var toggle = type.GetMethod("RegisterToggle");
            var action = type.GetMethod("RegisterAction");
            var slider = type.GetMethod("RegisterSlider");
            if (toggle == null || action == null)
            {
                if (!warnedNoRows)
                {
                    warnedNoRows = true;
                    if (log != null) log.LogWarning("FATE Mod Menu has no RegisterToggle/RegisterAction; the player has no settings screen.");
                }
                Dispose();
                return;
            }
            simpleToggle = toggle;
            simpleAction = action;
            simpleSlider = slider;
            trackRow = removeRow = null;
            if (!warnedLegacy)
            {
                warnedLegacy = true;
                if (log != null) log.LogWarning("FATE Mod Menu API " + ApiVersion(api)
                    + " is older than the player: showing the compact song list (three rows per song). Install FATE_ModMenu 0.4.0 for one row per song.");
            }

            var songs = Library.Songs;
            var categories = songs.Select(song => song.Category).Distinct().ToList();
            // Three rows per song: on/off, preview, loop timer.
            var budget = (MenuCap - PacksRows) / 3;
            var shown = 0;
            foreach (var category in categories)
            {
                var label = category;
                foreach (var song in songs.Where(s => s.Category == label))
                {
                    if (shown >= budget) break;
                    shown++;
                    RegisterSimpleSong(toggle, action, slider, song);
                }
                action.Invoke(module, new object[] {
                    Id(label, "add"), "ADD SONG", new Action(() => AddSong(label)), label });
            }
            RegisterPackRows(action, note, shown, budget, songs.Count);
            if (log != null)
                log.LogInfo("Registered with FATE Mod Menu (compact): " + shown + " song row(s) in " + categories.Count + " category(ies).");
        }

        // The older menu: the same settings as separate rows.
        private static void RegisterSimpleSong(MethodInfo toggle, MethodInfo action, MethodInfo slider, Song song)
        {
            var target = song;
            toggle.Invoke(module, new object[] {
                Id("on", target.Id), target.Title,
                new Func<bool>(() => target.Enabled),
                new Action<bool>(value => Library.SetEnabled(target, value)),
                target.Category });
            action.Invoke(module, new object[] {
                Id("preview", target.Id), "   → Play first 5 seconds",
                new Action(() => Preview(target)), target.Category });
            if (slider == null) return;
            // The loop timer still works on an older menu: the slider row writes through
            // the same setter, so every change plays the end into the start.
            slider.Invoke(module, new object[] {
                Id("loop", target.Id), "   → Loop timer (seconds)",
                new Func<float>(() => target.LoopSeconds),
                new Action<float>(value => Library.SetLoopSeconds(target, value)),
                0f, Library.MaxLoopSeconds, 0.1f, target.Category, null, 1600f });
        }

        // One row: play, title, on/off and the loop timer in seconds. Selection was
        // removed from the design at the author's request; it never meant anything.
        private static void RegisterSongRow(MethodInfo registerTrack, MethodInfo removeAction, Song song)
        {
            var target = song;
            var id = Id("song", target.Id);
            registerTrack.Invoke(module, new object[] {
                id, target.Title,
                new Func<bool>(() => target.Enabled),
                new Action<bool>(value => Library.SetEnabled(target, value)),
                new Action(() => Preview(target)),
                new Action(() => AudioLoader.StopPreview()),
                new Func<bool>(() => AudioLoader.IsPlaying(target.Id)),
                null,
                null,
                new Func<float>(() => target.LoopSeconds),
                new Action<float>(value => Library.SetLoopSeconds(target, value)),
                0f, Library.MaxLoopSeconds, 0.1f,
                target.Category });
            // Only songs added from disk can be removed one by one; a pack's songs go
            // away with the pack, and the game's own tracks cannot be deleted.
            if (removeAction == null || !target.IsCustom || !string.IsNullOrEmpty(target.PackId)) return;
            removeAction.Invoke(module, new object[] { id, new Action(() => RemoveSong(target)) });
        }

        private static void RegisterPackRows(MethodInfo action, MethodInfo note, int shown, int budget, int total)
        {
            action.Invoke(module, new object[] {
                "packs.export", "EXPORT SONG PACK", new Action(() => PackExporter.Export()), "Song packs" });
            action.Invoke(module, new object[] {
                "packs.rescan", "RESCAN SONG PACKS",
                new Action(() => { Library.Scan(); RebuildSoon(); Toast("Song packs rescanned."); }), "Song packs" });
            if (note == null) return;
            note.Invoke(module, new object[] {
                "packs.note",
                "A pack installs into BepInEx/plugins/FateMusicPlayer/packs and adds its songs without touching yours. Removing the package removes its songs again.",
                "Song packs" });
            if (shown >= budget && total > shown)
                note.Invoke(module, new object[] {
                    "limit.note",
                    "This list is capped at " + shown + " songs by the menu's 256-setting limit; the rest stay in the pack or library but are not shown.",
                    "Song packs" });
        }

        private static object ApiVersion(Type api)
        {
            try
            {
                var property = api == null ? null : api.GetProperty("ApiVersion");
                return property == null ? (object)"?" : property.GetValue(null, null);
            }
            catch { return "?"; }
        }

        // The preview plays the first five seconds with FATE's own track muted.
        private static void Preview(Song song)
        {
            if (song == null) return;
            if (song.IsCustom)
            {
                if (log != null) log.LogInfo("Preview asked for " + song.Title + " (file: " + song.FilePath + ")");
                AudioLoader.Preview(song.Id, song.FilePath, 5f);
                return;
            }
            if (song.GameClip == null)
            {
                if (log != null) log.LogWarning("Preview asked for " + song.Title + " but it has no game clip.");
                Toast("That song has nothing to preview.");
                return;
            }
            if (log != null) log.LogInfo("Preview asked for " + song.Title + " (game clip)");
            AudioLoader.PreviewClip(song.Id, song.GameClip, 5f);
        }

        private static void RemoveSong(Song song)
        {
            var rowId = song == null ? null : Id("song", song.Id);
            if (!Library.RemoveUserSong(song))
            {
                Toast("That song could not be removed.");
                return;
            }
            AudioLoader.StopPreview();
            // Taking the row out of the module keeps the player on the page they are
            // looking at: a full re-registration disposes the module, and the menu only
            // shows settings for a module that is still alive, so it dropped them back
            // to the module list.
            if (!DropRow(rowId)) RebuildSoon();
            Toast("Removed " + song.Title + ".");
        }

        // Removes one already-registered row. The menu has no public call for this, so it
        // is done on the module it handed back: the row is marked removed, taken out of
        // the module's list and a redraw is asked for. Everything else on the page stays
        // exactly where it was.
        private static bool DropRow(string rowId)
        {
            if (module == null || string.IsNullOrEmpty(rowId)) return false;
            try
            {
                var settings = Settings();
                if (settings == null) return false;
                var row = FindRow(settings, rowId);
                if (row == null) return false;
                // ModSetting.Removed is what the screen checks before drawing and what
                // makes its redraw check notice the change.
                var removedField = FindField(row.GetType(), "Removed");
                if (removedField != null) removedField.SetValue(row, true);
                settings.Remove(row);
                RequestRefresh();
                return true;
            }
            catch (Exception error)
            {
                if (log != null) log.LogWarning("Could not take the row out in place, rebuilding the list instead: " + error.Message);
                return false;
            }
        }

        // The module keeps its rows in a private list; it is the only handle the API gives
        // a mod for editing a page that is already on screen.
        private static System.Collections.IList Settings()
        {
            if (module == null) return null;
            var field = module.GetType().GetField("Settings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(module) as System.Collections.IList;
        }

        private static object FindRow(System.Collections.IList settings, string rowId)
        {
            if (settings == null || string.IsNullOrEmpty(rowId)) return null;
            foreach (var setting in settings)
            {
                if (setting == null) continue;
                var idProperty = setting.GetType().GetProperty("Id");
                if (idProperty == null) continue;
                if (string.Equals(idProperty.GetValue(setting, null) as string, rowId, StringComparison.Ordinal)) return setting;
            }
            return null;
        }

        // Puts one song on the module the player is already looking at. A full
        // registration would dispose the module, which is what dropped them back to the
        // module list after every added song.
        private static bool AppendSongRow(Song song)
        {
            if (module == null || song == null) return false;
            try
            {
                if (trackRow != null)
                {
                    RegisterSongRow(trackRow, removeRow, song);
                    // A rebuild would have drawn the song above its category's ADD SONG bar.
                    MoveBeforeAddRow(Id("song", song.Id), Id(song.Category, "add"));
                }
                else if (simpleToggle != null && simpleAction != null)
                {
                    RegisterSimpleSong(simpleToggle, simpleAction, simpleSlider, song);
                    MoveBeforeAddRow(Id("on", song.Id), Id(song.Category, "add"));
                    MoveBeforeAddRow(Id("preview", song.Id), Id(song.Category, "add"));
                    MoveBeforeAddRow(Id("loop", song.Id), Id(song.Category, "add"));
                }
                else return false;
                RequestRefresh();
                return true;
            }
            catch (Exception error)
            {
                if (log != null) log.LogWarning("Could not add the row in place, rebuilding the list instead: " + error.Message);
                return false;
            }
        }

        // Moves a registered row to just above its category's ADD SONG bar, which is where
        // a rebuild would have placed it.
        private static void MoveBeforeAddRow(string rowId, string addRowId)
        {
            var settings = Settings();
            if (settings == null) return;
            var row = FindRow(settings, rowId);
            var addRow = FindRow(settings, addRowId);
            if (row == null || addRow == null) return;
            var index = settings.IndexOf(addRow);
            settings.Remove(row);
            settings.Insert(index, row);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
            }
            return null;
        }

        // The menu redraws when it is asked to, not on its own.
        private static void RequestRefresh()
        {
            var plugin = AccessTools.TypeByName("ZombieGhost.ModMenu.ModMenuPlugin");
            var request = plugin == null ? null : AccessTools.Method(plugin, "RequestRefresh", new Type[0]);
            if (request == null) return;
            try { request.Invoke(null, null); }
            catch { }
        }

        // The category a song is added to decides which location slot it plays in.
        private static void AddSong(string category)
        {
            var bucket = Library.BucketForCategory(category);
            if (string.IsNullOrEmpty(bucket))
            {
                Toast("This category has no game music slot to play in.");
                return;
            }
            var path = Dialogs.PickAudioFile();
            if (string.IsNullOrEmpty(path)) return;
            if (AudioLoader.AudioTypeFor(path) == AudioType.UNKNOWN)
            {
                Toast("Use an .ogg, .wav or .mp3 file.");
                return;
            }
            List<string> replaced;
            var song = Library.AddUserSong(path, category, bucket, out replaced);
            // A re-import replaces the older copy of the song, so its rows go rather than
            // sitting next to the new ones. Everything happens on the open module: it is
            // what keeps the page on screen.
            foreach (var id in replaced)
            {
                DropRow(Id("song", id));
                DropRow(Id("on", id));
                DropRow(Id("preview", id));
                DropRow(Id("loop", id));
            }
            if (song == null || !AppendSongRow(song)) RebuildSoon();
            Toast("Added " + System.IO.Path.GetFileName(path) + ".");
        }

        private static string Id(string prefix, string value)
        {
            var text = (prefix + "." + value).Replace('|', '_');
            return text.Length > 96 ? text.Substring(0, 96) : text;
        }

        private static void Toast(string message)
        {
            FateMusicPlayerPlugin.Toast(message);
        }

        private static void Dispose()
        {
            if (module == null) return;
            try { (module as IDisposable).Dispose(); }
            catch { }
            module = null;
        }
    }
}
