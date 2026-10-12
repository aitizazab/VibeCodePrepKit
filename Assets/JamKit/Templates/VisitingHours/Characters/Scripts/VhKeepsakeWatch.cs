using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.VisitingHours.Characters
{
    /// <summary>
    /// Lena's keepsake (docs/REDESIGN.md section 8): her star-shaped wristwatch, which Nadia wears on her right wrist once
    /// Lena ends "Heard out". Built in code (no asset): a gold five-pointed star case with a bevel, a cream enamel star
    /// face with hour markers and a tiny star at twelve, hour, minute and second hands (each its own pivot, so a later
    /// mechanic can drive them), a centre cap, lugs, and a wine-red leather strap round the wrist.
    ///
    ///   var watch = VhKeepsakeWatch.Attach(nadiaBodyRoot);   // finds the Mixamo right forearm and hand; idempotent
    ///   watch.SetOwned(true);                                 // shows it (false hides it); or VhKeepsakeWatch.Ensure(body, owned)
    ///   watch.SetTime(9, 41);  watch.running = true;  watch.rate = 1;   // the hands; or turn hourHand / minuteHand yourself
    ///
    /// The watch's local frame: +Y out of the face (the back of the wrist), +Z along the forearm toward the fingers; the
    /// child "Head" (case, dial, hands) is turned so twelve points across the wrist, toward the little finger.
    /// Sizes are in metres whatever the body's scale. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhKeepsakeWatch : MonoBehaviour
    {
        public const string ObjectName = "Keepsake watch (Lena's)";

        [Header("Hands")]
        public Transform hourHand;
        public Transform minuteHand;
        public Transform secondHand;
        [Tooltip("Advance the hands by themselves.")]
        public bool running;
        [Tooltip("Watch seconds per real second while running (1 = real time).")]
        public float rate = 1f;
        [Tooltip("The time shown, in hours (0-12). SetTime sets it; running advances it.")]
        public float hours = 9f + 41f / 60f;

        [Header("Fit")]
        [Tooltip("Wrist half-width and half-depth at the watch, metres (scaled with the body when attached).")]
        public Vector2 wrist = new Vector2(0.027f, 0.021f);
        [Tooltip("How far up the forearm from the wrist joint, metres.")]
        public float upForearm = 0.035f;
        [Tooltip("Worn face-in (on the inside of the wrist), as Lena wore it.")]
        public bool faceIn;

        bool owned = true;
        readonly List<Renderer> renderers = new List<Renderer>();

        public bool Owned => owned;

        static Material gold, enamel, steel, leather;
        static Texture2D dial;

        // ---- Use ----

        /// <summary>The watch on this body (made the first time), shown or hidden.</summary>
        public static VhKeepsakeWatch Ensure(Transform body, bool ownedNow)
        {
            var w = Attach(body);
            if (w != null) w.SetOwned(ownedNow);
            return w;
        }

        /// <summary>Builds the watch on the body's right forearm (Mixamo bone names), or returns the one already there.
        /// Null (with a warning) if the body has no right forearm and hand.</summary>
        public static VhKeepsakeWatch Attach(Transform body)
        {
            if (body == null) return null;
            var existing = body.GetComponentInChildren<VhKeepsakeWatch>(true);
            if (existing != null) return existing;
            Transform forearm = Find(body, "RightForeArm"), hand = Find(body, "RightHand");
            Transform middle = Find(body, "RightHandMiddle1"), thumb = Find(body, "RightHandThumb1");
            if (forearm == null || hand == null) { Debug.LogWarning("VhKeepsakeWatch: no RightForeArm / RightHand under " + body.name); return null; }

            // The body's height sets the wrist's size (the watch itself stays its real size).
            float height = 1.65f;
            var b = new Bounds();
            bool any = false;
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y > 0.5f) height = b.size.y;

            var go = new GameObject(ObjectName);
            var w = go.AddComponent<VhKeepsakeWatch>();
            float k = height / 1.65f;
            w.wrist *= k;
            w.Build();

            // Frame: along the forearm toward the hand; the back of the hand is up (right hand: thumb x fingers).
            Vector3 along = (hand.position - forearm.position).normalized;
            Vector3 fingers = middle != null ? (middle.position - hand.position).normalized : along;
            Vector3 thumbDir = thumb != null ? (thumb.position - hand.position) : Vector3.Cross(along, Vector3.up);
            thumbDir = Vector3.ProjectOnPlane(thumbDir, fingers).normalized;
            Vector3 back = Vector3.Cross(thumbDir, fingers).normalized;
            back = Vector3.ProjectOnPlane(back, along).normalized;
            if (back.sqrMagnitude < 0.5f) back = Vector3.ProjectOnPlane(Vector3.up, along).normalized;
            if (w.faceIn) back = -back;
            var rot = Quaternion.LookRotation(along, back);
            Vector3 pos = hand.position - along * w.upForearm * k + back * w.wrist.y;
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.SetParent(forearm, true);
            // Real size whatever the rig's scale.
            var ls = forearm.lossyScale;
            float s = Mathf.Max(1e-4f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
            go.transform.localScale = Vector3.one / s;
            w.Apply();
            return w;
        }

        public void SetOwned(bool on)
        {
            owned = on;
            if (renderers.Count == 0) GetComponentsInChildren(true, renderers);
            for (int i = 0; i < renderers.Count; i++) if (renderers[i] != null) renderers[i].enabled = on;
        }

        /// <summary>Sets the hands (12-hour; minutes and seconds fractional allowed).</summary>
        public void SetTime(float hour, float minute, float second = 0f)
        {
            hours = Mathf.Repeat(hour + minute / 60f + second / 3600f, 12f);
            Apply();
        }

        void Update()
        {
            if (!running) return;
            hours = Mathf.Repeat(hours + Time.deltaTime * rate / 3600f, 12f);
            Apply();
        }

        /// <summary>Turns the hands to <see cref="hours"/>. Clockwise seen from the face (+Y), twelve toward the fingers.</summary>
        public void Apply()
        {
            float h = hours;
            float m = Mathf.Repeat(h * 60f, 60f);
            float sec = Mathf.Repeat(h * 3600f, 60f);
            if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, h / 12f * 360f, 0f);
            if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, m / 60f * 360f, 0f);
            if (secondHand != null) secondHand.localRotation = Quaternion.Euler(0f, Mathf.Floor(sec) / 60f * 360f, 0f);
        }

        static Transform Find(Transform root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                int colon = n.LastIndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                if (n == bone) return t;
            }
            return null;
        }

        // ---- Building ----

        const float R = 0.0175f;          // the star's outer radius (35 mm point to point)
        const float Inner = 0.55f;        // inner radius / outer
        const float CaseH = 0.0075f;      // case height above the strap
        const float FaceY = 0.0062f;      // the dial's height

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            Materials();
            Part("Strap", Strap(), leather, transform);
            // The head: twelve and six where the strap leaves the case (across the forearm), twelve toward the little
            // finger, so it reads upright when she lifts her right forearm across her body to look.
            var head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Part("Case", StarCase(), gold, head);
            Part("Dial", StarDial(), enamel, head);
            Part("Lugs", Lugs(), gold, head);
            Part("Centre cap", Disc(0.0011f, FaceY + 0.0016f, 16), gold, head);
            hourHand = Part("Hour hand", Hand(0.0068f, 0.0011f, FaceY + 0.0006f, 0.0018f), steel, head).transform;
            minuteHand = Part("Minute hand", Hand(0.0105f, 0.0008f, FaceY + 0.0009f, 0.0022f), steel, head).transform;
            secondHand = Part("Second hand", Hand(0.0118f, 0.00028f, FaceY + 0.0012f, 0.0030f), leather, head).transform;
            renderers.Clear();
            GetComponentsInChildren(true, renderers);
            Apply();
        }

        static GameObject Part(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        static void Materials()
        {
            if (gold != null) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            gold = new Material(lit) { name = "Watch gold" };
            gold.SetColor("_BaseColor", new Color(1f, 0.79f, 0.45f));
            gold.SetFloat("_Metallic", 1f);
            gold.SetFloat("_Smoothness", 0.78f);
            steel = new Material(lit) { name = "Watch hands" };
            steel.SetColor("_BaseColor", new Color(0.09f, 0.11f, 0.2f));
            steel.SetFloat("_Metallic", 0.85f);
            steel.SetFloat("_Smoothness", 0.8f);
            leather = new Material(lit) { name = "Watch strap (wine leather)" };
            leather.SetColor("_BaseColor", new Color(0.42f, 0.09f, 0.14f));
            leather.SetFloat("_Metallic", 0f);
            leather.SetFloat("_Smoothness", 0.42f);
            dial = DialTexture();
            enamel = new Material(lit) { name = "Watch dial (enamel)" };
            enamel.SetTexture("_BaseMap", dial);
            enamel.SetColor("_BaseColor", Color.white);
            enamel.SetFloat("_Metallic", 0f);
            enamel.SetFloat("_Smoothness", 0.82f);
        }

        /// <summary>The dial: cream enamel with a fine gold inner star, twelve markers (bars at the star's points and at
        /// three, six, nine; dots between) and a tiny wine-red star under twelve. UV (0.5, 0.5) is the centre, +V toward twelve.</summary>
        static Texture2D DialTexture()
        {
            const int S = 512;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "Watch dial", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color32[S * S];
            var cream = new Color(0.97f, 0.93f, 0.84f);
            var goldC = new Color(0.78f, 0.6f, 0.3f);
            var ink = new Color(0.12f, 0.12f, 0.2f);
            var wine = new Color(0.55f, 0.1f, 0.17f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;   // -1..1, v up = twelve
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(u, v);                                       // 0 at twelve, clockwise
                    var c = cream * (0.97f + 0.03f * Mathf.Sin(r * 60f));               // a faint guilloché
                    // The fine gold star line, at 0.78 of the dial's star.
                    float sd = StarDistance(u, v, 0.78f);
                    c = Color.Lerp(c, goldC, Mathf.Clamp01(1f - Mathf.Abs(sd) / 0.012f));
                    // Markers.
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        float du = Mathf.Sin(a), dv = Mathf.Cos(a);
                        float along = u * du + v * dv, across = -u * dv + v * du;
                        bool big = i % 3 == 0;
                        float r0 = big ? 0.36f : 0.4f, r1 = big ? 0.52f : 0.46f, wdt = big ? 0.035f : 0.022f;
                        if (along > r0 && along < r1 && Mathf.Abs(across) < wdt) c = ink;
                    }
                    // The little star under twelve.
                    float ss = StarDistance(u, v - 0.2f, 0.09f);
                    if (ss < 0f) c = wine;
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Signed distance-ish to a five-pointed star of outer radius rOut (negative inside), point up.</summary>
        static float StarDistance(float u, float v, float rOut)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(u, v);
            return r - StarRadius(ang, rOut);
        }

        /// <summary>The star's radius at an angle: rounded points (a smoothed blend between the inner and outer radius).</summary>
        static float StarRadius(float ang, float rOut)
        {
            float seg = Mathf.PI * 2f / 5f;
            float t = Mathf.Abs(Mathf.Repeat(ang + seg * 0.5f, seg) - seg * 0.5f) / (seg * 0.5f);   // 0 at a point, 1 between
            float rIn = rOut * Inner;
            // Straight edges between point and notch (a true star polygon), softened at the point and the notch.
            float straight = Mathf.Lerp(rOut, rIn, t);
            float soft = Mathf.Lerp(rOut * 0.93f, rIn * 1.04f, Mathf.SmoothStep(0f, 1f, t));
            return Mathf.Lerp(straight, soft, 0.45f);
        }

        // Outline points of the star in the XZ plane (+Z = twelve), clockwise seen from +Y.
        static Vector3[] StarOutline(float rOut, float y, int perSeg = 24)
        {
            int n = perSeg * 5;
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Mathf.PI * 2f;
                float r = StarRadius(ang, rOut);
                p[i] = new Vector3(Mathf.Sin(ang) * r, y, Mathf.Cos(ang) * r);
            }
            return p;
        }

        /// <summary>The case: a star prism from the strap up to a bevelled top rim, its top open where the dial sits.</summary>
        static Mesh StarCase()
        {
            var v = new List<Vector3>();
            var tris = new List<int>();
            var rings = new[]
            {
                StarOutline(R * 0.97f, -0.0015f), StarOutline(R, 0.001f), StarOutline(R, CaseH - 0.0018f),
                StarOutline(R * 0.95f, CaseH), StarOutline(R * 0.86f, CaseH), StarOutline(R * 0.84f, FaceY + 0.0004f),
            };
            int n = rings[0].Length;
            foreach (var ring in rings) v.AddRange(ring);
            for (int k = 0; k < rings.Length - 1; k++)
                for (int i = 0; i < n; i++)
                {
                    int a = k * n + i, b = k * n + (i + 1) % n, c = (k + 1) * n + i, d = (k + 1) * n + (i + 1) % n;
                    // Facing out of the case (Unity: the front face's normal is cross(b - a, c - a)).
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            // The underside (closed).
            int center = v.Count;
            v.Add(new Vector3(0f, -0.0015f, 0f));
            for (int i = 0; i < n; i++) { tris.Add(center); tris.Add((i + 1) % n); tris.Add(i); }
            return Finish("Watch case", v, tris);
        }

        static Mesh StarDial()
        {
            var outline = StarOutline(R * 0.84f, FaceY);
            var v = new List<Vector3> { new Vector3(0f, FaceY, 0f) };
            v.AddRange(outline);
            var uv = new List<Vector2>();
            float span = R * 0.84f;
            foreach (var p in v) uv.Add(new Vector2(0.5f + p.x / span * 0.5f, 0.5f + p.z / span * 0.5f));
            var tris = new List<int>();
            int n = outline.Length;
            for (int i = 0; i < n; i++) { tris.Add(0); tris.Add(1 + i); tris.Add(1 + (i + 1) % n); }
            var m = Finish("Watch dial", v, tris, uv);
            return m;
        }

        /// <summary>A hand pointing to twelve (+Z) from the centre: a slim tapered blade with a short tail, a little thick.</summary>
        static Mesh Hand(float length, float width, float y, float tail)
        {
            var v = new List<Vector3>
            {
                new Vector3(0f, y, length), new Vector3(width, y, length * 0.2f), new Vector3(width * 0.7f, y, -tail),
                new Vector3(-width * 0.7f, y, -tail), new Vector3(-width, y, length * 0.2f),
                new Vector3(0f, y - 0.0003f, length), new Vector3(width, y - 0.0003f, length * 0.2f), new Vector3(width * 0.7f, y - 0.0003f, -tail),
                new Vector3(-width * 0.7f, y - 0.0003f, -tail), new Vector3(-width, y - 0.0003f, length * 0.2f),
            };
            var tris = new List<int> { 0, 1, 4, 1, 2, 4, 2, 3, 4 };
            // Bottom and the sides.
            tris.AddRange(new[] { 5, 9, 6, 6, 9, 7, 7, 9, 8 });
            for (int i = 0; i < 5; i++)
            {
                int a = i, b = (i + 1) % 5, c = i + 5, d = (i + 1) % 5 + 5;
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
            }
            return Finish("Watch hand", v, tris);
        }

        static Mesh Disc(float radius, float y, int seg)
        {
            var v = new List<Vector3> { new Vector3(0f, y + radius * 0.4f, 0f) };
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius));
            }
            var tris = new List<int>();
            for (int i = 0; i < seg; i++) { tris.Add(0); tris.Add(1 + i); tris.Add(1 + (i + 1) % seg); }
            return Finish("Watch cap", v, tris);
        }

        /// <summary>Four lugs at the case's sides along the forearm, where the strap meets it.</summary>
        static Mesh Lugs()
        {
            var v = new List<Vector3>();
            var tris = new List<int>();
            foreach (float sz in new[] { -1f, 1f })
                foreach (float sx in new[] { -1f, 1f })
                    Box(v, tris, new Vector3(sx * 0.0068f, 0.0018f, sz * (R * 0.78f)), new Vector3(0.0016f, 0.0028f, 0.006f));
            return Finish("Watch lugs", v, tris);
        }

        static void Box(List<Vector3> v, List<int> tris, Vector3 c, Vector3 half)
        {
            int b = v.Count;
            for (int i = 0; i < 8; i++)
                v.Add(c + new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z));
            int[] q = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
            for (int f = 0; f < 6; f++)
            {
                int a = b + q[f * 4], b1 = b + q[f * 4 + 1], c1 = b + q[f * 4 + 2], d = b + q[f * 4 + 3];
                tris.Add(a); tris.Add(b1); tris.Add(c1); tris.Add(a); tris.Add(c1); tris.Add(d);
            }
        }

        /// <summary>The strap: a band round the wrist (an ellipse about the forearm's axis, Z), 18 mm wide, 2.5 mm thick,
        /// with soft rounded edges; it passes under the case.</summary>
        Mesh Strap()
        {
            const int seg = 64, across = 6;
            float half = 0.009f, thick = 0.0025f;
            var v = new List<Vector3>();
            var tris = new List<int>();
            float cy = -wrist.y;
            // A cross-section: a rounded rectangle (thick x width), swept round the ellipse.
            var section = new Vector2[across * 2];
            for (int i = 0; i < across; i++)
            {
                float t = i / (float)(across - 1);
                float z = Mathf.Lerp(-half, half, t);
                float round = 1f - Mathf.Pow(Mathf.Abs(t * 2f - 1f), 6f);
                section[i] = new Vector2(thick * (0.55f + 0.45f * round), z);          // outer surface
                section[across * 2 - 1 - i] = new Vector2(0f, z);                        // inner surface (on the skin)
            }
            int sn = section.Length;
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));                       // a = 0 at the top (under the case)
                var p = new Vector2(dir.x * wrist.x, cy + dir.y * wrist.y);
                var nrm = new Vector2(dir.x / wrist.x, dir.y / wrist.y).normalized;
                foreach (var sc in section) v.Add(new Vector3(p.x + nrm.x * sc.x, p.y + nrm.y * sc.x, sc.y));
            }
            for (int s = 0; s < seg; s++)
                for (int i = 0; i < sn; i++)
                {
                    int a = s * sn + i, b = s * sn + (i + 1) % sn, c = (s + 1) * sn + i, d = (s + 1) * sn + (i + 1) % sn;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            return Finish("Watch strap", v, tris);
        }

        static Mesh Finish(string name, List<Vector3> v, List<int> tris, List<Vector2> uv = null)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            if (uv != null) m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
