using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Stealth
{
    /// <summary>
    /// The seeker's walking map: points on its floor and the straight, clear walks between them. <see cref="AutoConnect"/>
    /// joins every pair within a distance whose line is clear at knee and chest height; <see cref="FindPath"/> is
    /// Dijkstra into a caller's list (buffers are made once, so planning a path doesn't allocate).
    /// </summary>
    public sealed class WaypointGraph
    {
        readonly List<Vector3> _nodes = new List<Vector3>();
        readonly List<List<int>> _edges = new List<List<int>>();
        float[] _dist = new float[0];
        int[] _prev = new int[0];
        bool[] _done = new bool[0];

        public int Count => _nodes.Count;
        public Vector3 this[int i] => _nodes[i];
        public IReadOnlyList<int> Neighbours(int i) => _edges[i];

        public int Add(Vector3 p)
        {
            _nodes.Add(p);
            _edges.Add(new List<int>(6));
            return _nodes.Count - 1;
        }

        public void Connect(int a, int b)
        {
            if (a == b || _edges[a].Contains(b)) return;
            _edges[a].Add(b);
            _edges[b].Add(a);
        }

        /// <summary>Joins every pair of nodes within <paramref name="maxDistance"/> whose walk is clear (sight lines at
        /// 0.5 m and 1.2 m above the floor, so a low wall blocks a walk but not a glance).</summary>
        public void AutoConnect(IStealthWorld world, float maxDistance)
        {
            for (int a = 0; a < _nodes.Count; a++)
                for (int b = a + 1; b < _nodes.Count; b++)
                    if (Vector3.Distance(_nodes[a], _nodes[b]) <= maxDistance && WalkClear(world, _nodes[a], _nodes[b])) Connect(a, b);
        }

        public static bool WalkClear(IStealthWorld world, Vector3 a, Vector3 b)
        {
            if (world == null) return true;
            var knee = new Vector3(0f, 0.5f, 0f);
            var chest = new Vector3(0f, 1.2f, 0f);
            return world.LineClear(a + knee, b + knee) && world.LineClear(a + chest, b + chest);
        }

        public int Nearest(Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float d = Vector3.DistanceSquared(new Vector3(p.X, 0f, p.Z), new Vector3(_nodes[i].X, 0f, _nodes[i].Z));
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>The nearest node with a clear walk to <paramref name="p"/> (or the nearest at all if none is clear).</summary>
        public int NearestReachable(IStealthWorld world, Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float d = Vector3.DistanceSquared(new Vector3(p.X, 0f, p.Z), new Vector3(_nodes[i].X, 0f, _nodes[i].Z));
                if (d < bd && WalkClear(world, new Vector3(p.X, _nodes[i].Y, p.Z), _nodes[i])) { bd = d; best = i; }
            }
            return best >= 0 ? best : Nearest(p);
        }

        /// <summary>Fills <paramref name="path"/> with the node indices from <paramref name="from"/> to <paramref name="to"/>
        /// (both included). False when they aren't connected.</summary>
        public bool FindPath(int from, int to, List<int> path)
        {
            path.Clear();
            int n = _nodes.Count;
            if (from < 0 || to < 0 || from >= n || to >= n) return false;
            if (_dist.Length < n) { _dist = new float[n]; _prev = new int[n]; _done = new bool[n]; }
            for (int i = 0; i < n; i++) { _dist[i] = float.MaxValue; _prev[i] = -1; _done[i] = false; }
            _dist[from] = 0f;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                    if (!_done[i] && _dist[i] < best) { best = _dist[i]; u = i; }
                if (u < 0 || u == to) break;
                _done[u] = true;
                var e = _edges[u];
                for (int k = 0; k < e.Count; k++)
                {
                    int v = e[k];
                    float nd = _dist[u] + Vector3.Distance(_nodes[u], _nodes[v]);
                    if (nd < _dist[v]) { _dist[v] = nd; _prev[v] = u; }
                }
            }
            if (_dist[to] == float.MaxValue) return false;
            for (int v = to; v >= 0; v = _prev[v]) path.Add(v);
            path.Reverse();
            return true;
        }

        /// <summary>True when every node can reach every other.</summary>
        public bool Connected()
        {
            if (_nodes.Count == 0) return true;
            var seen = new bool[_nodes.Count];
            var stack = new Stack<int>();
            stack.Push(0);
            seen[0] = true;
            int count = 1;
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                foreach (int v in _edges[u])
                    if (!seen[v]) { seen[v] = true; count++; stack.Push(v); }
            }
            return count == _nodes.Count;
        }
    }
}
