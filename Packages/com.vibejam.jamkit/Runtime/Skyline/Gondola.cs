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

        /// <summary>The rail's two ends.</summary>
        public Vector3 Start => _a;
        public Vector3 End => _b;

        /// <summary>Total distance moved (m); never decreases, so mine spacing is measured on it, not on the position.</summary>
        public float Travelled => (float)_travelled;

        /// <summary>The rail's length (m; 0 for a rail that stays put).</summary>
        public float Length => _length;

        /// <summary>Where along the rail its ride started (m): 0, or what <see cref="StartAt"/> set.</summary>
        public float StartedAt { get; private set; }

        /// <summary>Sets it riding from <paramref name="along"/> m along the rail, on toward its end (the gondola's idle washing,
        /// <see cref="GondolaIdle"/>, hands over here when it wakes). Call it before the first <see cref="Tick"/>: the distance
        /// travelled starts at <paramref name="along"/> and only grows from there.</summary>
        public void StartAt(float along)
        {
            if (!(along > 0f) || float.IsInfinity(along) || !(_length > 0f)) return;
            if (_travelled > 0.0) return;   // already riding: the hand-over is once, at the start
            _travelled = Math.Min(along, _length);
            StartedAt = Travelled;
            Position = PositionAt(Travelled);
        }

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

    /// <summary>Where a <see cref="MineLayer"/> puts its mines.</summary>
    public enum MinePlacement { Auto, OnRail, Facade }

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
    /// than <c>mineMax</c> alive. When the gondola is wrecked the caller stops laying; the mines on the wall live on. A rail
    /// that started partway along (<see cref="GondolaRail.StartAt"/>: the gondola woke during its idle washing) counts its
    /// first spacing from its spawn, so it lays on the same rail points as a gondola woken at its spawn.</summary>
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

        /// <summary>Where this layer drops: by default (Auto) on the rail itself, except along the Food Court's façade rail, where
        /// the mine goes on the inside of the glass at the nearest service hatch (<see cref="FacadeHatches"/>). The gondola rides
        /// outside; the sim lays along a rail already offset to the inside (<see cref="FightArea.mineRailOffset"/>): both land on the
        /// same hatch points.</summary>
        public MinePlacement placement = MinePlacement.Auto;

        /// <summary>Added to the rail point before a mine is placed (OnRail placement only; the hatch placement fixes x and y).</summary>
        public Vector3 mineOffset;

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
                // no back-laying for the distance already ridden; a rail that started partway along (StartAt) counts from its
                // spawn, so the mines go on the same rail points as a ride from the spawn
                _nextAt = rail.Travelled + spacing - rail.StartedAt;
                if (_nextAt <= rail.Travelled) _nextAt = rail.Travelled + spacing;
            }
            // Mines go at exact multiples of the spacing even when one tick covers several; only the last mineMax matter.
            int guard = 0;
            bool facade = placement == MinePlacement.Facade || (placement == MinePlacement.Auto && FacadeHatches.IsFacadeRail(rail));
            while (rail.Travelled >= _nextAt && guard++ < 1000)
            {
                Drop(rail.PositionAt(_nextAt), facade);
                _nextAt += spacing;
            }
            if (rail.Travelled >= _nextAt) _nextAt = rail.Travelled + spacing; // a jump beyond the guard: resume from here
        }

        void Drop(Vector3 at, bool facade)
        {
            if (_def.mineMax < 1) return;
            if (facade)
            {
                at = FacadeHatches.MinePoint(FacadeHatches.NearestIndex(at.Z));
                // one mine to a hatch: a ride that comes back to the same hatch before the old mine is gone lays nothing
                for (int i = 0; i < _mines.Count; i++)
                    if (Vector3.DistanceSquared(_mines[i].Position, at) < 0.25f) return;
            }
            else at += mineOffset;
            while (_mines.Count >= _def.mineMax) _mines.RemoveAt(0); // the oldest live mine goes
            _mines.Add(new WallMine(at, _def));
        }
    }
}

namespace JamKit.Skyline
{
    /// <summary>The Food Court façade's twelve service hatches and the rail outside them (docs/SKYLINE-PLAN-FLOOR1.md Task G1,
    /// SKYLINE-FLOOR-REPOSSESS.md section 10): the gondola rides x 36.5, y 3.0, z -27..27 outside the glass; its mines land on the
    /// inside face of the glass (x 34.9, y 2.5) at the hatch nearest the rail, z -22..22 every 4 m.</summary>
    public static class FacadeHatches
    {
        public const int Count = 12;
        public const float FirstZ = -22f, Step = 4f;
        /// <summary>The glass's inner face, where the hatches sit.</summary>
        public const float GlassX = 35f;
        /// <summary>Where a mine lands: just inside the glass, at the hatches' mid height.</summary>
        public const float MineX = 34.9f, MineY = 2.5f;
        public const float HatchMinY = 2f, HatchMaxY = 3f;
        /// <summary>The outside rail the gondola rides.</summary>
        public const float RailX = 36.5f, RailY = 3f, RailEndZ = 27f;

        public static float ZOf(int index) => FirstZ + Step * Math.Max(0, Math.Min(Count - 1, index));

        /// <summary>The hatch nearest to <paramref name="z"/> (clamped to the ends; NaN gives hatch 0).</summary>
        public static int NearestIndex(float z)
        {
            if (float.IsNaN(z)) return 0;
            double i = Math.Floor((z - FirstZ) / Step + 0.5);
            return (int)Math.Max(0.0, Math.Min(Count - 1, i));
        }

        /// <summary>The z of the hatch nearest to <paramref name="z"/>.</summary>
        public static float HatchSnap(float z) => ZOf(NearestIndex(z));

        /// <summary>Where a mine through hatch <paramref name="index"/> lands.</summary>
        public static Vector3 MinePoint(int index) => new Vector3(MineX, MineY, ZOf(index));

        /// <summary>True for a point a hatch mine can be at (within 5 cm of a hatch's mine point).</summary>
        public static bool IsHatchPoint(Vector3 p)
        {
            if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z)) return false;
            return Math.Abs(p.X - MineX) < 0.05f && Math.Abs(p.Y - MineY) < 0.05f && Math.Abs(p.Z - HatchSnap(p.Z)) < 0.05f;
        }

        /// <summary>True for a rail that runs along the façade: both ends within 2 m of the glass line (the outside rail or the sim's
        /// inside copy), at the rail's height or the mines' (2.3..3.2 m), and at least 40 m long.</summary>
        public static bool IsFacadeRail(GondolaRail rail)
        {
            if (rail == null || rail.Length < 40f) return false;
            return NearFacade(rail.Start) && NearFacade(rail.End);
        }

        static bool NearFacade(Vector3 p) => p.X > MineX - 0.2f && p.X < RailX + 0.2f && p.Y > 2.3f && p.Y < 3.2f;
    }

    /// <summary>The façade's glass as the rules see it (pure; the Unity side is SkylineFacade): which pane a point of the wall belongs to,
    /// which panes are broken, and what an open pane does to a Unit near it. A shot through glass breaks the pane (decision D21); the
    /// tile is gone from the wall-run, its open edge cuts anyone in it, and falling out wrecks the Unit.</summary>
    public sealed class FacadeGlass
    {
        public const float EdgeDamage = 8f, EdgeInterval = 0.6f;
        /// <summary>How far past a pane's opening (m) its edge still cuts, and how deep into the hall.</summary>
        public const float EdgeReach = 0.25f, EdgeDepth = 0.7f;
        /// <summary>The Unit's chest below this y has fallen out and is wrecked (the sim's <c>fallDeathY</c>).</summary>
        public const float FallDeathY = -12f;

        static readonly float[] RowEdges = { 0f, 4f, 8f, 12f, RepossessArena.HallClearY };

        readonly HashSet<string> _broken = new HashSet<string>();
        float _cutCooldown;

        public int BrokenCount => _broken.Count;
        public bool IsBroken(string paneId) => paneId != null && _broken.Contains(paneId);

        /// <summary>Breaks a pane; false when the id is not a pane or it is already broken.</summary>
        public bool Break(string paneId)
        {
            if (!TryParse(paneId, out _, out _)) return false;
            return _broken.Add(paneId);
        }

        /// <summary>Mends every pane (the floor restarts).</summary>
        public void MendAll()
        {
            _broken.Clear();
            _cutCooldown = 0f;
        }

        /// <summary>Appends the ids of the broken panes to <paramref name="into"/>.</summary>
        public void BrokenIds(List<string> into)
        {
            if (into == null) return;
            foreach (var id in _broken) into.Add(id);
        }

        public static bool TryParse(string id, out int row, out int col)
        {
            row = col = -1;
            if (!RepossessArena.IsPane(id)) return false;
            string[] parts = id.Substring(RepossessArena.PanePrefix.Length).Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out row) || !int.TryParse(parts[1], out col)) return false;
            return row >= 0 && row < RepossessArena.PaneRows && col >= 0 && col < RepossessArena.PaneCols;
        }

        /// <summary>The pane whose face holds the wall point (y, z), or null on the posts and above the hall.</summary>
        public static string PaneAt(float y, float z)
        {
            if (float.IsNaN(y) || float.IsNaN(z)) return null;
            if (y < 0f || y >= RepossessArena.HallClearY || z < RepossessArena.MinZ || z >= RepossessArena.MaxZ) return null;
            int row = 0;
            while (row < RepossessArena.PaneRows - 1 && y >= RowEdges[row + 1]) row++;
            int col = Math.Min(RepossessArena.PaneCols - 1, (int)Math.Floor((z - RepossessArena.MinZ) / RepossessArena.PaneWidth));
            return RepossessArena.PaneId(row, col);
        }

        /// <summary>The pane a segment from <paramref name="from"/> to <paramref name="to"/> crosses on the glass plane (x = 35.2, the
        /// middle of the pane), or null when it does not cross it or crosses on a post.</summary>
        public static string PaneCrossed(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X;
            if (!(Math.Abs(dx) > 1e-6f)) return null;
            float t = (RepossessArena.MaxX + 0.2f - from.X) / dx;
            if (!(t >= 0f && t <= 1f)) return null;
            return PaneAt(from.Y + (to.Y - from.Y) * t, from.Z + (to.Z - from.Z) * t);
        }

        /// <summary>True when the chest is in the open edge of a broken pane (its opening, <see cref="EdgeReach"/> past it, from
        /// <see cref="EdgeDepth"/> inside the hall to a little way out).</summary>
        public bool InGap(Vector3 chest)
        {
            if (_broken.Count == 0) return false;
            if (!(chest.X > RepossessArena.MaxX - EdgeDepth && chest.X < RepossessArena.MaxX + 1.2f)) return false;
            foreach (var id in _broken)
            {
                if (!TryParse(id, out int row, out int col)) continue;
                float z0 = RepossessArena.MinZ + RepossessArena.PaneWidth * col, z1 = z0 + RepossessArena.PaneWidth;
                if (chest.Y >= RowEdges[row] - EdgeReach && chest.Y <= RowEdges[row + 1] + EdgeReach && chest.Z >= z0 - EdgeReach && chest.Z <= z1 + EdgeReach)
                    return true;
            }
            return false;
        }

        /// <summary>The damage the open edges do this tick (<see cref="EdgeDamage"/> once per <see cref="EdgeInterval"/> while the chest
        /// is in a gap), else 0. A chest that is not finite is nowhere.</summary>
        public float Tick(float dt, Vector3 chest)
        {
            if (dt > 0f && !float.IsInfinity(dt) && _cutCooldown > 0f) _cutCooldown -= dt;
            if (float.IsNaN(chest.X) || float.IsNaN(chest.Y) || float.IsNaN(chest.Z)) return 0f;
            if (_cutCooldown > 0f || !InGap(chest)) return 0f;
            _cutCooldown = EdgeInterval;
            return EdgeDamage;
        }

        /// <summary>True when the chest has fallen out of the building.</summary>
        public static bool HasFallen(Vector3 chest) => chest.Y < FallDeathY;
    }
}
