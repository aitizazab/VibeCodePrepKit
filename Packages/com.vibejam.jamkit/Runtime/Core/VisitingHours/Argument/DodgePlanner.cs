using System;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>
    /// Plans an escape through a pressure phase: a search over every spot her light can reach, step by step, against
    /// the attacks as they will be (they all move in straight lines, so the future is exact,
    /// <see cref="PressurePhase.HurtsDuring"/>). The box is a grid of spots; each step the reachable set grows by one
    /// move (at the given speed) and loses every spot an attack would touch at any moment of that step. A move is
    /// allowed only between two spots that are both safe for the whole step, with extra room of half a move (plus
    /// <c>pad</c>), so every point along the way is safe too: the plan holds between its samples, not just at them.
    /// If any spot survives until no attack can reach the box, there is a way out, and the path to the survivor
    /// nearest <c>home</c> is kept (it starts exactly where the light is).
    ///
    /// Used twice: by the phase to refuse an attack that would leave no way out (<see cref="PressureRule"/>), and by
    /// <see cref="PlanningDodger"/>, the fairness tests' player. All buffers are made once; nothing is allocated
    /// per plan.
    /// </summary>
    public sealed class DodgePlanner
    {
        readonly float _cell, _dt, _pad, _basePad, _move, _x0, _y0;
        readonly int _nx, _ny, _cells, _maxSteps;
        readonly int[] _ndx, _ndy;
        readonly int _neighbours;
        bool[] _reach, _next;
        readonly byte[] _state;
        readonly int[] _from;
        readonly Vector2[] _path;
        int _pathLength;
        float _delay;
        readonly PressureSweep _sweep = new PressureSweep(), _rest = new PressureSweep();
        readonly bool _settleEarly;
        bool _survives;
        // Her sky: only spots on her lines can be reached (the light moves along them).
        readonly bool[] _mask;
        Rails _maskRails;
        int _maskVersion = -1;
        bool _useMask;

        /// <param name="cell">Grid spacing (surface units).</param>
        /// <param name="dt">Seconds per step.</param>
        /// <param name="horizon">The longest look ahead (seconds).</param>
        /// <param name="speed">How fast the planned player moves (units per second).</param>
        /// <param name="pad">Extra room kept from every attack, on top of half a move.</param>
        /// <param name="settleEarly">Stop as soon as a reachable spot is safe from every attack for good (the
        /// in-game check: proof enough, and much cheaper). Off for a player who should keep moving (the tests').</param>
        public DodgePlanner(float cell, float dt, float horizon, float speed, float pad, bool settleEarly = false)
        {
            _settleEarly = settleEarly;
            _cell = cell;
            _dt = dt;
            _move = speed * dt;
            _basePad = pad;
            _pad = pad + _move * 0.5f;
            float w = 2f * (PressurePhase.HalfWidth - PressurePhase.LightRadius);
            float h = 2f * (PressurePhase.HalfHeight - PressurePhase.LightRadius);
            _nx = (int)(w / cell) + 1;
            _ny = (int)(h / cell) + 1;
            // Centre the grid in the box.
            _x0 = -(_nx - 1) * cell * 0.5f;
            _y0 = -(_ny - 1) * cell * 0.5f;
            _cells = _nx * _ny;
            _maxSteps = (int)Math.Ceiling(horizon / dt);
            float move = _move;
            int r = (int)(move / cell) + 1;
            var dx = new int[(2 * r + 1) * (2 * r + 1)];
            var dy = new int[dx.Length];
            int n = 0;
            for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                    if ((float)Math.Sqrt(i * i + j * j) * cell <= move + 1e-4f) { dx[n] = i; dy[n] = j; n++; }
            _ndx = dx;
            _ndy = dy;
            _neighbours = n;
            _reach = new bool[_cells];
            _next = new bool[_cells];
            _state = new byte[_cells];
            _from = new int[(_maxSteps + 1) * _cells];
            _path = new Vector2[_maxSteps + 1];
            _mask = new bool[_cells];
        }

        /// <summary>On rails, only the spots on the lines count (within three quarters of a cell of one).</summary>
        void PrepareMask(Rails rails)
        {
            _useMask = rails != null && rails.Walkable > 0;
            if (!_useMask || (rails == _maskRails && rails.Version == _maskVersion)) return;
            _maskRails = rails;
            _maskVersion = rails.Version;
            for (int c = 0; c < _cells; c++) _mask[c] = rails.DistanceTo(Spot(c)) <= _cell * 0.75f;
        }

        /// <summary>Seconds between the path's points.</summary>
        public float Step => _dt;
        /// <summary>Points in the last plan's path (point k is where to be k steps after the plan started).</summary>
        public int PathLength => _pathLength;
        /// <summary>The last plan found a way out.</summary>
        public bool Survives => _survives;
        public Vector2 PathAt(int k) => _path[k < 0 ? 0 : k >= _pathLength ? _pathLength - 1 : k];

        Vector2 Spot(int c) => new Vector2(_x0 + (c % _nx) * _cell, _y0 + (c / _nx) * _cell);

        int Nearest(Vector2 p)
        {
            int i = (int)Math.Round((p.X - _x0) / _cell), j = (int)Math.Round((p.Y - _y0) / _cell);
            i = i < 0 ? 0 : i >= _nx ? _nx - 1 : i;
            j = j < 0 ? 0 : j >= _ny ? _ny - 1 : j;
            return j * _nx + i;
        }

        /// <summary>Plans from <paramref name="start"/> against the attacks at least <paramref name="minAge"/> old.
        /// True if a way out exists; the path (to the surviving spot nearest <paramref name="home"/>, or to the spot
        /// that lasted longest) is in <see cref="PathAt"/>, point 0 being <paramref name="start"/> itself.</summary>
        public bool Plan(PressurePhase phase, Vector2 start, float minAge, Vector2 home) => Plan(phase, start, minAge, home, 0f);

        /// <summary>As <see cref="Plan(PressurePhase, Vector2, float, Vector2)"/>, starting <paramref name="delay"/>
        /// seconds from now (the attacks have moved on by then).</summary>
        public bool Plan(PressurePhase phase, Vector2 start, float minAge, Vector2 home, float delay)
        {
            _delay = delay;
            PrepareMask(phase.Rails);
            float threatEnd = phase.ThreatEnd(minAge) - delay;
            float end = Math.Min(threatEnd, _maxSteps * _dt);
            int steps = Math.Max(1, Math.Min(_maxSteps, (int)Math.Ceiling(end / _dt) + 1));
            int alive = 0;
            Array.Clear(_reach, 0, _cells);

            // Step 1: from the light itself to any spot within one move, both safe for the whole step.
            bool any = false;
            phase.Sweep(_sweep, delay, delay + _dt, minAge);
            // Where the light is now is a fact, not a plan: it needs only the base room (it may be threading a gap).
            if (!_sweep.Hurts(start, _basePad))
            {
                int s0 = Nearest(start);
                int ci = s0 % _nx, cj = s0 / _nx, r = (int)(_move / _cell) + 2;
                for (int nj = Math.Max(0, cj - r); nj <= Math.Min(_ny - 1, cj + r); nj++)
                    for (int ni = Math.Max(0, ci - r); ni <= Math.Min(_nx - 1, ci + r); ni++)
                    {
                        int n = nj * _nx + ni;
                        if (Vector2.Distance(Spot(n), start) > _move + 1e-4f) continue;
                        if (_useMask && !_mask[n]) continue;
                        if (_sweep.Hurts(Spot(n), _pad)) continue;
                        _reach[n] = true;
                        _from[_cells + n] = -1;
                        any = true;
                    }
            }
            if (any) alive = 1;
            int settled = any ? Settled(phase, 1, threatEnd, minAge) : -1;

            for (int k = 2; any && settled < 0 && k <= steps; k++)
            {
                Array.Clear(_next, 0, _cells);
                Array.Clear(_state, 0, _cells);
                any = false;
                float t0 = delay + (k - 1) * _dt, t1 = delay + k * _dt;
                phase.Sweep(_sweep, t0, t1, minAge);
                int row = k * _cells;
                // Standing still first, so a plan only moves when it has to (as a person would).
                for (int c = 0; c < _cells; c++)
                {
                    if (!_reach[c] || !Safe(c)) continue;
                    _next[c] = true;
                    _from[row + c] = c;
                    any = true;
                }
                for (int c = 0; c < _cells; c++)
                {
                    if (!_reach[c]) continue;
                    // Leaving c means being on the way from c during this step: c must be safe all through it.
                    if (!Safe(c)) continue;
                    int ci = c % _nx, cj = c / _nx;
                    for (int m = 0; m < _neighbours; m++)
                    {
                        int ni = ci + _ndx[m], nj = cj + _ndy[m];
                        if (ni < 0 || nj < 0 || ni >= _nx || nj >= _ny) continue;
                        int n = nj * _nx + ni;
                        if (_next[n] || !Safe(n)) continue;
                        _next[n] = true;
                        _from[row + n] = c;
                        any = true;
                    }
                }
                if (!any) break;
                var swap = _reach; _reach = _next; _next = swap;
                alive = k;
                if (k % 3 == 0) settled = Settled(phase, k, threatEnd, minAge);
            }
            _survives = settled >= 0 || alive >= steps;

            // The end spot: one that's safe for good, or the survivor nearest home.
            int best = settled;
            float bestD = float.MaxValue;
            if (alive > 0 && settled < 0)
                for (int c = 0; c < _cells; c++)
                {
                    if (!_reach[c]) continue;
                    float d = Vector2.DistanceSquared(Spot(c), home);
                    if (d < bestD) { bestD = d; best = c; }
                }
            // Back along the path.
            _pathLength = alive + 1;
            int cur = best;
            for (int k = alive; k >= 1; k--)
            {
                _path[k] = Spot(cur);
                cur = _from[k * _cells + cur];
            }
            _path[0] = start;
            return _survives;
        }

        /// <summary>With <c>settleEarly</c>: a spot reachable at step <paramref name="k"/> that no attack can touch
        /// from then until they've all gone, or -1.</summary>
        int Settled(PressurePhase phase, int k, float threatEnd, float minAge)
        {
            if (!_settleEarly) return -1;
            float from = _delay + k * _dt;
            phase.Sweep(_rest, from, Math.Max(from, _delay + threatEnd) + _dt, minAge);
            for (int c = 0; c < _cells; c++)
                if (_reach[c] && !_rest.Hurts(Spot(c), _pad)) return c;
            return -1;
        }

        bool Safe(int c)
        {
            if (_useMask && !_mask[c]) return false;
            byte st = _state[c];
            if (st == 0)
            {
                st = _sweep.Hurts(Spot(c), _pad) ? (byte)2 : (byte)1;
                _state[c] = st;
            }
            return st == 1;
        }
    }

    /// <summary>
    /// The fairness tests' player: it notices each attack <see cref="Reaction"/> seconds after it appears, plans an
    /// escape against everything it has noticed (<see cref="DodgePlanner"/>), and follows it, re-planning whenever
    /// something new is noticed. Between attacks it goes back to its <see cref="Home"/>, which the tests set to every
    /// awkward spot (corners, edges), so attacks also meet it where it least wants to be.
    /// </summary>
    public sealed class PlanningDodger
    {
        readonly DodgePlanner _planner;
        readonly float _reaction;
        Vector2 _home;
        int _noticed = -1;
        float _t, _since;
        int _plans, _stuck;

        public PlanningDodger(DodgePlanner planner, float reaction, Vector2 home)
        {
            _planner = planner;
            _reaction = reaction;
            _home = home;
        }

        public float Reaction => _reaction;
        public Vector2 Home => _home;

        /// <summary>Moves its home (the battle's autopilot heads for the statement it means to hit). A home that moved
        /// far re-plans at the next step.</summary>
        public void SetHome(Vector2 home)
        {
            if (Vector2.DistanceSquared(home, _home) > 0.09f) _since = 1f;
            _home = home;
        }

        /// <summary>Forgets its plan (a new phase).</summary>
        public void Reset()
        {
            _noticed = -1;
            _t = _since = 0f;
            _plans = 0;
            _stuck = 0;
        }
        /// <summary>Plans made, and plans that found no way out.</summary>
        public int Plans => _plans;
        public int Stuck => _stuck;

        /// <summary>The move input for the next tick of <paramref name="dt"/>. It re-plans only on the plan's own
        /// steps (where the light is exactly on a planned spot), so a new plan's margins hold from its first move.</summary>
        public Vector2 Steer(PressurePhase phase, float dt)
        {
            int noticed = phase.NewestNoticed(_reaction);
            float step = _planner.Step;
            float span = (_planner.PathLength - 1) * step;
            float k0 = _t / step;
            bool onStep = Math.Abs(k0 - (float)Math.Round(k0)) < 1e-3f;
            bool due = noticed != _noticed || _since >= 1f || _t + dt > span + 1e-4f;
            if (_plans == 0 || (due && onStep) || _t + dt > span + step)
            {
                _noticed = noticed;
                _plans++;
                if (!_planner.Plan(phase, phase.Light, _reaction, _home)) _stuck++;
                _t = 0f;
                _since = 0f;
            }
            float at = (_t + dt) / step;
            int k = (int)(at + 1e-4f);
            float f = Math.Max(0f, at - k);
            var target = Vector2.Lerp(_planner.PathAt(k), _planner.PathAt(k + 1), f);
            if (_planner.PathLength <= 1) target = Vector2.Lerp(phase.Light, _home, 0.5f);
            _t += dt;
            _since += dt;
            var move = (target - phase.Light) / (PressurePhase.LightSpeed * dt);
            if (move.LengthSquared() > 1f) move = Vector2.Normalize(move);
            return move;
        }
    }
}
