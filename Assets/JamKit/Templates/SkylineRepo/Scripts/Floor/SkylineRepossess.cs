using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The repossession on the floor (task R2; docs/SKYLINE-FLOOR-REPOSSESS.md section 2): wraps the pure <see cref="Repossession"/>
    /// and <see cref="TowWatch"/> and plays what they decide. It builds the three assets at the sockets <see cref="Repossession"/>
    /// picked (placeholder meshes in the objective ink role), a lit <see cref="SkylineTowCircle"/> under each (the builder's grey-box
    /// discs on the sockets are hidden), Dot's tow drone with its violet beam, the alarm (CONCIERGE's sting and a spinning amber
    /// beacon on the nearest wall, once per asset when its meter first starts), the winch that hoists a towed asset away in 1.2 s,
    /// the west service lift's shutter and the exit run. The director (task X0) owns the phases: it calls <see cref="Begin"/> at the
    /// drop, reacts to the events below and calls <see cref="ResetRun"/> on a wreck.
    /// <para><b>Events for the director</b> (all raised from <see cref="Step"/>, in this order within one tick: MeterStarted and
    /// AlarmSounded, Towed, AllTowed, LeftCircleMidTow, ExitReached): <see cref="MeterStarted"/> is <c>BeatTrigger.MeterStarted</c>;
    /// <see cref="AlarmSounded"/> says where to centre the next wave (<see cref="ConsumeAlarmAsset"/> is the same position, once);
    /// <see cref="Towed"/> is <c>BeatTrigger.AssetTowed</c> (<see cref="TowedCount"/> == 1 is the first tow, when Dot's "Clamped!
    /// Get Moving!" ends); <see cref="LeftCircleMidTow"/> is <c>BeatTrigger.LeftCircleMidTow</c> (the beat plays once);
    /// <see cref="AllTowed"/> ends the waves (the director calls <c>FloorFlow.AllTowed()</c>); <see cref="ExitReached"/> is the end
    /// of the floor (<c>FloorFlow.ReachedExit()</c>).</para>
    /// <para>The feet check is the pure one: 3D, horizontal radius 3 m and a vertical reach of
    /// <see cref="Repossession.CircleHalfHeight"/> (a player on the bridge does not fill the pit kiosk's circle).</para>
    /// Logs (all prefixed SKYLINE_FLOOR): <c>socket &lt;asset&gt;=&lt;socket id&gt;</c> at setup, <c>meter-start &lt;asset&gt;</c>,
    /// <c>towed &lt;asset&gt; at &lt;s&gt;</c>, <c>left-circle &lt;asset&gt;</c>, <c>all-towed</c>, <c>exit reached</c>, <c>restart</c>.
    /// </summary>
    public sealed class SkylineRepossess : MonoBehaviour
    {
        [Tooltip("The Unit's root (its position is its feet). The director sets it; Step can also be driven with a position.")]
        public Transform feet;
        [Tooltip("The seed that picks one socket per group and shuffles the assets over the groups (same seed, same sockets).")]
        public int seed = 1;
        [Tooltip("Build the assets, circles, drone, beacon and lift marks. Off for logic-only tests.")]
        public bool buildVisuals = true;
        [Tooltip("Call Setup from Start with this object's parent as the arena root (the director can instead call Setup itself).")]
        public bool setupOnStart = true;

        Repossession _objective;
        TowWatch _watch;
        SkylineTowCircle[] _circles = new SkylineTowCircle[0];
        SkylineRepossessAsset[] _assets = new SkylineRepossessAsset[0];
        SkylineRepossessDrone[] _drones = new SkylineRepossessDrone[0];
        SkylineRepossessAlarm[] _alarms = new SkylineRepossessAlarm[0];
        SkylineRepossessExit _exit;
        Transform _fxRoot;
        Transform _arena;
        bool _running, _allTowed, _exitReached, _ready;
        float _floorSeconds;
        readonly bool[] _alarmRaised = new bool[8];

        // ---------------------------------------------------------------- state

        public bool IsReady => _ready;
        /// <summary>The pure objective: the sheet (asset + socket + meter per entry), the circle test, the towed count.</summary>
        public Repossession Objective => _objective;
        /// <summary>The pure event surface (also the source of <see cref="LeaveCount"/>).</summary>
        public TowWatch Watch => _watch;
        /// <summary>True between <see cref="Begin"/> and <see cref="Stop"/>: the meters tick.</summary>
        public bool Running => _running;
        /// <summary>Seconds since <see cref="Begin"/> (counted while running): the time stamped in the log.</summary>
        public float FloorSeconds => _floorSeconds;
        public int TowedCount => _objective != null ? _objective.TowedCount : 0;
        public bool AllDone => _allTowed;
        public bool ExitHasBeenReached => _exitReached;
        /// <summary>How many times the Unit left a circle mid-tow (a wreck does not clear it).</summary>
        public int LeaveCount => _watch != null ? _watch.LeaveCount : 0;
        public SkylineRepossessExit Exit => _exit;
        public IReadOnlyList<SkylineTowCircle> Circles => _circles;
        public IReadOnlyList<SkylineRepossessAsset> Assets => _assets;
        public IReadOnlyList<SkylineRepossessDrone> Drones => _drones;
        public IReadOnlyList<SkylineRepossessAlarm> Alarms => _alarms;
        /// <summary>The Unity-space position of an asset (its socket's floor point).</summary>
        public Vector3 AssetPosition(int i) => SkylineRepossessFx.U(_objective.Sheet[i].socket.position);

        // ---------------------------------------------------------------- events for the director

        /// <summary>An asset's meter first started (once per asset per run).</summary>
        public event Action<JobEntry> MeterStarted;
        /// <summary>The alarm sounded for this asset's first meter start, with the asset's world position.</summary>
        public event Action<JobEntry, Vector3> AlarmSounded;
        /// <summary>An asset's tow completed.</summary>
        public event Action<JobEntry> Towed;
        /// <summary>The Unit left an asset's circle while its tow was under way.</summary>
        public event Action<JobEntry> LeftCircleMidTow;
        /// <summary>Once: the third asset was towed.</summary>
        public event Action AllTowed;
        /// <summary>Once: after AllTowed, the Unit's feet reached the west lift.</summary>
        public event Action ExitReached;

        // ---------------------------------------------------------------- setup

        void Start()
        {
            if (setupOnStart && !_ready) Setup(transform.parent != null ? transform.parent : transform, feet, (ulong)Mathf.Max(0, seed));
        }

        /// <summary>Picks the sockets, builds the objective and (when <see cref="buildVisuals"/>) everything the player sees.
        /// <paramref name="arena"/> is the scene's arena root (the sockets' markers and the lift shutter are found under it).
        /// Safe to call once; a second call does nothing.</summary>
        public void Setup(Transform arena, Transform feetSource, ulong seedValue)
        {
            if (_ready) return;
            _arena = arena;
            if (feetSource != null) feet = feetSource;
            seed = (int)(seedValue & 0x7FFFFFFF);
            _objective = new Repossession(RepossessSites.Assets(), seedValue);
            _watch = new TowWatch(_objective);
            _watch.MeterStarted += OnMeterStarted;
            _watch.Towed += OnTowed;
            _watch.AllTowed += OnAllTowed;
            _watch.LeftCircleMidTow += OnLeft;

            var sheet = _objective.Sheet;
            for (int i = 0; i < sheet.Count; i++)
                Debug.Log("SKYLINE_FLOOR socket " + sheet[i].asset.id + "=" + sheet[i].socket.id);

            if (buildVisuals) BuildVisuals(arena);
            _ready = true;
        }

        void BuildVisuals(Transform arena)
        {
            _fxRoot = new GameObject("Repossession").transform;
            _fxRoot.SetParent(arena != null ? arena : transform, false);
            _fxRoot.localPosition = Vector3.zero;
            _fxRoot.localRotation = Quaternion.identity;

            // The builder's grey-box discs: every socket has one; the ten are hidden, the three live ones get a real circle.
            foreach (var s in RepossessSites.Sockets())
            {
                var marker = SkylineRepossessFx.FindDeep(arena, "Socket " + s.id);
                var decal = marker != null ? marker.Find("Circle Decal (grey-box)") : null;
                if (decal != null) decal.gameObject.SetActive(false);
            }

            var sheet = _objective.Sheet;
            int n = sheet.Count;
            _circles = new SkylineTowCircle[n];
            _assets = new SkylineRepossessAsset[n];
            _drones = new SkylineRepossessDrone[n];
            _alarms = new SkylineRepossessAlarm[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 floor = AssetPositionRaw(sheet[i]);
                _circles[i] = SkylineTowCircle.Create(_fxRoot, floor, "Tow circle " + sheet[i].asset.id);
                _assets[i] = SkylineRepossessAsset.Build(_fxRoot, sheet[i]);
                _drones[i] = SkylineRepossessDrone.Build(_fxRoot, floor, "Tow drone " + sheet[i].asset.id);
                _alarms[i] = SkylineRepossessAlarm.Build(_fxRoot, "Alarm beacon " + sheet[i].asset.id);
            }
            _exit = new SkylineRepossessExit(arena, _fxRoot);
        }

        static Vector3 AssetPositionRaw(JobEntry e) => SkylineRepossessFx.U(e.socket.position);

        // ---------------------------------------------------------------- running

        /// <summary>The drop: the objective is live (the meters tick) and the floor clock the log reads starts at 0.</summary>
        public void Begin()
        {
            _running = true;
            _floorSeconds = 0f;
        }

        /// <summary>The meters stop ticking (a wreck, the lobby). The visuals keep animating.</summary>
        public void Stop() => _running = false;

        /// <summary>The Unit's feet: the circle test, the meters, the events, the exit check. <c>Update</c> calls it with
        /// <see cref="feet"/> while <see cref="Running"/>; tests and the demo can call it directly with any position.</summary>
        public void Step(float dt, Vector3 feetPosition)
        {
            if (!_ready || !_running) return;
            _floorSeconds += Mathf.Max(0f, dt);
            if (!_allTowed) _watch.Tick(dt, SkylineRepossessFx.N(feetPosition));
            else if (!_exitReached && SkylineRepossessExit.InLift(feetPosition))
            {
                _exitReached = true;
                Debug.Log("SKYLINE_FLOOR exit reached");
                ExitReached?.Invoke();
            }
        }

        void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            if (_running && feet != null) Step(dt, feet.position);
            Animate(dt);
        }

        /// <summary>The visuals for this frame: rings, clamps, drones, beacons, winches, the lift. Cheap; no allocation.</summary>
        public void Animate(float dt)
        {
            if (!_ready || !buildVisuals) return;
            int active = _objective.ActiveCircle;
            var sheet = _objective.Sheet;
            for (int i = 0; i < sheet.Count; i++)
            {
                bool towed = sheet[i].Towed;
                float p = sheet[i].meter.Progress;
                bool inside = i == active;
                if (!towed) _circles[i].SetState(p, inside);
                if (!towed)
                {
                    _assets[i].SetClamp(sheet[i].meter.Started ? 1f : 0f, inside ? p : 0f);
                    _drones[i].SetBeam(inside);
                }
                _assets[i].Tick(dt);
                _drones[i].Tick(dt);
                _alarms[i].Tick(dt);
            }
            _exit?.Tick(dt);
        }

        // ---------------------------------------------------------------- the objective's events

        int IndexOf(JobEntry e)
        {
            var sheet = _objective.Sheet;
            for (int i = 0; i < sheet.Count; i++) if (ReferenceEquals(sheet[i], e)) return i;
            return -1;
        }

        void OnMeterStarted(JobEntry e)
        {
            int i = IndexOf(e);
            Debug.Log("SKYLINE_FLOOR meter-start " + e.asset.id);
            if (buildVisuals && i >= 0)
            {
                _drones[i].Arrive();
                if (!_alarmRaised[i]) _alarms[i].Raise(AssetPositionRaw(e));
            }
            if (i >= 0) _alarmRaised[i] = true;
            MeterStarted?.Invoke(e);
            AlarmSounded?.Invoke(e, AssetPositionRaw(e));
        }

        void OnTowed(JobEntry e)
        {
            int i = IndexOf(e);
            Debug.Log("SKYLINE_FLOOR towed " + e.asset.id + " at " + _floorSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            if (buildVisuals && i >= 0)
            {
                _circles[i].SetState(1f, false);
                _circles[i].Hide();
                _assets[i].SetClamp(1f, 0f);
                _assets[i].StartWinch();
                _drones[i].Leave();
                _alarms[i].Stop();
            }
            Towed?.Invoke(e);
        }

        void OnAllTowed()
        {
            _allTowed = true;
            Debug.Log("SKYLINE_FLOOR all-towed");
            _exit?.OpenUp();
            AllTowed?.Invoke();
        }

        void OnLeft(JobEntry e)
        {
            Debug.Log("SKYLINE_FLOOR left-circle " + e.asset.id);
            LeftCircleMidTow?.Invoke(e);
        }

        /// <summary>Takes the pending alarm position (the asset whose meter first started since the last call), or null: the next
        /// wave's doors are chosen round it (<c>SpawnQuery.alarmAsset</c>).</summary>
        public NVec3? ConsumeAlarmAsset() => _watch?.ConsumeAlarm();

        /// <summary>The pending alarm position without taking it.</summary>
        public NVec3? PendingAlarmAsset => _watch?.AlarmAsset;

        // ---------------------------------------------------------------- restart

        /// <summary>A wreck: meters back to 0, the SAME sockets, assets back on them whole, circles empty, drones and beacons gone,
        /// the lift shut, the floor clock stopped at 0. The alarm can sound again. Call <see cref="Begin"/> again at the next drop.</summary>
        public void ResetRun()
        {
            if (!_ready) return;
            _running = false;
            _allTowed = false;
            _exitReached = false;
            _floorSeconds = 0f;
            _watch.Reset();
            for (int i = 0; i < _alarmRaised.Length; i++) _alarmRaised[i] = false;
            if (buildVisuals)
            {
                for (int i = 0; i < _assets.Length; i++)
                {
                    _assets[i].Restore();
                    _circles[i].Show();
                    _drones[i].Reset();
                    _alarms[i].Stop();
                }
                _exit?.Close();
            }
            Debug.Log("SKYLINE_FLOOR restart");
        }
    }
}
