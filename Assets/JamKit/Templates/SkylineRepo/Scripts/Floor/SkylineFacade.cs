using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The east façade at run time (task G1; docs/SKYLINE-FLOOR-REPOSSESS.md section 10, plan decision D21): the glass wall-run
    /// the gondola works outside of.
    /// <list type="bullet">
    /// <item><b>Panes.</b> Finds the 80 <c>facade-pane-r-c</c> objects the floor builder made (glass layer, one collider each) and
    /// breaks them: <see cref="Break(string,Vector3,Vector3)"/> / <see cref="TryBreak"/> turn a pane's collider and renderer off (the
    /// tile leaves the wall-run: nothing to run on, nothing to stop a shot or a Unit), flings shards, leaves jagged slivers in the
    /// frame, and raises <see cref="PaneBroken"/>. The pure state is <see cref="FacadeGlass"/> (<see cref="Glass"/>). A Unit in an
    /// open pane's edge is cut (<see cref="FacadeGlass.EdgeDamage"/> per <see cref="FacadeGlass.EdgeInterval"/>), and falling out of
    /// the building is a wreck (<see cref="FacadeGlass.FallDeathY"/>, the sim's rule). <see cref="Mend"/> puts every pane back (the
    /// floor restarts).</item>
    /// <item><b>Hatches.</b> Twelve service hatches on the inside face (<see cref="FacadeHatches"/>, z -22..22 every 4 m, y 2..3).
    /// Each slides open for a moment when a gondola's mine is laid through it (<see cref="SkylineBot.MineEvent"/>), so the toss from
    /// outside reads as going through the wall.</item>
    /// <item><b>The outside rail</b> with its trolley: a track at x 36.5 the length of the façade, and a trolley that follows the
    /// live gondola (parked where it last was when none is alive).</item>
    /// <item><b>The gondola's spawn</b> at a rail end: <see cref="SpawnGondola"/> (the director's door telegraph is SD1's).</item>
    /// </list>
    /// How shooting calls it (A2's <c>SkylineShooting</c>): a ray that hits a collider on the glass layer whose name is a pane
    /// (<see cref="IsPane"/>) calls <see cref="TryBreak"/> with the hit point and the shot's direction and then carries on past the
    /// pane (it did not stop there); balustrades and the skylight stop the shot as before. Bots' bolts may do the same.
    /// X0 adds this component to the arena root (one per scene; it finds everything itself in <c>Start</c>).
    /// </summary>
    public sealed class SkylineFacade : MonoBehaviour
    {
        /// <summary>The one live façade, or null.</summary>
        public static SkylineFacade Instance;

        /// <summary>A pane broke: its id and the world point of the hit.</summary>
        public static event Action<string, Vector3> PaneBroken;

        /// <summary>What the edge cuts the Unit with, and a fall's damage (anything over the Unit's health).</summary>
        public const string EdgeAttackerId = "facade-edge", FallAttackerId = "facade-fall";
        public const float FallDamage = 9999f;

        const float HatchWidth = 1.4f, HatchOpenSeconds = 0.18f, HatchHoldSeconds = 0.7f, HatchCloseSeconds = 0.4f;
        const int ShardsPerPane = 12, EdgeSliversPerPane = 7, MaxShards = 160;
        const float ShardLife = 2.6f;

        /// <summary>The pure state of the glass.</summary>
        public readonly FacadeGlass Glass = new FacadeGlass();

        sealed class PaneObj
        {
            public GameObject go;
            public Collider col;
            public Renderer rend;
            public Bounds box;
            public GameObject slivers;
        }

        sealed class Hatch
        {
            public Transform panel;
            public float open, holdUntil = -1f, zClosed;
        }

        struct Shard
        {
            public Transform t;
            public Vector3 v, spin;
            public float age, size;
        }

        readonly Dictionary<string, PaneObj> _panes = new Dictionary<string, PaneObj>();
        readonly Hatch[] _hatches = new Hatch[FacadeHatches.Count];
        readonly List<Shard> _shards = new List<Shard>();
        readonly List<string> _ids = new List<string>();
        Transform _built, _trolley;
        float _trolleyZ = -FacadeHatches.RailEndZ + 1f;
        SkylinePlayer _player;
        float _findPlayerAt, _fallAgain;

        /// <summary>How many panes the scene has / how many are broken.</summary>
        public int PaneCount => _panes.Count;
        public int BrokenCount => Glass.BrokenCount;

        /// <summary>True for a collider that is one of the façade's panes (by the name the floor builder gives it).</summary>
        public static bool IsPane(Collider c) => c != null && RepossessArena.IsPane(c.name);

        /// <summary>The shooting hook: if <paramref name="hit"/> is a live façade pane, breaks it (shards fly along
        /// <paramref name="direction"/>) and returns true; the shot should carry on past it. False for anything else and when there
        /// is no façade in the scene.</summary>
        public static bool TryBreak(Collider hit, Vector3 point, Vector3 direction)
        {
            if (Instance == null || !IsPane(hit)) return false;
            return Instance.Break(hit.name, point, direction);
        }

        // ------------------------------------------------------------------ lifecycle

        void Awake() => Instance = this;

        void OnEnable()
        {
            Instance = this;
            SkylineBot.MineEvent += OnMine;
        }

        void OnDisable()
        {
            SkylineBot.MineEvent -= OnMine;
            if (Instance == this) Instance = null;
        }

        void Start() => Build();

        void Build()
        {
            if (_built != null) return;
            _built = new GameObject("Facade Runtime").transform;
            _built.SetParent(transform, false);
            _built.gameObject.AddComponent<SkylineNoCollider>();

            foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!RepossessArena.IsPane(col.name) || _panes.ContainsKey(col.name)) continue;
                _panes[col.name] = new PaneObj { go = col.gameObject, col = col, rend = col.GetComponent<Renderer>(), box = col.bounds };
            }

            BuildHatches();
            BuildRail();
            if (_panes.Count == 0) Debug.LogWarning("SKYLINE_FACADE no facade panes found in the scene (is this the floor-1 scene?)");
        }

        // ------------------------------------------------------------------ the panes

        /// <summary>Breaks the pane with this id, if it exists and is whole: its collider and renderer go, shards fly from
        /// <paramref name="point"/> along <paramref name="direction"/>. False when it is not a pane, is not in the scene or is already broken.</summary>
        public bool Break(string paneId, Vector3 point, Vector3 direction)
        {
            if (!_panes.TryGetValue(paneId ?? "", out var p) || Glass.IsBroken(paneId)) return false;
            Glass.Break(paneId);
            if (p.col != null) p.col.enabled = false;
            if (p.rend != null) p.rend.enabled = false;
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.left;
            Shatter(p, point, dir);
            Slivers(paneId, p);
            SkylineSounds.PlayAt("facade/pane_break", point);
            PaneBroken?.Invoke(paneId, point);
            return true;
        }

        /// <summary>Puts every pane back, removes the shards and slivers, and closes the hatches (the floor restarts).</summary>
        public void Mend()
        {
            Glass.MendAll();
            foreach (var p in _panes.Values)
            {
                if (p.col != null) p.col.enabled = true;
                if (p.rend != null) p.rend.enabled = true;
                if (p.slivers != null) Destroy(p.slivers);
                p.slivers = null;
            }
            for (int i = 0; i < _shards.Count; i++) if (_shards[i].t != null) Destroy(_shards[i].t.gameObject);
            _shards.Clear();
            for (int i = 0; i < _hatches.Length; i++)
                if (_hatches[i] != null) { _hatches[i].open = 0f; _hatches[i].holdUntil = -1f; }
        }

        static Material GlassLook() => SkylineBotLook.Lit(new Color(0.78f, 0.9f, 1f));

        void Shatter(PaneObj p, Vector3 point, Vector3 dir)
        {
            Vector3 c = p.box.center, e = p.box.extents;
            for (int i = 0; i < ShardsPerPane && _shards.Count < MaxShards; i++)
            {
                float size = UnityEngine.Random.Range(0.18f, 0.55f);
                var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Pane Shard", _built, GlassLook(), false);
                go.transform.position = new Vector3(c.x, c.y + UnityEngine.Random.Range(-e.y, e.y), c.z + UnityEngine.Random.Range(-e.z, e.z));
                go.transform.rotation = UnityEngine.Random.rotation;
                go.transform.localScale = new Vector3(size, 0.02f, size * UnityEngine.Random.Range(0.5f, 1f));
                Vector3 v = dir * UnityEngine.Random.Range(2.5f, 6f) + new Vector3(0f, UnityEngine.Random.Range(0.5f, 3f), UnityEngine.Random.Range(-1.5f, 1.5f));
                _shards.Add(new Shard { t = go.transform, v = v, spin = UnityEngine.Random.insideUnitSphere * 360f, size = size });
            }
        }

        /// <summary>The jagged slivers left standing in the frame: the open edge (a hazard, so it is pale and sharp, never the hostile hue).</summary>
        void Slivers(string id, PaneObj p)
        {
            p.slivers = new GameObject("Slivers " + id);
            p.slivers.transform.SetParent(_built, false);
            Vector3 c = p.box.center, e = p.box.extents;
            for (int i = 0; i < EdgeSliversPerPane; i++)
            {
                // along the four edges of the opening in turn
                int side = i % 4;
                float along = UnityEngine.Random.Range(-0.9f, 0.9f);
                Vector3 pos = side == 0 ? new Vector3(c.x, c.y - e.y, c.z + along * e.z)
                    : side == 1 ? new Vector3(c.x, c.y + e.y, c.z + along * e.z)
                    : side == 2 ? new Vector3(c.x, c.y + along * e.y, c.z - e.z)
                    : new Vector3(c.x, c.y + along * e.y, c.z + e.z);
                float len = UnityEngine.Random.Range(0.25f, 0.6f);
                var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Sliver", p.slivers.transform, GlassLook(), false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(-30f, 30f));
                go.transform.localScale = new Vector3(0.12f, len, 0.03f);
            }
        }

        // ------------------------------------------------------------------ the hatches

        void BuildHatches()
        {
            var root = new GameObject("Facade Hatches").transform;
            root.SetParent(_built, false);
            for (int i = 0; i < FacadeHatches.Count; i++)
            {
                float z = FacadeHatches.ZOf(i);
                float midY = (FacadeHatches.HatchMinY + FacadeHatches.HatchMaxY) * 0.5f;
                // the backing: a dark frame; the panel in front of it slides along the wall to open
                SkylineBotLook.Part(PrimitiveType.Cube, "Hatch Frame " + i, root, new Vector3(FacadeHatches.GlassX - 0.04f, midY, z),
                    new Vector3(0.08f, FacadeHatches.HatchMaxY - FacadeHatches.HatchMinY + 0.16f, HatchWidth + 0.16f), SkylineBotLook.Dark, false);
                var panel = SkylineBotLook.Part(PrimitiveType.Cube, "Hatch Panel " + i, root, new Vector3(FacadeHatches.GlassX - 0.1f, midY, z),
                    new Vector3(0.05f, FacadeHatches.HatchMaxY - FacadeHatches.HatchMinY, HatchWidth), SkylineBotLook.Gold, false);
                _hatches[i] = new Hatch { panel = panel.transform, zClosed = z };
            }
        }

        void OnMine(SkylineBot bot, SkylineMineEvent ev, Vector3 at)
        {
            if (ev != SkylineMineEvent.Laid || !FacadeHatches.IsHatchPoint(new NVec3(at.x, at.y, at.z))) return;
            var h = _hatches[FacadeHatches.NearestIndex(at.z)];
            if (h != null) h.holdUntil = Time.time + HatchOpenSeconds + HatchHoldSeconds;
        }

        // ------------------------------------------------------------------ the outside rail

        void BuildRail()
        {
            float y = FacadeHatches.RailY + 1.3f;   // where the bot's own rail bar sits (its mount is 1.3 m above the cab)
            float len = FacadeHatches.RailEndZ * 2f + 1.6f;
            SkylineBotLook.Part(PrimitiveType.Cube, "Facade Rail", _built, new Vector3(FacadeHatches.RailX, y - 0.05f, 0f), new Vector3(0.2f, 0.1f, len), SkylineBotLook.Grey, false);
            SkylineBotLook.Part(PrimitiveType.Cube, "Rail Stop S", _built, new Vector3(FacadeHatches.RailX, y, -len * 0.5f), new Vector3(0.3f, 0.4f, 0.15f), SkylineBotLook.Dark, false);
            SkylineBotLook.Part(PrimitiveType.Cube, "Rail Stop N", _built, new Vector3(FacadeHatches.RailX, y, len * 0.5f), new Vector3(0.3f, 0.4f, 0.15f), SkylineBotLook.Dark, false);
            _trolley = SkylineBotLook.Part(PrimitiveType.Cube, "Rail Trolley", _built, new Vector3(FacadeHatches.RailX, y + 0.12f, _trolleyZ), new Vector3(0.5f, 0.3f, 0.7f), SkylineBotLook.Gold, false).transform;
        }

        // ------------------------------------------------------------------ the gondola's spawn

        /// <summary>The façade rail's end door (north is +z).</summary>
        public static SpawnDoor RailDoor(bool north)
        {
            SpawnDoor best = null;
            foreach (var d in RepossessSites.Doors())
                if (d.kind == DoorKind.RailEnd && (best == null || (north ? d.position.Z > best.position.Z : d.position.Z < best.position.Z))) best = d;
            return best;
        }

        /// <summary>Puts a gondola on the rail at one end, awake, riding to the other end (<see cref="SkylineBot.Spawn(string, SpawnDoor, ulong, ISkylineTarget, WritTuning, Transform, int)"/>).
        /// The 2 s door telegraph is the spawn doors' (SD1); call this when it ends.</summary>
        public static SkylineBot SpawnGondola(bool north, ulong seed, ISkylineTarget target, WritTuning writ, Transform parent, int group = -1)
        {
            var door = RailDoor(north);
            return door == null ? null : SkylineBot.Spawn(SkylineEnemies.GondolaId, door, seed, target, writ, parent, group);
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateShards(dt);
            UpdateHatches();
            UpdateTrolley(dt);
            UpdateHazards(dt);
        }

        void UpdateShards(float dt)
        {
            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                var s = _shards[i];
                if (s.t == null) { _shards.RemoveAt(i); continue; }
                s.age += dt;
                s.v += Vector3.down * 9.8f * dt;
                s.t.position += s.v * dt;
                s.t.Rotate(s.spin * dt, Space.Self);
                float k = Mathf.Clamp01((ShardLife - s.age) / 0.5f);
                s.t.localScale = new Vector3(s.size * k, 0.02f, s.size * 0.7f * k);
                if (s.age >= ShardLife) { Destroy(s.t.gameObject); _shards.RemoveAt(i); continue; }
                _shards[i] = s;
            }
        }

        void UpdateHatches()
        {
            float now = Time.time;
            for (int i = 0; i < _hatches.Length; i++)
            {
                var h = _hatches[i];
                if (h == null || h.panel == null) continue;
                bool wantOpen = now < h.holdUntil;
                float rate = wantOpen ? 1f / HatchOpenSeconds : -1f / HatchCloseSeconds;
                h.open = Mathf.Clamp01(h.open + rate * Time.deltaTime);
                var p = h.panel.position;
                p.z = h.zClosed + h.open * (HatchWidth - 0.1f);
                h.panel.position = p;
            }
        }

        void UpdateTrolley(float dt)
        {
            if (_trolley == null) return;
            SkylineBot gondola = null;
            for (int i = 0; i < SkylineBot.Live.Count; i++)
            {
                var b = SkylineBot.Live[i];
                if (b != null && b.Alive && b.Def != null && b.Def.layer == ThreatLayer.Walls) { gondola = b; break; }
            }
            if (gondola != null) _trolleyZ = Mathf.MoveTowards(_trolleyZ, gondola.transform.position.z, 8f * dt);
            var p = _trolley.position;
            p.z = _trolleyZ;
            _trolley.position = p;
        }

        void UpdateHazards(float dt)
        {
            if (_player == null && Time.time >= _findPlayerAt)
            {
                _player = FindFirstObjectByType<SkylinePlayer>();
                _findPlayerAt = Time.time + 1f;
            }
            if (_player == null) return;
            Vector3 chest = _player.Chest;
            NVec3 c = new NVec3(chest.x, chest.y, chest.z);
            if (_fallAgain > 0f) _fallAgain -= dt;
            if (FacadeGlass.HasFallen(c) && _fallAgain <= 0f)
            {
                _fallAgain = 1f;
                _player.TakeAttack(FallDamage, FallAttackerId, chest + Vector3.up * 5f);   // out of the building: wrecked
                return;
            }
            if (Glass.BrokenCount == 0) return;
            float cut = Glass.Tick(dt, c);
            if (cut > 0f) _player.TakeAttack(cut, EdgeAttackerId, NearestGapCentre(chest));
        }

        Vector3 NearestGapCentre(Vector3 from)
        {
            _ids.Clear();
            Glass.BrokenIds(_ids);
            Vector3 best = from + Vector3.right;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < _ids.Count; i++)
            {
                if (!_panes.TryGetValue(_ids[i], out var p)) continue;
                float d = (p.box.center - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p.box.center; }
            }
            return best;
        }
    }
}
