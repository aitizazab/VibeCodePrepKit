using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// A memory as a paper cut-out (docs/REDESIGN.md section 7; docs/redesign/people.md "Billboards"): Lena, Theo, Walt,
    /// the neighbours, the dog. The two real minds (Nadia, Elias) are solid; everyone else in the dream is card.
    ///
    /// The card is real paper: its picture on the front, mid-grey board on the back, and an edge of real thickness cut
    /// along the figure's outline (traced from the picture's alpha once per picture and shared by every cut-out that uses
    /// it). The shader (JamKit/Dream/Cutout) lights it with the scene's sun and lamps, casts and receives shadows, lets
    /// about 30% of the light through when backlit, prints an overlay over the picture where one exists (Lena's star
    /// watch), and has a faint fibre grain and a hand-drawn "boil" (the picture shifts by a hair a few times a second).
    /// Paper meshes are prewarmed after every scene load (<see cref="Prewarm"/>).
    ///
    /// Alive: a slow sway and breath about its feet. Facing: it stands facing <see cref="restYaw"/>; once you come within
    /// <see cref="noticeDistance"/> it turns about its feet to watch you, a beat late, and keeps watching after you've
    /// passed. Poses: <see cref="Flip"/> turns the card edge-on, swaps the picture and turns it back (a paper flip).
    ///
    /// Builders make one with <see cref="Create"/>, or (the older code) a Quad child named "Card" with the cut-out
    /// material and this component pointing at it: the paper is built from that card whenever the component is enabled
    /// (in edit mode too, as a DontSave child, so scenes are saved with only the quad and captures still see the paper).
    /// No per-frame allocation.
    /// </summary>
    [ExecuteAlways]
    public sealed class DreamCutoutView : MonoBehaviour
    {
        public const string ShaderName = "JamKit/Dream/Cutout";

        public Transform card;
        public float noticeDistance = 32f;
        [Tooltip("Degrees per second at most.")]
        public float turnSpeed = 140f;
        [Tooltip("Seconds it takes to notice you.")]
        public float noticeDelay = 0.35f;
        public float restYaw;

        [Header("Paper")]
        [Tooltip("Card thickness, metres (a person-sized cut-out reads as foam board at about 1.6 cm).")]
        public float thickness = 0.016f;
        [Tooltip("Sway: degrees of roll about the feet.")]
        public float sway = 0.9f;
        [Tooltip("Breath: a fraction of the height.")]
        public float breath = 0.006f;

        Transform _target;
        float _yaw, _vel, _noticed = -1f, _phase;
        Vector3 _baseScale;
        bool _haveBase;
        GameObject _paper;
        MeshFilter _paperFilter;
        MeshRenderer _paperRenderer;
        MaterialPropertyBlock _block;
        Texture _picture;
        // The flip: -1 = none; else 0..1 over _flipSeconds; the picture swaps half-way, when the card is edge-on.
        float _flip = -1f, _flipSeconds = 0.32f;
        Texture _flipTo;
        bool _flipSwapped;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly int BackColorId = Shader.PropertyToID("_BackColor");
        static readonly int TranslucencyId = Shader.PropertyToID("_Translucency");
        static readonly int ShowThroughId = Shader.PropertyToID("_ShowThrough");
        static readonly int OverlayId = Shader.PropertyToID("_Overlay");
        static readonly int OverlayOnId = Shader.PropertyToID("_OverlayOn");
        static readonly Dictionary<Texture, Mesh> Meshes = new Dictionary<Texture, Mesh>();
        static readonly Dictionary<string, Texture2D> Overlays = new Dictionary<string, Texture2D>();

        /// <summary>The card's back: mid-grey board (the judge, round 1: the cream back "glowed like a peach ghost").
        /// Forced through the property block, so saved materials with the old values follow.</summary>
        public static readonly Color CardBack = new Color(0.5f, 0.49f, 0.47f, 1f);
        /// <summary>Light through the card when backlit: about 30% (thick card, not tracing paper).</summary>
        public const float Translucency = 0.3f;
        /// <summary>Overlays printed over a picture, by the picture's name: Resources/People/Overlays/&lt;picture&gt;.png, the
        /// same size as the picture, transparent except what it changes (Lena's star watch over the round one in her
        /// drawing). The drawings themselves are not touched.</summary>
        public const string OverlayFolder = "People/Overlays/";

        /// <summary>The cut-out's yaw now (degrees).</summary>
        public float Yaw => _yaw;
        /// <summary>The picture showing now.</summary>
        public Texture Picture => _picture;
        public bool IsFlipping => _flip >= 0f;
        /// <summary>The generated paper (null until built, or without a GPU).</summary>
        public MeshRenderer PaperRenderer => _paperRenderer;

        // ---------------------------------------------------------------------------------------------------------
        // Building
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A cut-out standing at <paramref name="feet"/>, <paramref name="height"/> metres tall (the picture's aspect gives
        /// its width), facing <paramref name="restYaw"/> (degrees about Y; 0 faces +Z). <paramref name="material"/> is a
        /// material on the JamKit/Dream/Cutout shader with the picture as _MainTex (editor builders pass their saved asset);
        /// null makes one at runtime from <paramref name="picture"/>.
        /// </summary>
        public static DreamCutoutView Create(string name, Texture2D picture, Transform parent, Vector3 feet, float height,
            float restYaw = 180f, Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            go.transform.rotation = Quaternion.Euler(0f, restYaw, 0f);
            var cardGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cardGo.name = "Card";
            var col = cardGo.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }
            cardGo.transform.SetParent(go.transform, false);
            cardGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the quad's front toward the cut-out's +Z
            float aspect = picture != null && picture.height > 0 ? picture.width / (float)picture.height : 0.5f;
            cardGo.transform.localScale = new Vector3(height * aspect, height, 1f);
            cardGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName)) { name = "Cutout_" + (picture != null ? picture.name : name) };
                material.SetTexture(MainTexId, picture);
            }
            var r = cardGo.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);   // so OnEnable sees the card
            var view = go.AddComponent<DreamCutoutView>();
            view.restYaw = restYaw;
            view.card = cardGo.transform;
            go.SetActive(true);
            return view;
        }

        void OnEnable()
        {
            _phase = transform.position.x * 1.7f + transform.position.z * 0.31f;
            Rebuild();
        }

        void OnDisable()
        {
            if (_paper != null)
            {
                if (Application.isPlaying) Destroy(_paper); else DestroyImmediate(_paper);
                _paper = null;
                _paperFilter = null;
                _paperRenderer = null;
            }
            var r = card != null ? card.GetComponent<MeshRenderer>() : null;
            if (r != null) r.forceRenderingOff = false;
        }

        /// <summary>(Re)builds the paper from the card: its picture's outline, extruded to <see cref="thickness"/>.</summary>
        public void Rebuild()
        {
            if (card == null) return;
            var cardRenderer = card.GetComponent<MeshRenderer>();
            if (cardRenderer == null || cardRenderer.sharedMaterial == null) return;
            if (!_haveBase) { _baseScale = card.localScale; _haveBase = true; }
            if (_picture == null) _picture = cardRenderer.sharedMaterial.GetTexture(MainTexId);
            var mesh = PaperMesh(_picture, thickness);
            if (mesh == null) return;   // no GPU (batch mode without graphics): the plain quad stays
            if (_paper == null)
            {
                _paper = new GameObject("Paper") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
                _paper.transform.SetParent(card, false);
                _paperFilter = _paper.AddComponent<MeshFilter>();
                _paperRenderer = _paper.AddComponent<MeshRenderer>();
            }
            _paperFilter.sharedMesh = mesh;
            _paperRenderer.sharedMaterial = cardRenderer.sharedMaterial;
            _paperRenderer.shadowCastingMode = ShadowCastingMode.On;
            _paperRenderer.receiveShadows = true;
            _paperRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            cardRenderer.forceRenderingOff = true;
            ApplyBlock();
            Apply(0f);
        }

        void ApplyBlock()
        {
            if (_paperRenderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _paperRenderer.GetPropertyBlock(_block);
            if (_picture != null) _block.SetTexture(MainTexId, _picture);
            _block.SetFloat(PhaseId, Mathf.Repeat(_phase * 0.37f, 1f));
            _block.SetColor(BackColorId, CardBack);
            _block.SetFloat(TranslucencyId, Translucency);
            _block.SetFloat(ShowThroughId, 0.06f);
            var overlay = OverlayFor(_picture);
            _block.SetTexture(OverlayId, overlay != null ? overlay : Texture2D.whiteTexture);
            _block.SetFloat(OverlayOnId, overlay != null ? 1f : 0f);
            _paperRenderer.SetPropertyBlock(_block);
        }

        /// <summary>The overlay for a picture (Resources/People/Overlays/&lt;name&gt;), or null; looked up once per name.</summary>
        public static Texture2D OverlayFor(Texture picture)
        {
            if (picture == null) return null;
            if (Overlays.TryGetValue(picture.name, out var o)) return o;
            o = Resources.Load<Texture2D>(OverlayFolder + picture.name);
            Overlays[picture.name] = o;
            return o;
        }

        /// <summary>Builds every paper mesh now, so no cut-out traces its outline (a GPU read-back and marching squares)
        /// in the middle of play: the cut-outs in the loaded scenes (inactive ones too) and any extra pictures (flip
        /// poses). Runs by itself after each scene load; builders may call it again behind a loading screen.</summary>
        public static int Prewarm(IList<Texture> extraPictures = null)
        {
            int n = 0;
            foreach (var v in Object.FindObjectsByType<DreamCutoutView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (v == null || v.card == null) continue;
                var r = v.card.GetComponent<MeshRenderer>();
                var pic = v._picture != null ? v._picture : r != null && r.sharedMaterial != null ? r.sharedMaterial.GetTexture(MainTexId) : null;
                if (pic != null && !Meshes.ContainsKey(pic) && PaperMesh(pic, v.thickness) != null) n++;
                OverlayFor(pic);
            }
            if (extraPictures != null)
                foreach (var pic in extraPictures)
                    if (pic != null && !Meshes.ContainsKey(pic) && PaperMesh(pic, 0.016f) != null) n++;
            return n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PrewarmOnLoad()
        {
            Prewarm();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { Prewarm(); }

        // ---------------------------------------------------------------------------------------------------------
        // Behaviour
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>What it watches (the player's camera); null finds the main camera.</summary>
        public void Watch(Transform target) => _target = target;

        /// <summary>A paper flip to another picture (a pose: waving, pointing, the notebook): the card turns edge-on, the
        /// picture swaps, the card turns back and settles. Draw poses at the same height in their frame.</summary>
        public void Flip(Texture2D picture, float seconds = 0.32f)
        {
            if (picture == null) return;
            _flipTo = picture;
            _flipSeconds = Mathf.Max(0.08f, seconds);
            _flip = 0f;
            _flipSwapped = false;
        }

        void Start()
        {
            _yaw = Application.isPlaying ? restYaw : transform.eulerAngles.y;
            if (Application.isPlaying) transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        void LateUpdate()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0f;
            if (Application.isPlaying)
            {
                if (_target == null)
                {
                    var cam = Camera.main;
                    if (cam != null) _target = cam.transform;
                }
                float want = restYaw;
                if (_target != null)
                {
                    Vector3 d = _target.position - transform.position;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist < noticeDistance)
                    {
                        if (_noticed < 0f) _noticed = 0f;
                        _noticed += dt;
                    }
                    if (_noticed >= noticeDelay && dist > 0.01f) want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
                if (dt > 0f) _yaw = Mathf.SmoothDampAngle(_yaw, want, ref _vel, 0.28f, turnSpeed, dt);
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            else _yaw = transform.eulerAngles.y;   // edit mode: whoever placed it decides
            Apply(dt);
        }

        void Apply(float dt)
        {
            if (card == null) return;
            if (!_haveBase) { _baseScale = card.localScale; _haveBase = true; }
            // Edit mode stays neutral (the builder's transform exactly), so a saved scene never keeps a frozen sway.
            bool live = Application.isPlaying;
            float t = live ? Time.time : 0f;
            float roll = live ? sway * Mathf.Sin(t * 0.83f + _phase) + sway * 0.35f * Mathf.Sin(t * 1.91f + _phase * 2.3f) : 0f;
            float breathe = live ? 1f + breath * Mathf.Sin(t * 1.9f + _phase) : 1f;
            float flipYaw = 0f;
            if (_flip >= 0f)
            {
                _flip += dt / _flipSeconds;
                if (!_flipSwapped && _flip >= 0.5f)
                {
                    _flipSwapped = true;
                    _picture = _flipTo;
                    var mesh = PaperMesh(_picture, thickness);
                    if (mesh != null && _paperFilter != null) _paperFilter.sharedMesh = mesh;
                    float aspect = _picture.height > 0 ? _picture.width / (float)_picture.height : 0.5f;
                    _baseScale = new Vector3(_baseScale.y * aspect, _baseScale.y, _baseScale.z);
                    ApplyBlock();
                }
                if (_flip >= 1f) _flip = -1f;
                else
                {
                    // Ease to edge-on, swap, ease back with a small overshoot (the paper settling).
                    float k = _flip < 0.5f ? _flip * 2f : 2f - _flip * 2f;
                    flipYaw = 88f * Mathf.SmoothStep(0f, 1f, k);
                    if (_flip > 0.5f) flipYaw -= 7f * Mathf.Sin((_flip - 0.5f) * 2f * Mathf.PI);
                }
            }
            var s = new Vector3(_baseScale.x, _baseScale.y * breathe, _baseScale.z);
            card.localScale = s;
            // The card pivots about its feet: the half-height offset turns with the roll.
            var rot = Quaternion.Euler(0f, flipYaw, roll);
            card.localRotation = rot * Quaternion.Euler(0f, 180f, 0f);
            card.localPosition = rot * new Vector3(0f, s.y * 0.5f, 0f);
        }

        // ---------------------------------------------------------------------------------------------------------
        // The paper mesh: front, back and a cut edge along the picture's outline
        // ---------------------------------------------------------------------------------------------------------

        const int GridH = 192;

        /// <summary>The paper mesh for a picture (cached per picture), in the card quad's space (x, y in -0.5..0.5; the
        /// front faces -Z like Unity's quad), <paramref name="thickness"/> metres deep. The part is in uv2.x: 0 = the
        /// picture, 1 = the back, 2 = the edge. Null without a GPU.</summary>
        public static Mesh PaperMesh(Texture picture, float thickness)
        {
            if (picture == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            if (Meshes.TryGetValue(picture, out var cached) && cached != null) return cached;
            int h = GridH;
            int w = Mathf.Clamp(Mathf.RoundToInt(GridH * picture.width / (float)Mathf.Max(1, picture.height)), 8, 512);
            var alpha = ReadAlpha(picture, w, h);
            if (alpha == null) return null;
            var mesh = BuildPaper(alpha, w, h, thickness);
            mesh.name = "Paper (" + picture.name + ")";
            mesh.hideFlags = HideFlags.DontSave;
            Meshes[picture] = mesh;
            return mesh;
        }

        /// <summary>The picture's alpha at w x h, read back through the GPU (the picture need not be readable).</summary>
        static float[] ReadAlpha(Texture picture, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(picture, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                tmp.Apply(false);
                var px = tmp.GetPixels32();
                if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
                var a = new float[w * h];
                for (int i = 0; i < a.Length; i++) a[i] = px[i].a / 255f;
                return a;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Marching squares on the alpha grid (threshold 0.5): every outline segment becomes a quad between the
        /// front and back faces, its normal facing out of the figure. The front and back are whole quads (the shader clips
        /// them by the picture's alpha at the same threshold), so faces and edge meet along the outline.</summary>
        static Mesh BuildPaper(float[] a, int w, int h, float thickness)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var part = new List<Vector2>();
            var tris = new List<int>();
            float zf = -thickness * 0.5f, zb = thickness * 0.5f;

            void Face(float z, Vector3 normal, float p, bool flip)
            {
                int b = v.Count;
                v.Add(new Vector3(-0.5f, -0.5f, z)); v.Add(new Vector3(0.5f, -0.5f, z));
                v.Add(new Vector3(0.5f, 0.5f, z)); v.Add(new Vector3(-0.5f, 0.5f, z));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                for (int i = 0; i < 4; i++) { n.Add(normal); part.Add(new Vector2(p, 0f)); }
                if (!flip) { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                else { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
            }
            Face(zf, Vector3.back, 0f, false);     // the picture, facing -Z (as Unity's quad)
            Face(zb, Vector3.forward, 1f, true);   // the back: card stock

            float A(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? 0f : a[y * w + x];
            Vector2 P(float gx, float gy) => new Vector2((gx + 0.5f) / w - 0.5f, (gy + 0.5f) / h - 0.5f);
            Vector2 Cross(int x0, int y0, int x1, int y1)
            {
                float a0 = A(x0, y0), a1 = A(x1, y1);
                float t = Mathf.Abs(a1 - a0) < 1e-5f ? 0.5f : Mathf.Clamp01((0.5f - a0) / (a1 - a0));
                return P(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t));
            }
            void Edge(Vector2 p0, Vector2 p1, int cx, int cy)
            {
                Vector2 d = p1 - p0;
                if (d.sqrMagnitude < 1e-12f) return;
                var nrm = new Vector2(d.y, -d.x).normalized;
                // Outward: away from the inside corner nearest the segment (robust at the saddles).
                Vector2 mid = (p0 + p1) * 0.5f, near = mid;
                float best = float.MaxValue;
                for (int j = 0; j < 4; j++)
                {
                    int xx = cx + (j & 1), yy = cy + (j >> 1);
                    if (A(xx, yy) < 0.5f) continue;
                    var c = P(xx, yy);
                    float dd = (c - mid).sqrMagnitude;
                    if (dd < best) { best = dd; near = c; }
                }
                if (Vector2.Dot(nrm, mid - near) < 0f) nrm = -nrm;
                int b = v.Count;
                v.Add(new Vector3(p0.x, p0.y, zf)); v.Add(new Vector3(p1.x, p1.y, zf));
                v.Add(new Vector3(p1.x, p1.y, zb)); v.Add(new Vector3(p0.x, p0.y, zb));
                var u0 = new Vector2(p0.x + 0.5f, p0.y + 0.5f);
                var u1 = new Vector2(p1.x + 0.5f, p1.y + 0.5f);
                uv.Add(u0); uv.Add(u1); uv.Add(u1); uv.Add(u0);
                var n3 = new Vector3(nrm.x, nrm.y, 0f);
                for (int i = 0; i < 4; i++) { n.Add(n3); part.Add(new Vector2(2f, 0f)); }
                // Wind so the quad faces along n3 (the shader is two-sided anyway; this keeps shadows and normals tidy).
                var faceN = Vector3.Cross(v[b + 1] - v[b], v[b + 2] - v[b]);
                if (Vector3.Dot(faceN, n3) > 0f) { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                else { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
            }

            // Corners: 1 = bottom-left (x, y), 2 = bottom-right, 4 = top-left, 8 = top-right.
            for (int y = -1; y < h; y++)
                for (int x = -1; x < w; x++)
                {
                    int code = (A(x, y) >= 0.5f ? 1 : 0) | (A(x + 1, y) >= 0.5f ? 2 : 0) | (A(x, y + 1) >= 0.5f ? 4 : 0) | (A(x + 1, y + 1) >= 0.5f ? 8 : 0);
                    if (code == 0 || code == 15) continue;
                    Vector2 eB = Cross(x, y, x + 1, y), eR = Cross(x + 1, y, x + 1, y + 1);
                    Vector2 eT = Cross(x, y + 1, x + 1, y + 1), eL = Cross(x, y, x, y + 1);
                    switch (code)
                    {
                        case 1: case 14: Edge(eL, eB, x, y); break;
                        case 2: case 13: Edge(eB, eR, x, y); break;
                        case 3: case 12: Edge(eL, eR, x, y); break;
                        case 4: case 11: Edge(eL, eT, x, y); break;
                        case 5: case 10: Edge(eB, eT, x, y); break;
                        case 7: case 8: Edge(eT, eR, x, y); break;
                        case 6: Edge(eB, eR, x, y); Edge(eL, eT, x, y); break;
                        case 9: Edge(eL, eB, x, y); Edge(eT, eR, x, y); break;
                    }
                }

            var mesh = new Mesh();
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, part);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
