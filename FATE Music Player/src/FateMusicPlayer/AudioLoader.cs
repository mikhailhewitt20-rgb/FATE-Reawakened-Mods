using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace FateMusicPlayer
{
    // Decodes a song file into an AudioClip, plays the five-second preview, and plays
    // the end-into-start blend while the loop timer is being adjusted. One song is
    // audible at a time, so the menu can draw a single play button and a single row.
    internal sealed class AudioLoader : MonoBehaviour
    {
        // How much of the tail is heard before the loop point, and how long the start
        // keeps playing after the overlap has finished.
        private const float EdgeLead = 1.2f;
        private const float EdgeLinger = .8f;
        // A drag produces an update per pointer event; one blend at a time is enough.
        private const float EdgeRepeatGap = .3f;

        private static AudioLoader host;
        private static ManualLogSource log;
        private static string playingSong;
        private static float playingUntil;
        private static string lastEdgeSong;
        private static float lastEdgeAt = -99f;
        private static bool listenerWasPaused;

        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> requested = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        // A decode that failed is retried after a pause instead of being remembered as
        // impossible: a file written or locked for a moment used to make its song silent
        // for the rest of the session, which only a relaunch cleared.
        private readonly Dictionary<string, float> failed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private string loadingPath;
        private float loadingStartedAt;
        private int previewRequest;
        // A DownloadHandlerAudioClip owns the clip it produced: disposing its request
        // destroys the clip, so successful requests are kept for the session.
        private readonly List<UnityWebRequest> live = new List<UnityWebRequest>();
        private AudioSource previewSource;
        private AudioSource edgeTail;
        private AudioSource edgeHead;
        private Coroutine playingRoutine;

        internal static void Create(ManualLogSource logger)
        {
            EnsureHost(logger);
        }

        // The host is created on first use rather than at plugin Awake: that runs while
        // Unity is still inside its own startup, and an object made that early does not
        // survive into the game, which left every preview returning silently.
        internal static AudioLoader EnsureHost(ManualLogSource logger)
        {
            if (logger != null) log = logger;
            if (host != null) return host;
            var go = new GameObject("FATE Music Player audio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<AudioLoader>();
            if (log != null) log.LogInfo("Audio host ready for previews.");
            return host;
        }

        internal static AudioLoader Host { get { return host; } }

        internal static AudioClip Cached(string path)
        {
            if (host == null || string.IsNullOrEmpty(path)) return null;
            AudioClip clip;
            return host.cache.TryGetValue(path, out clip) ? clip : null;
        }

        // Asks for a decode at most once every few seconds per path, so a slot that is
        // retried every frame cannot spawn a request storm. A path that failed is tried
        // again after a longer pause rather than being written off for the session.
        internal static void Request(string path)
        {
            if (host == null || string.IsNullOrEmpty(path)) return;
            if (host.cache.ContainsKey(path)) return;
            float last;
            if (host.requested.TryGetValue(path, out last) && Time.unscaledTime - last < 5f) return;
            float failedAt;
            if (host.failed.TryGetValue(path, out failedAt) && Time.unscaledTime - failedAt < 10f) return;
            host.requested[path] = Time.unscaledTime;
            host.StartCoroutine(host.Load(path, null));
        }

        // True while this song is audible from a preview or a loop blend.
        internal static bool IsPlaying(string songId)
        {
            return songId != null && playingSong == songId && Time.unscaledTime < playingUntil;
        }

        internal static void StopPreview()
        {
            if (host != null) host.Halt();
        }

        // Game clips are already decoded by FATE, so they can play immediately.
        internal static void PreviewClip(string songId, AudioClip clip, float seconds)
        {
            if (clip == null) return;
            var loader = EnsureHost(null);
            if (loader == null) return;
            loader.PlayPreview(songId, clip, seconds);
        }

        internal static void Preview(string songId, string path, float seconds)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (EnsureHost(null) == null) return;
            var clip = Cached(path);
            if (clip != null)
            {
                host.PlayPreview(songId, clip, seconds);
                return;
            }
            // Pressing play again while the same file is still decoding must not start a
            // second request: the one in flight plays when it is ready.
            if (host.loadingPath == path && Time.unscaledTime - host.loadingStartedAt < 1.5f) return;
            host.loadingPath = path;
            host.loadingStartedAt = Time.unscaledTime;
            // Only the newest press is allowed to start audio; a stop, or a play on
            // another song, makes this decode answer nothing even when it finishes.
            var request = ++host.previewRequest;
            host.StartCoroutine(host.Load(path, delegate(AudioClip loaded)
            {
                if (loaded != null && host.previewRequest == request) host.PlayPreview(songId, loaded, seconds);
            }));
        }

        // Plays the end of the song running into its start, overlapping by fade
        // seconds, so the loop timer can be judged by ear. Zero fades by cutting from
        // the end straight back to the beginning.
        internal static void PreviewLoopEdge(string songId, string path, AudioClip clip, float fade)
        {
            var loader = EnsureHost(null);
            if (loader == null) return;
            host = loader;
            if (clip == null)
            {
                if (string.IsNullOrEmpty(path)) return;
                var cached = Cached(path);
                if (cached != null) clip = cached;
                else
                {
                    if (host.loadingPath == path && Time.unscaledTime - host.loadingStartedAt < 1.5f) return;
                    host.loadingPath = path;
                    host.loadingStartedAt = Time.unscaledTime;
                    var request = ++host.previewRequest;
                    host.StartCoroutine(host.Load(path, delegate(AudioClip loaded)
                    {
                        if (loaded != null && host.previewRequest == request) PreviewLoopEdge(songId, path, loaded, fade);
                    }));
                    return;
                }
            }
            if (songId == lastEdgeSong && Time.unscaledTime - lastEdgeAt < EdgeRepeatGap) return;
            lastEdgeSong = songId;
            lastEdgeAt = Time.unscaledTime;
            host.PlayEdge(songId, clip, fade);
        }

        private void PlayPreview(string songId, AudioClip clip, float seconds)
        {
            Halt();
            if (previewSource == null) previewSource = NewSource("FATE Music Player preview");
            previewSource.clip = clip;
            previewSource.loop = false;
            // A preview is music, so it belongs on the game's music mixer group and follows
            // the in-game music volume slider.
            GameMusic.Route(previewSource);
            previewSource.volume = GameMusic.MusicScale;
            // The game's own track steps aside so the preview is heard on its own.
            GameMusic.MutePlayback();
            BeginAudible();
            try { previewSource.time = 0f; } catch { }
            previewSource.Play();
            playingSong = songId;
            playingUntil = Time.unscaledTime + Mathf.Max(.5f, seconds);
            playingRoutine = StartCoroutine(StopAfter(seconds));
            if (log != null)
                log.LogInfo("Preview: " + clip.name + " (" + clip.length.ToString("0.0") + "s) for " + Format(seconds) + "s");
        }

        private void PlayEdge(string songId, AudioClip clip, float fade)
        {
            Halt();
            if (edgeTail == null) edgeTail = NewSource("FATE Music Player loop tail");
            if (edgeHead == null) edgeHead = NewSource("FATE Music Player loop head");
            GameMusic.Route(edgeTail);
            GameMusic.Route(edgeHead);
            var length = clip.length;
            fade = Mathf.Clamp(fade, 0f, Mathf.Max(0f, length * .5f));
            var tailStart = Mathf.Max(0f, length - fade - EdgeLead);
            var tailFor = Mathf.Max(.05f, (length - fade) - tailStart);
            var linger = Mathf.Min(EdgeLinger, Mathf.Max(.2f, fade));
            GameMusic.MutePlayback();
            BeginAudible();
            playingSong = songId;
            playingUntil = Time.unscaledTime + tailFor + fade + linger;
            playingRoutine = StartCoroutine(EdgeBlend(clip, fade, tailStart, tailFor, linger));
            if (log != null)
                log.LogInfo("Loop blend: " + clip.name + " (clip " + length.ToString("0.0") + "s, overlap " + fade.ToString("0.0") + "s)");
        }

        // The tail plays up to the loop point, the start begins on top of it, and the
        // two trade volume across the overlap.
        private IEnumerator EdgeBlend(AudioClip clip, float fade, float tailStart, float tailFor, float linger)
        {
            var scale = GameMusic.MusicScale;
            edgeTail.clip = clip;
            edgeTail.loop = false;
            edgeTail.volume = scale;
            try { edgeTail.time = tailStart; } catch { edgeTail.time = 0f; }
            edgeTail.Play();
            var tailEnd = Time.unscaledTime + tailFor;
            while (Time.unscaledTime < tailEnd) yield return null;

            edgeHead.clip = clip;
            edgeHead.loop = false;
            edgeHead.volume = fade <= .001f ? scale : 0f;
            try { edgeHead.time = 0f; } catch { }
            edgeHead.Play();
            if (fade > .001f)
            {
                var start = Time.unscaledTime;
                while (true)
                {
                    var t = (Time.unscaledTime - start) / fade;
                    if (t >= 1f) break;
                    edgeHead.volume = scale * t;
                    edgeTail.volume = scale * (1f - t);
                    yield return null;
                }
            }
            edgeTail.Stop();
            edgeHead.volume = scale;
            var lingerEnd = Time.unscaledTime + linger;
            while (Time.unscaledTime < lingerEnd) yield return null;
            edgeHead.Stop();
            playingSong = null;
            playingUntil = 0f;
            GameMusic.RestorePlayback();
            EndAudible();
            playingRoutine = null;
        }

        private IEnumerator StopAfter(float seconds)
        {
            var end = Time.unscaledTime + Mathf.Max(.5f, seconds);
            while (Time.unscaledTime < end) yield return null;
            if (previewSource != null) previewSource.Stop();
            playingSong = null;
            playingUntil = 0f;
            GameMusic.RestorePlayback();
            EndAudible();
            playingRoutine = null;
        }

        private void Halt()
        {
            // Any decode still in flight answers nothing now.
            previewRequest++;
            if (playingRoutine != null)
            {
                StopCoroutine(playingRoutine);
                playingRoutine = null;
            }
            if (previewSource != null) previewSource.Stop();
            if (edgeTail != null) edgeTail.Stop();
            if (edgeHead != null) edgeHead.Stop();
            playingSong = null;
            playingUntil = 0f;
            GameMusic.RestorePlayback();
            EndAudible();
        }

        // The game pauses its audio listener while the pause menu is up, which would
        // swallow a preview played from that menu. It is held open while anything is
        // playing and handed back afterwards.
        private static void BeginAudible()
        {
            listenerWasPaused = AudioListener.pause;
            if (AudioListener.pause) AudioListener.pause = false;
        }

        private static void EndAudible()
        {
            if (listenerWasPaused) AudioListener.pause = true;
            listenerWasPaused = false;
        }

        private void Update()
        {
            if (playingSong == null) return;
            if (AudioListener.pause) AudioListener.pause = false;
            // The music volume can change during the five seconds, and this is the only
            // place that has to follow it when the mixer group could not be applied.
            if (previewSource != null) previewSource.volume = GameMusic.MusicScale;
        }

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

        private IEnumerator Load(string path, Action<AudioClip> done)
        {
            var type = AudioTypeFor(path);
            if (type == AudioType.UNKNOWN)
            {
                if (log != null) log.LogWarning("Unsupported audio file: " + path);
                Fail(path);
                if (done != null) done(null);
                yield break;
            }
            var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            var request = UnityWebRequestMultimedia.GetAudioClip(uri, type);
            yield return request.SendWebRequest();
            if (request.error != null)
            {
                if (log != null) log.LogWarning("Could not read " + path + ": " + request.error + " (will try again in 10s)");
                Fail(path);
                request.Dispose();
                if (done != null) done(null);
                yield break;
            }
            var handler = request.downloadHandler as DownloadHandlerAudioClip;
            var clip = handler == null ? null : handler.audioClip;
            if (clip == null)
            {
                if (log != null) log.LogWarning("No audio in " + path + " (will try again in 10s)");
                Fail(path);
                request.Dispose();
                if (done != null) done(null);
                yield break;
            }
            clip.name = Path.GetFileNameWithoutExtension(path);
            cache[path] = clip;
            failed.Remove(path);
            live.Add(request);
            FinishLoading(path);
            if (done != null) done(clip);
        }

        // A failed decode is remembered only long enough to stop a retry storm.
        private void Fail(string path)
        {
            cache.Remove(path);
            failed[path] = Time.unscaledTime;
            FinishLoading(path);
        }

        private void FinishLoading(string path)
        {
            if (string.Equals(loadingPath, path, StringComparison.OrdinalIgnoreCase)) loadingPath = null;
        }

        private static string Format(float seconds)
        {
            return seconds.ToString("0.0");
        }

        private void OnDestroy()
        {
            GameMusic.RestorePlayback();
            EndAudible();
            foreach (var request in live) if (request != null) request.Dispose();
            live.Clear();
            host = null;
        }

        internal static AudioType AudioTypeFor(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                case ".mp3": return AudioType.MPEG;
                case ".aiff":
                case ".aif": return AudioType.AIFF;
                default: return AudioType.UNKNOWN;
            }
        }
    }
}
