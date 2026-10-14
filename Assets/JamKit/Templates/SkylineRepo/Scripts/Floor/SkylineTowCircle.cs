using JamKit.Skyline;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// One tow circle drawn on the floor (task R2; docs/SKYLINE-FLOOR-REPOSSESS.md section 2): a lit violet ring as wide as the
    /// circle (<see cref="FloorRules.CircleRadius"/>, 6 m across) and, inside it, a band that fills clockwise from north as the
    /// meter fills, with a violet spot light on the pool from above. The ring brightens while the Unit stands in the circle.
    /// <para>Everything is two flat meshes made once (the fill is the first k segments of a fixed index buffer, so a frame
    /// allocates nothing), glow materials from <see cref="SkylineBotLook.Glow"/> (build-included, no shader looked up by name),
    /// no collider, no shadow. Created by <see cref="Create"/> at run time by <see cref="SkylineRepossess"/>.</para>
    /// </summary>
    public sealed class SkylineTowCircle : MonoBehaviour
    {
        /// <summary>Segments in the fill band (a full circle).</summary>
        public const int Segments = 64;
        /// <summary>Ring and fill widths (m): the ring hugs the circle's edge, the fill band sits just inside it.</summary>
        public const float RingWidth = 0.18f, FillOuterGap = 0.3f, FillWidth = 0.42f;
        /// <summary>How far above the socket's floor point the flat meshes lie (m): clear of z-fighting, below the Unit's ankle.</summary>
        public const float Lift = 0.06f;

        MeshRenderer _ring, _fill;
        MeshFilter _fillFilter;
        Material _ringDim, _ringBright, _fillMat;
        Light _light;
        float _progress;
        bool _active, _hidden;
        int _visibleSegments;

        /// <summary>0..1: the meter shown.</summary>
        public float Progress => _progress;
        /// <summary>True while the Unit stands in this circle.</summary>
        public bool Active => _active;
        public bool Hidden => _hidden;
        /// <summary>How many fill segments are drawn (0..<see cref="Segments"/>).</summary>
        public int VisibleSegments => _visibleSegments;
        public Light PoolLight => _light;
        public Renderer RingRenderer => _ring;
        public Renderer FillRenderer => _fill;

        /// <summary>Makes a circle at <paramref name="centre"/> (the socket's floor point) under <paramref name="parent"/>.</summary>
        public static SkylineTowCircle Create(Transform parent, Vector3 centre, string name)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var c = go.AddComponent<SkylineTowCircle>();
            c.Build();
            return c;
        }

        void Build()
        {
            float r = FloorRules.CircleRadius;
            _ringDim = SkylineBotLook.Glow(SkylineRepossessFx.VioletDim);
            _ringBright = SkylineBotLook.Glow(SkylineRepossessFx.VioletHdr);
            _fillMat = SkylineBotLook.Glow(SkylineRepossessFx.VioletHdr);

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, Lift, 0f);
            ringGo.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh("Tow ring", r - RingWidth, r, Segments);
            _ring = ringGo.AddComponent<MeshRenderer>();
            _ring.sharedMaterial = _ringDim;
            NoShadow(_ring);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(0f, Lift + 0.01f, 0f);
            _fillFilter = fillGo.AddComponent<MeshFilter>();
            _fillFilter.sharedMesh = AnnulusMesh("Tow fill", r - FillOuterGap - FillWidth, r - FillOuterGap, Segments);
            _fill = fillGo.AddComponent<MeshRenderer>();
            _fill.sharedMaterial = _fillMat;
            NoShadow(_fill);

            // the violet pool from above: a spot looking straight down, wide enough for the whole circle
            var lightHolder = new GameObject("Pool").transform;
            lightHolder.SetParent(transform, false);
            lightHolder.localPosition = new Vector3(0f, 3.2f, 0f);
            lightHolder.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _light = SkylineRepossessFx.MakeLight(lightHolder, "Pool light", LightType.Spot, SkylineRepossessFx.VioletLight, 2.5f, 8f, 110f);

            SetState(0f, false);
        }

        static void NoShadow(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>A flat ring between radii <paramref name="inner"/> and <paramref name="outer"/>, facing up, as
        /// <paramref name="segments"/> quads (clockwise from +Z, the player's yaw direction): vertices 2 per step, indices 6 per
        /// segment in order, so a prefix of the index buffer is a prefix of the sweep.</summary>
        static Mesh AnnulusMesh(string name, float inner, float outer, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[verts.Length];
            var uv = new Vector2[verts.Length];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;      // 0 = +Z, increasing toward +X: clockwise from above
                float sx = Mathf.Sin(a), cz = Mathf.Cos(a);
                verts[i * 2] = new Vector3(sx * outer, 0f, cz * outer);
                verts[i * 2 + 1] = new Vector3(sx * inner, 0f, cz * inner);
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                uv[i * 2] = new Vector2((float)i / segments, 1f);
                uv[i * 2 + 1] = new Vector2((float)i / segments, 0f);
            }
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int o0 = i * 2, i0 = o0 + 1, o1 = o0 + 2, i1 = o0 + 3;
                // seen from above (+Y looking down), clockwise from north is the winding Unity treats as the front face
                tris[i * 6] = o0; tris[i * 6 + 1] = o1; tris[i * 6 + 2] = i0;
                tris[i * 6 + 3] = i0; tris[i * 6 + 4] = o1; tris[i * 6 + 5] = i1;
            }
            var m = new Mesh { name = name };
            m.vertices = verts;
            m.normals = normals;
            m.uv = uv;
            m.SetTriangles(tris, 0);
            m.bounds = new Bounds(Vector3.zero, new Vector3(outer * 2f, 0.1f, outer * 2f));
            return m;
        }

        /// <summary>Sets what the circle shows: the meter (0..1) and whether the Unit is in it. Cheap; call it every frame.</summary>
        public void SetState(float progress, bool active)
        {
            if (float.IsNaN(progress)) progress = 0f;
            progress = Mathf.Clamp01(progress);
            _progress = progress;
            _active = active;
            int k = Mathf.Clamp(Mathf.CeilToInt(progress * Segments - 1e-4f), 0, Segments);
            if (progress > 0f && k == 0) k = 1;
            if (k != _visibleSegments && _fillFilter != null)
            {
                _visibleSegments = k;
                _fillFilter.sharedMesh.SetSubMesh(0, new SubMeshDescriptor(0, k * 6), MeshUpdateFlags.DontValidateIndices);
            }
            if (_fill != null) _fill.enabled = !_hidden && k > 0;
            if (_ring != null && _ring.sharedMaterial != (active ? _ringBright : _ringDim)) _ring.sharedMaterial = active ? _ringBright : _ringDim;
            if (_light != null) _light.intensity = _hidden ? 0f : (active ? 4.5f : 1.6f);
        }

        /// <summary>The asset is gone (towed): the ring and its light go out.</summary>
        public void Hide()
        {
            _hidden = true;
            if (_ring != null) _ring.enabled = false;
            if (_fill != null) _fill.enabled = false;
            if (_light != null) _light.enabled = false;
        }

        /// <summary>A restart: the ring is back, empty and dim.</summary>
        public void Show()
        {
            _hidden = false;
            if (_ring != null) _ring.enabled = true;
            if (_light != null) _light.enabled = true;
            _visibleSegments = -1;      // force the fill mesh to be re-cut
            SetState(0f, false);
        }
    }
}
