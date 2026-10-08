using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// Coin pickups dropped by enemies: they pop up, drift down, and fly to the player when near
    /// (or all at once when the room is cleared). Struct array, drawn instanced.
    /// </summary>
    public sealed class CoinField : MonoBehaviour
    {
        public Material material;
        public int capacity = 256;
        public float z = 0.8f;
        public float size = 0.26f;
        public Color color = new Color(1f, 0.83f, 0.25f);
        [Tooltip("Reach of the pull toward the player, before the magnet stat.")]
        public float pullRadius = 2.2f;
        public float pullSpeed = 14f;
        public float fallSpeed = 1.6f;
        public float collectRadius = 0.45f;
        [Tooltip("Coins that fall off the bottom are lost after this long.")]
        public float lifetime = 12f;

        struct Coin { public Vector2 p, v; public float age; public int value; }

        Coin[] _coins;
        int _count;
        InstancedQuads _quads;
        System.Random _random = new System.Random(3);

        public int Count => _count;
        /// <summary>Pull every coin to the player regardless of distance (room cleared).</summary>
        public bool attractAll;

        void Awake() => Ensure();

        void Ensure()
        {
            if (_coins != null) return;
            _coins = new Coin[Mathf.Max(8, capacity)];
            _quads = new InstancedQuads(material, _coins.Length);
        }

        public void Spawn(Vector2 at, int coins)
        {
            Ensure();
            for (int i = 0; i < coins; i++)
            {
                if (_count >= _coins.Length) return;
                float a = (float)(_random.NextDouble() * Mathf.PI);
                float s = 1.5f + (float)_random.NextDouble() * 2.5f;
                _coins[_count++] = new Coin { p = at, v = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s + 1f), value = 1 };
            }
        }

        public void Clear() => _count = 0;

        /// <summary>Moves the coins; returns the value collected by a player at <paramref name="player"/> this frame.</summary>
        public int Tick(float dt, Vector2 player, float magnet, float bottom)
        {
            Ensure();
            if (!(dt > 0f)) return 0;
            int collected = 0;
            float reach = pullRadius * Mathf.Max(0f, magnet);
            int i = 0;
            while (i < _count)
            {
                ref Coin c = ref _coins[i];
                c.age += dt;
                Vector2 to = player - c.p;
                float d = to.magnitude;
                if (d < collectRadius)
                {
                    collected += c.value;
                    _coins[i] = _coins[--_count];
                    continue;
                }
                if ((attractAll && c.age > 0.25f) || d < reach)
                {
                    c.v = Vector2.MoveTowards(c.v, to / Mathf.Max(d, 0.001f) * pullSpeed, 60f * dt);
                }
                else
                {
                    c.v.x *= 1f - Mathf.Min(1f, 2f * dt);
                    c.v.y = Mathf.MoveTowards(c.v.y, -fallSpeed, 6f * dt);
                }
                c.p += c.v * dt;
                if (c.age > lifetime || c.p.y < bottom - 1f) { _coins[i] = _coins[--_count]; continue; }
                i++;
            }
            return collected;
        }

        void LateUpdate()
        {
            if (_coins == null || !InstancedQuads.CanDraw) return;
            _quads.Clear();
            float t = Time.time;
            for (int i = 0; i < _count; i++)
            {
                var c = _coins[i];
                float spin = Mathf.Abs(Mathf.Cos(t * 6f + i));
                _quads.Add(c.p.x, c.p.y, 0f, 0f, size * (0.35f + 0.65f * spin), size, color);
            }
            _quads.Draw(z);
        }
    }
}
