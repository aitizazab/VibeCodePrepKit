using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// A wrecked bot (2.4: "aggression heals"; 14: "Death: an error face ... the hulk stays as the wreck"). The bot's own model
    /// is kept and broken (judge round 1: every wreck had been the same grey lozenge, and the error face never showed): its
    /// plates are already gone (flung by <see cref="SkylineBot"/>), its face shows the wrecked "X X", and it comes to rest in a
    /// pose of its own:
    /// <list type="bullet">
    /// <item><b>Drone:</b> drops out of the air, clatters (one bounce) and lies tilted on its side, one rotor broken off.</item>
    /// <item><b>Floor-care unit:</b> hops as its canister bursts (the tank gone, a gush of acid round it) and slumps onto one
    /// side, as if a wheel gave.</item>
    /// <item><b>Spotlight:</b> its head sags forward and to one side on the stand with a dead-weight bob; the big lens goes
    /// dark (the glass flies off as shards).</item>
    /// <item><b>Gondola:</b> one cable snaps: the platform swings down from the other and hangs, swaying to a stop, under the
    /// trolley and winch that stay on the rail.</item>
    /// </list>
    /// While its <see cref="Wreck"/> is fresh and unstripped it throws sparks from its body; it goes dark when stale (after
    /// <see cref="Wreck.freshSeconds"/>, 8 s) or once stripped: the screen switches off and the body dulls. Nothing on it
    /// collides or can be shot. The player strips it by coming within <see cref="StripRadius"/> m (flat, and less than 2.5 m
    /// above the wreck's point, the floor under where the bot was, as FightSim):
    /// <c>PlayerArmor.Strip(Data, Time.time, Difficulties.Normal().scrapScale)</c>. Logs
    /// <c>SKYLINE_BOT stripped &lt;bot&gt; plating +&lt;n&gt;</c>.
    /// </summary>
    public sealed class SkylineWreck : MonoBehaviour
    {
        public const float StripRadius = 1.8f;
        const float MaxStripHeight = 2.5f;
        const float Gravity = 13f;

        /// <summary>Every husk in the scene (enabled ones), so nothing has to search the scene for them.</summary>
        public static readonly List<SkylineWreck> All = new List<SkylineWreck>();

        /// <summary>Raised once when the player strips a wreck and gains plating (wreck, plating gained; always above 0): the
        /// light-blue screen-edge flash (docs/SKYLINE-HUD.md) listens to it. Raised from <c>Update</c> on the main thread.</summary>
        public static event System.Action<SkylineWreck, float> Stripped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); Stripped = null; }

        /// <summary>The strippable-wreck outline strength now (1 full, 0.4 thinnest, 0 none or blinked off): the pure
        /// <see cref="SkylineWreckOutline.Strength"/> of the seconds left in the strip window. 0 once stripped or stale.</summary>
        public float OutlineStrength()
        {
            if (Data == null || !Data.Fresh(Time.time)) return 0f;
            return SkylineWreckOutline.Strength(SkylineWreckOutline.Left(Time.time, Data.wreckedAt, Data.freshSeconds), Data.freshSeconds);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        void OnDisable() => All.Remove(this);

        public Wreck Data;
        public string botName = "";
        /// <summary>Whoever can strip it (the player); anything that isn't a <see cref="SkylinePlayer"/> never strips.</summary>
        public ISkylineTarget stripper;

        enum Fall { Drop, Slump, Droop, Hang }

        Fall _fall;
        Transform _moving;            // what falls: the rig (drone, floor-care, spotlight) or the gondola's platform pivot
        Vector3 _fromPos, _restPos;
        Quaternion _fromRot, _restRot;
        float _age, _dropTime;
        bool _settled;
        readonly List<Renderer> _renderers = new List<Renderer>();
        Renderer _face;
        float _nextSpark;
        bool _dark;
        readonly float _scrapScale = Difficulties.Normal().scrapScale;
        static MaterialPropertyBlock _block;

        /// <summary>Takes over <paramref name="bot"/>'s body (its rig and, for the spotlight, its stand) as the wreck of
        /// <paramref name="def"/> whose strip point is <paramref name="floorPoint"/>, wrecked now. <paramref name="seed"/> picks
        /// which way it falls.</summary>
        public static SkylineWreck Create(EnemyDef def, string botName, Vector3 floorPoint, float yaw, ISkylineTarget stripper, Transform parent,
            SkylineBot bot, int seed)
        {
            var root = new GameObject("Wreck " + botName);
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(floorPoint, Quaternion.Euler(0f, yaw, 0f));
            var w = root.AddComponent<SkylineWreck>();
            w.Data = new Wreck(def, Time.time);
            w.botName = botName;
            w.stripper = stripper;
            if (bot != null && bot.Parts != null && bot.Parts.rig != null) w.TakeBody(def, bot, floorPoint, seed);
            return w;
        }

        void TakeBody(EnemyDef def, SkylineBot bot, Vector3 floor, int seed)
        {
            _bot = bot;
            var parts = bot.Parts;
            var rig = parts.rig;
            // nothing on a wreck collides or is shot (as the old husk): the hit zones and colliders go
            StripColliders(rig);
            if (parts.mount != null) StripColliders(parts.mount);
            rig.SetParent(transform, true);
            if (parts.mount != null) parts.mount.SetParent(transform, true);
            _face = parts.face != null ? parts.face.GetComponent<Renderer>() : null;
            _moving = rig;
            _fromPos = rig.position;
            _fromRot = rig.rotation;
            float side = (seed & 1) == 0 ? 1f : -1f;
            float yaw = rig.eulerAngles.y;

            if (def.layer == ThreatLayer.Walls) Hang(rig, side);
            else if (def.moveSpeed > 0f)
            {
                // the floor-care unit: a hop as the canister bursts, then slumped onto one side
                _fall = Fall.Slump;
                _restPos = new Vector3(_fromPos.x, floor.y - 0.02f, _fromPos.z);
                _restRot = Quaternion.Euler(0f, yaw + side * 6f, 0f) * Quaternion.Euler(-5f, 0f, side * 9f);
            }
            else if (parts.mount != null || bot.Beam != null)
            {
                // the spotlight: its head sags forward and to one side on the stand
                _fall = Fall.Droop;
                _restPos = _fromPos;
                _restRot = Quaternion.Euler(52f, yaw + side * 10f, side * 14f);
            }
            else
            {
                // the drone: drops, clatters and lies on its side (its centre about 0.3 m up, rolled onto a duct)
                _fall = Fall.Drop;
                _restPos = new Vector3(_fromPos.x, floor.y + 0.3f, _fromPos.z);
                _restRot = Quaternion.Euler(0f, yaw + side * 20f, 0f) * Quaternion.Euler(16f, 0f, side * 27f);
                float h = Mathf.Max(0.02f, _fromPos.y - _restPos.y);
                _dropTime = Mathf.Sqrt(2f * h / Gravity);
            }
            _moving.SetPositionAndRotation(_fromPos, _fromRot);

            foreach (var r in GetComponentsInChildren<Renderer>(true)) _renderers.Add(r);
            BakeBlocks();
            for (int i = 0; i < _renderers.Count; i++) _inkBase.Add(_renderers[i] != null ? _renderers[i].sharedMaterials : null);
            ApplyOutline(SkylineWreckOutline.Steps);   // fresh and unstripped: outlined light blue at full width (comic ink, rule 2)
        }

        // ------------------------------------------------------------------ drawing cost (judge round 2, perf)
        //
        // A wreck keeps the bot's whole model (20-40 parts), and a late fight has a dozen or more on the floor. The bot hands
        // its body over tinted by material property blocks (its wear, the spotlight's dead lens), and the wreck dulled itself
        // with one more: a renderer with a property block falls out of URP's SRP Batcher, so every wreck part cost a full
        // draw call in the main pass and again in every shadow cascade. The tints are now baked into shared copies of the
        // materials (one per material and colour, shared by every wreck), the blocks cleared, and going dark swaps to the
        // dark copies. Once a wreck lies still, its small parts stop casting shadows (they add nothing to its contact shadow
        // and cost a draw in each cascade). The models' meshes aren't readable at run time, so the parts can't be merged
        // into one mesh here.

        // Perf round 4 (pass 20261014-0700): the skylight fight's slow frames were main-thread bound (13.3 ms of CPU, 2.6 ms
        // of waiting) with 26-31 wrecks lying about, up to 2,392 batches and 687 shadow casters. So, on top of the above:
        // - a wreck that has come to rest casts no shadows at all (every part, not only the small ones);
        // - at most MaxWrecks husks stay: past that, the oldest dark ones (stale or stripped: nothing left to strip, so no
        //   gameplay goes with them) are removed while no camera sees them, one a frame; if more than MaxWrecks + 4 remain
        //   and every candidate is in view, the oldest sinks into the floor over SinkSeconds and is then removed;
        // - once dark, the dead bot's own scripts (its brain, face, acid, beam) are switched off, unless it still has mines on
        //   the wall to tick (the gondola).
        // Nothing on a wreck has a collider or a rigidbody (StripColliders), so there is no physics to put to sleep.

        public const int MaxWrecks = 12;
        const float SinkSeconds = 2.5f, SinkDepth = 1.6f;
        const float DarkScale = 0.55f;
        static int _cullFrame = -1;
        SkylineBot _bot;
        bool _sinking;
        float _sinkAge;
        Vector3 _sinkFrom;
        static readonly Dictionary<long, Material> _tinted = new Dictionary<long, Material>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
        readonly List<Material[]> _plain = new List<Material[]>();   // each renderer's materials before any tint (the dark copies are made from them)
        Material _faceDark;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTints() => _tinted.Clear();

        /// <summary>A shared copy of <paramref name="source"/> in <paramref name="c"/> (the same copy for every wreck).</summary>
        static Material Tinted(Material source, Color c)
        {
            if (source == null) return null;
            Color32 q = c;
            long key = ((long)source.GetInstanceID() << 32) | (uint)(q.r | q.g << 8 | q.b << 16 | q.a << 24);
            if (_tinted.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(source) { name = source.name + " (wreck)" };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            _tinted[key] = m;
            return m;
        }

        /// <summary>Each renderer's property-block colour (the bot's wear, a dead lens) baked into shared material copies, and
        /// the block cleared, so the wreck's parts stay in the SRP Batcher.</summary>
        void BakeBlocks()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                var mats = r != null ? r.sharedMaterials : null;
                _plain.Add(mats);
                if (r == null || !r.HasPropertyBlock()) continue;
                r.GetPropertyBlock(_block);
                bool has = _block.HasColor(BaseColorId), hasLegacy = _block.HasColor(ColorId);
                if (!has && !hasLegacy) continue;   // a block without a colour: left as it is
                Color c = has ? _block.GetColor(BaseColorId) : _block.GetColor(ColorId);
                var baked = new Material[mats.Length];
                for (int k = 0; k < mats.Length; k++) baked[k] = Tinted(mats[k], c);
                r.SetPropertyBlock(null);
                r.sharedMaterials = baked;
            }
        }

        readonly List<Material[]> _inkBase = new List<Material[]>();   // each renderer's materials before any ink role (the outline steps are made from them)
        int _outlineStep = -1;

        /// <summary>Gives every part of the wreck an ink copy of its material at outline width step <paramref name="step"/>
        /// (<see cref="SkylineWreckOutline.Step"/>; 0 = no outline, the role-less ink copy). The copies are shared per material and
        /// step, so the SRP Batcher keeps batching; the outline is light blue while the wreck is fresh and worth stripping,
        /// thinner as the window runs out, blinked off in its last 0.4 s.</summary>
        void ApplyOutline(int step)
        {
            if (step == _outlineStep) return;
            _outlineStep = step;
            int role = step > 0 ? SkylineInkRoles.Scrap : SkylineInkRoles.None;
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                var src = i < _inkBase.Count ? _inkBase[i] : null;
                if (r == null || src == null) continue;
                var mats = new Material[src.Length];
                bool changed = false;
                for (int k = 0; k < src.Length; k++)
                {
                    var ink = SkylineInkRoles.FromLit(src[k], role, step);
                    mats[k] = ink != null ? ink : src[k];
                    if (ink != null) changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>Settled: no part casts a shadow any more (it still receives them).</summary>
        void QuietShadows()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Dark: the dead bot's scripts stop (none of them has anything left to do), unless its mines still tick.</summary>
        void RetireBot()
        {
            if (_bot == null || _bot.Mines != null) return;
            foreach (var b in _bot.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && b.enabled) b.enabled = false;
        }

        /// <summary>Whether any camera draws one of its parts this frame.</summary>
        bool Seen()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r != null && r.enabled && r.isVisible) return true;
            }
            return false;
        }

        /// <summary>Once a frame (whichever wreck gets there first): over <see cref="MaxWrecks"/>, removes the oldest dark,
        /// settled wreck that nothing sees; far over, sinks the oldest one even in view.</summary>
        static void Retire()
        {
            if (_cullFrame == Time.frameCount) return;
            _cullFrame = Time.frameCount;
            int kept = 0;
            for (int i = 0; i < All.Count; i++) if (All[i] != null && !All[i]._sinking) kept++;
            if (kept <= MaxWrecks) return;
            SkylineWreck firstSeen = null;
            for (int i = 0; i < All.Count; i++)
            {
                var w = All[i];
                if (w == null || w._sinking || !w._dark || !w._settled) continue;
                if (!w.Seen())
                {
                    Destroy(w.gameObject);
                    return;
                }
                if (firstSeen == null) firstSeen = w;
            }
            if (firstSeen != null && kept > MaxWrecks + 4)
            {
                firstSeen._sinking = true;
                firstSeen._sinkFrom = firstSeen.transform.position;
            }
        }

        void Sink(float dt)
        {
            _sinkAge += dt;
            float u = Mathf.Clamp01(_sinkAge / SinkSeconds);
            transform.position = _sinkFrom + Vector3.down * (SinkDepth * u * u);
            if (u >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_faceDark != null) Destroy(_faceDark);
        }

        /// <summary>The gondola: the platform's parts go under a pivot at the left stirrup's eye (the right one if
        /// <paramref name="side"/> is negative), the other cable snaps (hidden), and the trolley, the winch and the kept cable
        /// stay on the rail.</summary>
        void Hang(Transform rig, float side)
        {
            _fall = Fall.Hang;
            string keep = side > 0f ? "CableL" : "CableR", snap = side > 0f ? "CableR" : "CableL";
            Vector3 eyeLocal = new Vector3(side > 0f ? -1.02f : 1.02f, 0.6f, 0.14f);
            var eye = SkylineBotBodies.Find(rig, side > 0f ? "StirrupEyeL" : "StirrupEyeR");
            Vector3 pivotWorld = eye != null ? eye.position : rig.TransformPoint(eyeLocal);
            var pivot = new GameObject("Platform").transform;
            pivot.SetParent(rig, false);
            pivot.position = pivotWorld;
            pivot.rotation = rig.rotation;
            foreach (var top in TopParts(rig))
            {
                if (top == pivot) continue;
                string n = top.name;
                if (n == snap) { top.gameObject.SetActive(false); continue; }
                if (n == keep || n == "Rail" || n.StartsWith("Winch") || n == "WeakPoint" || n.StartsWith("Cheek") || n.StartsWith("Wheel")) continue;
                if (rig.InverseTransformPoint(Centre(top)).y > 0.9f) continue;   // anything else up by the trolley stays
                top.SetParent(pivot, true);
            }
            _moving = pivot;
            _fromPos = pivot.position;
            _fromRot = pivot.rotation;
            _restPos = _fromPos;
            // the free end drops: rolled about the platform's own forward axis, nearly hanging from the kept eye (the roll's
            // sign from where the eye is, so it holds whichever way the model's x runs)
            float eyeX = rig.InverseTransformPoint(pivotWorld).x;
            _restRot = rig.rotation * Quaternion.Euler(0f, 0f, (eyeX < 0f ? -1f : 1f) * 68f);
        }

        /// <summary>The model's parts: the rig's children, or, when the model came in under one root node, that node's.</summary>
        static List<Transform> TopParts(Transform rig)
        {
            var list = new List<Transform>();
            Transform holder = rig;
            if (rig.childCount == 1 && rig.GetChild(0).childCount > 3 && rig.GetChild(0).GetComponent<Renderer>() == null) holder = rig.GetChild(0);
            for (int i = 0; i < holder.childCount; i++) list.Add(holder.GetChild(i));
            return list;
        }

        static Vector3 Centre(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return t.position;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.center;
        }

        static void StripColliders(Transform t)
        {
            foreach (var z in t.GetComponentsInChildren<SkylineHitZone>(true)) Destroy(z);
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
                Destroy(c);
            }
        }

        public bool Fresh => Data != null && Data.Fresh(Time.time);

        void Update()
        {
            if (Data == null) return;
            float dt = Time.deltaTime;
            if (_sinking) { Sink(dt); return; }
            if (_dark && _settled) Retire();
            if (dt > 0f && !_settled && _moving != null) Animate(dt);
            float now = Time.time;
            if (Data.Fresh(now))
            {
                TryStrip(now);
                if (Data.Fresh(now)) ApplyOutline(SkylineWreckOutline.Step(OutlineStrength()));
                _nextSpark -= dt;
                if (_nextSpark <= 0f && !Data.stripped && _renderers.Count > 0)
                {
                    _nextSpark = Random.Range(0.12f, 0.3f);
                    var b = Bounds();
                    Vector3 at = b.center + new Vector3(Random.Range(-0.4f, 0.4f) * b.extents.x, Random.Range(0f, 0.6f) * b.extents.y, Random.Range(-0.4f, 0.4f) * b.extents.z);
                    SkylineSpark.Burst(at, Vector3.up, Random.Range(2, 5), 3f);
                }
            }
            if (!_dark && !Data.Fresh(now)) GoDark();
        }

        void Animate(float dt)
        {
            _age += dt;
            switch (_fall)
            {
                case Fall.Drop:
                {
                    // a free fall onto the floor, turning toward its resting tilt; then one clattering bounce
                    const float Bounce = 0.26f;
                    if (_age < _dropTime)
                    {
                        float u = _age / Mathf.Max(1e-3f, _dropTime);
                        Vector3 p = Vector3.Lerp(_fromPos, _restPos, u);
                        p.y = _fromPos.y - 0.5f * Gravity * _age * _age;
                        _moving.SetPositionAndRotation(p, Quaternion.Slerp(_fromRot, _restRot, u * u));
                    }
                    else if (_age < _dropTime + Bounce)
                    {
                        float u = (_age - _dropTime) / Bounce;
                        float hop = 0.1f * Mathf.Sin(Mathf.PI * u);
                        var wobble = Quaternion.Euler(0f, 0f, 9f * Mathf.Sin(Mathf.PI * u) * (1f - u));
                        _moving.SetPositionAndRotation(_restPos + Vector3.up * hop, _restRot * wobble);
                    }
                    else Settle();
                    break;
                }
                case Fall.Slump:
                {
                    const float T = 0.55f;
                    float u = Mathf.Clamp01(_age / T);
                    float hop = 0.14f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.6f));
                    _moving.SetPositionAndRotation(Vector3.Lerp(_fromPos, _restPos, u) + Vector3.up * hop,
                        Quaternion.SlerpUnclamped(_fromRot, _restRot, BackOut(u)));
                    if (u >= 1f) Settle();
                    break;
                }
                case Fall.Droop:
                {
                    // a dead weight sagging on its yoke: overshoots and bobs to rest
                    float k = 1f - Mathf.Exp(-4.5f * _age) * Mathf.Cos(8f * _age);
                    _moving.rotation = Quaternion.SlerpUnclamped(_fromRot, _restRot, k);
                    if (_age > 1.6f) Settle();
                    break;
                }
                case Fall.Hang:
                {
                    // a pendulum swinging down and to a stop
                    float k = 1f - Mathf.Exp(-1.6f * _age) * Mathf.Cos(4.2f * _age);
                    _moving.rotation = Quaternion.SlerpUnclamped(_fromRot, _restRot, k);
                    _moving.position = _fromPos;
                    if (_age > 3.5f) Settle();
                    break;
                }
            }
        }

        static float BackOut(float u)
        {
            const float s = 1.6f;
            float x = u - 1f;
            return 1f + x * x * ((s + 1f) * x + s);
        }

        void Settle()
        {
            _settled = true;
            if (_fall == Fall.Hang) _moving.SetPositionAndRotation(_fromPos, _restRot);
            else _moving.SetPositionAndRotation(_restPos, _restRot);
            QuietShadows();
        }

        Bounds Bounds()
        {
            Bounds b = default(Bounds);
            bool any = false;
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds(transform.position + Vector3.up * 0.3f, Vector3.one * 0.5f);
        }

        void TryStrip(float now)
        {
            var p = stripper as SkylinePlayer;
            if (p == null || p.Armor == null) return;
            Vector3 feet = p.transform.position, at = transform.position;
            float dx = feet.x - at.x, dz = feet.z - at.z;
            if (dx * dx + dz * dz > StripRadius * StripRadius || feet.y - at.y >= MaxStripHeight) return;
            float gained = p.Armor.Strip(Data, now, _scrapScale);
            if (Data.stripped)
            {
                Debug.Log("SKYLINE_BOT stripped " + botName + " plating +" + gained.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                if (gained > 0f) Stripped?.Invoke(this, gained);
            }
        }

        /// <summary>Stale or stripped: the screen switches off and the body dulls (each part's own colour, darkened), by
        /// swapping to shared dark copies of its materials (no property blocks: see <see cref="BakeBlocks"/>).</summary>
        void GoDark()
        {
            _dark = true;
            var screenOff = new Color(0.03f, 0.035f, 0.04f, 1f);
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                var plain = i < _plain.Count && _plain[i] != null ? _plain[i] : r.sharedMaterials;
                if (r == _face)
                {
                    // the face's material is the bot's own (its texture changes per bot): its own dark copy, not a shared one
                    var src = r.sharedMaterial;
                    if (src == null) continue;
                    _faceDark = new Material(src) { name = src.name + " (off)" };
                    if (_faceDark.HasProperty(BaseColorId)) _faceDark.SetColor(BaseColorId, screenOff);
                    if (_faceDark.HasProperty(ColorId)) _faceDark.SetColor(ColorId, screenOff);
                    r.SetPropertyBlock(null);
                    SkylineInkRoles.SetRole(_faceDark, SkylineInkRoles.None);
                    r.sharedMaterial = _faceDark;
                    continue;
                }
                var dark = new Material[plain.Length];
                for (int k = 0; k < plain.Length; k++)
                {
                    var m = plain[k];
                    Color c = m != null ? m.color : Color.grey;
                    dark[k] = m != null ? Tinted(m, new Color(c.r * DarkScale, c.g * DarkScale, c.b * DarkScale, c.a)) : null;
                    if (dark[k] != null) dark[k] = SkylineInkRoles.FromLit(dark[k], SkylineInkRoles.None) ?? dark[k];   // stripped or stale: the light-blue outline goes
                }
                r.SetPropertyBlock(null);
                r.sharedMaterials = dark;
            }
            RetireBot();
        }
    }
}
