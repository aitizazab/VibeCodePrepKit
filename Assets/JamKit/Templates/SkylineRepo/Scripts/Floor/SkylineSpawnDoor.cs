using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// One in-world spawn door and its 2 s telegraph (task SD1; docs/SKYLINE-FLOOR-REPOSSESS.md section 3). It sits on the builder's
    /// <c>Door &lt;id&gt;</c> marker (or makes its own object at the door's position) and draws the door by kind:
    /// a <b>shop shutter</b> (a roll-up panel on the shop's front that rattles and rises 2.5 m in 2 s with a warm light spilling out),
    /// a <b>skylight vent</b> (two hatch flaps slide apart under a blinking white beacon), a <b>service hatch</b> (a roll-up panel
    /// with an amber hazard beacon) and a <b>rail end</b> (the gondola's lamp lights, the cable twangs). The sounds, light colours,
    /// distances and blink rates are data (<see cref="DoorTelegraphPlan"/>); the same <see cref="FloorRules.DoorTelegraphSeconds"/>
    /// the pure <see cref="WaveSpawner"/> counts runs here as visuals, and <see cref="TelegraphEnded"/> fires once when it is up.
    /// <para>The door is <see cref="Busy"/> from <see cref="BeginTelegraph"/> to the end; <see cref="Cancel"/> (a wreck) stops it
    /// and shuts the door at once. Lights are only on while the door moves (27 doors would blow the light budget).</para>
    /// </summary>
    public sealed class SkylineSpawnDoor : MonoBehaviour
    {
        /// <summary>After the telegraph the door holds open this long, then closes over <see cref="CloseSeconds"/>.</summary>
        public const float HoldSeconds = 0.6f, CloseSeconds = 0.9f;
        /// <summary>The slack on the 2 s (a frame's float sum can land a hair under).</summary>
        const float EndSlack = 1e-4f;

        SpawnDoor _data;
        DoorTelegraphLook _look;
        bool _built, _telegraphing, _hasBlinkSound;
        float _elapsed, _settle = -1f, _soundDelay;
        int _blinksPlayed, _fireCount;
        float _openAmount;
        bool _startSoundPending;

        Transform _panel, _flapA, _flapB, _cable;
        Renderer _beaconRenderer;
        Material _beaconBright, _beaconDim;
        Light _light;
        Vector3 _panelRest, _flapARest, _flapBRest, _cableRest;

        /// <summary>The door's data (null until <see cref="Init"/>).</summary>
        public SpawnDoor Data => _data;
        public string DoorId => _data != null ? _data.id : "";
        public DoorKind Kind => _data != null ? _data.kind : DoorKind.ShopShutter;
        public DoorTelegraphLook Look => _look;
        /// <summary>True from <see cref="BeginTelegraph"/> until the 2 s are up or <see cref="Cancel"/> (W2: a busy door is never chosen).</summary>
        public bool Busy => _telegraphing;
        /// <summary>Seconds into the running telegraph (0 when none).</summary>
        public float Elapsed => _telegraphing ? _elapsed : 0f;
        /// <summary>How many telegraphs have run to their end (a cancelled one does not count).</summary>
        public int FireCount => _fireCount;
        public Light DoorLight => _light;
        public Transform Panel => _panel;
        /// <summary>0..1: how open the door is right now (the panel risen, the flaps apart).</summary>
        public float OpenAmount => _openAmount;

        /// <summary>Raised once when the telegraph has run its 2 s: the bots of this door are due.</summary>
        public event Action<SkylineSpawnDoor> TelegraphEnded;

        // ---------------------------------------------------------------- creation

        /// <summary>Puts a door on <paramref name="marker"/> (the builder's <c>Door &lt;id&gt;</c> object), or on a new object under
        /// <paramref name="fallbackParent"/> when there is none. The grey-box lit block on the marker is hidden.</summary>
        public static SkylineSpawnDoor Attach(Transform marker, Transform fallbackParent, SpawnDoor data)
        {
            GameObject go;
            if (marker != null)
            {
                go = marker.gameObject;
                var b = marker.Find("Marker");
                if (b != null) b.gameObject.SetActive(false);
                var n = marker.Find("Marker Nose");
                if (n != null) n.gameObject.SetActive(false);
            }
            else
            {
                go = new GameObject("Door " + data.id);
                if (fallbackParent != null) go.transform.SetParent(fallbackParent, false);
                go.transform.SetPositionAndRotation(SkylineRepossessFx.U(data.position), Quaternion.Euler(0f, data.yawDeg, 0f));
            }
            var door = go.GetComponent<SkylineSpawnDoor>();
            if (door == null) door = go.AddComponent<SkylineSpawnDoor>();
            door.Init(data);
            return door;
        }

        /// <summary>Builds the door's parts for its kind. Safe to call once.</summary>
        public void Init(SpawnDoor data)
        {
            if (_built) return;
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _look = DoorTelegraphPlan.For(data.kind);
            _hasBlinkSound = !string.IsNullOrEmpty(_look.blinkSound);
            var lc = new Color(_look.lightR, _look.lightG, _look.lightB);
            switch (data.kind)
            {
                case DoorKind.ShopShutter: BuildShutter(); break;
                case DoorKind.SkylightVent: BuildVent(); break;
                case DoorKind.ServiceHatch: BuildHatch(); break;
                default: BuildRail(); break;
            }
            // one light per door, only on while it moves; placed just in front (forward = the way the bot faces on arrival)
            float lz = data.kind == DoorKind.RailEnd ? 0.3f : 0.5f;
            float ly = data.kind == DoorKind.SkylightVent ? -0.6f : (data.kind == DoorKind.RailEnd ? 0f : 1.0f);
            var lightType = data.kind == DoorKind.SkylightVent ? LightType.Spot : LightType.Point;
            var holder = new GameObject("Door light").transform;
            holder.SetParent(transform, false);
            holder.localPosition = new Vector3(0f, ly, lz);
            if (data.kind == DoorKind.SkylightVent) holder.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _light = SkylineRepossessFx.MakeLight(holder, "Light", lightType, lc, 0f, data.kind == DoorKind.ShopShutter ? 9f : 7f, 70f);
            _light.enabled = false;
            ApplyPose(0f, 0f);
            _built = true;
        }

        void BuildShutter()
        {
            // drawn on the shop's front, a hand's breadth in front of the face; the warm gap shows as the panel rises
            var steel = SkylineBotLook.Lit(new Color(0.58f, 0.62f, 0.69f));
            var dark = SkylineBotLook.Lit(SkylineBotLook.Dark);
            var warm = SkylineBotLook.Glow(new Color(_look.lightR, _look.lightG, _look.lightB));
            float w = _look.width, h = 3.0f;
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Gap glow", transform, new Vector3(0f, _look.travel * 0.5f, 0.02f), new Vector3(w - 0.4f, _look.travel, 0.02f), warm);
            _panel = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Shutter panel", transform, new Vector3(0f, h * 0.5f, 0.06f), new Vector3(w, h, 0.06f), steel).transform;
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Bottom bar", _panel, new Vector3(0f, -0.5f + 0.02f, 1f), new Vector3(1f, 0.04f, 1.6f), dark);
            _panelRest = _panel.localPosition;
        }

        void BuildVent()
        {
            var steel = SkylineBotLook.Lit(new Color(0.7f, 0.74f, 0.8f));
            float half = _look.width * 0.5f;
            _flapA = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Flap A", transform, new Vector3(-half * 0.5f, 0f, 0f), new Vector3(half, 0.06f, _look.width), steel).transform;
            _flapB = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Flap B", transform, new Vector3(half * 0.5f, 0f, 0f), new Vector3(half, 0.06f, _look.width), steel).transform;
            _flapARest = _flapA.localPosition;
            _flapBRest = _flapB.localPosition;
            MakeBeacon(new Vector3(0f, -0.2f, 0f), 0.28f);
        }

        void BuildHatch()
        {
            // rolled up and tucked under the lintel at rest; on the telegraph it clatters down part way and rolls back up
            var amber = SkylineBotLook.Lit(new Color(0.88f, 0.68f, 0.1f));
            var dark = SkylineBotLook.Lit(SkylineBotLook.Dark);
            _panel = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Hatch panel", transform, new Vector3(0f, 2.9f, 0f), new Vector3(_look.width, 0.4f, 0.08f), amber).transform;
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Hazard bar", _panel, new Vector3(0f, -0.4f, 0f), new Vector3(1f, 0.25f, 1.2f), dark);
            _panelRest = _panel.localPosition;
            MakeBeacon(new Vector3(0f, 3.2f, 0.25f), 0.26f);
        }

        void BuildRail()
        {
            var cable = SkylineBotLook.Lit(SkylineBotLook.Dark);
            _cable = SkylineRepossessFx.Piece(PrimitiveType.Cylinder, "Cable", transform, new Vector3(0f, 3.2f, 0f), new Vector3(0.05f, 3.2f, 0.05f), cable).transform;
            _cableRest = _cable.localPosition;
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Lamp housing", transform, new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.35f, 0.4f), SkylineBotLook.Lit(SkylineBotLook.Grey));
            MakeBeacon(new Vector3(0f, 0f, 0.24f), 0.3f);
        }

        void MakeBeacon(Vector3 local, float size)
        {
            var c = new Color(_look.lightR, _look.lightG, _look.lightB);
            _beaconBright = SkylineBotLook.Glow(c);
            _beaconDim = SkylineBotLook.Glow(c * 0.18f);
            var go = SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Beacon", transform, local, new Vector3(size, size, size), _beaconDim);
            _beaconRenderer = go.GetComponent<Renderer>();
        }

        // ---------------------------------------------------------------- the telegraph

        /// <summary>Starts the 2 s telegraph: the sound, the light, the motion. No-op while one is already running.
        /// <paramref name="soundDelay"/> holds the first sound back a little, so two doors that start in one frame do not drop each
        /// other's sound at the mix's retrigger gap.</summary>
        public bool BeginTelegraph(float soundDelay = 0f)
        {
            if (!_built || _telegraphing) return false;
            _telegraphing = true;
            _elapsed = 0f;
            _settle = -1f;
            _blinksPlayed = 0;
            _soundDelay = Mathf.Max(0f, soundDelay);
            _startSoundPending = true;
            _light.enabled = true;
            Debug.Log("SKYLINE_DOOR telegraph " + _data.id + " " + _data.kind);
            ApplyFrame();
            if (_soundDelay <= 0f) PlayStart();
            return true;
        }

        void PlayStart()
        {
            _startSoundPending = false;
            _blinksPlayed = _hasBlinkSound ? DoorTelegraphPlan.BlinksStarted(_look, _elapsed) : 0;   // the start sound is the first beep
            SkylineRepossessFx.Sound(_look.startSound, transform.position);
        }

        /// <summary>Stops a running telegraph and shuts the door at once (a wreck). Nothing fires.</summary>
        public void Cancel()
        {
            if (!_built) return;
            bool was = _telegraphing || _settle >= 0f;
            _telegraphing = false;
            _settle = -1f;
            _elapsed = 0f;
            _startSoundPending = false;
            _light.enabled = false;
            _light.intensity = 0f;
            ApplyPose(0f, 0f);
            if (was) Debug.Log("SKYLINE_DOOR cancel " + _data.id);
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Advances the telegraph (and the door's closing afterwards) by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (!_built || !(dt > 0f)) return;
            if (_telegraphing)
            {
                _elapsed += dt;
                if (_startSoundPending)
                {
                    _soundDelay -= dt;
                    if (_soundDelay <= 0f) PlayStart();
                }
                if (_hasBlinkSound && !_startSoundPending)
                {
                    int started = DoorTelegraphPlan.BlinksStarted(_look, Mathf.Min(_elapsed, FloorRules.DoorTelegraphSeconds));
                    if (started > _blinksPlayed)
                    {
                        _blinksPlayed = started;
                        SkylineRepossessFx.Sound(_look.blinkSound, transform.position, 0.8f);
                    }
                }
                if (_elapsed + EndSlack >= FloorRules.DoorTelegraphSeconds)
                {
                    _elapsed = FloorRules.DoorTelegraphSeconds;
                    _telegraphing = false;
                    _fireCount++;
                    _settle = 0f;
                    ApplyFrame();
                    TelegraphEnded?.Invoke(this);
                    return;
                }
                ApplyFrame();
                return;
            }
            if (_settle >= 0f)
            {
                _settle += dt;
                float open = _settle < HoldSeconds ? 1f : 1f - SkylineRepossessFx.Smooth01((_settle - HoldSeconds) / CloseSeconds);
                ApplyPose(open, FloorRules.DoorTelegraphSeconds);
                SetLight(open, FloorRules.DoorTelegraphSeconds);
                if (_settle >= HoldSeconds + CloseSeconds)
                {
                    _settle = -1f;
                    _light.enabled = false;
                    ApplyPose(0f, 0f);
                }
            }
        }

        void ApplyFrame()
        {
            float p = DoorTelegraphPlan.Progress(_elapsed);
            float open = DoorTelegraphPlan.Open(p);
            ApplyPose(open, _elapsed);
            SetLight(open, _elapsed);
        }

        void SetLight(float open, float elapsed)
        {
            if (_light == null) return;
            float level = _look.blinkHz > 0f ? DoorTelegraphPlan.Beacon(_look, elapsed) : Mathf.Max(0.25f, open);
            _light.intensity = _look.lightIntensity * level;
        }

        // Moves the door's parts for an open amount (0 shut, 1 fully open) and the seconds into the telegraph (rattle, twang, blink).
        void ApplyPose(float open, float elapsed)
        {
            _openAmount = open;
            switch (_look.motion)
            {
                case DoorMotion.ShutterRise:
                    if (_panel != null)
                    {
                        float rattle = open < 0.02f && elapsed > 0f ? DoorTelegraphPlan.Rattle(elapsed) : 0f;
                        _panel.localPosition = _panelRest + new Vector3(rattle, _look.travel * open, 0f);
                    }
                    break;
                case DoorMotion.HatchSlide:
                    if (_flapA != null)
                    {
                        _flapA.localPosition = _flapARest + new Vector3(-_look.travel * open, 0f, 0f);
                        _flapB.localPosition = _flapBRest + new Vector3(_look.travel * open, 0f, 0f);
                    }
                    break;
                case DoorMotion.HatchRoll:
                    if (_panel != null)
                    {
                        // the panel drops 85% of the way shut in the first fifth, then rolls up again with the opening
                        float p = DoorTelegraphPlan.Progress(elapsed);
                        float shut = elapsed <= 0f ? 0f : (p < 0.2f ? SkylineRepossessFx.Smooth01(p / 0.2f) * 0.85f : 0.85f * (1f - open));
                        if (_settle >= 0f) shut = 0f;                  // held open while the bots come out
                        _panel.localPosition = _panelRest + new Vector3(0f, -_look.travel * shut, 0f);
                    }
                    break;
                default:
                    if (_cable != null)
                    {
                        float tw = elapsed > 0f ? DoorTelegraphPlan.Twang(elapsed, _look.travel) : 0f;
                        _cable.localPosition = _cableRest + new Vector3(tw, 0f, 0f);
                    }
                    break;
            }
            if (_beaconRenderer != null)
            {
                bool lit = elapsed > 0f && (_look.blinkHz > 0f ? DoorTelegraphPlan.Beacon(_look, elapsed) > 0.5f : open > 0.02f || _telegraphing);
                var m = lit ? _beaconBright : _beaconDim;
                if (_beaconRenderer.sharedMaterial != m) _beaconRenderer.sharedMaterial = m;
            }
        }
    }

    /// <summary>
    /// Every spawn door of the floor, for the director (task X0). The director's loop is: <c>WaveSpawner.Tick(now, due, started)</c>;
    /// for each started assignment <see cref="Telegraph"/>; for each due one <see cref="Release"/>; a wreck calls
    /// <see cref="CancelAll"/> (and <c>WaveSpawner.Cancel()</c>). Not a MonoBehaviour (the doors are).
    /// </summary>
    public sealed class SkylineSpawnDoors
    {
        /// <summary>The first sound of the n-th door that begins in one frame waits this much longer than the one before, so the mix's
        /// per-sound retrigger gap (0.12 s for a bot sound) does not drop it.</summary>
        public const float SoundStagger = 0.13f;

        readonly Dictionary<string, SkylineSpawnDoor> _byId = new Dictionary<string, SkylineSpawnDoor>();
        readonly List<SkylineSpawnDoor> _all = new List<SkylineSpawnDoor>();
        int _beginFrame = -1, _beginsThisFrame;

        public int Count => _all.Count;
        public IReadOnlyList<SkylineSpawnDoor> All => _all;

        /// <summary>A bot came through a door (after <see cref="Release"/>).</summary>
        public event Action<SkylineBot, SpawnAssignment> BotSpawned;

        /// <summary>Gives every door of <see cref="RepossessSites.Doors"/> its component: on the builder's <c>Door &lt;id&gt;</c>
        /// marker under <paramref name="arena"/> when there is one, else on a new object in a "Spawn Doors" group.</summary>
        public static SkylineSpawnDoors Attach(Transform arena)
        {
            var set = new SkylineSpawnDoors();
            Transform group = null;
            foreach (var d in RepossessSites.Doors())
            {
                var marker = SkylineRepossessFx.FindDeep(arena, "Door " + d.id);
                if (marker == null && group == null && arena != null)
                {
                    group = new GameObject("Spawn Doors (runtime)").transform;
                    group.SetParent(arena, false);
                }
                var door = SkylineSpawnDoor.Attach(marker, group, d);
                set._byId[d.id] = door;
                set._all.Add(door);
            }
            return set;
        }

        /// <summary>The door component with this id, or null.</summary>
        public SkylineSpawnDoor Get(string doorId) => doorId != null && _byId.TryGetValue(doorId, out var d) ? d : null;

        /// <summary>Starts the door's telegraph for an assignment (a door that is already telegraphing keeps its clock: a wave that
        /// sends two bots through one door starts it once). False if the door is unknown.</summary>
        public bool Telegraph(SpawnAssignment a)
        {
            var d = a?.door != null ? Get(a.door.id) : null;
            if (d == null) return false;
            if (d.Busy) return true;
            int frame = Time.frameCount;
            if (frame != _beginFrame) { _beginFrame = frame; _beginsThisFrame = 0; }
            d.BeginTelegraph(_beginsThisFrame * SoundStagger);
            _beginsThisFrame++;
            return true;
        }

        /// <summary>The bot of an assignment whose 2 s are up comes through its door (<see cref="SkylineBot.Spawn"/> with the door
        /// overload: it starts awake and arrives). Returns the bot, or null if it could not be made.</summary>
        public SkylineBot Release(SpawnAssignment a, ulong seed, ISkylineTarget target, WritTuning writ, Transform parent, int group = -1)
        {
            if (a?.door == null) return null;
            var bot = SkylineBot.Spawn(a.enemy, a.door, seed, target, writ, parent, group);
            if (bot == null) return null;
            Debug.Log("SKYLINE_DOOR spawn " + a.door.id + " " + a.enemy);
            BotSpawned?.Invoke(bot, a);
            return bot;
        }

        /// <summary>The ids of the doors with a telegraph running (feed it to <c>SpawnQuery.busyDoors</c>; the pure
        /// <c>WaveSpawner.BusyDoors()</c> says the same).</summary>
        public List<string> BusyDoors()
        {
            var l = new List<string>();
            for (int i = 0; i < _all.Count; i++) if (_all[i].Busy) l.Add(_all[i].DoorId);
            return l;
        }

        /// <summary>A wreck: every telegraph stops and every door shuts.</summary>
        public void CancelAll()
        {
            for (int i = 0; i < _all.Count; i++) _all[i].Cancel();
        }
    }
}
