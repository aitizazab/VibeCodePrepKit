using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.InputSystem;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Relay (docs/SKYLINE-RELAY.md): a timed chain across six stations in M1's hall while waves of bots shoot at the Unit.
    /// <see cref="SkylineRelayMode"/> adds this to the arena scene when the mode is on, and <see cref="SkylineArenaDirector"/> stays
    /// off. The rules are pure (<see cref="RelayRun"/>, <see cref="RelayWaves"/>, <see cref="RelayArena"/>); this class is the
    /// Unity side: it builds the lane walls, the climbing blocks and the six props, reads the interact press (E, or the pad's B), moves the
    /// cable's costs onto the player, draws the tether, spawns the waves, and handles the wipe through the player's own wreck flow.
    /// <para>Logs (for the automated pass): <c>SKYLINE_RELAY start order=&lt;letters&gt; seconds=&lt;n&gt;</c>,
    /// <c>SKYLINE_RELAY step &lt;i&gt; &lt;kind&gt; done at &lt;t&gt;</c>, <c>SKYLINE_RELAY won|wiped at &lt;t&gt;</c> (t: run seconds), and
    /// <c>SKYLINE_RELAY wave &lt;n&gt; ...</c>.</para>
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class SkylineRelayDirector : MonoBehaviour
    {
        /// <summary>The seconds the won screen holds before a new run starts.</summary>
        public const float WinHold = 4.5f;
        const float CardPopSeconds = 0.5f;

        SkylinePlayer _player;
        SkylineHud _hud;
        ArenaNav _nav;
        RelayStationMap _map;
        RelayRun _run;
        RelayWaves _waves;
        Rng _spawnRng;
        Transform _stage, _botsRoot, _propsRoot;
        SkylineRelayTether _tether;
        readonly SkylineRelayHud _ui = new SkylineRelayHud();
        readonly SkylineRelayProp[] _props = new SkylineRelayProp[RelayChain.Roles];   // by role: button, reel, socket, reader, door, lift
        readonly Vector3[] _labelPoints = new Vector3[RelayArena.StationCount];
        readonly List<SkylineBot> _bots = new List<SkylineBot>();
        readonly List<string[]> _due = new List<string[]>();
        Transform _card;
        float _cardAge;
        bool _ready, _timerTicking;
        float _hold, _restartAt = -1f;
        int _waveNumber, _seed;

        public RelayRun Run => _run;

        // ------------------------------------------------------------------ start

        void Start()
        {
            _player = FindAnyObjectByType<SkylinePlayer>();
            if (_player == null)
            {
                Debug.LogWarning("SKYLINE_RELAY no player; the Relay is off");
                enabled = false;
                return;
            }
            _player.Wrecked += OnPlayerWrecked;
        }

        void OnDestroy()
        {
            if (_player != null)
            {
                _player.Wrecked -= OnPlayerWrecked;
                _player.MoveScale = 1f;
                _player.SprintBlocked = false;
            }
            if (_run != null)
            {
                _run.StepDone -= OnStepDone;
                _run.Ended -= OnEnded;
                _run.KeycardTaken -= OnKeycardTaken;
                _run.DoorPassedEvent -= OnDoorPassed;
            }
            _ui.Destroy();
        }

        /// <summary>Waits for the HUD and the start cover, then builds the stage and starts the first run.</summary>
        void TryBegin()
        {
            _hud = SkylineHud.Instance;
            if (_hud == null || _hud.Safe == null || !SkylineStartCover.Done || SkylinePrewarm.Busy) return;
            if (!_ui.Build(_hud)) return;

            _stage = SkylineRelayStage.Build();
            _botsRoot = new GameObject("Relay Bots").transform;
            _propsRoot = new GameObject("Relay Stations").transform;
            _tether = new SkylineRelayTether(transform);
            _nav = ArenaNav.Build(RelayArena.Solids(), 2f, ArenaNav.HopHeight);
            _map = RelayStationMap.Build(_nav);
            for (int i = 0; i < _labelPoints.Length; i++)
                _labelPoints[i] = U(RelayArena.Stations[i]) + Vector3.up * SkylineRelayProp.LabelHeight;

            string seedArg = SkylineRunArgs.Value(SkylineRelayMode.SeedArg);
            _seed = int.TryParse(seedArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? s : Environment.TickCount;
            _spawnRng = new Rng((ulong)(uint)_seed ^ 0x5EEDUL);
            _run = new RelayRun(_map, _seed);
            _run.StepDone += OnStepDone;
            _run.Ended += OnEnded;
            _run.KeycardTaken += OnKeycardTaken;
            _run.DoorPassedEvent += OnDoorPassed;
            _waves = new RelayWaves();
            _ready = true;
            BeginRun(true);
        }

        /// <summary>Builds the props for the current order, resets the cable, the card, the bots and the waves, puts the Unit at the
        /// start with a fresh chassis (<paramref name="resetPlayer"/>: not when the wreck flow just did it) and logs the start.</summary>
        void BeginRun(bool resetPlayer)
        {
            ClearBots();
            ClearProps();
            for (int role = 0; role < RelayChain.Roles; role++)
            {
                int st = _run.Order[role];
                _props[role] = SkylineRelayProp.Create((SkylineRelayProp.Role)role, st, U(RelayArena.Stations[st]), Vector3.zero, _propsRoot);
            }
            RefreshObjective();
            _tether.Hide();
            _hold = 0f;
            _restartAt = -1f;
            _waveNumber = 0;
            _waves.Reset();
            _waves.Start();
            _player.MoveScale = 1f;
            _player.SprintBlocked = false;
            if (resetPlayer) _player.Restart(U(RelayArena.PlayerStart), 0f);
            _ui.SetCard(false);
            _ui.SetHold(-1f);
            _ui.SetTimer(_run.Remaining, false);
            Debug.Log("SKYLINE_RELAY start order=" + _run.Letters + " seconds=" + _run.Seconds.ToString("0.##", CultureInfo.InvariantCulture));
        }

        void ClearProps()
        {
            for (int i = 0; i < _props.Length; i++)
            {
                if (_props[i] != null) _props[i].Destroy();
                _props[i] = null;
            }
            if (_card != null) Destroy(_card.gameObject);
            _card = null;
        }

        void ClearBots()
        {
            _bots.Clear();
            if (_botsRoot != null) Destroy(_botsRoot.gameObject);
            _botsRoot = new GameObject("Relay Bots").transform;
            SkylineBolt.ClearAll();
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (!_ready)
            {
                TryBegin();
                return;
            }
            bool paused = SkylineDeviceInput.Paused || SkylinePauseMenu.IsOpen;
            float dt = paused ? 0f : Time.deltaTime;
            foreach (var p in _props) if (p != null) p.Tick(dt);
            _ui.Tick(Time.unscaledDeltaTime);

            if (_restartAt >= 0f && Time.time >= _restartAt)
            {
                _run.RestartNew();
                BeginRun(true);
            }
            if (_run.Running && !paused)
            {
                Vector3 feet = _player.transform.position;
                _timerTicking = true;
                _run.Tick(dt);
                _timerTicking = false;
                if (_run.Outcome == RelayOutcome.Wiped)
                {
                    WipeByTimer();   // the clock ran out: the Unit is wrecked, and its wreck flow starts the next run
                    return;
                }
                if (_run.Running)
                {
                    int at = StationAt(feet);
                    _run.PlayerAt(at);
                    Interact(at, dt);
                    TickWaves(dt);
                }
            }
            if (_run.Running || _run.Outcome == RelayOutcome.Won) ApplyCableCosts();
            TickCard(dt);
            DrawTether();
            _ui.SetTimer(_run.Remaining, _run.Outcome == RelayOutcome.Won);
            _ui.UpdateLetters(_player.Camera, _labelPoints, _run.Running ? _run.TargetStation : -1);
        }

        // ------------------------------------------------------------------ the presses

        static bool InteractHeld()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.eKey.isPressed) || (pad != null && pad.buttonEast.isPressed);
        }

        static bool InteractPressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }

        /// <summary>The station whose zone holds the feet (the nearest), or -1.</summary>
        int StationAt(Vector3 feet)
        {
            int best = -1;
            float bestD = float.MaxValue;
            foreach (var p in _props)
            {
                if (p == null) continue;
                Vector3 c = U(RelayArena.Stations[p.station]);
                float dx = feet.x - c.x, dz = feet.z - c.z, d = dx * dx + dz * dz;
                if (d > p.ZoneRadius * p.ZoneRadius || Mathf.Abs(feet.y - c.y) > 2.5f || d >= bestD) continue;
                best = p.station;
                bestD = d;
            }
            return best;
        }

        void Interact(int at, float dt)
        {
            if (_run.Step == 0)
            {
                // the button is held for half a second
                if (InteractHeld() && at >= 0 && at == _run.ButtonStation)
                {
                    _hold += dt;
                    _ui.SetHold(_hold / RelayRun.ButtonHold);
                    _run.Interact(at, _hold);
                }
                else
                {
                    _hold = 0f;
                    _ui.SetHold(-1f);
                }
                return;
            }
            _ui.SetHold(-1f);
            if (at >= 0 && InteractPressed()) _run.Interact(at, 0f);
        }

        // ------------------------------------------------------------------ the run's events

        void OnStepDone(int index, RelayStepKind kind, float t)
        {
            Debug.Log("SKYLINE_RELAY step " + (index + 1) + " " + kind.ToString().ToLowerInvariant() + " done at " + t.ToString("0.0", CultureInfo.InvariantCulture));
            _ui.Pulse();
            switch (kind)
            {
                case RelayStepKind.Button:
                    _props[0].Complete();
                    break;
                case RelayStepKind.Cable:
                    _props[1].Complete();
                    break;
                case RelayStepKind.Plug:
                    _props[2].Complete();
                    SpawnCard();
                    break;
                case RelayStepKind.Swipe:
                    _props[3].Complete();
                    _props[4].Open();
                    _props[5].Open();
                    _ui.SetCard(false);
                    SkylineSounds.PlayAt("breacher/dry_clunk", _props[4].root.position + Vector3.up * 1.5f, 1f);
                    SkylineSounds.PlayAt("breacher/equip_lock", _props[4].root.position + Vector3.up * 1.5f, 1f);
                    break;
                case RelayStepKind.Escape:
                    _props[5].Complete();
                    break;
            }
            if (kind != RelayStepKind.Swipe) SkylineSounds.PlayViewmodel(kind == RelayStepKind.Escape ? "concierge/sting" : "concierge/chime", 0.8f);
            RefreshObjective();
        }

        void OnKeycardTaken(float t)
        {
            if (_card != null) Destroy(_card.gameObject);
            _card = null;
            _ui.SetCard(true);
            _ui.Pulse();
            SkylineSounds.PlayViewmodel("concierge/chime", 0.6f);
        }

        void OnDoorPassed(float t)
        {
            RefreshObjective();
            _ui.Pulse();
        }

        void OnEnded(RelayOutcome outcome, float t)
        {
            string at = t.ToString("0.0", CultureInfo.InvariantCulture);
            _tether.Hide();
            _waves.Stop();
            _ui.SetHold(-1f);
            RefreshObjective();
            if (outcome == RelayOutcome.Won)
            {
                Debug.Log("SKYLINE_RELAY won at " + at);
                _ui.Pulse();
                _restartAt = Time.time + WinHold;
                return;
            }
            Debug.Log("SKYLINE_RELAY wiped at " + at + (_timerTicking ? " cause=timer" : " cause=wrecked"));
        }

        /// <summary>The timer ran out: the floor is wiped. The Unit is wrecked through its own flow (<see cref="OnPlayerWrecked"/>
        /// then restarts the run).</summary>
        void WipeByTimer()
        {
            Vector3 from = _player.transform.position + _player.transform.forward * 3f + Vector3.up * 2f;
            _player.TakeAttack(1000000f, "relay-wipe", from);
        }

        void OnPlayerWrecked()
        {
            if (!_ready) return;
            if (_run.Running)
            {
                _run.Wipe();
            }
            _run.RestartNew();
            BeginRun(true);
        }


        // ------------------------------------------------------------------ the cable, the card, the objective

        void RefreshObjective()
        {
            int target = _run.Running ? _run.TargetStation : -1;
            for (int r = 0; r < _props.Length; r++)
                if (_props[r] != null) _props[r].SetObjective(_props[r].station == target);
        }

        Vector3 HandPoint()
        {
            var c = _player.Camera.transform;
            return c.position + c.right * 0.3f - c.up * 0.3f + c.forward * 0.5f;
        }

        void ApplyCableCosts()
        {
            _player.MoveScale = _run.MoveScale;
            _player.SprintBlocked = !_run.SprintAllowed;
            if (!_run.Carrying) return;
            var mv = _player.Movement;
            if (mv == null || mv.Sim == null) return;
            Vector3 feet = _player.transform.position, anchor = U(_run.TetherAnchor);
            Vector3 d = feet - anchor;
            float dist = d.magnitude, len = _run.TetherLength;
            if (dist < len - 0.3f || dist < 1e-3f) return;
            Vector3 n = d / dist, nf = new Vector3(n.x, 0f, n.z);
            nf = nf.sqrMagnitude > 1e-6f ? nf.normalized : Vector3.zero;
            Vector3 v = mv.Velocity;
            float outward = Vector3.Dot(new Vector3(v.x, 0f, v.z), nf);
            if (dist > len + 0.12f)
            {
                // pulled taut and past it: back onto the cable's length, the outward part of the speed gone
                Vector3 c = anchor + n * len;
                if (outward > 0f) v -= nf * outward;
                mv.Teleport(c, _player.Yaw, v, _player.Pitch);
            }
            else if (outward > 0f)
            {
                mv.Sim.AddPush(new NVec3(-nf.x * outward, 0f, -nf.z * outward));
            }
        }

        void DrawTether()
        {
            if (_tether == null) return;
            if (_run.Carrying && _props[1] != null)
                _tether.Show(_props[1].CableAnchor, HandPoint(), _run.TetherLength);
            else if (_run.Plugged && _props[1] != null && _props[2] != null)
            {
                Vector3 a = _props[1].CableAnchor, b = _props[2].PlugPoint;
                _tether.Show(a, b, Vector3.Distance(a, b) * 1.08f);
            }
            else _tether.Hide();
        }

        void SpawnCard()
        {
            var mat = SkylineInkRoles.Flat(SkylineRelayProp.Amber, SkylineInkRoles.None) ?? SkylineBotLook.Lit(SkylineRelayProp.Amber);
            var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Relay Keycard", _propsRoot, mat);
            go.transform.localScale = new Vector3(0.42f, 0.03f, 0.27f);
            var stripe = SkylineBotLook.Visual(PrimitiveType.Cube, "Stripe", go.transform, SkylineInkRoles.Flat(SkylineRelayProp.Ink, SkylineInkRoles.None) ?? SkylineBotLook.Lit(SkylineRelayProp.Ink));
            stripe.transform.localPosition = new Vector3(0f, 0.7f, -0.2f);
            stripe.transform.localScale = new Vector3(0.8f, 0.5f, 0.2f);
            go.transform.position = _props[2].CardSlot;
            _card = go.transform;
            _cardAge = 0f;
        }

        void TickCard(float dt)
        {
            if (_card == null || _props[2] == null) return;
            _cardAge += dt;
            float k = Mathf.Clamp01(_cardAge / CardPopSeconds);
            float e = 1f - (1f - k) * (1f - k);
            Vector3 pos = Vector3.Lerp(_props[2].CardSlot, _props[2].CardHover, e);
            pos.y += Mathf.Sin(_cardAge * 3f) * 0.05f * k;
            _card.position = pos;
            _card.rotation = Quaternion.Euler(0f, _cardAge * 120f, 0f) * _props[2].root.rotation;
        }

        // ------------------------------------------------------------------ the waves

        int AliveBots()
        {
            int n = 0;
            for (int i = _bots.Count - 1; i >= 0; i--)
            {
                var b = _bots[i];
                if (b == null) { _bots.RemoveAt(i); continue; }
                if (b.Alive) n++;
            }
            return n;
        }

        void TickWaves(float dt)
        {
            _due.Clear();
            _waves.Tick(dt, AliveBots(), _due);
            foreach (var w in _due) SpawnWave(w);
        }

        void SpawnWave(string[] who)
        {
            _waveNumber++;
            int drones = 0, floors = 0;
            foreach (var id in who) if (id == SkylineEnemies.FloorCareId) floors++; else drones++;
            int ti = _run.TargetStation;
            NVec3 target = ti >= 0 ? RelayArena.Stations[ti] : NVec3.Zero;
            Vector3 pp = _player.transform.position;
            var player = new NVec3(pp.x, pp.y, pp.z);
            var air = RelayWaves.PickSpawns(RelayArena.AirSpawns, target, player, drones, _spawnRng);
            var floor = RelayWaves.PickSpawns(RelayArena.FloorSpawns, target, player, floors, _spawnRng);
            int made = 0;
            foreach (var p in air) if (SpawnBot(SkylineEnemies.DroneId, p)) made++;
            foreach (var p in floor) if (SpawnBot(SkylineEnemies.FloorCareId, p)) made++;
            Debug.Log("SKYLINE_RELAY wave " + _waveNumber + " bots=" + made + "/" + who.Length + " alive=" + AliveBots() + " at " + _run.Elapsed.ToString("0.0", CultureInfo.InvariantCulture));
        }

        bool SpawnBot(string id, NVec3 at)
        {
            Vector3 pos = U(at), pp = _player.transform.position;
            float yaw = Mathf.Atan2(pp.x - pos.x, pp.z - pos.z) * Mathf.Rad2Deg;
            ulong seed = (ulong)(_waveNumber * 1000 + _bots.Count + 1);
            var bot = SkylineBot.Spawn(id, pos, yaw, seed, _player, _player.Writ, _botsRoot);
            if (bot == null) return false;
            SkylineBot.Wake(-1, bot);
            _bots.Add(bot);
            return true;
        }

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
