using System;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The start freeze (judge round 2, E 10: the arena's slowest warm-up frame took 2.97 s). On Direct3D 12 the driver
    /// builds a pipeline state for every shader, variant and render state the first time it is drawn, and the arena's first
    /// frame draws nearly all of them at once; on top of that come the scene's own start-up jobs (pools, faces, sounds and
    /// bot bodies, now spread over the first frames by <see cref="SkylineWarmQueue"/>). Two parts:
    /// <list type="bullet">
    /// <item><b>Warm-up from a trace.</b> When the build carries <c>StreamingAssets/Skyline/SkylineArena.graphicsstate</c> (a
    /// trace the perf run saves, <see cref="SkylinePerfProbe"/>), its pipeline states are built before the first frame,
    /// in one go. A trace from another graphics API or quality level is skipped by Unity itself.</item>
    /// <item><b>A cover.</b> In a player, a Skyline scene opens on black, which stays until the frames have run smoothly for a
    /// moment (eight frames under 50 ms in a row, at least 0.3 s in, after the start-up warm-ups, at most 8 s), then fades out over half a second: the
    /// player sees the scene fade in, not a frozen first frame. Not in the editor (the recordings start on the scene).</item>
    /// </list>
    /// Logs <c>SKYLINE_START ...</c> when the cover lifts (how long it stayed, the slowest frame under it).
    /// </summary>
    public sealed class SkylineStartCover : MonoBehaviour
    {
        public const string TraceFolder = "Skyline";
        // perf round 5: MaxHold 8 -> 20 s; the warm-up now also draws every scene material and every ink template on every mesh
        // layout (SkylinePrewarm.DrawEverythingOnce), so its pipeline-state builds take a few seconds more behind the cover
        const float MinHold = 0.3f, MaxHold = 20f, Fade = 0.5f, SmoothMs = 50f;
        const int SmoothFrames = 8;

        float _born, _fadeFrom = -1f, _slowest;
        int _smooth;
        static Texture2D _black;
        static bool _covering;

        /// <summary>False while the black cover is up (the scene is still warming); true once it starts to fade, and always in the editor
        /// and in batch mode (no cover). The perf probe starts measuring only when this is true, so the load is not counted as play.</summary>
        public static bool Done => !_covering;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _covering = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            var scene = SceneManager.GetActiveScene().name;
            if (scene == null || !scene.StartsWith("Skyline", StringComparison.Ordinal)) return;
            WarmFromTrace(scene);
            var go = new GameObject("Skyline start cover");
            go.AddComponent<SkylineStartCover>()._born = Time.realtimeSinceStartup;
            _covering = true;
        }

        static void WarmFromTrace(string scene)
        {
            string path = Path.Combine(Application.streamingAssetsPath, TraceFolder, scene + ".graphicsstate");
            if (!File.Exists(path)) return;
            try
            {
                float t0 = Time.realtimeSinceStartup;
                var states = new GraphicsStateCollection();
                if (!states.LoadFromFile(path))
                {
                    Debug.Log("SKYLINE_START pipeline-state trace unreadable: " + path);
                    return;
                }
                states.WarmUp().Complete();
                Debug.Log("SKYLINE_START warmed " + states.totalGraphicsStateCount + " pipeline states in " +
                          ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            }
            catch (Exception e)
            {
                Debug.Log("SKYLINE_START pipeline-state warm-up skipped: " + e.Message);
            }
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            float ms = Time.unscaledDeltaTime * 1000f;
            if (_fadeFrom < 0f)
            {
                if (ms > _slowest) _slowest = ms;
                // perf round 4: the start-up warm-ups (sounds, bodies, idle drawings, glyphs) must finish under the cover
                bool warming = SkylinePrewarm.Busy || SkylineWarmQueue.Pending > 0;
                _smooth = ms < SmoothMs && !warming ? _smooth + 1 : 0;
                float held = now - _born;
                if ((held >= MinHold && _smooth >= SmoothFrames) || held >= MaxHold)
                {
                    _fadeFrom = now;
                    _covering = false;
                    Debug.Log("SKYLINE_START cover lifted after " + held.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                              " s (slowest frame under it " + _slowest.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms)");
                }
            }
            else if (now - _fadeFrom >= Fade) Destroy(gameObject);
        }

        void OnDestroy() => _covering = false;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float a = _fadeFrom < 0f ? 1f : Mathf.Clamp01(1f - (Time.realtimeSinceStartup - _fadeFrom) / Fade);
            if (a <= 0f) return;
            if (_black == null) _black = Texture2D.whiteTexture;
            GUI.depth = -100000;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, a * a * (3f - 2f * a));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
            GUI.color = old;
        }
    }
}
