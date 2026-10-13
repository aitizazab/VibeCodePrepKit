using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The window-washer gondola's rail (docs/SKYLINE-DESIGN.md 2.5): a segment along a wall that it rides back and
    /// forth. Position is a pure function of the distance travelled, so a long tick can't skip past an end.</summary>
    public sealed class GondolaRail
    {
        readonly Vector3 _a, _b;
        readonly float _length;
        readonly float _speed;
        double _travelled;

        public GondolaRail(Vector3 a, Vector3 b, float speed)
        {
            _a = a;
            float len = Vector3.Distance(a, b);
            // A zero-length or non-finite rail stays put at a.
            _b = len > 0f && !float.IsInfinity(len) ? b : a;
            _length = _b == _a ? 0f : len;
            _speed = speed > 0f && !float.IsInfinity(speed) ? speed : 0f; // NaN, zero and negative: a rail that doesn't move
            Position = a;
        }

        public Vector3 Position { get; private set; }

        /// <summary>Total distance moved (m); never decreases, so mine spacing is measured on it, not on the position.</summary>
        public float Travelled => (float)_travelled;

        public void Tick(float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            _travelled += (double)_speed * dt;
            Position = PositionAt(Travelled);
        }

        /// <summary>Where the gondola is after riding <paramref name="travelled"/> m from the start (a ping-pong).</summary>
        public Vector3 PositionAt(float travelled)
        {
            if (!(_length > 0f) || !(travelled > 0f) || float.IsInfinity(travelled)) return _a;
            double lap = 2.0 * _length;
            double s = travelled % lap;
            double along = s <= _length ? s : lap - s;
            return Vector3.Lerp(_a, _b, (float)(along / _length));
        }
    }

    public enum MineState { Arming, Armed, Fusing, Exploded, Expired }

    /// <summary>One mine stuck to the wall where the gondola dropped it. It arms, waits for a chest to come close, beeps for
    /// the fuse (leaving the wall in time dodges the blast), then goes off once. Distances are spherical, to the chest.</summary>
    public sealed class WallMine
    {
        readonly EnemyDef _def;
        float _fuse;

        public WallMine(Vector3 position, EnemyDef def)
        {
            Position = position;
            _def = def ?? throw new ArgumentNullException(nameof(def));
        }

        public Vector3 Position { get; }
        public MineState State { get; private set; } = MineState.Arming;
        /// <summary>Seconds since it was dropped.</summary>
        public float Age { get; private set; }

        static bool Finite(Vector3 v) =>
            !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
            && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);

        bool Within(Vector3 chest, float radius) =>
            Finite(chest) && radius > 0f && Vector3.DistanceSquared(Position, chest) <= radius * radius;

        /// <summary>Returns true on the tick it explodes; <paramref name="damage"/> is the def's attackDamage if the chest is
        /// inside the blast radius at that moment, else 0. A tick moves it one step along its states, so a mine never arms
        /// and triggers in the same tick.</summary>
        public bool Tick(float dt, Vector3 playerChest, out float damage)
        {
            damage = 0f;
            if (!(dt > 0f) || float.IsInfinity(dt)) return false;
            if (State == MineState.Exploded || State == MineState.Expired) return false;
            Age += dt;
            switch (State)
            {
                case MineState.Arming:
                    if (Age >= _def.mineLifetime) State = MineState.Expired;
                    else if (Age >= _def.mineArmSeconds) State = MineState.Armed;
                    break;
                case MineState.Armed:
                    if (Age >= _def.mineLifetime) State = MineState.Expired;
                    else if (Within(playerChest, _def.mineTriggerRadius)) State = MineState.Fusing;
                    break;
                case MineState.Fusing:
                    // Once triggered, the fuse runs out even past the lifetime: the beep is a promise.
                    _fuse += dt;
                    if (_fuse >= _def.mineFuseSeconds)
                    {
                        State = MineState.Exploded;
                        if (Within(playerChest, _def.mineBlastRadius)) damage = _def.attackDamage;
                        return true;
                    }
                    break;
            }
            return false;
        }
    }

    /// <summary>The gondola's mine-laying: a mine every <c>mineSpacing</c> m of rail travel while it is laying, never more
    /// than <c>mineMax</c> alive. When the gondola is wrecked the caller stops laying; the mines on the wall live on.</summary>
    public sealed class MineLayer
    {
        readonly EnemyDef _def;
        readonly List<WallMine> _mines = new List<WallMine>();
        bool _wasLaying;
        float _nextAt;

        public MineLayer(EnemyDef def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
        }

        public IReadOnlyList<WallMine> Mines => _mines;

        /// <summary>Ticks every mine (adding each blast's damage, if above 0, to <paramref name="blasts"/>), drops the
        /// finished ones, then lays new mines along the rail while <paramref name="laying"/>.</summary>
        public void Tick(float dt, GondolaRail rail, bool laying, Vector3 playerChest, List<float> blasts)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            for (int i = 0; i < _mines.Count; i++)
            {
                if (_mines[i].Tick(dt, playerChest, out float dmg) && dmg > 0f && blasts != null) blasts.Add(dmg);
            }
            _mines.RemoveAll(m => m.State == MineState.Exploded || m.State == MineState.Expired);

            if (!laying || rail == null)
            {
                _wasLaying = false;
                return;
            }
            float spacing = _def.mineSpacing;
            if (!(spacing > 0f) || float.IsInfinity(spacing)) return;
            if (!_wasLaying)
            {
                _wasLaying = true;
                _nextAt = rail.Travelled + spacing; // no back-laying for the distance already ridden
            }
            // Mines go at exact multiples of the spacing even when one tick covers several; only the last mineMax matter.
            int guard = 0;
            while (rail.Travelled >= _nextAt && guard++ < 1000)
            {
                Drop(rail.PositionAt(_nextAt));
                _nextAt += spacing;
            }
            if (rail.Travelled >= _nextAt) _nextAt = rail.Travelled + spacing; // a jump beyond the guard: resume from here
        }

        void Drop(Vector3 at)
        {
            if (_def.mineMax < 1) return;
            while (_mines.Count >= _def.mineMax) _mines.RemoveAt(0); // the oldest live mine goes
            _mines.Add(new WallMine(at, _def));
        }
    }
}
