using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Perf
{
    /// <summary>
    /// A camera flight for the perf probe and the editor estimate: legs of travel (a straight line at a steady speed, the
    /// view along it) and stations (a fixed eye panning between two yaws). A leg may start with a cut (a teleport) and an
    /// action (enter a ward area): the probe keeps the frames just after a cut out of the steady-state numbers and reports
    /// them as hitches. Built once before flying; sampling allocates nothing.
    /// </summary>
    public sealed class VhPerfFlight
    {
        public struct Leg
        {
            public string label;
            public Vector3 eye0, eye1;
            public float yaw0, yaw1, pitch;
            public float seconds;
            public bool cut;
            /// <summary>Runs when the leg starts (null: nothing).</summary>
            public Action onStart;
        }

        public readonly string name;
        public readonly List<Leg> legs = new List<Leg>();
        /// <summary>False: the probe leaves the camera alone (the battle: its own camera; the probe only records).</summary>
        public bool overrideCamera = true;

        int _cursor;
        float _cursorStart;

        public VhPerfFlight(string name) { this.name = name; }

        public float Seconds
        {
            get { float s = 0f; foreach (var l in legs) s += l.seconds; return s; }
        }

        public int LegCount => legs.Count;

        /// <summary>A fixed eye panning from <paramref name="yaw0"/> to <paramref name="yaw1"/> (degrees; pitch + is down).</summary>
        public void Station(string label, Vector3 eye, float yaw0, float yaw1, float pitch, float seconds, bool cut = true, Action onStart = null)
        {
            legs.Add(new Leg { label = label, eye0 = eye, eye1 = eye, yaw0 = yaw0, yaw1 = yaw1, pitch = pitch, seconds = Mathf.Max(0.1f, seconds), cut = cut, onStart = onStart });
        }

        /// <summary>A view (eye and what it looks at), held while panning <paramref name="pan"/> degrees across it.</summary>
        public void View(string label, Vector3 eye, Vector3 look, float seconds, float pan = 30f)
        {
            YawPitch(look - eye, out float yaw, out float pitch);
            Station(label, eye, yaw - pan * 0.5f, yaw + pan * 0.5f, pitch, seconds);
        }

        /// <summary>A walk along <paramref name="feet"/> (foot positions) at eye height, looking along it slightly down. The
        /// first leg is a cut; points closer than <paramref name="step"/> m are merged.</summary>
        public void Walk(string label, IList<Vector3> feet, float eyeHeight = 1.6f, float speed = 7f, float step = 3f, float pitch = 6f, Action onStart = null)
        {
            if (feet == null || feet.Count < 2) return;
            var pts = new List<Vector3> { feet[0] };
            for (int i = 1; i < feet.Count; i++)
                if ((feet[i] - pts[pts.Count - 1]).magnitude >= step || i == feet.Count - 1) pts.Add(feet[i]);
            bool first = true;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i] + Vector3.up * eyeHeight;
                var b = pts[i + 1] + Vector3.up * eyeHeight;
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                YawPitch(new Vector3(d.x, 0f, d.z), out float yaw, out _);
                legs.Add(new Leg { label = label, eye0 = a, eye1 = b, yaw0 = yaw, yaw1 = yaw, pitch = pitch, seconds = len / Mathf.Max(0.5f, speed), cut = first, onStart = first ? onStart : null });
                first = false;
            }
        }

        /// <summary>Just time (the camera untouched): the battle.</summary>
        public void Hold(string label, float seconds, Action onStart = null)
        {
            legs.Add(new Leg { label = label, seconds = Mathf.Max(0.1f, seconds), onStart = onStart, cut = true });
        }

        public static void YawPitch(Vector3 dir, out float yaw, out float pitch)
        {
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float flat = new Vector2(dir.x, dir.z).magnitude;
            pitch = -Mathf.Atan2(dir.y, Mathf.Max(1e-4f, flat)) * Mathf.Rad2Deg;
        }

        /// <summary>Restart sampling from the beginning.</summary>
        public void Rewind()
        {
            _cursor = 0;
            _cursorStart = 0f;
        }

        /// <summary>
        /// The camera at <paramref name="t"/> seconds into the flight (t only grows between <see cref="Rewind"/>s). Returns the
        /// leg index, or -1 past the end. <paramref name="started"/> is true on the first sample of a leg.
        /// </summary>
        public int Sample(float t, out Vector3 eye, out Quaternion rotation, out bool started)
        {
            started = false;
            eye = Vector3.zero;
            rotation = Quaternion.identity;
            if (legs.Count == 0) return -1;
            int before = _cursor;
            while (_cursor < legs.Count && t >= _cursorStart + legs[_cursor].seconds)
            {
                _cursorStart += legs[_cursor].seconds;
                _cursor++;
            }
            if (_cursor >= legs.Count) return -1;
            started = _cursor != before || _firstSample;
            _firstSample = false;
            var leg = legs[_cursor];
            float k = Mathf.Clamp01((t - _cursorStart) / leg.seconds);
            eye = Vector3.Lerp(leg.eye0, leg.eye1, k);
            float yaw = Mathf.LerpAngle(leg.yaw0, leg.yaw1, k);
            rotation = Quaternion.Euler(leg.pitch, yaw, 0f);
            return _cursor;
        }

        bool _firstSample = true;

        /// <summary>Resets sampling state including the first-leg flag.</summary>
        public void Reset()
        {
            Rewind();
            _firstSample = true;
        }

        /// <summary>A few points along the flight (each leg's start and end): the editor estimate's viewpoints.</summary>
        public void Viewpoints(List<Vector3> eyes, List<Quaternion> rotations, List<string> labels, float minSpacing = 15f)
        {
            Vector3 last = new Vector3(float.MaxValue, 0f, 0f);
            foreach (var leg in legs)
            {
                if (!overrideCamera) continue;
                for (int e = 0; e < 2; e++)
                {
                    var eye = e == 0 ? leg.eye0 : leg.eye1;
                    float yaw = e == 0 ? leg.yaw0 : leg.yaw1;
                    if ((eye - last).magnitude < minSpacing && leg.yaw0 == leg.yaw1) continue;
                    eyes.Add(eye);
                    rotations.Add(Quaternion.Euler(leg.pitch, yaw, 0f));
                    labels.Add(leg.label);
                    last = eye;
                }
            }
        }
    }
}
