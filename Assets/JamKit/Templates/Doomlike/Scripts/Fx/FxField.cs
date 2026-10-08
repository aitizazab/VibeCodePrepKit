using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Short-lived effects as pooled billboards from the effects sheet (8 columns x 4 rows):
    /// row 0 bullet puffs, row 1 blood, row 2 explosions, row 3 the projectile glow. Blood chunks
    /// can fly and fall (gibs). Nothing is allocated after Awake.
    /// </summary>
    public sealed class FxField : MonoBehaviour
    {
        public enum Fx { Puff = 0, Blood = 1, Blast = 2, Glow = 3 }

        const int Capacity = 128;
        static readonly int[] FrameCount = { 4, 4, 6, 2 };
        static readonly float[] Fps = { 18f, 14f, 14f, 10f };

        struct Item
        {
            public Vector3 position, velocity;
            public float age, life, size;
            public Fx kind;
            public bool gravity, active;
        }

        public Material material;
        public Texture texture;
        readonly Item[] _items = new Item[Capacity];
        Billboard[] _views;
        int _next;
        int _active;

        public int ActiveCount => _active;

        /// <summary>Called once by DoomGame with the sprite material and the effects sheet.</summary>
        public void Init(Material spriteMaterial, Texture sheet)
        {
            material = spriteMaterial;
            texture = sheet;
            EnsureViews();
        }

        void EnsureViews()
        {
            if (_views != null) return;
            _views = new Billboard[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                _views[i] = Billboard.Create("fx " + i, transform, Vector3.zero, 1f, 1f, material, texture, Color.white, 0.8f);
                _views[i].SetVisible(false);
            }
        }

        public void Spawn(Fx kind, Vector3 position, float size, Color tint, Vector3 velocity = default, bool gravity = false)
        {
            EnsureViews();
            int i = _next;
            _next = (_next + 1) % Capacity;
            if (!_items[i].active) _active++;
            int frames = FrameCount[(int)kind];
            _items[i] = new Item
            {
                position = position,
                velocity = velocity,
                age = 0f,
                life = gravity ? 1.6f : frames / Fps[(int)kind],
                size = size,
                kind = kind,
                gravity = gravity,
                active = true,
            };
            var v = _views[i];
            v.SetTint(tint);
            v.SetSize(size, size);
            v.transform.position = position - Vector3.up * (size * 0.5f);
            v.SetFrame(SheetCell((int)kind, 0));
            v.SetVisible(true);
        }

        public static Vector4 SheetCell(int row, int col)
        {
            const float w = 1f / 8f, h = 1f / 4f;
            return new Vector4(col * w, 1f - (row + 1) * h, w, h);
        }

        public void Tick(float dt, float cameraYaw)
        {
            if (_views == null) return;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_items[i].active) continue;
                ref var it = ref _items[i];
                it.age += dt;
                if (it.age >= it.life)
                {
                    it.active = false;
                    _active--;
                    _views[i].SetVisible(false);
                    continue;
                }
                if (it.gravity)
                {
                    it.velocity.y -= 18f * dt;
                    it.position += it.velocity * dt;
                    if (it.position.y < it.size * 0.3f)
                    {
                        it.position.y = it.size * 0.3f;
                        it.velocity = Vector3.zero;
                    }
                }
                else it.position += it.velocity * dt;
                int frames = FrameCount[(int)it.kind];
                int f = it.gravity ? Mathf.Min(frames - 1, (int)(it.age * 6f)) : Mathf.Min(frames - 1, (int)(it.age * Fps[(int)it.kind]));
                var v = _views[i];
                v.transform.position = it.position - Vector3.up * (it.size * 0.5f);
                v.SetFrame(SheetCell((int)it.kind, f));
                v.Face(cameraYaw);
            }
        }

        public void Clear()
        {
            if (_views == null) return;
            for (int i = 0; i < Capacity; i++)
            {
                _items[i].active = false;
                _views[i].SetVisible(false);
            }
            _active = 0;
        }
    }
}
