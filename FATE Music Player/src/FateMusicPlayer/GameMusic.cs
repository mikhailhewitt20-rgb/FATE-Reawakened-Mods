using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace FateMusicPlayer
{
    // What one of the game's music slots contains: the enum value the game passes to
    // AudioManager.PlayMusic, the clip set behind it, and the playback defaults.
    internal sealed class BucketInfo
    {
        public int Value;
        public string Name;
        public AudioClip[] Clips;
        public bool Loop;
        public float Volume;
        public string EditorName;
    }

    // Everything the player needs from Tableflip.AudioManager, resolved at runtime so
    // this mod does not hard-depend on game internals at compile time. The names come
    // from the shipped Assembly-CSharp: mAudioDataMusicList, audioSourceMusicChannels,
    // currentMusicData, hasStoppedByScript, GetFreeMusicAudioSource, PlayMusic and
    // ForcePlayMusic.
    internal static class GameMusic
    {
        internal static bool Bound;

        private static Type managerType;
        private static Type dataType;
        private static Type clipNameType;
        private static PropertyInfo instanceProperty;
        private static FieldInfo instanceField;
        private static FieldInfo musicListField;
        private static FieldInfo channelsField;
        private static FieldInfo currentMusicField;
        private static FieldInfo stoppedField;
        private static FieldInfo dataClipNameField;
        private static FieldInfo dataClipsField;
        private static FieldInfo dataLoopField;
        private static FieldInfo dataVolumeField;
        private static FieldInfo dataEditorNameField;
        private static MethodInfo freeMusicSourceMethod;
        private static Type levelType;
        private static FieldInfo levelSourceField;
        private static Type menuType;
        // The game's music volume is an AudioMixer parameter: OptionsUI.OnMusicSlider
        // calls AudioManager.SetMusicVol, which writes the exposed 'musicVolume' float on
        // masterMixer, and Init puts every music channel on masterMixer's "Music" group.
        // A source made by this mod has no group, so it goes straight to the listener and
        // the slider cannot reach it. Sources are put on that group before they play.
        private static FieldInfo mixerField;
        private static PropertyInfo settingsSaveProperty;
        private static FieldInfo settingsMusicVolumeField;
        private static AudioMixerGroup musicGroup;
        private static object musicGroupFor;
        private static bool musicGroupTried;
        private static bool warnedNoGroup;
        private static ManualLogSource logSource;
        private static float settingsVolume = 1f;
        private static float nextSettingsRead;
        // Channel volumes saved while a five-second preview is playing.
        private static readonly List<KeyValuePair<AudioSource, float>> muted = new List<KeyValuePair<AudioSource, float>>();

        internal static Type ManagerType { get { return managerType; } }
        internal static Type ClipNameType { get { return clipNameType; } }

        internal static bool Bind(ManualLogSource log)
        {
            if (log != null) logSource = log;
            if (Bound) return true;
            managerType = AccessTools.TypeByName("Tableflip.AudioManager");
            dataType = AccessTools.TypeByName("AudioData");
            clipNameType = AccessTools.TypeByName("AudioData+AudioClipName");
            if (managerType == null || dataType == null || clipNameType == null)
            {
                if (log != null) log.LogWarning("FATE AudioManager was not found yet; music slots are unavailable.");
                return false;
            }
            instanceProperty = FindStaticProperty(managerType, "Instance");
            instanceField = FindInstanceField(managerType, managerType);
            musicListField = AccessTools.Field(managerType, "mAudioDataMusicList");
            channelsField = AccessTools.Field(managerType, "audioSourceMusicChannels");
            currentMusicField = AccessTools.Field(managerType, "currentMusicData");
            stoppedField = AccessTools.Field(managerType, "hasStoppedByScript");
            dataClipNameField = AccessTools.Field(dataType, "mAudioClipName");
            dataClipsField = AccessTools.Field(dataType, "mAudioClip");
            dataLoopField = AccessTools.Field(dataType, "mLoop");
            dataVolumeField = AccessTools.Field(dataType, "mVolume");
            dataEditorNameField = AccessTools.Field(dataType, "editorName");
            freeMusicSourceMethod = AccessTools.Method(managerType, "GetFreeMusicAudioSource", new Type[0]);
            mixerField = AccessTools.Field(managerType, "masterMixer");
            // Read by MusicScale only when the mixer group above cannot be resolved.
            var saveType = AccessTools.TypeByName("Tableflip.SaveData");
            settingsSaveProperty = saveType == null ? null
                : saveType.GetProperty("SettingsSave", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            settingsMusicVolumeField = settingsSaveProperty == null ? null
                : AccessTools.Field(settingsSaveProperty.PropertyType, "musicVolume");
            levelType = AccessTools.TypeByName("Tableflip.LevelGenerator");
            levelSourceField = levelType == null ? null : AccessTools.Field(levelType, "audioSource");
            menuType = AccessTools.TypeByName("Tableflip.MainMenuUI");
            Bound = instanceProperty != null && musicListField != null && channelsField != null;
            if (!Bound && log != null) log.LogWarning("FATE AudioManager layout changed; music slots are unavailable.");
            else if (Bound && log != null)
                log.LogInfo("AudioManager instance read from " + (instanceField != null ? "field " + instanceField.Name : "the Instance property")
                    + "; music list field " + (musicListField != null ? "found" : "missing") + ".");
            return Bound;
        }

        private static PropertyInfo FindStaticProperty(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property != null) return property;
            }
            return null;
        }

        internal static object Manager()
        {
            try
            {
                // Read the cached singleton field, not the Instance property: the property
                // builds the singleton when it is missing, and this game's AudioManager
                // throws from Init when it is constructed before the game is ready.
                if (instanceField != null) return instanceField.GetValue(null);
                return instanceProperty == null ? null : instanceProperty.GetValue(null, null);
            }
            catch { return null; }
        }

        // SingletonBehaviour<T> keeps its instance in a static field of type T.
        private static FieldInfo FindInstanceField(Type type, Type instanceType)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var fields = current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var field in fields)
                    if (field.FieldType == instanceType && field.Name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0)
                        return field;
            }
            for (var current = type; current != null; current = current.BaseType)
            {
                var fields = current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var field in fields)
                    if (field.FieldType == instanceType) return field;
            }
            return null;
        }

        // The game's own music entries, one per slot, with every clip behind each slot.
        internal static List<BucketInfo> Buckets()
        {
            var buckets = new List<BucketInfo>();
            if (!Bound) return buckets;
            var manager = Manager();
            if (manager == null || musicListField == null) return buckets;
            var list = musicListField.GetValue(manager) as Array;
            if (list == null) return buckets;
            foreach (var item in list)
            {
                if (item == null) continue;
                var nameValue = dataClipNameField == null ? null : dataClipNameField.GetValue(item);
                if (nameValue == null) continue;
                var bucket = new BucketInfo();
                bucket.Value = Convert.ToInt32(nameValue);
                bucket.Name = nameValue.ToString();
                bucket.Clips = dataClipsField == null ? new AudioClip[0] : (dataClipsField.GetValue(item) as AudioClip[]) ?? new AudioClip[0];
                bucket.Loop = dataLoopField == null || Convert.ToBoolean(dataLoopField.GetValue(item));
                bucket.Volume = dataVolumeField == null ? 1f : Convert.ToSingle(dataVolumeField.GetValue(item));
                bucket.EditorName = dataEditorNameField == null ? null : dataEditorNameField.GetValue(item) as string;
                buckets.Add(bucket);
            }
            return buckets;
        }

        internal static BucketInfo FindBucket(int value)
        {
            foreach (var bucket in Buckets()) if (bucket.Value == value) return bucket;
            return null;
        }

        // The mixer group the game's own music plays through, so the in-game music volume
        // slider governs this mod's music exactly as it governs FATE's. Re-resolved when
        // the manager instance changes, because a scene load can bring a new one.
        internal static AudioMixerGroup MusicGroup()
        {
            var manager = Manager();
            if (manager == null) return null;
            if (ReferenceEquals(musicGroupFor, manager)
                && (musicGroup != null || musicGroupTried)) return musicGroup;
            musicGroupFor = manager;
            musicGroupTried = true;
            musicGroup = null;
            if (mixerField == null) return null;
            try
            {
                var mixer = mixerField.GetValue(manager) as AudioMixer;
                if (mixer == null) return null;
                var groups = mixer.FindMatchingGroups("Music");
                if (groups != null && groups.Length > 0) musicGroup = groups[0];
            }
            catch { musicGroup = null; }
            if (musicGroup == null && !warnedNoGroup)
            {
                warnedNoGroup = true;
                if (logSource != null) logSource.LogWarning("The game's Music mixer group was not found; the in-game music volume slider cannot reach this mod's music.");
            }
            return musicGroup;
        }

        // Every source this mod plays on is put on the music group first. Without it the
        // source reaches the listener directly and ignores the game's music volume.
        internal static void Route(AudioSource source)
        {
            if (source == null) return;
            var group = MusicGroup();
            if (group == null) return;
            try { if (source.outputAudioMixerGroup != group) source.outputAudioMixerGroup = group; }
            catch { }
        }

        // The factor this mod has to apply itself. When the sources are on the mixer the
        // slider is already applied there, so this is 1 and nothing is scaled twice. It
        // only returns the game's saved music volume when the group could not be found.
        internal static float MusicScale
        {
            get
            {
                if (MusicGroup() != null) return 1f;
                var now = Time.unscaledTime;
                if (now < nextSettingsRead) return settingsVolume;
                nextSettingsRead = now + .25f;
                try
                {
                    var settings = settingsSaveProperty == null ? null : settingsSaveProperty.GetValue(null, null);
                    if (settings != null && settingsMusicVolumeField != null)
                        settingsVolume = Mathf.Clamp01(Convert.ToSingle(settingsMusicVolumeField.GetValue(settings)));
                }
                catch { }
                return settingsVolume;
            }
        }

        internal static AudioSource FreeMusicSource()
        {
            var manager = Manager();
            if (manager == null || freeMusicSourceMethod == null) return null;
            try { return freeMusicSourceMethod.Invoke(manager, null) as AudioSource; }
            catch { return null; }
        }

        internal static AudioSource[] MusicChannels()
        {
            var manager = Manager();
            if (manager == null || channelsField == null) return new AudioSource[0];
            try { return (channelsField.GetValue(manager) as AudioSource[]) ?? new AudioSource[0]; }
            catch { return new AudioSource[0]; }
        }

        internal static void StopAllMusic()
        {
            LoopPlayer.Stop();
            foreach (var source in MusicChannels()) if (source != null) source.Stop();
        }

        // A preview plays on its own source, so the game's current track has to be
        // silenced for those seconds or the two are heard together.
        internal static void MutePlayback()
        {
            if (muted.Count > 0) return;
            LoopPlayer.SetSilenced(true);
            foreach (var source in MusicChannels())
            {
                if (source == null || source.volume <= 0f) continue;
                muted.Add(new KeyValuePair<AudioSource, float>(source, source.volume));
                source.volume = 0f;
            }
        }

        internal static void RestorePlayback()
        {
            for (var index = 0; index < muted.Count; index++)
            {
                var source = muted[index].Key;
                if (source != null) source.volume = muted[index].Value;
            }
            muted.Clear();
            LoopPlayer.SetSilenced(false);
        }

        // Keeps AudioManager.Update from restarting a different track: it only retries
        // PlayMusic(currentMusicData) when nothing is playing and no script stopped it.
        internal static void RememberSlot(int bucketValue, bool stopped)
        {
            var manager = Manager();
            if (manager == null) return;
            try
            {
                if (currentMusicField != null) currentMusicField.SetValue(manager, Enum.ToObject(clipNameType, bucketValue));
                if (stoppedField != null) stoppedField.SetValue(manager, stopped);
            }
            catch { }
        }

        // Every way the game silences music: the parameterless stop, the per-slot stop,
        // and the two force variants when this build has them.
        private static List<MethodInfo> StopMethods()
        {
            var methods = new List<MethodInfo>();
            foreach (var name in new[] { "StopMusic", "ForceStopMusic" })
            {
                var plain = AccessTools.Method(managerType, name, new Type[0]);
                if (plain != null) methods.Add(plain);
                var slot = clipNameType == null ? null : AccessTools.Method(managerType, name, new[] { clipNameType });
                if (slot != null) methods.Add(slot);
            }
            return methods;
        }

        // The slot the game's dungeons would use if they went through the manager. The
        // floors actually play TemplateData.musicClips, so this value is what the Dungeon
        // Music rows are filed under.
        internal static int DungeonMusicValue()
        {
            if (clipNameType == null) return -1;
            try { return Convert.ToInt32(Enum.Parse(clipNameType, "DungeonMusic")); }
            catch { return -1; }
        }

        // The floor's own track is not on the manager's channels, so it has to be
        // stopped through the generator that started it.
        internal static void SilenceDungeonSource(object level)
        {
            if (level == null || levelSourceField == null) return;
            try
            {
                var source = levelSourceField.GetValue(level) as AudioSource;
                if (source != null) source.Stop();
            }
            catch { }
        }

        internal static bool Patch(Harmony harmony, ManualLogSource log)
        {
            if (!Bind(log)) return false;
            var patched = false;
            foreach (var name in new[] { "PlayMusic", "ForcePlayMusic" })
            {
                var method = AccessTools.Method(managerType, name, new[] { clipNameType, typeof(bool) });
                if (method == null)
                {
                    if (log != null) log.LogWarning("AudioManager." + name + " was not found; that slot keeps playing vanilla music.");
                    continue;
                }
                try
                {
                    harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(MusicEngine), "BeforePlayMusic"));
                    patched = true;
                }
                catch (Exception error)
                {
                    if (log != null) log.LogError("Could not patch AudioManager." + name + ": " + error.Message);
                }
            }
            // A dungeon does not call PlayMusic at all: LevelGenerator.PlayDungeonMusic
            // stops the manager's channels and then plays the floor's own clips on its own
            // source. A track running on the mod's loop sources has to end with that stop,
            // or the song chosen for the village keeps playing over the dungeon.
            foreach (var stop in StopMethods())
            {
                try
                {
                    harmony.Patch(stop, prefix: new HarmonyMethod(typeof(MusicEngine), "BeforeGameStopsMusic"));
                    patched = true;
                }
                catch (Exception error)
                {
                    if (log != null) log.LogWarning("Could not patch AudioManager." + stop.Name + ": " + error.Message);
                }
            }
            // Dungeons never call PlayMusic: LevelGenerator fades the manager's channels
            // and then plays the floor's own clip list on its own source. Hooking that
            // call is the only way the Dungeon Music category can be heard in a depth.
            var dungeon = levelType == null ? null : AccessTools.Method(levelType, "PlayDungeonMusic", new Type[0]);
            if (dungeon == null)
            {
                if (log != null) log.LogWarning("LevelGenerator.PlayDungeonMusic was not found; songs in the dungeon slot cannot play.");
            }
            else
            {
                try
                {
                    harmony.Patch(dungeon, postfix: new HarmonyMethod(typeof(MusicEngine), "AfterDungeonMusic"));
                    patched = true;
                }
                catch (Exception error)
                {
                    if (log != null) log.LogWarning("Could not patch LevelGenerator.PlayDungeonMusic: " + error.Message);
                }
            }
            // The title screen chooses one intro slot by Fate version, so its music is
            // only controllable as a whole through the call that starts it.
            var menuStart = menuType == null ? null : AccessTools.Method(menuType, "Start", new Type[0]);
            if (menuStart == null)
            {
                if (log != null) log.LogWarning("MainMenuUI.Start was not found; the title screen keeps its own music.");
            }
            else
            {
                try
                {
                    harmony.Patch(menuStart, postfix: new HarmonyMethod(typeof(MusicEngine), "AfterMainMenuMusic"));
                    patched = true;
                }
                catch (Exception error)
                {
                    if (log != null) log.LogWarning("Could not patch MainMenuUI.Start: " + error.Message);
                }
            }
            // This game destroys the loader's plugin object, so the player ticks from the
            // audio manager's own frame update instead of a MonoBehaviour's Update.
            var update = AccessTools.Method(managerType, "Update", new Type[0]);
            if (update == null)
            {
                if (log != null) log.LogWarning("AudioManager.Update was not found; the mod's own ticker object drives the player instead.");
            }
            else
            {
                try
                {
                    harmony.Patch(update, postfix: new HarmonyMethod(typeof(MusicRuntime), "AfterAudioManagerUpdate"));
                    patched = true;
                }
                catch (Exception error)
                {
                    if (log != null) log.LogWarning("Could not hook AudioManager.Update: " + error.Message);
                }
            }
            return patched;
        }
    }
}
