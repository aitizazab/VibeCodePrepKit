using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Colours and materials for the bots (CONCIERGE white and gold). Materials are shared per colour and made from
    /// the primitives' own default material (the tracers' approach: no shader lookups for the ordinary parts). Only the Writ's
    /// cracked casing and a fresh wreck's sparks may glow (the creator's damage-feedback rules), so <see cref="Glow"/> is used
    /// by those two alone.</summary>
    public static class SkylineBotLook
    {
        public static readonly Color White = new Color(0.93f, 0.92f, 0.88f);
        public static readonly Color Gold = new Color(0.86f, 0.66f, 0.22f);
        public static readonly Color Dark = new Color(0.12f, 0.12f, 0.14f);
        public static readonly Color Grey = new Color(0.45f, 0.46f, 0.5f);
        public static readonly Color Lens = new Color(0.16f, 0.22f, 0.42f);
        public static readonly Color Acid = new Color(0.55f, 0.85f, 0.2f);
        public static readonly Color Scorched = new Color(0.22f, 0.21f, 0.2f);

        static readonly Dictionary<Color, Material> _lit = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> _glow = new Dictionary<Color, Material>();
        static readonly Dictionary<PrimitiveType, Mesh> _meshes = new Dictionary<PrimitiveType, Mesh>();
        static Material _template;

        /// <summary>The scene's own glowing material (URP Unlit, set by the arena's or the gallery's director from a material
        /// the scene builder saved in the scene), so <see cref="Glow"/> copies a shader variant the build is known to carry
        /// instead of looking the shader up by name at run time. Null: the name lookup, as before.</summary>
        public static Material GlowTemplate;

        /// <summary>The primitives' default (lit) material, read once from a throwaway cube.</summary>
        static Material Template()
        {
            if (_template != null) return _template;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _template = go.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(go);
            return _template;
        }

        /// <summary>A primitive's mesh, read once per type from a throwaway primitive.</summary>
        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (_meshes.TryGetValue(type, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            _meshes[type] = m;
            return m;
        }

        /// <summary>A primitive drawn but never collided with: a mesh filter and renderer only (no collider made and then
        /// destroyed, which costs a physics-scene change each time). For effects, dressing and every part without a collider.</summary>
        public static GameObject Visual(PrimitiveType type, string name, Transform parent, Material material, bool castShadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(type);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            if (!castShadows)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
        }

        /// <summary>A shared lit material of <paramref name="c"/>.</summary>
        public static Material Lit(Color c)
        {
            if (_lit.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Template()) { color = c };
            _lit[c] = m;
            return m;
        }

        static readonly Dictionary<Color, Material> _wet = new Dictionary<Color, Material>();

        /// <summary>A shared lit material of <paramref name="c"/> that reads wet: very smooth, so it catches the sun and the
        /// sky's reflection (acid, puddles).</summary>
        public static Material Wet(Color c)
        {
            if (_wet.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Template()) { color = c };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.93f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.93f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            _wet[c] = m;
            return m;
        }

        /// <summary>The glow's material asset in a Resources folder (URP Unlit, opaque): always in the build with its shader
        /// variant, whatever the scene holds. Judge round 2 found a magenta bar in the build only: a glow made before the arena
        /// director handed over its scene material had looked URP's Unlit up by name, and the build had no such variant.</summary>
        public const string GlowResource = "SkylineFx/SK_Emissive_FxGlow";
        /// <summary>The acid's material asset in a Resources folder (URP Lit, very smooth, with the normal-map and emission
        /// variants switched on, so both are built).</summary>
        public const string AcidResource = "SkylineFx/SK_Fx_Acid";

        /// <summary>A shared unlit material for the few things that glow (HDR colours read as hot under bloom): a copy of the
        /// scene's glow material when a director set one, else of <see cref="GlowResource"/>; never a shader looked up by name
        /// (a build strips variants no material asset uses, and those render magenta). Without either, the lit template.</summary>
        public static Material Glow(Color hdr)
        {
            if (_glow.TryGetValue(hdr, out var m) && m != null) return m;
            var template = GlowTemplate != null ? GlowTemplate : GlowAsset();
            m = template != null ? new Material(template) : new Material(Template());
            m.color = hdr;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            _glow[hdr] = m;
            return m;
        }

        static Material _glowAsset, _acidAsset;
        static Texture2D _ripples;

        static Material GlowAsset()
        {
            if (_glowAsset == null) _glowAsset = Resources.Load<Material>(GlowResource);
            return _glowAsset;
        }

        static readonly Dictionary<Color, Material> _acid = new Dictionary<Color, Material>();

        /// <summary>A shared acid material: URP Lit from <see cref="AcidResource"/>, base <paramref name="baseColor"/>, a faint
        /// emissive <paramref name="glow"/>, very smooth, with a rippled normal map (<see cref="Ripples"/>), so it shades like a
        /// wet, slightly lit-from-within liquid rather than a flat sticker. Falls back to <see cref="Wet"/>.</summary>
        public static Material AcidMaterial(Color baseColor, Color glow, float smoothness = 0.95f)
        {
            var key = new Color(baseColor.r + glow.r * 7.1f, baseColor.g + glow.g * 13.3f, baseColor.b + glow.b * 3.7f, smoothness);
            if (_acid.TryGetValue(key, out var m) && m != null) return m;
            if (_acidAsset == null) _acidAsset = Resources.Load<Material>(AcidResource);
            if (_acidAsset == null) return Wet(baseColor);
            m = new Material(_acidAsset) { name = "Acid" };
            m.color = baseColor;
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetColor("_EmissionColor", glow);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_BumpMap", Ripples());
            m.SetFloat("_BumpScale", 0.7f);
            m.EnableKeyword("_NORMALMAP");
            _acid[key] = m;
            return m;
        }

        /// <summary>Moves every acid material's ripples on (a slow drift, so the liquid's highlights crawl). Call once a frame.</summary>
        public static void FlowAcid(float time)
        {
            foreach (var kv in _acid)
                if (kv.Value != null) kv.Value.SetTextureOffset("_BumpMap", new Vector2(time * 0.07f, time * 0.11f));
        }

        /// <summary>A tiling normal map of soft overlapping ripples, made in code (linear).</summary>
        public static Texture2D Ripples()
        {
            if (_ripples != null) return _ripples;
            const int N = 64;
            var h = new float[N * N];
            var rng = new System.Random(5309);
            for (int k = 0; k < 9; k++)
            {
                float cx = (float)rng.NextDouble() * N, cy = (float)rng.NextDouble() * N, f = 0.35f + 0.5f * (float)rng.NextDouble(), ph = (float)rng.NextDouble() * 6.3f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        // the nearest wrapped copy of the centre, so the map tiles
                        float dx = Mathf.Repeat(x - cx + N * 0.5f, N) - N * 0.5f, dy = Mathf.Repeat(y - cy + N * 0.5f, N) - N * 0.5f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        h[y * N + x] += Mathf.Sin(r * f + ph) * Mathf.Exp(-r * 0.06f);
                    }
            }
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float gx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                    float gy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                    var n = new Vector3(-gx * 0.6f, -gy * 0.6f, 1f).normalized;
                    // plain RGB with alpha 1 decodes right both ways: the RGB decode reads xyz, the RG-or-AG decode multiplies
                    // alpha by red and reads x from it
                    px[y * N + x] = new Color32((byte)(n.x * 127.5f + 127.5f), (byte)(n.y * 127.5f + 127.5f), (byte)(n.z * 127.5f + 127.5f), 255);
                }
            _ripples = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "Acid ripples", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            _ripples.SetPixels32(px);
            _ripples.Apply(true, true);
            return _ripples;
        }

        /// <summary>A primitive with a shared material; its collider removed when <paramref name="collider"/> is false.</summary>
        public static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool collider = true, Vector3 euler = default(Vector3))
        {
            GameObject go;
            if (collider)
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.GetComponent<Renderer>().sharedMaterial = Lit(color);
            }
            else go = Visual(type, name, parent, Lit(color));
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>How many shard shapes <see cref="ShardMesh"/> makes.</summary>
        public const int ShardVariants = 4;
        static readonly Mesh[] _shards = new Mesh[ShardVariants];

        /// <summary>A broken-off piece of shell about 1 unit across (scale it to size): an irregular five- to seven-sided plate,
        /// domed a little on top like a piece of a curved casing, with hard edges. A wreck's debris, in the bot's own material.</summary>
        public static Mesh ShardMesh(int variant)
        {
            variant = ((variant % ShardVariants) + ShardVariants) % ShardVariants;
            if (_shards[variant] != null) return _shards[variant];
            var rng = new System.Random(7919 * (variant + 1));
            int n = 5 + variant % 3;
            const float Thick = 0.14f, Dome = 0.1f;
            var rim = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) / n * Mathf.PI * 2f;
                float r = 0.3f + 0.25f * (float)rng.NextDouble() + (i == 0 ? 0.2f : 0f);   // one long point: a torn corner
                rim[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 top = new Vector3(0f, Thick * 0.5f + Dome, 0f), bottom = new Vector3(0f, -Thick * 0.5f + Dome * 0.6f, 0f);
            for (int i = 0; i < n; i++)
            {
                Vector3 a = rim[i], b = rim[(i + 1) % n];
                Vector3 at = a + Vector3.up * Thick * 0.5f, bt = b + Vector3.up * Thick * 0.5f;
                Vector3 ab = a - Vector3.up * Thick * 0.5f, bb = b - Vector3.up * Thick * 0.5f;
                int k = verts.Count;
                verts.Add(top); verts.Add(bt); verts.Add(at);                       // the domed top
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                k = verts.Count;
                verts.Add(bottom); verts.Add(ab); verts.Add(bb);                    // the underside
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                k = verts.Count;
                verts.Add(at); verts.Add(bt); verts.Add(bb); verts.Add(ab);         // the broken edge
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
            }
            var m = new Mesh { name = "Wreck shard " + variant };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            _shards[variant] = m;
            return m;
        }

        public static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }

    /// <summary>The named parts of one bot's body (the model contract's children).</summary>
    public sealed class SkylineBotParts
    {
        /// <summary>Turns and wobbles; the bot's own space for weak spots. Every hittable part is under it.</summary>
        public Transform rig;
        /// <summary>The spotlight's fixed base (not turning), or null.</summary>
        public Transform mount;
        /// <summary>Plate_&lt;plateId&gt; per PlateDef, in the def's order (null where the model lacks one).</summary>
        public Transform[] plates;
        public Transform weakPoint, face, muzzle, nozzle, lamp, rail, mineDrop;
        /// <summary>Moving bits: the drone's rotors, the floor-care unit's brush, the gondola's squeegee (may be null).</summary>
        public Transform spinner, squeegee;
        /// <summary>Where it is shot at and sees from, in rig space.</summary>
        public Vector3 centerLocal;
        public readonly List<Renderer> bodyRenderers = new List<Renderer>();
        public bool fromModel;
    }

    /// <summary>
    /// Builds a bot's body: from its model <c>Resources/SkylineModels/bots/&lt;id&gt;</c> when present (children
    /// <c>Plate_&lt;plateId&gt;</c> for each PlateDef, <c>WeakPoint</c>, <c>Face</c>, and per bot <c>Muzzle</c> (drone,
    /// spotlight), <c>Nozzle</c> (floor-care unit), <c>Lamp</c> (spotlight), <c>Rail</c> and <c>MineDrop</c> (gondola)), else
    /// from primitives named by the same contract, in CONCIERGE white and gold. Then every collider under a plate gets a
    /// <see cref="SkylineHitZone"/> with <c>zone = Plate</c> and its index, under <c>WeakPoint</c> <c>zone = WeakPoint</c>, the
    /// rest <c>zone = Body</c>. A model's renderers without colliders get box colliders.
    /// <para>The gondola's contract (Task 15 ruling 1): the model's origin is the point riding the rail (the gondola's centre in
    /// the pure GondolaRail; mines are dropped there); <c>Rail</c> is the trolley on the visible rail bar, above and toward the
    /// wall (the model faces +Z, away from the wall); <c>MineDrop</c> is where a mine leaves the platform on its way to the
    /// wall.</para>
    /// </summary>
    public static class SkylineBotBodies
    {
        const string ModelFolder = "SkylineModels/bots/";
        static readonly HashSet<string> _logged = new HashSet<string>();

        public static SkylineBotParts Build(EnemyDef def, Transform root, MonoBehaviour owner)
        {
            // a body built ahead at load (judge round 2, perf: no Instantiate, colliders or hit zones on the frame a fight starts)
            var pooled = SkylineBodyPool.Take(def, root, owner);
            if (pooled != null) return pooled;
            var parts = new SkylineBotParts();
            var model = Resources.Load<GameObject>(ModelFolder + def.id);
            if (model != null)
            {
                parts.fromModel = true;
                FromModel(def, root, model, parts);
            }
            else
            {
                LogOnce("SKYLINE_BOT missing model " + def.id + " (primitives)");
                parts.rig = SkylineBotLook.Empty("Rig", root, Vector3.zero);
                switch (def.id)
                {
                    case SkylineEnemies.FloorCareId: FloorCare(def, parts); break;
                    case SkylineEnemies.SpotlightId: Spotlight(def, root, parts); break;
                    case SkylineEnemies.GondolaId: Gondola(def, parts); break;
                    default: Drone(def, parts); break;
                }
            }
            AssignZones(def, parts, owner);
            return parts;
        }

        /// <summary>A model body built ahead under <paramref name="holder"/> for <see cref="SkylineBodyPool"/>, exactly as
        /// <see cref="Build"/> builds it (the model, its box colliders, the weak point cleared, the hit zones) but with no owner
        /// yet; null when the bot has no model (the primitive bodies are cheap and stay built on spawn).</summary>
        public static SkylineBotParts BuildAhead(EnemyDef def, Transform holder)
        {
            var model = Resources.Load<GameObject>(ModelFolder + def.id);
            if (model == null) return null;
            var parts = new SkylineBotParts { fromModel = true };
            FromModel(def, holder, model, parts);
            AssignZones(def, parts, null);
            return parts;
        }

        public static void LogOnce(string line)
        {
            if (_logged.Add(line)) Debug.Log(line);
        }

        // ---------------- model ----------------

        static void FromModel(EnemyDef def, Transform root, GameObject model, SkylineBotParts p)
        {
            var go = Object.Instantiate(model, root, false);
            go.name = "Rig";
            p.rig = go.transform;
            int n = def.plates != null ? def.plates.Length : 0;
            p.plates = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                p.plates[i] = Find(p.rig, "Plate_" + def.plates[i].id);
                if (p.plates[i] == null) LogOnce("SKYLINE_BOT model " + def.id + " lacks Plate_" + def.plates[i].id);
            }
            p.weakPoint = Need(def, p.rig, "WeakPoint");
            p.face = Need(def, p.rig, "Face");
            p.muzzle = Find(p.rig, "Muzzle");
            p.nozzle = Find(p.rig, "Nozzle");
            p.lamp = Find(p.rig, "Lamp");
            p.rail = Find(p.rig, "Rail");
            p.mineDrop = Find(p.rig, "MineDrop");
            // the moving bits, named as the primitive bodies name them (Task 20): the drone's "Rotors" (each child spins
            // about its own local Y), the floor-care unit's "Brush" (spins about its local Y), the gondola's "SqueegeeArm"
            p.spinner = Find(p.rig, "Rotors");
            if (p.spinner == null) p.spinner = Find(p.rig, "Brush");
            p.squeegee = Find(p.rig, "SqueegeeArm");
            // a missing moving part would fail silently (the floor-care unit would lose half its telegraph): logged once per bot
            if (def.id == SkylineEnemies.DroneId && (p.spinner == null || p.spinner.name != "Rotors")) LogOnce("SKYLINE_BOT model " + def.id + " lacks Rotors");
            if (def.id == SkylineEnemies.FloorCareId && (p.spinner == null || p.spinner.name != "Brush")) LogOnce("SKYLINE_BOT model " + def.id + " lacks Brush");
            if (def.id == SkylineEnemies.GondolaId && p.squeegee == null) LogOnce("SKYLINE_BOT model " + def.id + " lacks SqueegeeArm");
            if ((def.id == SkylineEnemies.DroneId || def.id == SkylineEnemies.SpotlightId) && p.muzzle == null) p.muzzle = Need(def, p.rig, "Muzzle");
            if (def.id == SkylineEnemies.FloorCareId && p.nozzle == null) p.nozzle = Need(def, p.rig, "Nozzle");
            if (def.id == SkylineEnemies.SpotlightId && p.lamp == null) p.lamp = Need(def, p.rig, "Lamp");
            if (def.id == SkylineEnemies.GondolaId)
            {
                if (p.rail == null) p.rail = Need(def, p.rig, "Rail");
                if (p.mineDrop == null) p.mineDrop = Need(def, p.rig, "MineDrop");
            }
            p.centerLocal = def.id == SkylineEnemies.FloorCareId ? new Vector3(0f, FightSim.FloorUnitCenter, 0f) : Vector3.zero;
            foreach (var r in p.rig.GetComponentsInChildren<Renderer>(true))
                if (r.GetComponent<Collider>() == null && r is MeshRenderer) r.gameObject.AddComponent<BoxCollider>();
            // a fixed stand (Task 20, the spotlight's): Resources/SkylineModels/bots/<id>_mount, when there is one, sits under
            // the root beside the rig and never turns (the rig pitches and yaws); it is hit as the body, as the primitive mount
            var mountModel = Resources.Load<GameObject>(ModelFolder + def.id + "_mount");
            if (mountModel != null)
            {
                var mount = Object.Instantiate(mountModel, root, false);
                mount.name = "Mount";
                p.mount = mount.transform;
                foreach (var r in p.mount.GetComponentsInChildren<Renderer>(true))
                    if (r.GetComponent<Collider>() == null && r is MeshRenderer) r.gameObject.AddComponent<BoxCollider>();
            }
            ClearWeakPoint(p);
        }

        /// <summary>
        /// Keeps the weak point shootable (judge round 1: no Writ shot could reach the drone's lens, so the spot lesson
        /// failed). A box collider is a mesh's bounds, and a round part's box swallows what stands proud of its true surface:
        /// the drone's egg got a box reaching z 0.374 with the lens (front at 0.337) inside it, so every shot at the lens hit
        /// the body. Any body or plate box that holds the weak point's centre has its face on the weak point's side pulled
        /// back to the weak point's inner face, so the weak point's outer half stands clear. Worked in each box's own frame;
        /// <c>tools/skyline/bots/weakpoint_reach.py</c> mirrors this rule and ray-tests every bot's front arc.
        /// </summary>
        static void ClearWeakPoint(SkylineBotParts p)
        {
            if (p.weakPoint == null) return;
            var corners = new List<Vector3>();
            foreach (var mf in p.weakPoint.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                    corners.Add(mf.transform.TransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            }
            if (corners.Count == 0) return;
            Vector3 wc = Vector3.zero;
            {
                Vector3 lo = corners[0], hi = corners[0];
                for (int i = 1; i < corners.Count; i++) { lo = Vector3.Min(lo, corners[i]); hi = Vector3.Max(hi, corners[i]); }
                wc = (lo + hi) * 0.5f;
            }
            var boxes = new List<BoxCollider>(p.rig.GetComponentsInChildren<BoxCollider>(true));
            if (p.mount != null) boxes.AddRange(p.mount.GetComponentsInChildren<BoxCollider>(true));
            foreach (var box in boxes)
            {
                if (box.transform.IsChildOf(p.weakPoint)) continue;
                var t = box.transform;
                Vector3 c = t.InverseTransformPoint(wc);
                Vector3 bmin = box.center - box.size * 0.5f, bmax = box.center + box.size * 0.5f;
                if (c.x < bmin.x || c.x > bmax.x || c.y < bmin.y || c.y > bmax.y || c.z < bmin.z || c.z > bmax.z) continue;
                // the weak point's bounds in this box's frame
                Vector3 wlo = t.InverseTransformPoint(corners[0]), whi = wlo;
                for (int i = 1; i < corners.Count; i++)
                {
                    Vector3 q = t.InverseTransformPoint(corners[i]);
                    wlo = Vector3.Min(wlo, q);
                    whi = Vector3.Max(whi, q);
                }
                // the axis the weak point sits furthest out along (relative to the box's half size)
                int a = 0;
                float best = -1f;
                for (int k = 0; k < 3; k++)
                {
                    float r = Mathf.Abs(c[k] - box.center[k]) / Mathf.Max(1e-6f, box.size[k] * 0.5f);
                    if (r > best) { best = r; a = k; }
                }
                if (c[a] >= box.center[a]) bmax[a] = Mathf.Max(bmin[a] + MinBoxThickness, wlo[a]);
                else bmin[a] = Mathf.Min(bmax[a] - MinBoxThickness, whi[a]);
                box.center = (bmin + bmax) * 0.5f;
                box.size = bmax - bmin;
            }
        }

        const float MinBoxThickness = 0.02f;

        static Transform Need(EnemyDef def, Transform rig, string name)
        {
            var t = Find(rig, name);
            if (t != null) return t;
            LogOnce("SKYLINE_BOT model " + def.id + " lacks " + name);
            return SkylineBotLook.Empty(name, rig, Vector3.zero);
        }

        public static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = Find(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        // ---------------- zones ----------------

        static void AssignZones(EnemyDef def, SkylineBotParts p, MonoBehaviour owner)
        {
            foreach (var c in p.rig.GetComponentsInChildren<Collider>(true)) Zone(c, owner, HitZone.Body, -1);
            if (p.mount != null) foreach (var c in p.mount.GetComponentsInChildren<Collider>(true)) Zone(c, owner, HitZone.Body, -1);
            if (p.weakPoint != null) foreach (var c in p.weakPoint.GetComponentsInChildren<Collider>(true)) Zone(c, owner, HitZone.WeakPoint, -1);
            if (p.plates != null)
                for (int i = 0; i < p.plates.Length; i++)
                    if (p.plates[i] != null)
                        foreach (var c in p.plates[i].GetComponentsInChildren<Collider>(true)) Zone(c, owner, HitZone.Plate, i);
            p.bodyRenderers.Clear();
            foreach (var r in p.rig.GetComponentsInChildren<Renderer>(true))
            {
                var z = r.GetComponentInParent<SkylineHitZone>();
                if (z == null || z.zone == HitZone.Body) p.bodyRenderers.Add(r);
            }
            if (p.mount != null) p.bodyRenderers.AddRange(p.mount.GetComponentsInChildren<Renderer>(true));
        }

        static void Zone(Collider c, MonoBehaviour owner, HitZone zone, int plate)
        {
            var z = c.GetComponent<SkylineHitZone>();
            if (z == null) z = c.gameObject.AddComponent<SkylineHitZone>();
            z.owner = owner;
            z.zone = zone;
            z.plate = plate;
        }

        // ---------------- primitives ----------------

        static void Plates(EnemyDef def, SkylineBotParts p) =>
            p.plates = new Transform[def.plates != null ? def.plates.Length : 0];

        static void SetPlate(EnemyDef def, SkylineBotParts p, int i, GameObject go)
        {
            if (p.plates == null || i >= p.plates.Length) return;
            go.name = "Plate_" + def.plates[i].id;
            p.plates[i] = go.transform;
        }

        static Transform FaceQuad(Transform parent, Vector3 pos, float w, float h)
        {
            // A Quad faces -Z; turned round so it reads from the front (+Z).
            var q = SkylineBotLook.Part(PrimitiveType.Quad, "Face", parent, pos, new Vector3(w, h, 1f), Color.white, false, new Vector3(0f, 180f, 0f));
            return q.transform;
        }

        /// <summary>Mall-cop drone: an egg-shaped chassis with a gold cap (shell) and a gold brow (lens-guard), its face on a
        /// screen, the camera lens below it (the weak point), the gun under that, rotors either side. Centre at the origin.</summary>
        static void Drone(EnemyDef def, SkylineBotParts p)
        {
            var rig = p.rig;
            Plates(def, p);
            SkylineBotLook.Part(PrimitiveType.Sphere, "Chassis", rig, Vector3.zero, new Vector3(0.72f, 0.58f, 0.72f), SkylineBotLook.White);
            SetPlate(def, p, 0, SkylineBotLook.Part(PrimitiveType.Sphere, "Shell", rig, new Vector3(0f, 0.17f, -0.04f), new Vector3(0.76f, 0.34f, 0.74f), SkylineBotLook.Gold));
            SetPlate(def, p, 1, SkylineBotLook.Part(PrimitiveType.Cube, "LensGuard", rig, new Vector3(0f, 0.2f, 0.3f), new Vector3(0.44f, 0.08f, 0.14f), SkylineBotLook.Gold));
            SkylineBotLook.Part(PrimitiveType.Cube, "Screen", rig, new Vector3(0f, 0.04f, 0.3f), new Vector3(0.38f, 0.24f, 0.08f), SkylineBotLook.Dark);
            p.face = FaceQuad(rig, new Vector3(0f, 0.04f, 0.341f), 0.34f, 0.2f);
            p.weakPoint = SkylineBotLook.Part(PrimitiveType.Cylinder, "WeakPoint", rig, new Vector3(0f, -0.17f, 0.29f), new Vector3(0.15f, 0.03f, 0.15f), SkylineBotLook.Lens, true, new Vector3(90f, 0f, 0f)).transform;
            SkylineBotLook.Part(PrimitiveType.Cylinder, "Barrel", rig, new Vector3(0f, -0.27f, 0.26f), new Vector3(0.06f, 0.11f, 0.06f), SkylineBotLook.Dark, false, new Vector3(90f, 0f, 0f));
            p.muzzle = SkylineBotLook.Empty("Muzzle", rig, new Vector3(0f, -0.27f, 0.4f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Arms", rig, new Vector3(0f, 0.08f, 0f), new Vector3(1.15f, 0.05f, 0.09f), SkylineBotLook.Grey);
            var rotors = SkylineBotLook.Empty("Rotors", rig, new Vector3(0f, 0.13f, 0f));
            SkylineBotLook.Part(PrimitiveType.Cylinder, "RotorL", rotors, new Vector3(-0.58f, 0f, 0f), new Vector3(0.34f, 0.008f, 0.34f), SkylineBotLook.Grey, false);
            SkylineBotLook.Part(PrimitiveType.Cylinder, "RotorR", rotors, new Vector3(0.58f, 0f, 0f), new Vector3(0.34f, 0.008f, 0.34f), SkylineBotLook.Grey, false);
            p.spinner = rotors;
            p.centerLocal = Vector3.zero;
        }

        /// <summary>Floor-care unit: a roomba disc with a gold lid and a gold front bumper, a raised screen with its face, the
        /// acid tank behind (the weak point), the nozzle low at the front. Origin on the floor; centre 0.4 m up.</summary>
        static void FloorCare(EnemyDef def, SkylineBotParts p)
        {
            var rig = p.rig;
            Plates(def, p);
            SkylineBotLook.Part(PrimitiveType.Cylinder, "Chassis", rig, new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.12f, 1f), SkylineBotLook.White);
            SetPlate(def, p, 0, SkylineBotLook.Part(PrimitiveType.Cylinder, "Lid", rig, new Vector3(0f, 0.35f, -0.04f), new Vector3(0.84f, 0.035f, 0.84f), SkylineBotLook.Gold));
            SetPlate(def, p, 1, SkylineBotLook.Part(PrimitiveType.Cube, "Bumper", rig, new Vector3(0f, 0.16f, 0.5f), new Vector3(0.96f, 0.16f, 0.14f), SkylineBotLook.Gold));
            SkylineBotLook.Part(PrimitiveType.Cube, "ScreenBlock", rig, new Vector3(0f, 0.52f, 0.28f), new Vector3(0.44f, 0.28f, 0.1f), SkylineBotLook.Dark);
            p.face = FaceQuad(rig, new Vector3(0f, 0.52f, 0.331f), 0.38f, 0.22f);
            p.weakPoint = SkylineBotLook.Part(PrimitiveType.Sphere, "WeakPoint", rig, new Vector3(0f, 0.5f, -0.24f), new Vector3(0.28f, 0.28f, 0.28f), SkylineBotLook.Acid).transform;
            SkylineBotLook.Part(PrimitiveType.Cylinder, "NozzleTip", rig, new Vector3(0f, 0.1f, 0.6f), new Vector3(0.08f, 0.06f, 0.08f), SkylineBotLook.Dark, false, new Vector3(90f, 0f, 0f));
            p.nozzle = SkylineBotLook.Empty("Nozzle", rig, new Vector3(0f, 0.1f, 0.66f));
            var brush = SkylineBotLook.Empty("Brush", rig, new Vector3(0.32f, 0.04f, 0.36f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Bristles", brush, Vector3.zero, new Vector3(0.36f, 0.03f, 0.05f), SkylineBotLook.Grey, false);
            SkylineBotLook.Part(PrimitiveType.Cube, "Bristles2", brush, Vector3.zero, new Vector3(0.05f, 0.03f, 0.36f), SkylineBotLook.Grey, false);
            p.spinner = brush;
            p.centerLocal = new Vector3(0f, FightSim.FloorUnitCenter, 0f);
        }

        /// <summary>Spotlight: a fixed base and yoke (the mount); the turning head is a white drum with gold housings left and
        /// right, the lens at the front (the weak point, and the Lamp the beam leaves from), a face screen on top, the rifle under
        /// it. The origin is the head's centre.</summary>
        static void Spotlight(EnemyDef def, Transform root, SkylineBotParts p)
        {
            var rig = p.rig;
            Plates(def, p);
            var mount = SkylineBotLook.Empty("Mount", root, Vector3.zero);
            p.mount = mount;
            SkylineBotLook.Part(PrimitiveType.Cube, "Base", mount, new Vector3(0f, -0.8f, 0f), new Vector3(0.8f, 0.16f, 0.8f), SkylineBotLook.Grey);
            SkylineBotLook.Part(PrimitiveType.Cube, "YokeL", mount, new Vector3(-0.56f, -0.42f, 0f), new Vector3(0.08f, 0.66f, 0.12f), SkylineBotLook.White);
            SkylineBotLook.Part(PrimitiveType.Cube, "YokeR", mount, new Vector3(0.56f, -0.42f, 0f), new Vector3(0.08f, 0.66f, 0.12f), SkylineBotLook.White);
            SkylineBotLook.Part(PrimitiveType.Cylinder, "Drum", rig, Vector3.zero, new Vector3(0.8f, 0.45f, 0.8f), SkylineBotLook.White, true, new Vector3(90f, 0f, 0f));
            SetPlate(def, p, 0, SkylineBotLook.Part(PrimitiveType.Cube, "HousingLeft", rig, new Vector3(-0.45f, 0f, 0f), new Vector3(0.1f, 0.62f, 0.72f), SkylineBotLook.Gold));
            SetPlate(def, p, 1, SkylineBotLook.Part(PrimitiveType.Cube, "HousingRight", rig, new Vector3(0.45f, 0f, 0f), new Vector3(0.1f, 0.62f, 0.72f), SkylineBotLook.Gold));
            p.weakPoint = SkylineBotLook.Part(PrimitiveType.Cylinder, "WeakPoint", rig, new Vector3(0f, 0f, 0.46f), new Vector3(0.6f, 0.02f, 0.6f), new Color(0.95f, 0.93f, 0.75f), true, new Vector3(90f, 0f, 0f)).transform;
            p.lamp = SkylineBotLook.Empty("Lamp", rig, new Vector3(0f, 0f, 0.5f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Visor", rig, new Vector3(0f, 0.48f, 0.12f), new Vector3(0.42f, 0.26f, 0.08f), SkylineBotLook.Dark);
            p.face = FaceQuad(rig, new Vector3(0f, 0.48f, 0.161f), 0.38f, 0.22f);
            SkylineBotLook.Part(PrimitiveType.Cube, "Rifle", rig, new Vector3(0f, -0.47f, 0.25f), new Vector3(0.08f, 0.08f, 0.62f), SkylineBotLook.Dark);
            p.muzzle = SkylineBotLook.Empty("Muzzle", rig, new Vector3(0f, -0.47f, 0.58f));
            p.centerLocal = Vector3.zero;
        }

        /// <summary>Window-washer gondola: a platform with a gold bucket at each end (the plates), a control box with the face,
        /// cables up to the trolley (<c>Rail</c>) on the rail bar, the winch under the trolley (the weak point), a squeegee on an
        /// arm, and <c>MineDrop</c> under the platform's wall side. The origin is the point on the rail line (the pure position);
        /// the model faces +Z, away from the wall.</summary>
        static void Gondola(EnemyDef def, SkylineBotParts p)
        {
            var rig = p.rig;
            Plates(def, p);
            SkylineBotLook.Part(PrimitiveType.Cube, "Platform", rig, new Vector3(0f, -0.55f, 0.2f), new Vector3(2.2f, 0.12f, 0.6f), SkylineBotLook.White);
            SkylineBotLook.Part(PrimitiveType.Cube, "RailingFront", rig, new Vector3(0f, -0.05f, 0.48f), new Vector3(2.2f, 0.06f, 0.05f), SkylineBotLook.Grey);
            SkylineBotLook.Part(PrimitiveType.Cube, "PostL", rig, new Vector3(-1.07f, -0.3f, 0.48f), new Vector3(0.05f, 0.5f, 0.05f), SkylineBotLook.Grey, false);
            SkylineBotLook.Part(PrimitiveType.Cube, "PostR", rig, new Vector3(1.07f, -0.3f, 0.48f), new Vector3(0.05f, 0.5f, 0.05f), SkylineBotLook.Grey, false);
            SetPlate(def, p, 0, SkylineBotLook.Part(PrimitiveType.Cube, "BucketLeft", rig, new Vector3(-0.82f, -0.3f, 0.2f), new Vector3(0.4f, 0.38f, 0.38f), SkylineBotLook.Gold));
            SetPlate(def, p, 1, SkylineBotLook.Part(PrimitiveType.Cube, "BucketRight", rig, new Vector3(0.82f, -0.3f, 0.2f), new Vector3(0.4f, 0.38f, 0.38f), SkylineBotLook.Gold));
            SkylineBotLook.Part(PrimitiveType.Cube, "ControlBox", rig, new Vector3(0f, -0.25f, 0.3f), new Vector3(0.5f, 0.42f, 0.16f), SkylineBotLook.Dark);
            p.face = FaceQuad(rig, new Vector3(0f, -0.25f, 0.381f), 0.44f, 0.26f);
            p.rail = SkylineBotLook.Empty("Rail", rig, new Vector3(0f, 1.3f, -0.55f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Trolley", p.rail, Vector3.zero, new Vector3(0.5f, 0.16f, 0.22f), SkylineBotLook.Grey);
            p.weakPoint = SkylineBotLook.Part(PrimitiveType.Sphere, "WeakPoint", rig, new Vector3(0f, 1.0f, -0.42f), new Vector3(0.32f, 0.32f, 0.32f), SkylineBotLook.Lens).transform;
            Cable(rig, new Vector3(-1.0f, -0.5f, 0.0f), new Vector3(-0.2f, 1.25f, -0.55f));
            Cable(rig, new Vector3(1.0f, -0.5f, 0.0f), new Vector3(0.2f, 1.25f, -0.55f));
            var arm = SkylineBotLook.Empty("SqueegeeArm", rig, new Vector3(0.45f, -0.05f, 0.45f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Pole", arm, new Vector3(0f, 0.45f, 0f), new Vector3(0.04f, 0.9f, 0.04f), SkylineBotLook.Grey, false);
            SkylineBotLook.Part(PrimitiveType.Cube, "Blade", arm, new Vector3(0f, 0.92f, 0f), new Vector3(0.5f, 0.06f, 0.06f), SkylineBotLook.Dark, false);
            p.squeegee = arm;
            p.mineDrop = SkylineBotLook.Empty("MineDrop", rig, new Vector3(0f, -0.65f, -0.15f));
            p.centerLocal = new Vector3(0f, -0.2f, 0.2f);
        }

        static void Cable(Transform rig, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            var c = SkylineBotLook.Part(PrimitiveType.Cube, "Cable", rig, (a + b) * 0.5f, new Vector3(0.025f, 0.025f, d.magnitude), SkylineBotLook.Dark, false);
            c.transform.localRotation = Quaternion.LookRotation(d.normalized);
        }
    }
}
