using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.MovementLab;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Visiting Hours' first escape: Nadia runs from the dream's immune system (the static) down the dream
    /// version of Walt's street, on the Titanfall-style movement module (docs/DREAM-ESCAPE.md). The route is core
    /// <see cref="DreamEscapeCourse"/> data built into the scene by DreamEscapeBuilder; this runs it: the static
    /// (core <see cref="Pursuer"/>, rubber-banded along the route) wakes at Walt's house, the two collapses fire
    /// by position, checkpoints catch you (caught or dropped: a quick white-out and you're back at the last one,
    /// running), the static quietly gives more room after repeated fails at one spot, the chase music follows
    /// the gap, and the safe light at the end finishes the escape. Presenter chapters, attract mode, the smoke
    /// run's drive and the Movement Lab's F3 debug HUD come with it.
    /// For the story: <see cref="Escaped"/> fires when Nadia reaches the light; with no listener the escape shows
    /// its time and starts again from the driveway (standalone play).
    /// </summary>
    public sealed class DreamEscapeGame : MonoBehaviour, IDreamRainHost
    {
        public MovementController player;
        public DreamStaticView staticView;
        public DreamChaseMusic music;
        public Transform courseRoot;
        public Transform skyDome;
        public Light exitLight;

        static DreamEscapeGame _current;
        static Action _escaped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _escaped = null;
        }

        public static DreamEscapeGame Current => _current;

        /// <summary>Fires when Nadia reaches the safe light (wire the story here). Without a listener the escape loops.</summary>
        public static event Action Escaped
        {
            add => _escaped += value;
            remove => _escaped -= value;
        }

        DreamEscapeCourse _dream;
        LabCourse _course;
        readonly List<DreamPieceView> _pieces = new List<DreamPieceView>();
        bool[] _collapsed;
        int[] _failsAt;
        Pursuer _pursuer;
        CourseAutopilot _pilot;
        LabHud _hud;
        VisualElement _white;
        float _whiteAlpha;
        int _checkpoint;
        bool _chasing, _respawning, _finishing, _leaving;
        float _runTime, _best, _lastTime, _s, _topSpeed, _waitAtStart, _launchSpeed;
        bool _timing;
        int _respawns, _caught, _falls, _finishes, _collapsesFired;
        Func<IEnumerator> _smokeDriver;
        Func<bool> _skip;
        MoveMode _lastMode = MoveMode.Air;
        readonly List<string> _trace = new List<string>(64);
        const string Newline = "\n";

        public DreamEscapeCourse Dream => _dream;
        public LabCourse Course => _course;
        public float S => _s;
        public int Checkpoint => _checkpoint;
        public bool Chasing => _chasing;
        public float Gap => _chasing ? _pursuer.Gap(_s) : 0f;
        public float Threat => _chasing ? _pursuer.Threat(_s) : 0f;
        public Pursuer Pursuer => _pursuer;
        public int Respawns => _respawns;
        public int Caught => _caught;
        public int Falls => _falls;
        public int Finishes => _finishes;
        public int CollapsesFired => _collapsesFired;
        public float LastTime => _lastTime;
        public float TopSpeed => _topSpeed;
        /// <summary>The horizontal speed the last respawn launched the player at.</summary>
        public float LaunchSpeed => _launchSpeed;
        public float WhiteAlpha => _whiteAlpha;
        public LabHud Hud => _hud;
        public bool AutopilotOn => player != null && player.Autopilot != null;
        /// <summary>True from the moment a fall or a catch is noticed until the player is back and the white-out is gone.</summary>
        public bool Respawning => _respawning;
        public bool Finishing => _finishing;
        /// <summary>True from the end of the rain path until the scene leaves (the light swells).</summary>
        public bool Leaving => _leaving;
        /// <summary>The Despair route: Nadia went on into the rain without the umbrella (kept in the save, across runs;
        /// nothing reads it yet but the debug HUD). Set by <see cref="DreamUmbrella"/>.</summary>
        public static bool Despair => GameRoot.Exists && GameRoot.Save.Data.HasFlag(DreamRain.DespairFlag);
        MovementController IDreamRainHost.Player => player;
        DreamRainSpot IDreamRainHost.RainSpot => _rainSpot ?? (_dream != null ? _rainSpot = _dream.RainSpot : null);
        DreamRainSpot _rainSpot;
        public IReadOnlyList<DreamPieceView> Pieces => _pieces;
        public IReadOnlyList<string> Trace => _trace;
        public string TraceText => string.Join(Newline, _trace);

        void Start()
        {
            // Nadia's body uses the waking-world material, which also adds the title's waking ambient (shader
            // globals) when it was set earlier this launch: the dream lights her with its own (her material's) only.
            Shader.SetGlobalColor("_VHWakeSky", Color.black);
            Shader.SetGlobalColor("_VHWakeGround", Color.black);
            Shader.SetGlobalFloat("_VHWakeSH", 0f);
            if (!GameRoot.Exists || player == null)
            {
                Debug.LogError("DreamEscapeGame: missing the player. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            _dream = DreamEscapeCourse.Build();
            _course = _dream.course;
            _pursuer = new Pursuer();
            ResetPursuerTuning(false);
            _pilot = new CourseAutopilot(_course);
            _collapsed = new bool[_course.collapses.Count];
            _failsAt = new int[_course.checkpoints.Count];
            for (int i = 0; i < _course.pieces.Count; i++) _pieces.Add(null);
            var views = courseRoot != null ? courseRoot.GetComponentsInChildren<DreamPieceView>(true) : FindObjectsByType<DreamPieceView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var v in views)
                if (v.index >= 0 && v.index < _pieces.Count) _pieces[v.index] = v;

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new LabHud(GameRoot.Hud.Root);
            if (_hud.Shown) _hud.Toggle(); // the speed HUD is for tuning: F3 (View) shows it
            _white = new VisualElement { name = "dream-whiteout", pickingMode = PickingMode.Ignore };
            _white.style.position = Position.Absolute;
            _white.style.left = 0; _white.style.right = 0; _white.style.top = 0; _white.style.bottom = 0;
            _white.style.backgroundColor = new Color(1f, 0.98f, 0.95f, 1f);
            _white.style.opacity = 0f;
            _white.style.display = DisplayStyle.None;
            GameRoot.Ui.Root.Add(_white);
            MovementComfortScreen.Ensure();

            DreamEscapePresenter.Register(PresenterMode.Chapters, this, this);
            PresenterMode.RegisterDriver(this, DreamEscapePresenter.Scene, AttractDrive);
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            _skip = SkipToNextCheckpoint;
            DebugMenu.SkipHandler = _skip;
            GameRoot.Run.Set("Dream", "Walt's street");
            GameRoot.Audio.StopMusic(0.4f);
            if (music != null) music.Begin();

            PlaceAt(0, false, false);
            GameRoot.Hud.Message("The street is waking up. Run: hold Shift (or click L3) to sprint.", 3.5f);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (!GameRoot.Exists) return;
            PresenterMode.Unregister(this);
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            if (DebugMenu.SkipHandler == _skip) DebugMenu.SkipHandler = null;
            MovementComfortScreen.Remove();
            _hud?.Remove();
            _white?.RemoveFromHierarchy();
        }

        void ResetPursuerTuning(bool eased)
        {
            var p = _course.pursuer;
            _pursuer.targetGap = p.targetGap + (eased ? _dream.easeGap : 0f);
            _pursuer.k = p.k;
            _pursuer.minSpeed = p.minSpeed;
            _pursuer.maxSpeed = p.maxSpeed - (eased ? 0.8f : 0f);
            _pursuer.catchGap = p.catchGap;
            _pursuer.catchTime = p.catchTime;
        }

        // ---------- The run ----------

        void Update()
        {
            if (_course == null) return;
            float dt = Time.deltaTime;
            Vector3 p = player.transform.position;
            _s = _course.route.Project(new System.Numerics.Vector3(p.x, p.y, p.z));
            if (player.Mode != _lastMode)
            {
                _lastMode = player.Mode;
                AddTrace(player.Mode.ToString() + (player.Mode == MoveMode.Wallrun ? (player.Sim.WallSide > 0 ? " (wall right)" : " (wall left)") : ""));
            }
            if (GameRoot.Flow.Current == GameState.Playing && !_respawning) Tick(dt, p);
            bool toggle = (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            if (toggle && GameRoot.Input.GameplayAllowed) _hud.Toggle();
            float threat = Threat;
            _hud.Tick(player, _course.SectionAt(_s), _runTime, _best, _chasing, Gap, threat, dt);
            if (music != null) music.SetThreat(_chasing ? threat : 0f);
            if (staticView != null && _chasing)
            {
                float ps = _pursuer.S;
                float floor = _dream.FloorTopAt(_course.LaneAt(ps), ps, p.y + 3f, out float top) ? top : p.y - 1f;
                var at = _course.route.PointAt(ps, 0f);
                staticView.Place(new Vector3(_course.LaneAt(ps), Mathf.Min(floor, p.y) - 1.5f, at.Z), threat, player.cam != null ? player.cam.transform : null);
            }
        }

        void LateUpdate()
        {
            if (skyDome != null && player != null && player.cam != null) skyDome.position = player.cam.transform.position;
        }

        void Tick(float dt, Vector3 p)
        {
            _topSpeed = Mathf.Max(_topSpeed, player.Speed);
            if (!_timing && _s > _course.checkpoints[0].s + 1f && !_finishing) { _timing = true; _runTime = 0f; }
            if (_timing) _runTime += dt;

            for (int i = _checkpoint + 1; i < _course.checkpoints.Count; i++)
            {
                if (_s < _course.checkpoints[i].s) break;
                _checkpoint = i;
                if (_failsAt[i] < _dream.easeAfterFails) ResetPursuerTuning(false);
                GameRoot.Audio.Play("mv_checkpoint", 0.45f);
            }

            for (int i = 0; i < _collapsed.Length; i++)
            {
                if (_collapsed[i] || _s < _course.collapses[i].triggerS) continue;
                Collapse(i, true);
            }

            // The static wakes when you run, or by itself if you stand in the driveway too long.
            if (!_chasing && !_finishing && _s < _course.chaseEndS)
            {
                _waitAtStart += dt;
                if (_s >= _course.chaseStartS || _waitAtStart >= _dream.wakeAfter) StartChase();
            }
            if (_chasing)
            {
                var dir = _course.route.DirectionAt(_s);
                float along = Vector3.Dot(player.Velocity, new Vector3(dir.X, dir.Y, dir.Z));
                bool caught = _pursuer.Step(_s, along, dt) && !DebugMenu.PlayerInvincible;
                if (_s >= _course.chaseEndS) StopChase();
                if (caught)
                {
                    _caught++;
                    GameRoot.Audio.Play("mv_caught", 0.8f);
                    StartCoroutine(Respawn("The static caught you."));
                    return;
                }
            }

            if (p.y < _course.killY)
            {
                _falls++;
                StartCoroutine(Respawn("The dream let go of you."));
                return;
            }

            if (!_finishing && _s >= _course.finishS && player.Sim.Grounded)
                Finish();
            // Out of the front door and down the rain path: she leaves the dream.
            if (_finishing && !_leaving && _s >= _dream.rainEndS && player.Sim.Grounded)
                StartCoroutine(Leave());
        }

        void StartChase()
        {
            _chasing = true;
            _pursuer.Reset(_s, player.Speed);
            if (staticView != null) staticView.Show(true);
            if (_s < _course.checkpoints[0].s + 6f) GameRoot.Audio.Play("glitch", 0.6f);
        }

        void StopChase()
        {
            _chasing = false;
            if (staticView != null) staticView.Show(false);
        }

        void Collapse(int i, bool fx)
        {
            _collapsed[i] = true;
            if (fx) _collapsesFired++;
            for (int k = 0; k < _pieces.Count; k++)
                if (_pieces[k] != null && _pieces[k].collapse == i) _pieces[k].Collapse(!fx);
            if (!fx) return;
            GameRoot.Audio.Play("mv_collapse", 0.8f);
            if (i < _dream.waves.Count && _dream.waves[i].style == DreamCollapseStyle.Peel) GameRoot.Audio.Play("page", 0.9f);
            GameRoot.Juice.Shake(0.2f);
        }

        void AddTrace(string what)
        {
            var p = player.transform.position;
            var inv = CultureInfo.InvariantCulture;
            if (_trace.Count >= 60) _trace.RemoveAt(0);
            _trace.Add(Time.time.ToString("0.00", inv) + "s  " + _course.SectionAt(_s) + "  s " + _s.ToString("0.0", inv)
                + "  pos (" + p.x.ToString("0.00", inv) + ", " + p.y.ToString("0.00", inv) + ", " + p.z.ToString("0.00", inv) + ")  "
                + what + "  " + player.Speed.ToString("0.0", inv) + " m/s");
        }

        void SetWhite(float a)
        {
            _whiteAlpha = Mathf.Clamp01(a);
            if (_white == null) return;
            _white.style.opacity = _whiteAlpha;
            _white.style.display = _whiteAlpha > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The forgiving fail: a quick white-out, and you're back at the checkpoint, running, with the
        /// static put back at its distance. After repeated fails at one checkpoint it quietly gives more room.</summary>
        IEnumerator Respawn(string message)
        {
            _respawning = true;
            _respawns++;
            if (_checkpoint < _failsAt.Length) _failsAt[_checkpoint]++;
            AddTrace("RESPAWN: " + message);
            Debug.Log("JAMKIT_DREAM respawn " + _respawns + ": " + message + Newline + TraceTail(8));
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime) { SetWhite(t / 0.15f); yield return null; }
            SetWhite(1f);
            bool eased = _checkpoint < _failsAt.Length && _failsAt[_checkpoint] >= _dream.easeAfterFails;
            ResetPursuerTuning(eased);
            PlaceAt(_checkpoint, true, true);
            GameRoot.Hud.Message(message, 1.6f);
            yield return null;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { SetWhite(1f - t / 0.35f); yield return null; }
            SetWhite(0f);
            _respawning = false;
        }

        /// <summary>Out of the static (the chase ends on the lit porch): the time, the chime. The front door and the rain
        /// path come next (<see cref="DreamUmbrella"/>); <see cref="Leave"/> ends the scene at the end of the path.</summary>
        void Finish()
        {
            _finishing = true;
            _finishes++;
            _lastTime = _runTime;
            _timing = false;
            StopChase();
            if (music != null) music.End();
            bool record = _best <= 0f || _runTime < _best;
            if (record) _best = _runTime;
            var inv = CultureInfo.InvariantCulture;
            GameRoot.Run.Set("Best escape", _best.ToString("0.0", inv) + " s");
            GameRoot.Audio.Play("chime", 0.7f);
            GameRoot.Hud.Message("Out. The light holds.   " + _runTime.ToString("0.0", inv) + " s" + (record ? " (best)" : "") + "   It's raining past the door.", 3.5f);
            Debug.Log("JAMKIT_DREAM escaped in " + _runTime.ToString("0.0", inv) + " s, top speed " + _topSpeed.ToString("0.0", inv) + " m/s, respawns " + _respawns);
        }

        /// <summary>The end of the rain path: the light swells, then the story takes over (<see cref="Escaped"/>) or the escape loops.</summary>
        IEnumerator Leave()
        {
            _leaving = true;
            // The light swells.
            for (float t = 0f; t < 1.2f && _finishing && GameRoot.Flow.InRun; t += Time.unscaledDeltaTime) { SetWhite(0.8f * t / 1.2f); yield return null; }
            if (!_finishing || !GameRoot.Flow.InRun) { SetWhite(0f); yield break; }
            if (_escaped != null)
            {
                _escaped.Invoke();
                yield break;
            }
            float until = Time.unscaledTime + 2.3f;
            while (Time.unscaledTime < until && _finishing && GameRoot.Flow.InRun) yield return null;
            if (!_finishing || !GameRoot.Flow.InRun) { SetWhite(0f); yield break; }
            PlaceAt(0, false, false);
            if (music != null) music.Begin();
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { SetWhite(0.8f * (1f - t / 0.6f)); yield return null; }
            SetWhite(0f);
        }

        /// <summary>Puts the player at a checkpoint (running at sprint, unless it's a fresh start), puts back the
        /// collapses that come after it, and sets the chase to match.</summary>
        void PlaceAt(int index, bool keepTimer, bool running)
        {
            index = Mathf.Clamp(index, 0, _course.checkpoints.Count - 1);
            var cp = _course.checkpoints[index];
            _checkpoint = index;
            _finishing = false;
            _leaving = false;
            Vector3 pos = new Vector3(cp.position.X, cp.position.Y, cp.position.Z);
            Vector3 fwd = Quaternion.Euler(0f, cp.yaw, 0f) * Vector3.forward;
            _launchSpeed = running ? player.Def.sprintSpeed : 0f;
            player.Teleport(pos, cp.yaw, fwd * _launchSpeed);
            for (int i = 0; i < _collapsed.Length; i++)
            {
                if (_course.collapses[i].triggerS <= cp.s + 0.01f) { if (!_collapsed[i]) Collapse(i, false); continue; }
                _collapsed[i] = false;
                for (int k = 0; k < _pieces.Count; k++)
                    if (_pieces[k] != null && _pieces[k].collapse == i) _pieces[k].Restore();
            }
            _s = cp.s;
            _waitAtStart = 0f;
            bool chase = running || cp.s >= _course.chaseStartS;
            if (chase && cp.s < _course.chaseEndS)
            {
                _chasing = true;
                _pursuer.Reset(cp.s, player.Def.sprintSpeed);
                if (staticView != null) staticView.Show(true);
            }
            else StopChase();
            _pilot.Reset(cp.s);
            if (index == 0 && !keepTimer) { _timing = false; _runTime = 0f; _topSpeed = 0f; }
        }

        string TraceTail(int n)
        {
            int from = Mathf.Max(0, _trace.Count - n);
            return string.Join(Newline, _trace.GetRange(from, _trace.Count - from));
        }

        // ---------- Presenter, smoke, autopilot ----------

        /// <summary>Presenter chapter: start at the checkpoint that carries this chapter (running, except the start).</summary>
        public void JumpToChapter(string chapter)
        {
            int i = _course.CheckpointIndex(chapter);
            if (i < 0) return;
            StopAllCoroutines();
            _respawning = false;
            SetWhite(0f);
            GameRoot.Ui.SetFade(0f);
            ResetPursuerTuning(false);
            PlaceAt(i, false, i > 0);
            if (music != null) music.Begin();
        }

        bool SkipToNextCheckpoint()
        {
            int next = _checkpoint + 1 < _course.checkpoints.Count ? _checkpoint + 1 : 0;
            StopAllCoroutines();
            _respawning = false;
            SetWhite(0f);
            PlaceAt(next, true, next > 0);
            return true;
        }

        /// <summary>The course autopilot drives the player (tests, the smoke run, attract mode); off hands control back.</summary>
        public void SetAutopilot(bool on)
        {
            if (on)
            {
                _pilot.Reset(_s - 1f);
                player.Autopilot = _pilot.Next;
            }
            else player.Autopilot = null;
        }

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            SetAutopilot(true);
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && GameRoot.Flow.InRun) yield return null;
            SetAutopilot(false);
            input.Clear();
        }

        /// <summary>The smoke run: the final run (roof slide, the last wall, the jump into the light) driven by the
        /// autopilot. It must reach the light with no respawn and well past sprint; otherwise it logs an error,
        /// which fails the smoke run.</summary>
        IEnumerator SmokeDrive()
        {
            JumpToChapter(DreamEscapeCourse.ChapterFinal);
            int respawns = _respawns, finishes = _finishes;
            float top = 0f;
            SetAutopilot(true);
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && GameRoot.Flow.InRun && _finishes == finishes)
            {
                top = Mathf.Max(top, player.Speed);
                yield return null;
            }
            SetAutopilot(false);
            bool escaped = _finishes > finishes;
            string report = "escaped " + escaped + ", respawns " + (_respawns - respawns) + ", top speed " + top.ToString("0.0", CultureInfo.InvariantCulture) + " m/s";
            if (!escaped || _respawns != respawns || top < 12f) Debug.LogError("JAMKIT_DREAM_SMOKE_FAIL the final run: " + report + Newline + TraceTail(16));
            else Debug.Log("JAMKIT_DREAM_SMOKE final run ok: " + report);
        }
    }
}
