using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The Movement Lab: a grey-box feel-test course for the Titanfall-style movement (docs/MOVEMENT.md). The course is
    /// core <see cref="GauntletCourse"/>: the old course (free roam) and the Gauntlet after it, whose timed run loop is
    /// <see cref="GauntletRun"/>; the lab opens on the Gauntlet's start pad.
    /// The course is core <see cref="LabCourse"/> data (built into the scene by MovementLabBuilder); this
    /// runs it: checkpoints (falling below the course or being caught respawns you at the last one in a
    /// quick fade, moving at sprint), the chase (a curtain of dream-static on the route that rubber-bands
    /// to keep a gap, core <see cref="Pursuer"/>), the two collapses triggered by your position, the
    /// run timer and best time, the debug HUD (F3), presenter chapters, attract mode and the smoke run's
    /// drive through the momentum chain. Reaching the finish shows your time and takes you back to the start.
    /// </summary>
    public sealed class MovementLabGame : MonoBehaviour
    {
        public MovementController player;
        public LabPursuerView pursuerView;
        public Transform courseRoot;

        static MovementLabGame _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static MovementLabGame Current => _current;

        LabCourse _course;
        GauntletCourse _gauntlet;
        DreamToolCourse _dream;
        readonly List<LabPieceView> _pieces = new List<LabPieceView>();
        bool[] _collapsed;
        Pursuer _pursuer;
        CourseAutopilot _pilot;
        LabHud _hud;
        int _checkpoint;
        bool _chasing;
        bool _respawning;
        bool _finishing;
        float _runTime, _best;
        bool _timing;
        float _s;
        int _respawns, _caught, _finishes;
        float _topSpeed;
        System.Func<IEnumerator> _smokeDriver;
        System.Func<bool> _skip;

        public LabCourse Course => _course;
        /// <summary>The Gauntlet (its course is <see cref="Course"/>: the old course, kept as free roam, then the Gauntlet).</summary>
        public GauntletCourse Gauntlet => _gauntlet;
        /// <summary>The dream tool chapter after the Gauntlet (its course is <see cref="Course"/> too; its run loop is <see cref="DreamToolRun"/>).</summary>
        public DreamToolCourse DreamCourse => _dream;
        /// <summary>Called whenever the player is put at a checkpoint (start, respawn, chapter jump, restart): (index, running).</summary>
        public System.Action<int, bool> OnPlaced;
        /// <summary>The same moment as <see cref="OnPlaced"/>, for any number of listeners (the dream tool's run loop).</summary>
        public event System.Action<int, bool> Placed;
        public float S => _s;
        public int Checkpoint => _checkpoint;
        public bool Chasing => _chasing;
        public float Gap => _chasing ? _pursuer.Gap(_s) : 0f;
        public int Respawns => _respawns;
        public int Caught => _caught;
        public int Finishes => _finishes;
        public float LastTime => _lastTime;
        public float TopSpeed => _topSpeed;
        public LabHud Hud => _hud;
        public bool AutopilotOn => player != null && player.Autopilot != null;
        /// <summary>True from the moment a fall or a catch is noticed until the player is back and the fade is gone.</summary>
        public bool Respawning => _respawning;
        /// <summary>The player's speed the moment the last respawn put them back (sprint, except at the start).</summary>
        public float LastRespawnSpeed => _lastRespawnSpeed;
        float _lastRespawnSpeed;
        /// <summary>The last moments of the run, for diagnosing a fall: every change of movement state and every
        /// respawn, with the section, position, state and speed (a line per change, not per frame).</summary>
        public System.Collections.Generic.IReadOnlyList<string> Trace => _trace;
        public string TraceText => string.Join(Newline, _trace);
        const string Newline = "\n";
        readonly List<string> _trace = new List<string>(64);
        MoveMode _lastMode = MoveMode.Air;
        float _lastTime;
        int _catchMissesSeen;
        bool _peekWas, _peekTaken;
        float _catchLogged = -10f;

        void Start()
        {
            if (!GameRoot.Exists || player == null)
            {
                Debug.LogError("MovementLabGame: missing the player. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            _dream = DreamToolCourse.Build(); // the old course, the Gauntlet, then the dream tool chapter
            _gauntlet = _dream.gauntlet;
            _course = _gauntlet.course;
            _pursuer = new Pursuer
            {
                targetGap = _course.pursuer.targetGap, k = _course.pursuer.k,
                minSpeed = _course.pursuer.minSpeed, maxSpeed = _course.pursuer.maxSpeed,
            };
            _pilot = new CourseAutopilot(_course);
            _collapsed = new bool[_course.collapses.Count];
            for (int i = 0; i < _course.pieces.Count; i++) _pieces.Add(null);
            var views = courseRoot != null ? courseRoot.GetComponentsInChildren<LabPieceView>(true) : FindObjectsByType<LabPieceView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var v in views)
                if (v.index >= 0 && v.index < _pieces.Count) _pieces[v.index] = v;

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new LabHud(GameRoot.Hud.Root);
            MovementComfortScreen.Ensure();
            // The lab keeps LB on Sprint and RB on Jump (its LB + d-pad switches, the pad tests): no peeking while it runs.
            _peekWas = GameRoot.Input.Bindings.PeekOnBumpers;
            _peekTaken = true;
            GameRoot.Input.Bindings.SetPeekOnBumpers(false);

            MovementLabPresenter.Register(PresenterMode.Chapters, this, this);
            PresenterMode.RegisterDriver(this, MovementLabPresenter.Scene, AttractDrive);
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            _skip = SkipToNextCheckpoint;
            DebugMenu.SkipHandler = _skip;
            GameRoot.Run.Set("Lab", "Movement Lab");

            PlaceAt(_gauntlet.startCheckpoint, false, false);
            GameRoot.Hud.Message("The Gauntlet: cross the gate to start the clock. Shift (L3) sprints, Space jumps twice, Ctrl or C slides; R (View) restarts.", 5f);
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
            if (_peekTaken) GameRoot.Input.Bindings.SetPeekOnBumpers(_peekWas);
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
            // The wall-catch diagnostic: in a development build, say why a wall nearby wasn't caught (at most once a second).
            var sim = player.Sim;
            if (sim != null && sim.CatchMisses != _catchMissesSeen)
            {
                _catchMissesSeen = sim.CatchMisses;
                if (Debug.isDebugBuild && Time.unscaledTime - _catchLogged > 1f && sim.LastCatchMiss != WallCatchMiss.None)
                {
                    _catchLogged = Time.unscaledTime;
                    AddTrace("wall not caught: " + LabHud.CatchWords(sim.LastCatchMiss));
                    Debug.Log("JAMKIT_WALLCATCH not caught: " + LabHud.CatchWords(sim.LastCatchMiss) + Newline + TraceTail(1));
                }
            }
            bool toggle = (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame); // View restarts the Gauntlet (GauntletRun)
            if (toggle && GameRoot.Input.GameplayAllowed) _hud.Toggle();
            _hud.Tick(player, _course.SectionAt(_s), _runTime, _best, _chasing, Gap, _chasing ? _pursuer.Threat(_s) : 0f, dt);
        }

        void Tick(float dt, Vector3 p)
        {
            _topSpeed = Mathf.Max(_topSpeed, player.Speed);
            // The old course's own timer (free roam); the Gauntlet has its clock (GauntletRun).
            if (!_timing && _s > _course.checkpoints[0].s + 2f && _s < _course.finishS && !_finishing) { _timing = true; _runTime = 0f; }
            if (_timing && _s > _course.finishS + 30f) _timing = false;
            if (_timing) _runTime += dt;

            for (int i = _checkpoint + 1; i < _course.checkpoints.Count; i++)
            {
                if (_s < _course.checkpoints[i].s) break;
                _checkpoint = i;
                if (_gauntlet.InGauntlet(_course.checkpoints[i].s) || DreamToolCourse.InChapter(_course.checkpoints[i].s)) continue; // the Gauntlet and the dream tool show splits instead
                GameRoot.Audio.Play("mv_checkpoint", 0.6f);
                GameRoot.Hud.Message("Checkpoint: " + _course.checkpoints[i].name, 1.5f);
            }

            for (int i = 0; i < _collapsed.Length; i++)
            {
                if (_collapsed[i] || _s < _course.collapses[i].triggerS) continue;
                Collapse(i, true);
            }

            if (!_chasing && _s >= _course.chaseStartS && _s < _course.chaseEndS)
            {
                _chasing = true;
                _pursuer.Reset(_s, player.Speed);
                pursuerView?.Show(true);
                GameRoot.Hud.Message("Something is coming. Run.", 2.5f);
            }
            if (_chasing)
            {
                var dir = _course.route.DirectionAt(_s);
                float along = Vector3.Dot(player.Velocity, new Vector3(dir.X, dir.Y, dir.Z));
                bool caught = _pursuer.Step(_s, along, dt);
                pursuerView?.Place(_pursuer.S, p.y + 2f, _pursuer.Threat(_s));
                if (_s >= _course.chaseEndS) { _chasing = false; pursuerView?.Show(false); }
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
                StartCoroutine(Respawn("A dream drop. Back to the checkpoint."));
                return;
            }

            if (!_finishing && _s >= _course.finishS && _s < _course.finishS + 30f && player.Sim.Grounded)
                StartCoroutine(Finish());
        }

        void Collapse(int i, bool fx)
        {
            _collapsed[i] = true;
            for (int k = 0; k < _pieces.Count; k++)
                if (_pieces[k] != null && _pieces[k].collapse == i) _pieces[k].Collapse();
            if (!fx) return;
            GameRoot.Audio.Play("mv_collapse", 0.9f);
            GameRoot.Juice.Shake(0.25f);
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

        IEnumerator Respawn(string message)
        {
            _respawning = true;
            _respawns++;
            AddTrace("RESPAWN: " + message);
            Debug.Log("JAMKIT_MOVEMENT respawn " + _respawns + ": " + message + Newline + TraceTail(8));
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime) { GameRoot.Ui.SetFade(t / 0.18f); yield return null; }
            GameRoot.Ui.SetFade(1f);
            PlaceAt(_checkpoint, true, _checkpoint != 0);
            _lastRespawnSpeed = player.Speed;
            GameRoot.Hud.Message(message, 1.6f);
            yield return null;
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime) { GameRoot.Ui.SetFade(1f - t / 0.22f); yield return null; }
            GameRoot.Ui.SetFade(0f);
            _respawning = false;
        }

        IEnumerator Finish()
        {
            _finishing = true;
            _finishes++;
            _lastTime = _runTime;
            _timing = false;
            bool record = _best <= 0f || _runTime < _best;
            if (record) _best = _runTime;
            GameRoot.Run.Set("Best course time", _best.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            GameRoot.Audio.Play("level_done", 0.7f);
            GameRoot.Hud.Message("Course " + _runTime.ToString("0.0", CultureInfo.InvariantCulture) + " s" + (record ? " (best!)" : "")
                + "   top speed " + _topSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " m/s. Back to the start...", 3.5f);
            float until = Time.unscaledTime + 3.5f;
            while (Time.unscaledTime < until && _finishing) yield return null;
            if (!_finishing) yield break;
            PlaceAt(0, false, false);
        }

        /// <summary>Puts the player at a checkpoint (moving at sprint, unless it's the start), puts back the
        /// collapses that come after it, and sets the chase to match.</summary>
        void PlaceAt(int index, bool keepTimer, bool running)
        {
            index = Mathf.Clamp(index, 0, _course.checkpoints.Count - 1);
            var cp = _course.checkpoints[index];
            _checkpoint = index;
            _finishing = false;
            Vector3 pos = new Vector3(cp.position.X, cp.position.Y, cp.position.Z);
            Vector3 fwd = Quaternion.Euler(0f, cp.yaw, 0f) * Vector3.forward;
            player.Teleport(pos, cp.yaw, running ? fwd * player.Def.sprintSpeed : Vector3.zero);
            for (int i = 0; i < _collapsed.Length; i++)
            {
                if (_course.collapses[i].triggerS <= cp.s + 0.01f) { if (!_collapsed[i]) Collapse(i, false); continue; }
                _collapsed[i] = false;
                for (int k = 0; k < _pieces.Count; k++)
                    if (_pieces[k] != null && _pieces[k].collapse == i) _pieces[k].Restore();
            }
            _s = cp.s;
            _chasing = cp.s >= _course.chaseStartS && cp.s < _course.chaseEndS;
            if (_chasing) _pursuer.Reset(cp.s, player.Def.sprintSpeed);
            pursuerView?.Show(_chasing);
            if (_chasing) pursuerView?.Place(_pursuer.S, pos.y + 2f, 0f);
            _pilot.Reset(cp.s);
            if (index == 0 && !keepTimer) { _timing = false; _runTime = 0f; _topSpeed = 0f; }
            OnPlaced?.Invoke(index, running);
            Placed?.Invoke(index, running);
        }

        string TraceTail(int n)
        {
            int from = Mathf.Max(0, _trace.Count - n);
            return string.Join(Newline, _trace.GetRange(from, _trace.Count - from));
        }

        // ---------- Presenter, smoke, autopilot ----------

        /// <summary>Presenter chapter: start at the checkpoint that carries this chapter.</summary>
        public void JumpToChapter(string chapter)
        {
            int i = _course.CheckpointIndex(chapter);
            if (i < 0) return;
            StopAllCoroutines();
            _respawning = false;
            GameRoot.Ui.SetFade(0f);
            PlaceAt(i, false, i != 0 && i != _gauntlet.startCheckpoint && i != _dream.startCheckpoint);
        }

        /// <summary>Puts the player at a checkpoint at once (the Gauntlet's restart): clears any fade or respawn in progress.</summary>
        public void PlaceAtCheckpoint(int index, bool running)
        {
            StopAllCoroutines();
            _respawning = false;
            GameRoot.Ui.SetFade(0f);
            PlaceAt(index, false, running);
        }

        bool SkipToNextCheckpoint()
        {
            int next = _checkpoint + 1 < _course.checkpoints.Count ? _checkpoint + 1 : 0;
            StopAllCoroutines();
            _respawning = false;
            GameRoot.Ui.SetFade(0f);
            PlaceAt(next, true, next != 0 && next != _gauntlet.startCheckpoint && next != _dream.startCheckpoint);
            return true;
        }

        /// <summary>The course autopilot drives the player (tests, the smoke run, attract mode); null hands control back.</summary>
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

        /// <summary>The smoke run: the momentum chain (slide, slide jump, wallrun, wall kick, double jump)
        /// driven by the autopilot. It must land on the far side without a respawn and reach a speed well
        /// over sprint; otherwise it logs an error, which fails the smoke run.</summary>
        IEnumerator SmokeDrive()
        {
            JumpToChapter(LabCourse.ChapterChain);
            int respawns = _respawns;
            float top = 0f;
            SetAutopilot(true);
            float end = Time.realtimeSinceStartup + 9f;
            bool landed = false;
            while (Time.realtimeSinceStartup < end && GameRoot.Flow.InRun)
            {
                top = Mathf.Max(top, player.Speed);
                if (_s >= 236f && player.Sim.Grounded) { landed = true; break; }
                yield return null;
            }
            SetAutopilot(false);
            string report = "landed " + landed + ", respawns " + (_respawns - respawns) + ", top speed " + top.ToString("0.0", CultureInfo.InvariantCulture) + " m/s";
            if (!landed || _respawns != respawns || top < 12f) Debug.LogError("JAMKIT_MOVEMENT_SMOKE_FAIL the momentum chain: " + report + Newline + TraceTail(16));
            else Debug.Log("JAMKIT_MOVEMENT_SMOKE chain ok: " + report);
        }
    }
}
