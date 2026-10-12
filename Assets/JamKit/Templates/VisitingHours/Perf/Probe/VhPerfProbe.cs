using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.Argument;
using JamKit.DeepBlock;
using JamKit.DreamEscape;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.VisitingHours;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JamKit.Perf
{
    /// <summary>
    /// The perf probe (docs/redesign/perf.md): a player flag that loads each gameplay scene, flies the camera along set paths
    /// at player height and from the vistas (<see cref="VhPerfScenes"/>), records every frame (<see cref="VhPerfRecorder"/>)
    /// and writes a JSON per scene plus a readable summary, then quits.
    /// <code>
    /// VisitingHours.exe -perf-probe "C:/.../out/perf/&lt;time&gt;" [-perf-scene hill1,house] [-perf-warmup 3] [-perf-max 120]
    ///     [-vh-tier high|low] [-perf-windowed] [-perf-stay] -logFile "C:/.../out/perf/&lt;time&gt;/player.log"
    /// </code>
    /// Scenes: hill1, hill2, house, escape, ward, battle (all by default). The run is 1920x1080, vSync off and the frame rate
    /// uncapped, so the numbers are what the machine can do. Frames within half a second of a cut (a teleport, entering a
    /// ward area) are kept out of the steady numbers and reported as hitches. Log lines start with JAMKIT_PERF; the last is
    /// JAMKIT_PERF_DONE (exit 0) or JAMKIT_PERF_FAIL (exit 1: a scene couldn't be measured).
    /// <para>Counters need a development build (draw calls, SetPass, triangles, shadow casters) and CPU / GPU times need
    /// Frame Timing Stats (on in development builds; VhPerfBuild turns it on for a release build). Frame times and memory
    /// are recorded in any build.</para>
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class VhPerfProbe : MonoBehaviour
    {
        public const string Flag = "-perf-probe", SceneFlag = "-perf-scene";
        public const float Settle = 0.5f;

        public string outDir;
        public string[] scenes = VhPerfScenes.All;
        public float warmup = 3f;
        public float maxSeconds = 150f;
        public bool windowed, stay;

        VhPerfFlight _flight;
        VhPerfRecorder _rec;
        bool _flying, _warming, _warmEntered, _done;
        float _t, _cutAt = -10f, _warmLeft;
        int _leg = -1;
        string _legLabel = "";
        readonly List<string> _summary = new List<string>();
        readonly List<string> _jsons = new List<string>();
        int _failed;

        static VhPerfProbe _instance;
        public static bool Running => _instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            string dir = null;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == Flag) dir = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : "out/perf/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            if (dir == null || _instance != null) return;
            var go = new GameObject("VH Perf Probe (JamKit.Perf)");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<VhPerfProbe>();
            p.outDir = Path.GetFullPath(dir);
            string only = Arg(args, SceneFlag);
            if (!string.IsNullOrEmpty(only) && only != "all") p.scenes = only.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (float.TryParse(Arg(args, "-perf-warmup"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) p.warmup = w;
            if (float.TryParse(Arg(args, "-perf-max"), NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) p.maxSeconds = m;
            p.windowed = Has(args, "-perf-windowed");
            p.stay = Has(args, "-perf-stay");
            _instance = p;
            // The opening isn't measured: straight to the title (as the ward stills do).
            VhBoot.SkipOpening = true;
        }

        static string Arg(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1].Trim('"');
            return null;
        }

        static bool Has(string[] args, string name) => Array.IndexOf(args, name) >= 0;

        IEnumerator Start()
        {
            yield return null;
            // The Look agent's first probe answered the same flag; this one replaces it.
            var old = GameObject.Find("VH Perf Probe");
            if (old != null) Destroy(old);
            Directory.CreateDirectory(outDir);
            Screen.SetResolution(1920, 1080, windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
            Log("start: " + string.Join(",", scenes) + " into " + outDir);
            yield return Until(() => GameRoot.Exists && GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, 90f);
            if (!GameRoot.Exists) { Finish("the game never reached the title"); yield break; }
            VhBoot.SkipOpening = true;
            GameRoot.Save.Data.seenHowToPlay = true;
            VisitingHoursGame.FastTransitions = true;
            yield return Real(1f);
            foreach (var id in scenes) yield return Measure(id.Trim());
            Finish(null);
        }

        // ---------- One scene ----------

        IEnumerator Measure(string id)
        {
            string scene = VhPerfScenes.SceneOf(id);
            string problem = null;
            if (scene == null) problem = "unknown probe scene \"" + id + "\" (use " + string.Join(", ", VhPerfScenes.All) + ")";
            else if (!Application.CanStreamedLevelBeLoaded(scene)) problem = "scene " + scene + " is not in the build";
            if (problem == null) yield return Enter(id, scene, p => problem = p);
            if (problem == null)
            {
                _flight = BuildFlight(id);
                if (_flight == null || _flight.legs.Count == 0) problem = "no flight for " + id + " (its game object wasn't found)";
            }
            if (problem != null)
            {
                _failed++;
                Log("FAIL " + id + ": " + problem);
                _summary.Add(id.PadRight(7) + " NOT MEASURED: " + problem);
                Write(id + ".json", "{ \"id\": " + VhPerfRecorder.Q(id) + ", \"problem\": " + VhPerfRecorder.Q(problem) + " }\n");
                yield break;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            int capacity = Mathf.CeilToInt(Mathf.Min(_flight.Seconds, maxSeconds) * 600f) + 600;
            _rec = new VhPerfRecorder(id, capacity);
            _flight.Reset();
            _t = 0f;
            _leg = -1;
            _warmLeft = warmup;
            _warming = true;
            _warmEntered = false;
            _flying = true;
            float limit = Time.realtimeSinceStartup + Mathf.Min(_flight.Seconds, maxSeconds) + warmup + 60f;
            while (_flying && Time.realtimeSinceStartup < limit) yield return null;
            _flying = false;
            var s = _rec.Compute();
            string line = _rec.SummaryLine(s);
            _summary.Add(line);
            foreach (var kv in _rec.WorstLegs(_flight, 5))
                _summary.Add("          worst: " + kv.Key + ": " + kv.Value.mean.ToString("0.00", CultureInfo.InvariantCulture) + " ms avg, 1% low " +
                             kv.Value.worst1.ToString("0.00", CultureInfo.InvariantCulture) + " ms" +
                             (kv.Value.tris > 0 ? ", " + (kv.Value.tris / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + "M tris, " + kv.Value.draws.ToString("0", CultureInfo.InvariantCulture) + " draws" : ""));
            string json = _rec.ToJson(_flight, Machine(), scene, null);
            _jsons.Add(json);
            Write(id + ".json", json);
            Log(line);
            _rec.Dispose();
            _rec = null;
            _flight = null;
        }

        IEnumerator Enter(string id, string scene, Action<string> fail)
        {
            if (id == VhPerfScenes.Ward)
            {
                if (SceneManager.GetActiveScene().name != scene && !GameRoot.Instance.StartRunAt(scene))
                {
                    yield return BackToTitle();
                    if (!GameRoot.Instance.StartRunAt(scene)) { fail("couldn't start " + scene); yield break; }
                }
                yield return Until(() => !GameRoot.IsLoadingScene && VisitingHoursGame.Current != null && VisitingHoursGame.Current.CurrentMode == VisitingHoursGame.Mode.Explore, 90f);
                if (VisitingHoursGame.Current == null) fail("the Visiting Hours scene didn't start");
                yield break;
            }
            var r = PresenterMode.Jump(scene, VhPerfScenes.ChapterOf(id));
            if (r == JumpResult.Failed)
            {
                yield return BackToTitle();
                r = PresenterMode.Jump(scene, VhPerfScenes.ChapterOf(id));
            }
            if (r == JumpResult.Failed || r == JumpResult.Unknown) { fail("presenter jump to " + scene + " / " + VhPerfScenes.ChapterOf(id) + " failed (" + r + ")"); yield break; }
            yield return Until(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == scene && GameObjectReady(id), 120f);
            if (!GameObjectReady(id)) { fail(scene + " didn't come up"); yield break; }
            yield return Real(2f);
            if (id == VhPerfScenes.Hill2 && DreamStreetGame.Current != null)
            {
                DreamStreetGame.Current.night = 2;
                DreamStreetGame.Current.NewNight();
                yield return Real(1f);
            }
            if (id == VhPerfScenes.Escape && DreamHouseGame.Current != null)
            {
                // The house folds shut on someone who stands still: stop the game's logic so the probe isn't thrown out.
                yield return Real(1f);
                DreamHouseGame.Current.enabled = false;
            }
        }

        static IEnumerator BackToTitle()
        {
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            yield return Until(() => !GameRoot.IsLoadingScene, 60f);
            GameRoot.Instance.QuitToTitle();
            yield return Until(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, 90f);
            yield return Real(1f);
        }

        static bool GameObjectReady(string id)
        {
            switch (id)
            {
                case VhPerfScenes.Hill1: case VhPerfScenes.Hill2: return DreamStreetGame.Current != null;
                case VhPerfScenes.House: case VhPerfScenes.Escape: return DreamHouseGame.Current != null;
                case VhPerfScenes.Battle: return ArgumentPrototype.Current != null;
                default: return true;
            }
        }

        VhPerfFlight BuildFlight(string id)
        {
            switch (id)
            {
                case VhPerfScenes.Hill1:
                {
                    var g = DreamStreetGame.Current;
                    if (g == null) return null;
                    return VhPerfScenes.Hill1Flight(g.Hill ?? LarchHill.Build(), g.LintRoutes());
                }
                case VhPerfScenes.Hill2:
                {
                    var g = DreamStreetGame.Current;
                    return g == null ? null : VhPerfScenes.Hill2Flight(g.Hill ?? LarchHill.Build());
                }
                case VhPerfScenes.House:
                    return DreamHouseGame.Current == null ? null : VhPerfScenes.HouseFlight(DreamHouseGame.Current.houseRoot);
                case VhPerfScenes.Escape:
                    return DreamHouseGame.Current == null ? null : VhPerfScenes.EscapeFlight(DreamHouseGame.Current.escapeRoot);
                case VhPerfScenes.Ward:
                    return VhPerfScenes.WardFlight(EnterWardArea);
                case VhPerfScenes.Battle:
                    return VhPerfScenes.BattleFlight(Mathf.Min(45f, maxSeconds));
                default:
                    return null;
            }
        }

        static void EnterWardArea(SliceArea area)
        {
            var game = VisitingHoursGame.Current;
            if (game == null) return;
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            game.DebugJumpTo(area);
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            VhPerfTiers.CapNow();
        }

        // ---------- Flying (after every other LateUpdate) ----------

        void LateUpdate()
        {
            if (!_flying || _flight == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_warming)
            {
                // Warm-up: the camera at the flight's start, nothing recorded (shaders, streaming, the first frames).
                if (!_warmEntered)
                {
                    // The first leg's action (entering the ward's first area) happens before the warm-up, not on the clock.
                    _warmEntered = true;
                    _flight.legs[0].onStart?.Invoke();
                }
                if (_flight.overrideCamera && _flight.Sample(0f, out var e0, out var r0, out _) >= 0) Place(e0, r0);
                _flight.Reset();
                _warmLeft -= dt;
                if (_warmLeft <= 0f) { _warming = false; _rec.Start(); _t = 0f; }
                return;
            }
            _t += dt;
            int leg = _flight.Sample(_t, out var eye, out var rot, out bool started);
            if (leg < 0 || _t > maxSeconds) { _flying = false; return; }
            if (started && leg != _leg)
            {
                var l = _flight.legs[leg];
                _legLabel = l.label;
                if (l.cut) _cutAt = _t;
                if (leg > 0 || !_warmEntered) l.onStart?.Invoke();
            }
            _leg = leg;
            if (_flight.overrideCamera) Place(eye, rot);
            _rec.Frame(dt * 1000f, leg, _t - _cutAt < Settle, _legLabel);
        }

        static void Place(Vector3 eye, Quaternion rot)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(eye, rot);
        }

        // ---------- Output ----------

        string Machine()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var sb = new StringBuilder();
            sb.Append("{ \"gpu\": ").Append(VhPerfRecorder.Q(SystemInfo.graphicsDeviceName))
              .Append(", \"vramMB\": ").Append(SystemInfo.graphicsMemorySize)
              .Append(", \"api\": ").Append(VhPerfRecorder.Q(SystemInfo.graphicsDeviceType.ToString()))
              .Append(", \"cpu\": ").Append(VhPerfRecorder.Q(SystemInfo.processorType))
              .Append(", \"cores\": ").Append(SystemInfo.processorCount)
              .Append(", \"ramMB\": ").Append(SystemInfo.systemMemorySize)
              .Append(", \"resolution\": ").Append(VhPerfRecorder.Q(Screen.width + "x" + Screen.height))
              .Append(", \"quality\": ").Append(VhPerfRecorder.Q(QualitySettings.names[QualitySettings.GetQualityLevel()]))
              .Append(", \"tier\": ").Append(VhPerfRecorder.Q(VhPerfTiers.Active ? VhPerfTiers.Current.id : "none"))
              .Append(", \"rpAsset\": ").Append(VhPerfRecorder.Q(asset != null ? asset.name : "none"));
            if (asset != null)
                sb.Append(", \"renderScale\": ").Append(asset.renderScale.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append(", \"upscaling\": ").Append(VhPerfRecorder.Q(asset.upscalingFilter.ToString()))
                  .Append(", \"msaa\": ").Append(asset.msaaSampleCount)
                  .Append(", \"shadowDistance\": ").Append(asset.shadowDistance.ToString("0", CultureInfo.InvariantCulture))
                  .Append(", \"shadowCascades\": ").Append(asset.shadowCascadeCount)
                  .Append(", \"shadowResolution\": ").Append(asset.mainLightShadowmapResolution)
                  .Append(", \"opaqueTexture\": ").Append(asset.supportsCameraOpaqueTexture ? "true" : "false");
            sb.Append(", \"lodBias\": ").Append(QualitySettings.lodBias.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(", \"vSync\": ").Append(QualitySettings.vSyncCount)
              .Append(", \"developmentBuild\": ").Append(Debug.isDebugBuild ? "true" : "false")
              .Append(", \"editor\": ").Append(Application.isEditor ? "true" : "false")
              .Append(", \"unity\": ").Append(VhPerfRecorder.Q(Application.unityVersion))
              .Append(", \"when\": ").Append(VhPerfRecorder.Q(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
              .Append(" }");
            return sb.ToString();
        }

        void Finish(string problem)
        {
            if (_done) return;
            _done = true;
            if (problem != null) { _failed++; _summary.Add("PROBE: " + problem); }
            var sb = new StringBuilder();
            sb.AppendLine("Visiting Hours perf probe, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            sb.AppendLine("Target 8.33 ms (120 fps) average; floor: 1% low (mean of the slowest 1% of frames) under 16.7 ms (60 fps).");
            sb.AppendLine("Machine: " + Machine());
            sb.AppendLine();
            foreach (var l in _summary) sb.AppendLine(l);
            Write("summary.txt", sb.ToString());
            var all = new StringBuilder();
            all.Append("[\n");
            for (int i = 0; i < _jsons.Count; i++) { if (i > 0) all.Append(",\n"); all.Append(_jsons[i]); }
            all.Append("]\n");
            Write("summary.json", all.ToString());
            Log(sb.ToString());
            Debug.Log((_failed == 0 ? "JAMKIT_PERF_DONE " : "JAMKIT_PERF_FAIL ") + _failed + " scene(s) not measured; " + Path.Combine(outDir, "summary.txt"));
            if (stay) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(_failed == 0 ? 0 : 1);
#endif
        }

        void Write(string file, string text)
        {
            try { File.WriteAllText(Path.Combine(outDir, file), text); }
            catch (Exception e) { Debug.LogWarning("JAMKIT_PERF couldn't write " + file + ": " + e.Message); }
        }

        static void Log(string s) => Debug.Log("JAMKIT_PERF " + s);

        static IEnumerator Until(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Real(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            _rec?.Dispose();
        }
    }
}
