using System.Collections.Generic;
using JamKit.Perf;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Per-room culling and the shadow budget for the ward (docs/redesign/perf.md, Day items 1 and 4). Each room's props,
    /// people and lights hang under its own root; a few times a second this turns off the rooms she can't see into: from
    /// inside a closed room (a patient's room, the staff room, the quiet room, the family room) only that room shows; from an
    /// open space (the corridor, the station, the lift lobby) the rooms within reach of a doorway show. The shell (floors,
    /// walls, ceiling) is never culled. Of the lights that may cast shadows, only the <see cref="shadowedLights"/> nearest
    /// her do (a room's soft shadows stay where she is; the rest are plain lights), and none on a tier without point-light
    /// shadows. Nothing allocated per frame.
    /// </summary>
    public sealed class VhWardRooms : MonoBehaviour
    {
        sealed class Room
        {
            public string name;
            public Transform root;
            public Bounds bounds;
            public bool open;
            public Renderer[] renderers;
            public Light[] lights;
            public bool shown = true;
        }

        [Tooltip("Seconds between checks.")]
        public float interval = 0.2f;
        [Tooltip("From an open space, closed rooms nearer than this (metres, to their walls) stay drawn.")]
        public float reach = 16f;
        [Tooltip("How many point/spot lights near her may cast shadows (perf.md: 1-2).")]
        public int shadowedLights = 2;

        readonly List<Room> _rooms = new List<Room>();
        readonly List<Light> _shadowCandidates = new List<Light>();
        readonly List<Light> _nearest = new List<Light>(4);
        float _next;
        Camera _cam;

        public int RoomCount => _rooms.Count;

        /// <summary>A room: its root (everything under it goes with it), the floor area it covers, and whether it's an open
        /// space she sees the others from (never culled itself).</summary>
        public void Add(string roomName, Transform root, Bounds bounds, bool open)
        {
            if (root == null) return;
            var r = new Room
            {
                name = roomName,
                root = root,
                bounds = bounds,
                open = open,
                renderers = root.GetComponentsInChildren<Renderer>(true),
                lights = root.GetComponentsInChildren<Light>(true),
            };
            _rooms.Add(r);
        }

        /// <summary>A light that may cast soft shadows when it's among the nearest (built with shadows on).</summary>
        public void AddShadowCandidate(Light l)
        {
            if (l == null || _shadowCandidates.Contains(l)) return;
            _shadowCandidates.Add(l);
            l.shadows = LightShadows.None;
        }

        /// <summary>Re-reads each room's renderers and lights (after people or props were added under a room).</summary>
        public void Refresh()
        {
            foreach (var r in _rooms)
            {
                if (r.root == null) continue;
                bool was = r.shown;
                Show(r, true);
                r.renderers = r.root.GetComponentsInChildren<Renderer>(true);
                r.lights = r.root.GetComponentsInChildren<Light>(true);
                r.shown = true;
                Show(r, was);
            }
        }

        /// <summary>Whether a room is drawn now (tests, the captures).</summary>
        public bool IsShown(string roomName)
        {
            foreach (var r in _rooms) if (r.name == roomName) return r.shown;
            return false;
        }

        /// <summary>Everything on (the captures' wide shots, the editor's probe bake).</summary>
        public void ShowAll()
        {
            foreach (var r in _rooms) Show(r, true);
        }

        void OnEnable() => _next = 0f;

        void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + interval;
            if (_cam == null)
            {
                var g = VisitingHoursGame.Current;
                _cam = g != null && g.cam != null ? g.cam : Camera.main;
                if (_cam == null) return;
            }
            var p = transform.InverseTransformPoint(_cam.transform.position);
            Cull(p);
            Shadows(_cam.transform.position);
        }

        /// <summary>Shows the rooms seen from <paramref name="local"/> (the place's own frame).</summary>
        public void Cull(Vector3 local)
        {
            // Inside a closed room only when really in it (past its doorway); otherwise in the open space she's in (with a
            // little slack at its edges), or nowhere (then everything within reach shows).
            Room inside = null;
            foreach (var r in _rooms)
            {
                if (r.open) continue;
                var b = r.bounds;
                b.Expand(new Vector3(-0.1f, 10f, -0.1f));
                if (b.Contains(local)) { inside = r; break; }
            }
            if (inside == null)
                foreach (var r in _rooms)
                {
                    if (!r.open) continue;
                    var b = r.bounds;
                    b.Expand(new Vector3(0.6f, 10f, 0.6f));
                    if (b.Contains(local)) { inside = r; break; }
                }
            foreach (var r in _rooms)
            {
                bool show;
                if (r.open) show = true;
                else if (inside == r) show = true;
                else if (inside != null && !inside.open) show = false;
                else show = Mathf.Sqrt(r.bounds.SqrDistance(new Vector3(local.x, r.bounds.center.y, local.z))) < reach;
                Show(r, show);
            }
        }

        static void Show(Room r, bool on)
        {
            if (r.shown == on) return;
            r.shown = on;
            for (int i = 0; i < r.renderers.Length; i++) if (r.renderers[i] != null) r.renderers[i].enabled = on;
            for (int i = 0; i < r.lights.Length; i++) if (r.lights[i] != null) r.lights[i].enabled = on;
        }

        void Shadows(Vector3 eye)
        {
            if (_shadowCandidates.Count == 0) return;
            bool allowed = !VhPerfTiers.Active || VhPerfTiers.Current.additionalLightShadows;
            _nearest.Clear();
            int want = allowed ? Mathf.Max(0, shadowedLights) : 0;
            for (int n = 0; n < want; n++)
            {
                Light best = null;
                float bestD = float.MaxValue;
                for (int i = 0; i < _shadowCandidates.Count; i++)
                {
                    var l = _shadowCandidates[i];
                    if (l == null || !l.enabled || _nearest.Contains(l)) continue;
                    float d = (l.transform.position - eye).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = l; }
                }
                if (best == null) break;
                _nearest.Add(best);
            }
            for (int i = 0; i < _shadowCandidates.Count; i++)
            {
                var l = _shadowCandidates[i];
                if (l == null) continue;
                var want2 = _nearest.Contains(l) ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != want2) l.shadows = want2;
            }
        }
    }
}
