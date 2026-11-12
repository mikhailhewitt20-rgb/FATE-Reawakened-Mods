using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace FateMusicPlayer
{
    [BepInPlugin(Guid, "FATE Music Player", Version)]
    [BepInDependency("zombieghost.modmenu", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FateMusicPlayerPlugin : BaseUnityPlugin
    {
        internal const string Guid = "mikhailkillszombies.musicplayer";
        internal const string Version = "0.4.3";

        internal static ManualLogSource LogSource;
        internal static bool ModEnabled = true;
        internal static string LastToast;
        internal static float LastToastAt;

        private ConfigEntry<bool> enabledSetting;

        internal static void Toast(string message)
        {
            LastToast = message;
            LastToastAt = UnityEngine.Time.unscaledTime;
        }

        private void Awake()
        {
            LogSource = Logger;
            enabledSetting = Config.Bind("General", "Enabled", true, "Let the music player choose tracks for the slots you edited.");
            ModEnabled = enabledSetting.Value;
            enabledSetting.SettingChanged += (sender, args) => ModEnabled = enabledSetting.Value;

            AudioLoader.Create(Logger);
            // Config and the packs folder are available immediately; the song list is
            // not, because AudioManager is a scene singleton and does not exist yet.
            Library.Init(Paths.ConfigPath, Path.GetDirectoryName(Info.Location), Logger);
            ModMenuBridge.Init(Logger);
            // The runtime owns the Harmony patches and the per-frame tick, because this
            // object does not survive startup in this game.
            MusicRuntime.Init(Logger, new Harmony(Guid));
            Logger.LogInfo("FATE Music Player " + Version + " loaded; waiting for the game's audio manager.");
        }

        // Fallback only: the game destroys this component shortly after startup, so the
        // runtime ticks itself from AudioManager.Update and its own ticker object.
        private void Update()
        {
            MusicRuntime.Tick();
        }
    }
}
