using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>
    /// Her case laid out on the table, for the sections presenter (docs/redesign/battle.md, round 2): the claim at the top,
    /// what holds it up below it, and what hangs from each of those under them, joined by the lines of her reasoning. In the
    /// kitchen it's a page of her notebook (each statement written where it sits, pencil lines between); under her sky the
    /// same places are stars and the lines are her constellation (SELECTION.md 7), and the light moves along them
    /// (<see cref="Rails"/>).
    ///
    /// Every premise has a fixed place from the start (hidden ones are simply not drawn yet), so the case grows in place as
    /// she reveals it. Surface units, as a pressure phase's (x -3..3, y -1.6..1.6, y up). Nothing allocated per tick.
    /// </summary>
    public sealed class CaseMap
    {
        public enum LinkState { Lit, Rubbed, Snapped }

        public struct Entry
        {
            /// <summary>The premise id, or <see cref="ArgumentEngine.ClaimId"/>.</summary>
            public string id;
            public Vector2 position;
            public bool claim, visible;
            public PremiseStatus status;
            /// <summary>The entry it hangs from (its index; the claim for what holds it up directly, -1 for the claim).</summary>
            public int parent;
        }

        public struct Link
        {
            /// <summary>Entry indices: the lower one, and what it holds up.</summary>
            public int a, b;
            public bool visible;
            /// <summary>Lit while the statement it carries stands; rubbed out once it's broken or conceded (still there,
            /// harmless); snapped once it was forced.</summary>
            public LinkState state;
            /// <summary>A line across, between neighbouring pillars of her case (the sky's loops; not drawn in the kitchen).</summary>
            public bool across;
        }

        public const float ClaimY = 1.2f, RootY = 0.35f, Drop = 0.78f, Spread = 0.95f, RootSpan = 4.6f;

        readonly List<Entry> _entries = new List<Entry>(16);
        readonly List<Link> _links = new List<Link>(24);
        readonly ArgumentDef _def;

        public CaseMap(ArgumentDef def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _entries.Add(new Entry { id = ArgumentEngine.ClaimId, position = new Vector2(0f, ClaimY), claim = true, visible = true, parent = -1, status = PremiseStatus.Standing });
            foreach (var p in def.premises)
                if (p != null) _entries.Add(new Entry { id = p.id, parent = 0 });
            // Parents: a premise hangs from its parent if there is one, else from the claim.
            for (int i = 1; i < _entries.Count; i++)
            {
                var e = _entries[i];
                var pd = def.Premise(e.id);
                int parent = string.IsNullOrEmpty(pd.parent) ? 0 : IndexOf(pd.parent);
                e.parent = parent < 0 ? 0 : parent;
                _entries[i] = e;
            }
            // The pillars of her case across the middle, then what hangs from each, spread under it.
            var roots = Children(0);
            for (int k = 0; k < roots.Count; k++)
            {
                float x = roots.Count == 1 ? 0f : -RootSpan * 0.5f + RootSpan * k / (roots.Count - 1);
                Place(roots[k], new Vector2(x, RootY));
            }
            for (int k = 0; k + 1 < roots.Count; k++) _links.Add(new Link { a = roots[k], b = roots[k + 1], across = true });
            for (int i = 1; i < _entries.Count; i++) _links.Add(new Link { a = i, b = _entries[i].parent });
        }

        void Place(int index, Vector2 at)
        {
            var e = _entries[index];
            e.position = new Vector2(Math.Max(-2.6f, Math.Min(2.6f, at.X)), Math.Max(-1.35f, at.Y));
            _entries[index] = e;
            var kids = Children(index);
            for (int k = 0; k < kids.Count; k++)
                Place(kids[k], new Vector2(e.position.X + (k - (kids.Count - 1) * 0.5f) * Spread, e.position.Y - Drop));
        }

        List<int> Children(int parent)
        {
            var list = new List<int>(4);
            for (int i = 1; i < _entries.Count; i++) if (_entries[i].parent == parent) list.Add(i);
            return list;
        }

        public IReadOnlyList<Entry> Entries => _entries;
        public IReadOnlyList<Link> Links => _links;
        public Entry this[int i] => _entries[i];
        public int Count => _entries.Count;

        public int IndexOf(string id)
        {
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].id == id) return i;
            return -1;
        }

        /// <summary>Reads what's on the table now: what's revealed, each statement's state, and each line's.</summary>
        public void Refresh(ArgumentEngine engine)
        {
            for (int i = 1; i < _entries.Count; i++)
            {
                var e = _entries[i];
                e.status = engine.Status(e.id);
                e.visible = e.status != PremiseStatus.Hidden;
                _entries[i] = e;
            }
            for (int i = 0; i < _links.Count; i++)
            {
                var l = _links[i];
                var a = _entries[l.a];
                var b = _entries[l.b];
                l.visible = a.visible && b.visible;
                if (l.across) l.state = a.status == PremiseStatus.Standing || b.status == PremiseStatus.Standing ? LinkState.Lit : LinkState.Rubbed;
                else l.state = a.status == PremiseStatus.Forced ? LinkState.Snapped : a.status == PremiseStatus.Standing ? LinkState.Lit : LinkState.Rubbed;
                _links[i] = l;
            }
        }

        /// <summary>The visible entry nearest <paramref name="p"/> (within <paramref name="within"/>), or -1.</summary>
        public int Nearest(Vector2 p, float within)
        {
            int best = -1;
            float bestD = within * within;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].visible) continue;
                float d = Vector2.DistanceSquared(_entries[i].position, p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>The visible entry the stick points to from <paramref name="from"/> (the nearest roughly that way), or
        /// <paramref name="from"/> if there's none.</summary>
        public int Step(int from, Vector2 dir)
        {
            if (from < 0 || from >= _entries.Count) return First();
            if (dir.LengthSquared() < 1e-4f) return from;
            dir = Vector2.Normalize(dir);
            var at = _entries[from].position;
            int best = from;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i == from || !_entries[i].visible) continue;
                var v = _entries[i].position - at;
                float d = v.Length();
                if (d < 1e-4f) continue;
                float c = Vector2.Dot(v / d, dir);
                if (c < 0.3f) continue;
                float score = d * (2f - c) * (2f - c);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>Where the selection starts: the first standing statement she's made, else the claim.</summary>
        public int First()
        {
            for (int i = 1; i < _entries.Count; i++) if (_entries[i].visible && _entries[i].status == PremiseStatus.Standing) return i;
            return 0;
        }

        /// <summary>Her constellation as lines the light can move along: every star on the table, every line between them
        /// but the snapped ones; the lines still lit carry her showers; a fact Nadia conceded is cover.</summary>
        public void BuildRails(Rails rails)
        {
            rails.Clear();
            var node = new int[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
            {
                node[i] = -1;
                if (!_entries[i].visible) continue;
                node[i] = rails.AddNode(_entries[i].position);
                if (_entries[i].status == PremiseStatus.Conceded) rails.AddCover(node[i]);
            }
            foreach (var l in _links)
            {
                if (!l.visible || node[l.a] < 0 || node[l.b] < 0) continue;
                rails.AddEdge(node[l.a], node[l.b], l.state == LinkState.Lit, l.state != LinkState.Snapped);
            }
        }
    }

    /// <summary>
    /// Lines the light moves along (her sky, SELECTION.md 7; Undertale's strings): the stick moves it along the line it's
    /// on, and at a star it takes the line that points the stick's way. A stick across the line drifts it toward the end
    /// whose lines go that way, so it never sticks. Built once per phase; nothing allocated per move.
    /// </summary>
    public sealed class Rails
    {
        public struct Edge
        {
            public int a, b;
            /// <summary>Her showers run along it (the statement it carries still stands).</summary>
            public bool lit;
            /// <summary>The light can travel it (not snapped).</summary>
            public bool walkable;
        }

        /// <summary>Within this of a star counts as at it.</summary>
        public const float StarSnap = 0.03f;
        /// <summary>A stick this far across a line, or less, moves along it at this share of full speed.</summary>
        public const float Across = 0.25f, AcrossSpeed = 0.6f;

        readonly List<Vector2> _nodes = new List<Vector2>(16);
        readonly List<Edge> _edges = new List<Edge>(32);
        readonly List<int> _cover = new List<int>(8), _lit = new List<int>(32), _walk = new List<int>(32);
        int _version;

        public IReadOnlyList<Vector2> Nodes => _nodes;
        public IReadOnlyList<Edge> Edges => _edges;
        /// <summary>Stars that stop streaks (facts Nadia conceded).</summary>
        public IReadOnlyList<int> Cover => _cover;
        /// <summary>Changes whenever the lines do (the planner's mask follows it).</summary>
        public int Version => _version;
        public int Walkable => _walk.Count;
        public int LitCount => _lit.Count;
        public int LitEdge(int k) => _lit[k];
        public int WalkableEdge(int k) => _walk[k];

        public void Clear()
        {
            _nodes.Clear();
            _edges.Clear();
            _cover.Clear();
            _lit.Clear();
            _walk.Clear();
            _version++;
        }

        public int AddNode(Vector2 p)
        {
            _nodes.Add(p);
            _version++;
            return _nodes.Count - 1;
        }

        public void AddEdge(int a, int b, bool lit, bool walkable)
        {
            if (a == b || a < 0 || b < 0 || a >= _nodes.Count || b >= _nodes.Count) return;
            _edges.Add(new Edge { a = a, b = b, lit = lit, walkable = walkable });
            int i = _edges.Count - 1;
            if (walkable) _walk.Add(i);
            if (walkable && lit) _lit.Add(i);
            _version++;
        }

        public void AddCover(int node)
        {
            if (!_cover.Contains(node)) _cover.Add(node);
            _version++;
        }

        /// <summary>The point <paramref name="t"/> (0..1) of the way along edge <paramref name="edge"/>.</summary>
        public Vector2 PointOn(int edge, float t)
        {
            if (edge < 0 || edge >= _edges.Count) return _nodes.Count > 0 ? _nodes[0] : Vector2.Zero;
            var e = _edges[edge];
            return Vector2.Lerp(_nodes[e.a], _nodes[e.b], Math.Max(0f, Math.Min(1f, t)));
        }

        /// <summary>The distance from <paramref name="p"/> to the nearest walkable line.</summary>
        public float DistanceTo(Vector2 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < _walk.Count; k++)
            {
                var e = _edges[_walk[k]];
                float d = Vector2.Distance(p, Closest(_nodes[e.a], _nodes[e.b], p));
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>The nearest point on a walkable line, and that line.</summary>
        public Vector2 Snap(Vector2 p, out int edge)
        {
            edge = -1;
            float best = float.MaxValue;
            var at = p;
            for (int k = 0; k < _walk.Count; k++)
            {
                int i = _walk[k];
                var e = _edges[i];
                var c = Closest(_nodes[e.a], _nodes[e.b], p);
                float d = Vector2.DistanceSquared(c, p);
                if (d < best) { best = d; at = c; edge = i; }
            }
            return at;
        }

        static Vector2 Closest(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            float len2 = ab.LengthSquared();
            if (len2 < 1e-8f) return a;
            float t = Math.Max(0f, Math.Min(1f, Vector2.Dot(p - a, ab) / len2));
            return a + ab * t;
        }

        /// <summary>How well the lines at star <paramref name="node"/> point along <paramref name="dir"/> (the best cosine).</summary>
        float Align(int node, Vector2 dir)
        {
            float best = -1f;
            for (int k = 0; k < _walk.Count; k++)
            {
                var e = _edges[_walk[k]];
                if (e.a != node && e.b != node) continue;
                var w = _nodes[e.a == node ? e.b : e.a] - _nodes[node];
                float len = w.Length();
                if (len < 1e-4f) continue;
                float c = Vector2.Dot(w / len, dir);
                if (c > best) best = c;
            }
            return best;
        }

        /// <summary>Moves the light from <paramref name="p"/> by up to <paramref name="dist"/> along the lines, the way the
        /// stick (<paramref name="input"/>, -1..1) points. <paramref name="edge"/> is the line it's on (kept between calls).</summary>
        public Vector2 Move(Vector2 p, ref int edge, Vector2 input, float dist)
        {
            if (_walk.Count == 0) return p;
            if (edge < 0 || edge >= _edges.Count || !_edges[edge].walkable) p = Snap(p, out edge);
            float mag = input.Length();
            if (mag < 0.15f || dist <= 0f) return p;
            var dir = input / mag;
            float left = dist * Math.Min(1f, mag);
            for (int guard = 0; guard < 6 && left > 1e-5f; guard++)
            {
                var e = _edges[edge];
                var a = _nodes[e.a];
                var ab = _nodes[e.b] - a;
                float len = ab.Length();
                if (len < 1e-4f) break;
                var u = ab / len;
                float t = Math.Max(0f, Math.Min(len, Vector2.Dot(p - a, u)));
                int star = t <= StarSnap ? e.a : t >= len - StarSnap ? e.b : -1;
                if (star >= 0)
                {
                    // At a star: take the line that points the stick's way most (or stay put).
                    int best = -1;
                    float bestC = 0.35f;
                    for (int k = 0; k < _walk.Count; k++)
                    {
                        var o = _edges[_walk[k]];
                        if (o.a != star && o.b != star) continue;
                        var w = _nodes[o.a == star ? o.b : o.a] - _nodes[star];
                        float wl = w.Length();
                        if (wl < 1e-4f) continue;
                        float c = Vector2.Dot(w / wl, dir);
                        if (c > bestC) { bestC = c; best = _walk[k]; }
                    }
                    if (best < 0) return _nodes[star];
                    edge = best;
                    e = _edges[edge];
                    a = _nodes[e.a];
                    ab = _nodes[e.b] - a;
                    len = ab.Length();
                    if (len < 1e-4f) break;
                    u = ab / len;
                    t = e.a == star ? 0f : len;
                }
                float s = Vector2.Dot(dir, u);
                if (Math.Abs(s) < Across) s = (Align(e.b, dir) >= Align(e.a, dir) ? 1f : -1f) * AcrossSpeed;
                float nt = Math.Max(0f, Math.Min(len, t + s * left));
                float moved = Math.Abs(nt - t);
                left -= moved / Math.Abs(s);
                p = a + u * nt;
                if (nt > StarSnap && nt < len - StarSnap) break; // still on the line: done
                if (moved < 1e-6f) break;
            }
            return p;
        }
    }
}
