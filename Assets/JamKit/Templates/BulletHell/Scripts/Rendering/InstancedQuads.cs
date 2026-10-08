using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.BulletHell
{
    /// <summary>
    /// Draws many coloured quads (bullets, coins, sparks, stars) with GPU instancing: fill it with
    /// <see cref="Add"/> during the frame, then <see cref="Draw"/> once. One draw call per 1023
    /// quads, per-instance colour through a MaterialPropertyBlock array. Needs a material whose
    /// shader supports instancing and reads <c>_BulletColor</c> per instance (JamKit/BulletInstanced).
    /// Works in URP and WebGL 2; does nothing on a null graphics device (batch mode, servers).
    /// Buffers grow in whole batches and are reused, so a steady frame allocates nothing.
    /// </summary>
    public sealed class InstancedQuads
    {
        public const int BatchSize = 1023;
        static readonly int ColorId = Shader.PropertyToID("_BulletColor");
        static Mesh _sharedQuad;

        readonly Material _material;
        readonly List<Matrix4x4[]> _matrices = new List<Matrix4x4[]>();
        readonly List<Vector4[]> _colors = new List<Vector4[]>();
        readonly List<MaterialPropertyBlock> _blocks = new List<MaterialPropertyBlock>();
        int _count;

        public InstancedQuads(Material material, int expected = BatchSize)
        {
            _material = material;
            int batches = Mathf.Max(1, (expected + BatchSize - 1) / BatchSize);
            for (int i = 0; i < batches; i++) AddBatch();
        }

        /// <summary>Quads added since the last <see cref="Clear"/>.</summary>
        public int Count => _count;

        /// <summary>Whether this machine can draw them (false in -nographics runs).</summary>
        public static bool CanDraw =>
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.supportsInstancing;

        /// <summary>A 1x1 quad centred on the origin, made once.</summary>
        public static Mesh Quad
        {
            get
            {
                if (_sharedQuad != null) return _sharedQuad;
                var m = new Mesh { name = "JamKit Quad" };
                m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                m.RecalculateBounds();
                m.hideFlags = HideFlags.DontSave;
                _sharedQuad = m;
                return m;
            }
        }

        void AddBatch()
        {
            _matrices.Add(new Matrix4x4[BatchSize]);
            _colors.Add(new Vector4[BatchSize]);
            _blocks.Add(new MaterialPropertyBlock());
        }

        public void Clear() => _count = 0;

        /// <summary>One quad at (x, y, z), rotated by <paramref name="angle"/> radians, sized
        /// <paramref name="width"/> x <paramref name="height"/> world units (width along the angle).</summary>
        public void Add(float x, float y, float z, float angle, float width, float height, Color color)
        {
            int batch = _count / BatchSize;
            if (batch >= _matrices.Count) AddBatch();
            int i = _count - batch * BatchSize;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            var m = new Matrix4x4();
            m.m00 = c * width; m.m01 = -s * height; m.m02 = 0f; m.m03 = x;
            m.m10 = s * width; m.m11 = c * height; m.m12 = 0f; m.m13 = y;
            m.m20 = 0f; m.m21 = 0f; m.m22 = 1f; m.m23 = z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            _matrices[batch][i] = m;
            _colors[batch][i] = new Vector4(color.r, color.g, color.b, color.a);
            _count++;
        }

        /// <summary>An unrotated square.</summary>
        public void Add(float x, float y, float z, float size, Color color) => Add(x, y, z, 0f, size, size, color);

        /// <summary>Submits everything added this frame. <paramref name="sortZ"/> is the depth the
        /// batch sorts at against sprites (smaller = nearer the camera = drawn later).</summary>
        public void Draw(float sortZ, int layer = 0)
        {
            if (_count == 0 || _material == null || !CanDraw) return;
            var mesh = Quad;
            var bounds = new Bounds(new Vector3(0f, 0f, sortZ), new Vector3(10000f, 10000f, 1f));
            int left = _count;
            for (int b = 0; left > 0; b++)
            {
                int n = Mathf.Min(left, BatchSize);
                var block = _blocks[b];
                block.SetVectorArray(ColorId, _colors[b]);
                var rp = new RenderParams(_material)
                {
                    matProps = block,
                    worldBounds = bounds,
                    layer = layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMeshInstanced(rp, mesh, 0, _matrices[b], n, 0);
                left -= n;
            }
        }
    }
}
