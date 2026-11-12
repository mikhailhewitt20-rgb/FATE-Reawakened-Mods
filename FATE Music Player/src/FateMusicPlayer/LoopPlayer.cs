using UnityEngine;

namespace FateMusicPlayer
{
    // Plays one music track with a loop timer: when the song reaches its last N
    // seconds, a second source starts at the beginning and the two trade volume across
    // those N seconds, so the end melts into the start instead of cutting. At zero the
    // game's own looping is used and this class is not involved.
    internal sealed class LoopPlayer : MonoBehaviour
    {
        private static LoopPlayer host;

        private AudioSource first;
        private AudioSource second;
        private AudioClip clip;
        private float level = 1f;
        private float fade;
        private int active;
        private bool fading;
        private float fadeStartedAt;
        private bool silenced;

        internal static void Play(AudioClip clip, float volume, float fadeSeconds)
        {
            if (clip == null) return;
            if (host == null)
            {
                var go = new GameObject("FATE Music Player loop");
                UnityEngine.Object.DontDestroyOnLoad(go);
                host = go.AddComponent<LoopPlayer>();
                host.first = host.NewSource("loop A");
                host.second = host.NewSource("loop B");
            }
            host.clip = clip;
            host.level = Mathf.Clamp01(volume);
            // Half the song is the most overlap that can still be heard as a loop.
            host.fade = Mathf.Clamp(fadeSeconds, 0f, Mathf.Max(0f, clip.length * .5f));
            host.active = 0;
            host.fading = false;
            host.silenced = false;
            // Put the sources on the game's music mixer group so the in-game music
            // volume slider governs them like any other music.
            GameMusic.Route(host.first);
            GameMusic.Route(host.second);
            host.first.Stop();
            host.second.Stop();
            host.first.clip = clip;
            host.first.loop = false;
            host.first.volume = host.Effective;
            host.first.time = 0f;
            host.first.Play();
        }

        internal static void Stop()
        {
            if (host == null) return;
            host.clip = null;
            host.fading = false;
            host.first.Stop();
            host.second.Stop();
        }

        internal static bool Running { get { return host != null && host.clip != null; } }

        // Previews step the music aside without losing the loop's place.
        internal static void SetSilenced(bool value)
        {
            if (host == null) return;
            host.silenced = value;
            if (host.first != null) host.first.volume = value ? 0f : host.Effective;
            if (host.second != null) host.second.volume = 0f;
        }

        // The track's own volume with the game's music volume applied when the sources
        // are not on its mixer, so a slider change is heard while a song is playing.
        private float Effective { get { return level * GameMusic.MusicScale; } }

        private AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void Update()
        {
            if (clip == null || silenced) return;
            var source = active == 0 ? first : second;
            var other = active == 0 ? second : first;
            if (!fading)
            {
                source.volume = Effective;
                var remaining = clip.length - source.time;
                if (remaining > fade) return;
                if (fade <= .001f)
                {
                    // No overlap asked for: restart at the loop point, like the game.
                    source.time = 0f;
                    return;
                }
                other.clip = clip;
                other.loop = false;
                other.time = 0f;
                other.volume = 0f;
                other.Play();
                fading = true;
                fadeStartedAt = Time.unscaledTime;
                return;
            }
            var progress = (Time.unscaledTime - fadeStartedAt) / fade;
            if (progress >= 1f)
            {
                source.Stop();
                source.volume = 0f;
                other.volume = Effective;
                active = 1 - active;
                fading = false;
                return;
            }
            var effective = Effective;
            other.volume = effective * progress;
            source.volume = effective * (1f - progress);
        }

        private void OnDestroy()
        {
            host = null;
        }
    }
}
