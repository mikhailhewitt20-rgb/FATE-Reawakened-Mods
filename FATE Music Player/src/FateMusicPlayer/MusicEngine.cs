using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FateMusicPlayer
{
    // Sits in front of AudioManager.PlayMusic/ForcePlayMusic. When the user has changed
    // nothing for a slot the call passes straight through, so vanilla fades, volume
    // and behaviour stay exactly as shipped. Once a slot is edited (a song disabled,
    // one selected, or a custom file present) the player takes over that slot only.
    internal static class MusicEngine
    {
        private static float lastComplaint;

        // Harmony injects the instance and the original arguments; the enum type is
        // resolved at runtime, so the arguments arrive boxed in __args.
        private static bool BeforePlayMusic(object __instance, object[] __args)
        {
            try
            {
                if (!FateMusicPlayerPlugin.ModEnabled) return true;
                if (__args == null || __args.Length < 2) return true;
                var bucketValue = Convert.ToInt32(__args[0]);
                var stopOthers = Convert.ToBoolean(__args[1]);
                return Intercept(bucketValue, stopOthers);
            }
            catch (Exception error)
            {
                Complain("playing music failed, falling back to vanilla: " + error.Message, true);
                return true;
            }
        }

        // False skips the original call; true lets FATE play its own track.
        internal static bool Intercept(int bucketValue, bool stopOthers)
        {
            var all = Library.SongsForBucket(bucketValue);
            // A slot this mod does not serve is taking over, so a song still running on
            // the loop sources would be heard on top of it.
            if (all.Count == 0)
            {
                LoopPlayer.Stop();
                return true;
            }
            var enabled = all.Where(song => song.Enabled).ToList();
            var selected = enabled.FirstOrDefault(song => song.Selected);
            var anyCustom = all.Any(song => song.IsCustom);
            // Nothing edited: leave FATE alone.
            if (!anyCustom && selected == null && enabled.Count == all.Count)
            {
                LoopPlayer.Stop();
                return true;
            }

            if (enabled.Count == 0)
            {
                GameMusic.StopAllMusic();
                GameMusic.RememberSlot(bucketValue, true);
                return false;
            }

            var chosen = selected ?? enabled[UnityEngine.Random.Range(0, enabled.Count)];
            return Play(chosen, bucketValue, stopOthers);
        }

        // Plays one row that has already been chosen. False means the game must not play
        // its own track for that slot.
        private static bool Play(Song chosen, int bucketValue, bool stopOthers)
        {
            var clip = chosen.GameClip;
            if (clip == null && chosen.IsCustom)
            {
                clip = AudioLoader.Cached(chosen.FilePath);
                if (clip == null)
                {
                    // Decoding takes a frame or two. Stop the old track and let
                    // AudioManager.Update retry; it does that on its own.
                    AudioLoader.Request(chosen.FilePath);
                    if (stopOthers) GameMusic.StopAllMusic();
                    GameMusic.RememberSlot(bucketValue, false);
                    return false;
                }
            }
            if (clip == null)
            {
                LoopPlayer.Stop();
                return true;
            }

            if (stopOthers) GameMusic.StopAllMusic();
            // A loop timer above zero overlaps the song's end with its start; zero
            // leaves the game's own loop point alone.
            if (chosen.Loop && chosen.LoopSeconds > .01f)
            {
                LoopPlayer.Play(clip, chosen.Volume, chosen.LoopSeconds);
                // The blend plays on the mod's own sources, so the game's channels stay
                // silent. AudioManager.Update retries PlayMusic every frame while nothing
                // is on its channels and no script has stopped the music, which would
                // restart the loop continuously, so report it as stopped by script.
                GameMusic.RememberSlot(bucketValue, true);
                return false;
            }
            LoopPlayer.Stop();
            var source = GameMusic.FreeMusicSource();
            if (source == null) return false;
            source.clip = clip;
            source.loop = chosen.Loop;
            source.volume = Mathf.Clamp01(chosen.Volume);
            source.time = 0f;
            source.Play();
            GameMusic.RememberSlot(bucketValue, false);
            return false;
        }

        // Harmony postfix for LevelGenerator.PlayDungeonMusic. The floor picks a clip and
        // plays it on the generator's own source without ever going through PlayMusic, so
        // the Dungeon Music rows would otherwise be a picture of music the game ignores.
        // Once anything in that slot is edited (a song turned off, one selected, or a file
        // added), the slot is handed to the same chooser the other slots use.
        private static void AfterDungeonMusic(object __instance)
        {
            try
            {
                var value = GameMusic.DungeonMusicValue();
                if (value < 0) return;
                var all = Library.SongsForBucket(value);
                // A slot with nothing to play but a placeholder keeps the floor's music.
                var playable = all.Where(song => song.GameClip != null || !string.IsNullOrEmpty(song.FilePath)).ToList();
                if (playable.Count == 0) return;
                var edited = playable.Any(song => song.GameClip == null) || playable.Any(song => !song.Enabled || song.Selected);
                if (!edited) return;
                GameMusic.SilenceDungeonSource(__instance);
                Intercept(value, true);
            }
            catch (Exception error)
            {
                Complain("taking over the dungeon music failed, the floor's own track plays: " + error.Message, false);
            }
        }

        // Harmony postfix for MainMenuUI.Start, which is the title screen. It starts one
        // intro slot chosen by Fate version (IntroFate1..4) plus an ambience layer, so a
        // player who edited any other intro row could never hear a change. Once anything
        // in Intro Music is edited, that category decides what the screen plays: an added
        // song if there is one, otherwise a pick from the rows still switched on.
        private static void AfterMainMenuMusic()
        {
            try
            {
                var slots = Library.CategorySlots("Intro Music");
                if (slots.Count == 0) return;
                var all = new List<Song>();
                foreach (var slot in slots) all.AddRange(Library.SongsForBucket(slot));
                var playable = all.Where(song => song.GameClip != null || !string.IsNullOrEmpty(song.FilePath)).ToList();
                if (playable.Count == 0) return;
                var edited = playable.Any(song => song.IsCustom) || playable.Any(song => !song.Enabled || song.Selected);
                if (!edited) return;
                var enabled = playable.Where(song => song.Enabled).ToList();
                if (enabled.Count == 0)
                {
                    GameMusic.StopAllMusic();
                    return;
                }
                // An added song is the reason the category was edited, so it is what the
                // screen should be heard with; otherwise the toggles pick among FATE's
                // own intro tracks.
                var pool = enabled.Any(song => song.IsCustom) ? enabled.Where(song => song.IsCustom).ToList() : enabled;
                var chosen = pool.FirstOrDefault(song => song.Selected) ?? pool[UnityEngine.Random.Range(0, pool.Count)];
                GameMusic.StopAllMusic();
                Play(chosen, chosen.BucketValue, true);
                if (FateMusicPlayerPlugin.LogSource != null)
                    FateMusicPlayerPlugin.LogSource.LogInfo("Title music: " + chosen.Title + " (slot " + chosen.BucketValue + ").");
            }
            catch (Exception error)
            {
                Complain("taking over the title music failed, the game's own plays: " + error.Message, false);
            }
        }

        // Harmony prefix for the game's own music stops. Entering a dungeon fades the
        // manager's channels and then plays the floor's clips on LevelGenerator's own
        // source, so the loop player has to be silenced here rather than in PlayMusic.
        private static void BeforeGameStopsMusic()
        {
            LoopPlayer.Stop();
        }

        internal static void Complain(string message, bool throttle)
        {
            if (throttle && Time.unscaledTime - lastComplaint < 10f) return;
            lastComplaint = Time.unscaledTime;
            if (FateMusicPlayerPlugin.LogSource != null) FateMusicPlayerPlugin.LogSource.LogWarning(message);
        }
    }
}
