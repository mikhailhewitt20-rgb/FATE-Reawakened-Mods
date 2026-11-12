using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FateMusicPlayer
{
    // This game destroys the loader's plugin object moments after startup: FATE Mod Menu
    // and FATE Rebind both log that destruction and keep their state in statics for the
    // same reason. A plugin's own Update therefore stops running, which is why the player
    // never registered itself with the menu. Ticking now comes from a Harmony postfix on
    // AudioManager.Update (which runs every frame in every scene), from a ticker component
    // on its own DontDestroyOnLoad object, and from the plugin's Update while it is alive.
    internal static class MusicRuntime
    {
        private static ManualLogSource log;
        private static Harmony harmony;
        private static bool patched;
        private static bool scanned;
        private static bool tickerCreated;
        private static float nextPatchAttempt;
        private static float nextTick;
        private static float nextScan;
        private static int emptyScans;

        internal static void Init(ManualLogSource logger, Harmony instance)
        {
            log = logger;
            harmony = instance;
            // Both drivers are started here, not lazily from Tick: Tick is what they call,
            // so a lazy start would wait on the very ticks it was meant to begin.
            EnsureTicker();
            patched = GameMusic.Patch(harmony, log);
            nextPatchAttempt = Time.unscaledTime + 3f;
        }

        // The loader's plugin object is destroyed by this game, so the ticker lives on an
        // object the mod owns. It is also re-made if the game ever takes it away.
        private static void EnsureTicker()
        {
            if (tickerCreated) return;
            tickerCreated = true;
            var ticker = new GameObject("FATE Music Player ticker");
            UnityEngine.Object.DontDestroyOnLoad(ticker);
            ticker.AddComponent<MusicTicker>();
        }

        // Cheap to call every frame: work happens at most five times a second.
        internal static void Tick()
        {
            var now = Time.unscaledTime;
            if (now < nextTick) return;
            nextTick = now + 0.2f;
            EnsureTicker();
            // Created on the first frame the game is really running: an object made during
            // the chainloader's Awake does not survive into the game, and every preview then
            // returned silently.
            AudioLoader.EnsureHost(log);
            if (!patched && now >= nextPatchAttempt)
            {
                nextPatchAttempt = now + 3f;
                patched = GameMusic.Patch(harmony, log);
            }
            if (GameMusic.Manager() != null)
            {
                if (!scanned)
                {
                    scanned = true;
                    Library.Scan();
                    ModMenuBridge.RebuildSoon();
                    if (log != null)
                        log.LogInfo("Music library built: " + Library.Songs.Count + " song(s) in " + Library.Categories.Count + " category(ies).");
                }
                // The audio manager exists before its music list is filled in on some
                // loads, and that first scan then finds only added songs. Keep looking
                // until the game's own tracks are there.
                else if (!Library.HasGameSongs && emptyScans < 30 && now >= nextScan)
                {
                    nextScan = now + 2f;
                    emptyScans++;
                    Library.Scan();
                    if (Library.HasGameSongs)
                    {
                        ModMenuBridge.RebuildSoon();
                        if (log != null)
                            log.LogInfo("Music library built after the audio manager filled in: " + Library.Songs.Count
                                + " song(s) in " + Library.Categories.Count + " category(ies).");
                    }
                }
            }
            Library.FlushSaves();
            ModMenuBridge.Tick();
        }

        // Harmony postfix for AudioManager.Update.
        internal static void AfterAudioManagerUpdate()
        {
            Tick();
        }

        internal static void Rescan()
        {
            scanned = true;
            Library.Scan();
            ModMenuBridge.RebuildSoon();
        }
    }

    // The loader's plugin object is destroyed by this game, so the ticker lives on an
    // object the mod owns. It keeps the player working even if the Harmony hook on
    // AudioManager.Update could not be applied.
    internal sealed class MusicTicker : MonoBehaviour
    {
        private void Update()
        {
            MusicRuntime.Tick();
        }
    }
}
